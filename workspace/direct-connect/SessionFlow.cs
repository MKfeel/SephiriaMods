using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SephiriaDirectConnect;

public sealed partial class Plugin
{
    private SessionOperation operation;
    private string operationError;
    private string recoveryTitle = "Title";
    private static readonly System.Reflection.FieldInfo authorityStartPhaseField =
        AccessTools.Field(typeof(PlayerSpawner), "authorityStartPhase");

    [HarmonyPatch(typeof(HorayNetworkAuthenticator), "OnClientVersionResponseMessage")]
    private static class ObserveDirectAuthentication
    {
        [HarmonyPrefix]
        private static void Prefix(HorayNetworkAuthenticator.VersionResponseMessage message)
        {
            SessionOperation current = instance?.operation;
            if (current == null || current.Recovering || !instance.directClientSession) return;
            current.AuthenticationAccepted = message.success;
            if (!message.success) current.Failure = SessionOperation.Rejection(message.errorMessage);
        }
    }

    [HarmonyPatch(typeof(HorayNetworkManager), "OnClientDisconnect")]
    private static class ObservePendingDirectDisconnect
    {
        [HarmonyPrefix]
        private static void Prefix(HorayNetworkManager __instance)
        {
            if (instance?.operation == null) return;
            // This transition owns its error UI and recovery; suppress the native
            // "HOST DISCONNECTED" dialog, but still run all native cleanup.
            __instance.requestSelfLeave = true;
            instance.operation.Disconnected = true;
        }
    }

    private void BeginSessionOperation(IEnumerator routine)
    {
        if (operation != null || SteamInvitation.waitForExternalConnect) return;
        operationError = null;
        operation = new SessionOperation();
        SteamInvitation.waitForExternalConnect = true;
        if (NetworkManager.singleton is HorayNetworkManager manager)
            recoveryTitle = manager.titleSceneName;
        StartCoroutine(RunSessionOperation(routine));
    }

    private IEnumerator RunSessionOperation(IEnumerator routine)
    {
        Exception failure = null;
        var pump = new RoutinePump(routine);
        try
        {
            while (true)
            {
                bool moved = false;
                try { moved = pump.MoveNext(); }
                catch (Exception ex) { failure = ex; }
                if (failure != null || !moved) break;
                yield return pump.Current;
            }
            try { pump.Dispose(); }
            catch (Exception ex) { failure = failure ?? ex; }

            if (failure != null)
            {
                operationError = failure.Message;
                Logger.LogWarning("Direct session transition failed: " + failure);
                if (operation.NeedsRecovery)
                {
                    operation.Recovering = true;
                    var recovery = new RoutinePump(RecoverToTitle());
                    try
                    {
                        while (true)
                        {
                            bool moved = false;
                            Exception recoveryFailure = null;
                            try { moved = recovery.MoveNext(); }
                            catch (Exception ex) { recoveryFailure = ex; }
                            if (recoveryFailure != null)
                            {
                                Logger.LogError("Direct session recovery failed: " + recoveryFailure);
                                operationError += " 恢复未完成：" + recoveryFailure.Message;
                                break;
                            }
                            if (!moved) break;
                            yield return recovery.Current;
                        }
                    }
                    finally { recovery.Dispose(); }
                }
            }
        }
        finally
        {
            try { pump.Dispose(); }
            finally
            {
                // Restore the global invitation gate even if cleanup itself fails.
                SteamInvitation.waitForExternalConnect = false;
                operation = null;
            }
        }
    }

    private IEnumerator WaitForState(string stage, float timeout, Func<bool> ready, bool requireClient = false)
    {
        operation.Enter(stage, Time.realtimeSinceStartup, timeout);
        Logger.LogInfo("Direct connection stage: " + stage);
        while (true)
        {
            operation.Check(Time.realtimeSinceStartup);
            if (requireClient && (operation.Disconnected || !NetworkClient.active))
                throw new InvalidOperationException("主机已断开连接（" + stage + "）。");
            if (ready()) yield break;
            yield return null;
        }
    }

    private static bool SceneIsSettled()
    {
        // Mirror schedules the offline scene via Invoke after disconnect. Checking
        // only loadingSceneAsync would race that not-yet-started scene transition.
        if (NetworkManager.singleton != null && NetworkManager.singleton.IsInvoking("ClientChangeOfflineScene"))
            return false;
        return NetworkManager.loadingSceneAsync == null &&
               !NetworkClient.isLoadingScene && !NetworkServer.isLoadingScene &&
               SceneManager.GetActiveScene().isLoaded;
    }

    private static bool FadeIsSettled()
    {
        return ScreenFader.Instance == null || !ScreenFader.Instance.IsFading;
    }

    private static bool LocalPlayerIsReady()
    {
        if (!NetworkClient.isConnected || NetworkClient.connection == null ||
            !NetworkClient.connection.isAuthenticated || !NetworkClient.ready ||
            NetworkClient.localPlayer == null || !SceneIsSettled() || !FadeIsSettled()) return false;
        PlayerSpawner spawner = NetworkClient.localPlayer.GetComponent<PlayerSpawner>();
        return spawner != null && spawner.isOwned && spawner.PlayerAvatar != null &&
               authorityStartPhaseField != null && (int)authorityStartPhaseField.GetValue(spawner) >= 2 &&
               GameCamera.Instance != null && GameCamera.Instance.Observer == spawner.PlayerAvatar &&
               DungeonManager.Instance != null && UIManager.Instance != null;
    }

    private IEnumerator SwitchToDirectSession(string address, ushort port)
    {
        // Capture before teardown, as OptionsBinding can be destroyed by a scene change.
        string profile = GetSelectedProfile();
        if (NetworkManager.singleton == null) throw new InvalidOperationException("网络管理器尚未就绪。");
        if (authorityStartPhaseField == null)
            throw new InvalidOperationException("当前游戏版本的角色就绪接口不兼容，已停止连接。");
        yield return WaitForState("等待存档写入完成", 15f,
            () => SaveManager.IsSaving != SaveManager.ESaveState.Saving);
        operation.NeedsRecovery = true;
        if (directHostActive || directHostStarting) StopDirectHost("Switching to another IP host.");
        LeaveSteamLobbyIfPresent();
        NetworkManager manager = NetworkManager.singleton;
        if (manager == null) throw new InvalidOperationException("旧会话的网络管理器已失效。");
        if (manager is HorayNetworkManager horay) horay.requestSelfLeave = true;
        if (NetworkServer.active) manager.StopHost();
        else if (NetworkClient.active) manager.StopClient();
        yield return WaitForState("关闭旧联机会话", 10f, () => !NetworkServer.active && !NetworkClient.active);
        yield return WaitForState("等待场景与画面切换完成", 20f, () => SceneIsSettled() && FadeIsSettled());
        yield return WaitForState("等待存档释放", 15f,
            () => SaveManager.IsSaving != SaveManager.ESaveState.Saving);

        // These native methods are synchronous; their outputs are the readiness contract.
        operation.Enter("加载当前存档", Time.realtimeSinceStartup, 15f);
        if (!SaveManager.Load(profile)) throw new InvalidOperationException("无法加载当前存档。");
        SaveManager.CreateNewTMP(profile);
        SaveManager.ApplyPostLoadSaveFixes();
        if (SaveManager.Current == null || SaveManager.CurrentRun == null || SaveManager.Binded != profile)
            throw new InvalidOperationException("当前存档或临时存档尚未正确初始化。");
        yield return WaitForState("等待网络管理器就绪", 15f, () =>
            NetworkManager.singleton != null && NetworkManager.singleton.isActiveAndEnabled &&
            NetworkManager.singleton.authenticator != null && SceneIsSettled());

        manager = NetworkManager.singleton;
        operation.Disconnected = false;
        ConfigureDirectClientKcp(manager, port);
        manager.networkAddress = address;
        if (manager is HorayNetworkManager client) client.ShowConnectingScreen();
        manager.StartClient();
        yield return WaitForState("正在连接主机", 15f, () => NetworkClient.isConnected, true);
        yield return WaitForState("正在验证版本与重连资格", 25f, () =>
            operation.AuthenticationAccepted && NetworkClient.connection != null &&
            NetworkClient.connection.isAuthenticated, true);
        yield return WaitForState("正在进入世界", 60f, LocalPlayerIsReady, true);
        if (NetworkManager.singleton is HorayNetworkManager connected) connected.HideConnectingScreen();
        operation.NeedsRecovery = false;
        Logger.LogInfo("Direct session ready: authentication, scene, local player and camera confirmed.");
        ShowSystemMessage("已进入 IP 联机世界。");
    }

    private IEnumerator SwitchToDirectHost(ushort port)
    {
        if (!NetworkServer.activeHost || NetworkManager.singleton == null)
            throw new InvalidOperationException("当前本地世界尚未作为主机运行。");
        // Reopening the same room does not disconnect its members.
        if (directHostActive && directHostPort == port && IsDirectHostRunning())
        {
            ShowDirectHostLobby(port);
            yield break;
        }
        yield return WaitForState("等待房间状态就绪", 15f, () =>
            SceneIsSettled() && FadeIsSettled() && SaveManager.IsSaving != SaveManager.ESaveState.Saving);
        operation.Enter("清理旧房间连接", Time.realtimeSinceStartup, 10f);
        // Keep the old transport active until its remote connections and Mirror IDs
        // have been removed. Never reuse old IDs under a new transport.
        operation.NeedsRecovery = true;
        try
        {
            DisconnectAllRemoteClients();
            yield return WaitForState("等待旧房间连接清理", 10f, NoRemoteClients);
            if (directHostActive || directHostTransport != null) StopDirectHost("Closing the previous IP room before changing port.");
            if (!EnableDirectHost(port))
                throw new InvalidOperationException("无法开启 IP 监听，已尝试恢复原传输。请检查端口是否被占用。");
            ShowDirectHostLobby(port);
        }
        finally
        {
            // A healthy old host can remain in its current world after a failed switch.
            // If rollback could not restore a listener, use the common title recovery.
            NetworkManager manager = NetworkManager.singleton;
            operation.NeedsRecovery = manager == null || !NetworkServer.activeHost ||
                manager.transport == null || !manager.transport.ServerActive();
        }
    }

    private static bool NoRemoteClients()
    {
        foreach (var item in NetworkServer.connections)
            if (item.Value != NetworkServer.localConnection) return false;
        return true;
    }

    private void DisconnectAllRemoteClients()
    {
        if (networkServerDisconnectedMethod == null)
            throw new InvalidOperationException("无法找到游戏的连接清理接口，已停止切换房间。");
        var connections = new List<NetworkConnectionToClient>(NetworkServer.connections.Values);
        foreach (NetworkConnectionToClient connection in connections)
        {
            if (connection == null || connection == NetworkServer.localConnection) continue;
            connection.Disconnect();
            if (NetworkServer.connections.ContainsKey(connection.connectionId))
                networkServerDisconnectedMethod.Invoke(null, new object[] { connection.connectionId });
        }
    }

    private IEnumerator RecoverToTitle()
    {
        operation.Enter("正在清理失败的连接", Time.realtimeSinceStartup, 15f);
        if (NetworkManager.singleton is HorayNetworkManager horay)
        {
            horay.requestSelfLeave = true;
            horay.HideConnectingScreen();
        }
        try
        {
            if (NetworkServer.active && NetworkManager.singleton != null) NetworkManager.singleton.StopHost();
        }
        catch (Exception ex) { Logger.LogError("Host stop failed during recovery: " + ex); }
        AbortDirectClient(NetworkManager.singleton, "Connection cancelled or failed.");
        if (NetworkServer.active) NetworkServer.Shutdown();
        yield return WaitForState("等待网络会话关闭", 10f, () => !NetworkServer.active && !NetworkClient.active);
        yield return WaitForState("等待已有场景加载结束", 20f, SceneIsSettled);
        RestoreDirectClientTransport("Recovery completed network cleanup.");
        // The native title routine refuses to run while this gate is set.
        // Our operation object continues to exclude re-entry during recovery.
        SteamInvitation.waitForExternalConnect = false;
        if (NetworkManager.singleton is HorayNetworkManager manager)
            manager.GoToTitleScene();
        else
            SceneManager.LoadSceneAsync(recoveryTitle);
        yield return WaitForState("正在返回标题界面", 25f, () =>
            SceneManager.GetActiveScene().name == recoveryTitle && SceneIsSettled());
        if (ScreenFader.Instance != null) ScreenFader.Instance.ClearLoadingScreen();
    }

    private void OnGUI()
    {
        if (operation == null && operationError == null) return;
        // A transient progress/cancel surface survives scene teardown and the native
        // connecting overlay. All normal lobby controls remain the original UI.
        int oldDepth = GUI.depth;
        GUI.depth = -10000;
        float width = Mathf.Min(540f, Screen.width - 24f);
        var rect = new Rect((Screen.width - width) / 2f, 20f, width, 118f);
        GUILayout.BeginArea(rect, GUI.skin.box);
        if (operation != null)
        {
            GUILayout.Label(operation.Stage ?? "正在准备连接");
            if (!operation.Recovering && GUILayout.Button("取消连接")) operation.Cancelled = true;
        }
        else
        {
            GUILayout.Label(operationError);
            if (GUILayout.Button("关闭")) operationError = null;
        }
        GUILayout.EndArea();
        GUI.depth = oldDepth;
    }
}
