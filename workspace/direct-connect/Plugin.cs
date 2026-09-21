using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using HeathenEngineering.SteamworksIntegration;
using Mirror;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SephiriaDirectConnect;

[BepInPlugin("dev.dreamyao.sephiria.directconnect", "Sephiria Direct Connect", "0.4.0")]
[BepInProcess("Sephiria.exe")]
public sealed partial class Plugin : BaseUnityPlugin
{
	[HarmonyPatch(typeof(LobbyManager), "Create", new Type[] { typeof(Action<EResult, LobbyData, bool>) })]
	private static class PreventSteamLobbyForDirectHostPatch
	{
		[HarmonyPrefix]
		private static bool Prefix()
		{
			if ((UnityEngine.Object)(object)instance == null || (!instance.directHostActive && !instance.directHostStarting))
			{
				return true;
			}
			if (!instance.steamLobbyCreateBlockLogged)
			{
				instance.steamLobbyCreateBlockLogged = true;
				instance.Logger.LogInfo((object)"Blocked automatic Steam Lobby creation while the direct IP host is active.");
			}
			return false;
		}
	}

	[HarmonyPatch(typeof(NetworkClient), "Shutdown")]
	private static class RestoreTransportAfterDirectClientShutdownPatch
	{
		[HarmonyPostfix]
		private static void Postfix()
		{
			if ((UnityEngine.Object)(object)instance != null && instance.directClientSession)
			{
				instance.RestoreDirectClientTransport("Mirror shut down the direct IP client.");
			}
		}
	}

	[HarmonyPatch(typeof(HorayNetworkManager), "get_AllowRejoin")]
	private static class EnableDirectClientRejoinPatch
	{
		[HarmonyPrefix]
		private static bool Prefix(ref bool __result)
		{
			if ((UnityEngine.Object)(object)instance == null || !instance.directClientSession)
			{
				return true;
			}
			__result = true;
			return false;
		}
	}

	[HarmonyPatch(typeof(HorayNetworkAuthenticator), "OnServerVersionMessage")]
	private static class RestrictMidRunDirectJoinToRejoinPatch
	{
		[HarmonyPrefix]
		private static bool Prefix(NetworkConnectionToClient conn, out bool __state)
		{
			__state = false;
			if ((UnityEngine.Object)(object)instance == null || (!instance.directHostActive && !instance.directHostStarting))
			{
				return true;
			}
			if (conn != null && conn != NetworkServer.localConnection && !instance.IsDirectKcpConnection(conn))
			{
				instance.Logger.LogWarning((object)("Rejected a non-KCP connection while the direct IP host was active: " + conn.connectionId + "."));
				try
				{
					if (networkServerDisconnectedMethod != null && NetworkServer.connections.ContainsKey(conn.connectionId))
					{
						networkServerDisconnectedMethod.Invoke(null, new object[1] { conn.connectionId });
					}
				}
				catch (Exception ex)
				{
					instance.Logger.LogError((object)"Failed to clean a rejected non-KCP connection.");
					instance.Logger.LogError((object)ex);
				}
				return false;
			}
			__state = instance.ShouldRestrictToDirectRejoin(conn);
			if (__state)
			{
				forceDirectRejoinGate = true;
			}
			return true;
		}

		[HarmonyFinalizer]
		private static void Finalizer(bool __state)
		{
			if (__state)
			{
				forceDirectRejoinGate = false;
			}
		}
	}

	[HarmonyPatch(typeof(HorayNetworkAuthenticator), "get_AccessDeny_InDungeon")]
	private static class ForceDirectRejoinGatePatch
	{
		[HarmonyPostfix]
		private static void Postfix(ref bool __result)
		{
			if (forceDirectRejoinGate)
			{
				__result = true;
			}
		}
	}

	public const string PluginGuid = "dev.dreamyao.sephiria.directconnect";

	public const string PluginName = "Sephiria Direct Connect";

	public const string PluginVersion = "0.4.0";

	private const string IpButtonRowName = "SephiriaDirectConnect_IPButtons";

	private const string IpJoinButtonName = "SephiriaDirectConnect_IPJoin";

	private const string IpHostButtonName = "SephiriaDirectConnect_IPHost";

	private const string IpJoinButtonLabel = "通过 IP 加入";

	private const string IpHostButtonLabel = "通过 IP 创建";

	private const int MinimumPort = 1;

	private const int MaximumPort = 65535;




	private ConfigEntry<int> defaultPort;

	private ConfigEntry<string> lastAddress;

	private readonly HashSet<int> injectedPanelIds = new HashSet<int>();

	private readonly Dictionary<PlayerSpawner, UI_MultiplayerUserIcon> directMemberIcons = new Dictionary<PlayerSpawner, UI_MultiplayerUserIcon>();

	private readonly HashSet<ulong> pendingAvatarRequests = new HashSet<ulong>();

	private GameObject directTransportObject;

	private Transport directHostOriginalTransport;

	private MultiplexTransport directHostTransport;

	private HorayKcpTransport directHostKcpTransport;

	private Transport directClientOriginalTransport;

	private HorayKcpTransport directClientTransport;

	private UI_MultiplayerPanel directHostPanel;

	private UI_MultiplayerPanel directMembersPanel;

	private Harmony harmony;

	private bool directHostActive;

	private bool directHostStarting;

	private bool directClientSession;

	private bool steamLobbyCreateBlockLogged;

	private ushort directHostPort;

	private float nextPanelScanTime;

	[ThreadStatic]
	private static bool forceDirectRejoinGate;

	private static Plugin instance;

	private static readonly MethodInfo networkServerDisconnectedMethod = AccessTools.Method(typeof(NetworkServer), "OnTransportDisconnected", new Type[1] { typeof(int) }, (Type[])null);

	private static readonly MethodInfo networkManagerClientDisconnectedMethod = AccessTools.Method(typeof(NetworkManager), "OnClientDisconnectInternal", (Type[])null, (Type[])null);

	private void Awake()
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Expected O, but got Unknown
		instance = this;
		harmony = new Harmony("dev.dreamyao.sephiria.directconnect");
		harmony.PatchAll();
		defaultPort = Config.Bind<int>("Connection", "DefaultPort", 7777, "UDP port used by the built-in KCP transport.");
		lastAddress = Config.Bind<string>("Connection", "LastAddress", "127.0.0.1:7777", "Last direct-connect address entered in the multiplayer panel.");
		Logger.LogInfo((object)"Sephiria Direct Connect 0.4.0 loaded.");
		WarnAboutTogetherIfPresent();
	}

	private void OnDestroy()
	{
		ClearDirectMemberIcons();
		if (harmony != null)
		{
			harmony.UnpatchSelf();
		}
		if ((UnityEngine.Object)(object)instance == (UnityEngine.Object)(object)this)
		{
			instance = null;
		}
	}

	private void Update()
	{
		if (directHostActive && !IsDirectHostRunning())
		{
			StopDirectHost("The game stopped the active IP host outside the direct lobby.");
		}
		if (directClientSession && !NetworkClient.active)
		{
			RestoreDirectClientTransport("The direct client session ended outside the normal disconnect callback.");
		}
		if (!(Time.unscaledTime < nextPanelScanTime))
		{
			nextPanelScanTime = Time.unscaledTime + 0.25f;
			if (directHostActive)
			{
				LeaveSteamLobbyIfPresent();
			}
			UI_MultiplayerPanel uI_MultiplayerPanel = UnityEngine.Object.FindFirstObjectByType<UI_MultiplayerPanel>();
			if (uI_MultiplayerPanel != null)
			{
				RefreshIpButtonLabels(uI_MultiplayerPanel);
				TryInstallPanelButtons(uI_MultiplayerPanel);
				RefreshDirectHostLobby(uI_MultiplayerPanel);
			}
			RefreshDirectPlayerHud();
		}
	}

	private void TryInstallPanelButtons(UI_MultiplayerPanel panel)
	{
		int instanceID = panel.GetInstanceID();
		if (injectedPanelIds.Contains(instanceID) || panel.searchLobbyGroup == null)
		{
			return;
		}
		if (FindDescendantByName(panel.transform, "SephiriaDirectConnect_IPButtons") != null)
		{
			injectedPanelIds.Add(instanceID);
			return;
		}
		try
		{
			Button button = FindButtonForMethod(panel.searchLobbyGroup, "OnCreateButton", null);
			Button button2 = FindButtonForMethod(panel.searchLobbyGroup, "OnJoinButton", button);
			if (!(button == null) && !(button2 == null))
			{
				injectedPanelIds.Add(instanceID);
				Transform parent = button2.transform.parent;
				if (parent == null || parent != button.transform.parent)
				{
					throw new InvalidOperationException("The original multiplayer buttons are not in the same row.");
				}
				RectTransform rectTransform = parent as RectTransform;
				if (rectTransform == null)
				{
					throw new InvalidOperationException("The original multiplayer button row has no RectTransform.");
				}
				float num = rectTransform.rect.height;
				if (num <= 1f)
				{
					num = rectTransform.sizeDelta.y;
				}
				if (num <= 1f)
				{
					num = 22f;
				}
				float num2 = num + Mathf.Max(4f, num * 0.18f);
				Vector2 anchoredPosition = rectTransform.anchoredPosition;
				GameObject gameObject = UnityEngine.Object.Instantiate(parent.gameObject, parent.parent);
				gameObject.name = "SephiriaDirectConnect_IPButtons";
				RectTransform obj = gameObject.transform as RectTransform;
				if (obj == null)
				{
					UnityEngine.Object.Destroy(gameObject);
					throw new InvalidOperationException("The cloned multiplayer button row has no RectTransform.");
				}
				Button button3 = FindButtonForMethod(gameObject, "OnCreateButton", null);
				Button button4 = FindButtonForMethod(gameObject, "OnJoinButton", button3);
				if (button3 == null || button4 == null)
				{
					UnityEngine.Object.Destroy(gameObject);
					throw new InvalidOperationException("Could not locate both buttons in the cloned multiplayer row.");
				}
				GameObject gameObject2 = button4.gameObject;
				GameObject gameObject3 = button3.gameObject;
				gameObject2.name = "SephiriaDirectConnect_IPJoin";
				gameObject3.name = "SephiriaDirectConnect_IPHost";
				Button button5 = button4;
				Button button6 = button3;
				PrepareClonedButton(button5, "通过 IP 加入", ShowIpJoinDialog);
				PrepareClonedButton(button6, "通过 IP 创建", ShowIpHostDialog);
				rectTransform.anchoredPosition = anchoredPosition + Vector2.up * num2;
				obj.anchoredPosition = anchoredPosition;
				ReserveRoomAboveButtons(panel.searchLobbyGroup, parent, num2);
				parent.SetAsLastSibling();
				gameObject.transform.SetAsLastSibling();
				ConfigureNavigation(button2, button, button5, button6);
				gameObject.SetActive(value: true);
				LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
				LayoutRebuilder.ForceRebuildLayoutImmediate(obj);
				Logger.LogInfo((object)"Added a second IP join/create button row to UI_MultiplayerPanel.");
			}
		}
		catch (Exception ex)
		{
			Logger.LogError((object)"Failed to add direct-connect buttons to the multiplayer panel.");
			Logger.LogError((object)ex);
		}
	}

	private static void RefreshIpButtonLabels(UI_MultiplayerPanel panel)
	{
		SetButtonLabel(FindDescendantByName(panel.transform, "SephiriaDirectConnect_IPJoin"), "通过 IP 加入");
		SetButtonLabel(FindDescendantByName(panel.transform, "SephiriaDirectConnect_IPHost"), "通过 IP 创建");
	}

	private static void SetButtonLabel(Transform buttonTransform, string label)
	{
		if (!(buttonTransform == null))
		{
			TMP_Text componentInChildren = buttonTransform.GetComponentInChildren<TMP_Text>(includeInactive: true);
			if (componentInChildren != null && !string.Equals(componentInChildren.text, label, StringComparison.Ordinal))
			{
				componentInChildren.text = label;
			}
		}
	}

	private static void ReserveRoomAboveButtons(GameObject searchLobbyGroup, Transform buttonRow, float amount)
	{
		ScrollRect[] componentsInChildren = searchLobbyGroup.GetComponentsInChildren<ScrollRect>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			RectTransform rectTransform = componentsInChildren[i].transform as RectTransform;
			if (!(rectTransform == null) && !(rectTransform == buttonRow) && !rectTransform.IsChildOf(buttonRow))
			{
				MoveBottomEdgeUp(rectTransform, amount);
				LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
			}
		}
	}

	private static void MoveBottomEdgeUp(RectTransform rect, float amount)
	{
		if (!Mathf.Approximately(rect.anchorMin.y, rect.anchorMax.y))
		{
			Vector2 offsetMin = rect.offsetMin;
			offsetMin.y += amount;
			rect.offsetMin = offsetMin;
		}
		else if (!(rect.rect.height <= amount + 1f))
		{
			Vector2 sizeDelta = rect.sizeDelta;
			sizeDelta.y -= amount;
			rect.sizeDelta = sizeDelta;
			Vector2 anchoredPosition = rect.anchoredPosition;
			anchoredPosition.y += amount * (1f - rect.pivot.y);
			rect.anchoredPosition = anchoredPosition;
		}
	}

	private static Transform FindDescendantByName(Transform root, string name)
	{
		if (root == null)
		{
			return null;
		}
		Transform transform = root.Find(name);
		if (transform != null)
		{
			return transform;
		}
		for (int i = 0; i < root.childCount; i++)
		{
			Transform transform2 = FindDescendantByName(root.GetChild(i), name);
			if (transform2 != null)
			{
				return transform2;
			}
		}
		return null;
	}

	private static Button FindButtonForMethod(GameObject root, string methodName, Button createButton)
	{
		Button[] componentsInChildren = root.GetComponentsInChildren<Button>(includeInactive: true);
		for (int i = 0; i < componentsInChildren.Length; i++)
		{
			Button.ButtonClickedEvent onClick = componentsInChildren[i].onClick;
			for (int j = 0; j < onClick.GetPersistentEventCount(); j++)
			{
				if (string.Equals(onClick.GetPersistentMethodName(j), methodName, StringComparison.Ordinal))
				{
					return componentsInChildren[i];
				}
			}
		}
		if (createButton == null)
		{
			return null;
		}
		RectTransform rectTransform = createButton.transform as RectTransform;
		Button result = null;
		float num = float.MaxValue;
		foreach (Button button in componentsInChildren)
		{
			if (button == createButton || button.transform.parent != createButton.transform.parent)
			{
				continue;
			}
			RectTransform rectTransform2 = button.transform as RectTransform;
			if (!(rectTransform2 == null) && !(rectTransform == null) && !(rectTransform2.anchoredPosition.x >= rectTransform.anchoredPosition.x))
			{
				float num2 = Mathf.Abs(rectTransform2.anchoredPosition.y - rectTransform.anchoredPosition.y);
				if (num2 < num)
				{
					num = num2;
					result = button;
				}
			}
		}
		return result;
	}

	private static void PrepareClonedButton(Button button, string label, UnityAction action)
	{
		if (button == null)
		{
			throw new InvalidOperationException("The cloned multiplayer button has no Button component.");
		}
		button.onClick = new Button.ButtonClickedEvent();
		button.onClick.AddListener(action);
		button.interactable = true;
		TMP_Text componentInChildren = button.GetComponentInChildren<TMP_Text>(includeInactive: true);
		if (componentInChildren != null)
		{
			componentInChildren.text = label;
		}
	}

	private static void ConfigureNavigation(Button join, Button create, Button ipJoin, Button ipHost)
	{
		UI_HorayButton obj = join as UI_HorayButton;
		UI_HorayButton uI_HorayButton = create as UI_HorayButton;
		UI_HorayButton uI_HorayButton2 = ipJoin as UI_HorayButton;
		UI_HorayButton uI_HorayButton3 = ipHost as UI_HorayButton;
		obj?.SetForceNavDown(ipJoin);
		uI_HorayButton?.SetForceNavDown(ipHost);
		uI_HorayButton2?.SetForceNavUp(join);
		uI_HorayButton2?.SetForceNavRight(ipHost);
		uI_HorayButton3?.SetForceNavUp(create);
		uI_HorayButton3?.SetForceNavLeft(ipJoin);
	}

	private void ShowIpHostDialog()
	{
		UI_MessageBox_InputYesNo inputDialog = GetInputDialog();
		if (!(inputDialog == null))
		{
			inputDialog.Open("请输入要监听的 UDP 端口", StartIpHost, null, ClampPort(defaultPort.Value).ToString(), "例如：7777", allowEmpty: false, 5);
		}
	}

	private void ShowIpJoinDialog()
	{
		if (DungeonManager.Instance != null && DungeonManager.Instance.Race != null && DungeonManager.Instance.Race.isMultiplayerBlocked)
		{
			ShowSystemMessage(DungeonManager.Instance.Race.multiplayerBlockedMessage.ToString());
			return;
		}
		UI_MessageBox_InputYesNo inputDialog = GetInputDialog();
		if (!(inputDialog == null))
		{
			string defaultInput = (string.IsNullOrWhiteSpace(lastAddress.Value) ? ("127.0.0.1:" + ClampPort(defaultPort.Value)) : lastAddress.Value);
			inputDialog.Open("请输入主机 IP 和端口", StartIpClient, null, defaultInput, "例如：192.168.1.20:7777", allowEmpty: false, 160);
		}
	}

	private UI_MessageBox_InputYesNo GetInputDialog()
	{
		if (UIManager.Instance == null)
		{
			Logger.LogWarning((object)"UIManager is not available.");
			return null;
		}
		UI_MessageBox_InputYesNo element = UIManager.Instance.GetElement<UI_MessageBox_InputYesNo>();
		if (element == null)
		{
			ShowSystemMessage("无法打开输入框，请重新打开联机石板后再试。");
		}
		return element;
	}

	private void StartIpHost(string value)
	{
		if (!TryReadPort(value, out var port))
		{
			ShowSystemMessage("端口无效，请输入 1 到 65535 之间的数字。");
			return;
		}
		if (operation != null || SteamInvitation.waitForExternalConnect)
		{
			ShowSystemMessage("正在切换联机会话，请稍候。");
			return;
		}
		defaultPort.Value = port;
		BeginSessionOperation(SwitchToDirectHost(port));
	}

	private bool EnableDirectHost(ushort port)
	{
		NetworkManager singleton = NetworkManager.singleton;
		if (singleton == null || !NetworkServer.activeHost)
		{
			ShowSystemMessage("当前本地世界尚未作为主机运行，无法开启 IP 监听。");
			return false;
		}
		directHostStarting = true;
		try
		{
			return EnableDirectHostCore(singleton, port);
		}
		finally
		{
			directHostStarting = false;
		}
	}

	private bool EnableDirectHostCore(NetworkManager manager, ushort port)
	{
		LeaveSteamLobbyIfPresent();
		Transport original = manager.transport != null ? manager.transport : Transport.active;
		if (original == null) return false;
		directHostOriginalTransport = original;
		try
		{
			directTransportObject = new GameObject("SephiriaDirectConnect_MultiplexTransport");
			directTransportObject.SetActive(false);
			directTransportObject.transform.SetParent(manager.transform, false);
			directHostKcpTransport = directTransportObject.AddComponent<HorayKcpTransport>();
			directHostKcpTransport.Port = port;
			MultiplexTransport multiplex = directTransportObject.AddComponent<MultiplexTransport>();
			multiplex.transports = new Transport[] { directHostKcpTransport };
			// Preserve callbacks before any operation which may fail while stopping.
			CopyServerCallbacks(original, multiplex);
			directHostTransport = multiplex;
			directTransportObject.SetActive(true);
			original.ServerStop();
			original.enabled = false;
			manager.transport = multiplex;
			Transport.active = multiplex;
			multiplex.ServerStart();
			if (!directHostKcpTransport.ServerActive())
				throw new InvalidOperationException("KCP listener did not become active.");
			LeaveSteamLobbyIfPresent();
			directHostActive = true;
			steamLobbyCreateBlockLogged = false;
			directHostPort = port;
			Logger.LogInfo("Direct host listening on UDP " + port + ". Previous remote connections were cleared.");
			return true;
		}
		catch (Exception ex)
		{
			Logger.LogError("Failed to enable direct host; restoring original transport: " + ex);
			try
			{
				if (directHostKcpTransport != null && directHostKcpTransport.ServerActive())
					directHostKcpTransport.ServerStop();
			}
			catch (Exception stopError) { Logger.LogError(stopError); }
			directHostActive = false;
			directHostPort = 0;
			RestoreOriginalTransport();
			return false;
		}
	}

	private static void CopyServerCallbacks(Transport source, Transport target)
	{
#pragma warning disable CS0618 // Preserve the legacy callback too when restoring the installed transport.
		target.OnServerConnected = source.OnServerConnected;
#pragma warning restore CS0618
		target.OnServerConnectedWithAddress = source.OnServerConnectedWithAddress;
		target.OnServerDataReceived = source.OnServerDataReceived;
		target.OnServerDataSent = source.OnServerDataSent;
		target.OnServerError = source.OnServerError;
		target.OnServerTransportException = source.OnServerTransportException;
		target.OnServerDisconnected = source.OnServerDisconnected;
	}

	private void ShowDirectHostLobby(ushort port)
	{
		UI_MultiplayerPanel uI_MultiplayerPanel = UnityEngine.Object.FindFirstObjectByType<UI_MultiplayerPanel>();
		if (uI_MultiplayerPanel == null)
		{
			Logger.LogWarning((object)"The direct host started, but UI_MultiplayerPanel is not available.");
			ShowSystemMessage("IP 主机已开启，但无法打开大厅界面；请重新打开联机石板。");
		}
		else
		{
			directHostPort = port;
			RefreshDirectHostLobby(uI_MultiplayerPanel);
		}
	}

	private void RefreshDirectHostLobby(UI_MultiplayerPanel panel)
	{
		if (directHostActive && directHostPort != 0 && !(panel == null))
		{
			bool flag = directHostPanel != panel;
			string text = "UDP " + directHostPort;
			bool num = panel.roomCodeText != null && !string.Equals(panel.roomCodeText.text, text, StringComparison.Ordinal);
			directHostPanel = panel;
			panel.searchLobbyGroup.SetActive(value: false);
			panel.createLobbyGroup.SetActive(value: false);
			panel.enteredLobbyGroup.SetActive(value: true);
			if (panel.rejoinGroup != null)
			{
				panel.rejoinGroup.SetActive(value: false);
			}
			if (panel.enterMultizoneButton != null)
			{
				panel.enterMultizoneButton.SetActive(value: true);
			}
			if (panel.roomCodeHideObj != null)
			{
				panel.roomCodeHideObj.SetActive(value: false);
			}
			if (panel.roomCodeText != null)
			{
				panel.roomCodeText.text = text;
			}
			if (panel.roomNameText != null)
			{
				panel.roomNameText.text = "IP 直连大厅";
			}
			RefreshDirectHostMemberIcons(panel);
			if (flag)
			{
				ConfigureDirectHostLobbyButtons(panel);
			}
			if (num)
			{
				Logger.LogInfo((object)("Restored the direct host lobby display for UDP port " + directHostPort + "."));
			}
		}
	}

	private void RefreshDirectHostMemberIcons(UI_MultiplayerPanel panel)
	{
		if (panel.memberPrefab == null || panel.contentRoot == null)
		{
			return;
		}
		if (directMembersPanel != panel)
		{
			ClearDirectMemberIcons();
			directMembersPanel = panel;
		}
		HashSet<PlayerSpawner> hashSet = new HashSet<PlayerSpawner>();
		foreach (PlayerSpawner multiplayer in PlayerSpawner.MultiplayerList)
		{
			if (multiplayer == null || multiplayer.PlayerAvatar == null)
			{
				continue;
			}
			hashSet.Add(multiplayer);
			if (!directMemberIcons.TryGetValue(multiplayer, out var value) || value == null)
			{
				GameObject gameObject = UnityEngine.Object.Instantiate(panel.memberPrefab, panel.contentRoot);
				gameObject.name = "SephiriaDirectConnect_Member_" + multiplayer.netId;
				value = gameObject.GetComponent<UI_MultiplayerUserIcon>();
				if (value == null)
				{
					UnityEngine.Object.Destroy(gameObject);
					continue;
				}
				directMemberIcons[multiplayer] = value;
				Logger.LogInfo((object)("Added direct lobby member: " + multiplayer.PlayerAvatar.Name + "."));
			}
			UpdateDirectMemberIcon(multiplayer, value);
		}
		List<PlayerSpawner> list = new List<PlayerSpawner>();
		foreach (KeyValuePair<PlayerSpawner, UI_MultiplayerUserIcon> directMemberIcon in directMemberIcons)
		{
			if (directMemberIcon.Key == null || !hashSet.Contains(directMemberIcon.Key))
			{
				if (directMemberIcon.Value != null)
				{
					UnityEngine.Object.Destroy(directMemberIcon.Value.gameObject);
				}
				list.Add(directMemberIcon.Key);
			}
		}
		foreach (PlayerSpawner item in list)
		{
			directMemberIcons.Remove(item);
		}
	}

	private void UpdateDirectMemberIcon(PlayerSpawner spawner, UI_MultiplayerUserIcon icon)
	{
		string text = (string.IsNullOrWhiteSpace(spawner.PlayerAvatar.Name) ? "玩家" : spawner.PlayerAvatar.Name);
		Texture2D texture2D = null;
		if (TryGetSteamUser(spawner.steamID, out var user))
		{
			icon.userData = user;
			if (!string.IsNullOrWhiteSpace(user.Nickname))
			{
				text = user.Nickname;
			}
			texture2D = GetOrRequestAvatar(user);
		}
		if (icon.userNameText != null)
		{
			icon.userNameText.text = text;
		}
		if (icon.hostImage != null)
		{
			icon.hostImage.gameObject.SetActive(spawner.isHost);
		}
		if (icon.kickButton != null)
		{
			icon.kickButton.SetActive(value: false);
		}
		if (texture2D != null)
		{
			icon.SetAvatar(texture2D);
		}
	}

	private void RefreshDirectPlayerHud()
	{
		if (!IsDirectSessionActive() || UIManager.Instance == null)
		{
			return;
		}
		UI_MultiplayerHUD element = UIManager.Instance.GetElement<UI_MultiplayerHUD>();
		if (element == null || element.contentsZone == null)
		{
			return;
		}
		PlayerAvatar playerAvatar = ((GameCamera.Instance != null) ? GameCamera.Instance.Observer : null);
		foreach (PlayerSpawner multiplayer in PlayerSpawner.MultiplayerList)
		{
			if (!(multiplayer == null) && !(multiplayer.PlayerAvatar == null) && !(multiplayer.HPBarObject == null) && !multiplayer.isOwned && !(multiplayer.PlayerAvatar == playerAvatar))
			{
				UI_MultiplayerHPBar hPBarObject = multiplayer.HPBarObject;
				bool flag = false;
				if (hPBarObject.transform.parent != element.contentsZone)
				{
					hPBarObject.transform.SetParent(element.contentsZone);
					hPBarObject.transform.localScale = Vector3.one;
					flag = true;
				}
				if (!hPBarObject.gameObject.activeSelf)
				{
					hPBarObject.gameObject.SetActive(value: true);
					flag = true;
				}
				if (multiplayer.WorldUserName != null)
				{
					multiplayer.WorldUserName.text = multiplayer.PlayerAvatar.Name;
					multiplayer.WorldUserName.gameObject.SetActive(value: true);
				}
				string text = string.Empty;
				Texture2D face = null;
				if (TryGetSteamUser(multiplayer.steamID, out var user))
				{
					text = user.Nickname;
					face = GetOrRequestAvatar(user);
					element.avatarNicknames[multiplayer.steamID] = text;
				}
				hPBarObject.SetSteamProfile(text, face);
				if (flag)
				{
					Logger.LogInfo((object)("Activated direct multiplayer HUD for " + multiplayer.PlayerAvatar.Name + "."));
				}
			}
		}
	}

	private bool IsDirectSessionActive()
	{
		if (directHostActive)
		{
			return true;
		}
		NetworkManager singleton = NetworkManager.singleton;
		if (singleton != null && NetworkClient.active && !NetworkServer.active)
		{
			return singleton.transport is HorayKcpTransport;
		}
		return false;
	}

	private static bool TryGetSteamUser(ulong steamId, out UserData user)
	{
		user = default(UserData);
		if (steamId == 0L)
		{
			return false;
		}
		try
		{
			user = UserData.Get(steamId);
			return user.IsValid;
		}
		catch
		{
			return false;
		}
	}

	private Texture2D GetOrRequestAvatar(UserData user)
	{
		if (ScreenFader.Instance != null && ScreenFader.Instance.avatars.TryGetValue(user.SteamId, out var value) && value != null)
		{
			return value;
		}
		if (!pendingAvatarRequests.Add(user.SteamId))
		{
			return null;
		}
		try
		{
			user.LoadAvatar(delegate(Texture2D loadedAvatar)
			{
				pendingAvatarRequests.Remove(user.SteamId);
				if (loadedAvatar != null && ScreenFader.Instance != null)
				{
					ScreenFader.Instance.avatars[user.SteamId] = loadedAvatar;
				}
			});
		}
		catch (Exception ex)
		{
			pendingAvatarRequests.Remove(user.SteamId);
			Logger.LogDebug((object)("Could not request Steam avatar " + user.SteamId + ": " + ex.Message));
		}
		return null;
	}

	private void ClearDirectMemberIcons()
	{
		foreach (UI_MultiplayerUserIcon value in directMemberIcons.Values)
		{
			if (value != null)
			{
				UnityEngine.Object.Destroy(value.gameObject);
			}
		}
		directMemberIcons.Clear();
		directMembersPanel = null;
	}

	private void ConfigureDirectHostLobbyButtons(UI_MultiplayerPanel panel)
	{
		if (panel.enterMultiZoneButton != null)
		{
			panel.enterMultiZoneButton.onClick.RemoveListener(EnterDirectMultiZone);
			panel.enterMultiZoneButton.onClick.AddListener(EnterDirectMultiZone);
		}
		Button button = ((panel.leaveLobbyButton != null) ? panel.leaveLobbyButton.GetComponent<Button>() : null);
		if (button != null)
		{
			button.onClick = new Button.ButtonClickedEvent();
			button.onClick.AddListener(ConfirmLeaveDirectHost);
		}
	}

	private void EnterDirectMultiZone()
	{
		if (directHostActive && !(DungeonManager.Instance == null) && !(GameCamera.Instance == null))
		{
			PlayerAvatar observer = GameCamera.Instance.Observer;
			FloorData floorData = DungeonManager.Instance.FindFloorByName("MultiZone");
			if (observer == null || floorData == null)
			{
				ShowSystemMessage("无法进入联机区域，请稍后再试。");
			}
			else
			{
				DungeonManager.Instance.MoveFloor(observer, floorData.guid, "FLOORSTARTING", 0, recordHistory: false, allowSave: false, keepPrevFloor: false, randomPosition: true);
			}
		}
	}

	private void ConfirmLeaveDirectHost()
	{
		if (!(directHostPanel == null) && !(UIManager.Instance == null))
		{
			UIManager.Instance.GetElement<UI_MessageBoxHolder>().OpenYesNo(directHostPanel.leaveLobbyMessageString.ToString(), LeaveDirectHost, null);
		}
	}

	private void LeaveDirectHost()
	{
		StopDirectHost("The player left the direct IP lobby.");
	}

	private bool IsDirectHostRunning()
	{
		NetworkManager singleton = NetworkManager.singleton;
		if (singleton != null && NetworkServer.activeHost && directHostTransport != null && directHostKcpTransport != null && singleton.transport == directHostTransport)
		{
			return directHostKcpTransport.ServerActive();
		}
		return false;
	}

	private bool IsDirectKcpConnection(NetworkConnectionToClient connection)
	{
		if ((!directHostActive && !directHostStarting) || connection == null || connection == NetworkServer.localConnection || directHostTransport == null || directHostKcpTransport == null)
		{
			return false;
		}
		int num = Array.IndexOf(directHostTransport.transports, directHostKcpTransport);
		if (num >= 0 && directHostTransport.OriginalId(connection.connectionId, out var _, out var transportIndex))
		{
			return transportIndex == num;
		}
		return false;
	}

	private bool ShouldRestrictToDirectRejoin(NetworkConnectionToClient connection)
	{
		if (IsDirectKcpConnection(connection) && DungeonManager.Instance != null)
		{
			return DungeonManager.Instance.isRunStarted;
		}
		return false;
	}

	private void StopDirectHost(string reason)
	{
		UI_MultiplayerPanel uI_MultiplayerPanel = directHostPanel;
		directHostActive = false;
		directHostStarting = false;
		steamLobbyCreateBlockLogged = false;
		directHostPort = 0;
		ClearDirectMemberIcons();
		DisconnectDirectClients();
		if (directHostKcpTransport != null)
		{
			try
			{
				if (directHostKcpTransport.ServerActive())
				{
					directHostKcpTransport.ServerStop();
				}
			}
			catch (Exception ex)
			{
				Logger.LogWarning((object)("Failed to stop the direct KCP listener cleanly: " + ex.Message));
			}
		}
		RestoreOriginalTransport();
		LeaveSteamLobbyIfPresent();
		if (uI_MultiplayerPanel != null)
		{
			if (uI_MultiplayerPanel.enterMultiZoneButton != null)
			{
				uI_MultiplayerPanel.enterMultiZoneButton.onClick.RemoveListener(EnterDirectMultiZone);
			}
			Button button = ((uI_MultiplayerPanel.leaveLobbyButton != null) ? uI_MultiplayerPanel.leaveLobbyButton.GetComponent<Button>() : null);
			if (button != null)
			{
				button.onClick = new Button.ButtonClickedEvent();
				button.onClick.AddListener(uI_MultiplayerPanel.OnLeaveButton);
			}
			uI_MultiplayerPanel.searchLobbyGroup.SetActive(value: true);
			uI_MultiplayerPanel.createLobbyGroup.SetActive(value: false);
			uI_MultiplayerPanel.enteredLobbyGroup.SetActive(value: false);
			if (uI_MultiplayerPanel.rejoinGroup != null)
			{
				uI_MultiplayerPanel.rejoinGroup.SetActive(value: false);
			}
			if (uI_MultiplayerPanel.roomCodeText != null)
			{
				uI_MultiplayerPanel.roomCodeText.text = string.Empty;
			}
			if (uI_MultiplayerPanel.roomNameText != null)
			{
				uI_MultiplayerPanel.roomNameText.text = string.Empty;
			}
		}
		directHostPanel = null;
		Logger.LogInfo((object)(reason + " Direct connections and lobby state were cleared."));
	}

	private void DisconnectDirectClients()
	{
		if (directHostTransport == null || directHostKcpTransport == null)
		{
			return;
		}
		int num = Array.IndexOf(directHostTransport.transports, directHostKcpTransport);
		if (num < 0)
		{
			return;
		}
		List<int> list = new List<int>();
		foreach (KeyValuePair<int, NetworkConnectionToClient> connection in NetworkServer.connections)
		{
			if (connection.Key != 0 && connection.Value != null && directHostTransport.OriginalId(connection.Key, out var _, out var transportIndex) && transportIndex == num)
			{
				list.Add(connection.Key);
			}
		}
		foreach (int item in list)
		{
			if (!directHostTransport.OriginalId(item, out var originalConnectionId2, out var transportIndex2))
			{
				continue;
			}
			if (NetworkServer.connections.TryGetValue(item, out var value) && value != null)
			{
				try
				{
					if (Transport.active == directHostTransport)
					{
						value.Disconnect();
					}
				}
				catch (Exception ex)
				{
					Logger.LogWarning((object)("Failed to send the disconnect packet to direct client " + item + ": " + ex.Message));
				}
				try
				{
					if (NetworkServer.connections.ContainsKey(item) && networkServerDisconnectedMethod != null)
					{
						networkServerDisconnectedMethod.Invoke(null, new object[1] { item });
					}
				}
				catch (Exception ex2)
				{
					Logger.LogError((object)("Failed to clean Mirror connection " + item + "."));
					Logger.LogError((object)ex2);
				}
			}
			directHostTransport.RemoveFromLookup(originalConnectionId2, transportIndex2);
		}
		if (list.Count > 0)
		{
			Logger.LogInfo((object)("Disconnected and cleaned " + list.Count + " direct client connection(s)."));
		}
	}

	private void RestoreOriginalTransport()
	{
		MultiplexTransport multiplexTransport = directHostTransport;
		HorayKcpTransport horayKcpTransport = directHostKcpTransport;
		Transport transport = directHostOriginalTransport;
		NetworkManager singleton = NetworkManager.singleton;
		if (transport != null)
		{
			if (multiplexTransport != null)
			{
				CopyServerCallbacks(multiplexTransport, transport);
			}
			transport.enabled = true;
			if (singleton != null && singleton.transport == multiplexTransport)
			{
				singleton.transport = transport;
			}
			if (Transport.active == multiplexTransport)
			{
				Transport.active = transport;
			}
			if (NetworkServer.active && !transport.ServerActive())
			{
				try
				{
					transport.ServerStart();
					Logger.LogInfo((object)"Restored the original Steam transport after the direct IP host stopped.");
				}
				catch (Exception ex)
				{
					Logger.LogError((object)"Failed to restart the original multiplayer transport.");
					Logger.LogError((object)ex);
				}
			}
		}
		if (multiplexTransport != null)
		{
			multiplexTransport.transports = ((!(horayKcpTransport != null)) ? new Transport[0] : new Transport[1] { horayKcpTransport });
		}
		if (directTransportObject != null)
		{
			UnityEngine.Object.Destroy(directTransportObject);
		}
		directTransportObject = null;
		directHostOriginalTransport = null;
		directHostTransport = null;
		directHostKcpTransport = null;
	}

	private void LeaveSteamLobbyIfPresent()
	{
		try
		{
			GameObject gameObject = SingletonObject.Find("SteamManager");
			if (gameObject != null && gameObject.TryGetComponent<LobbyManager>(out var component) && component.HasLobby)
			{
				component.Leave();
				Logger.LogInfo((object)"Left an unexpected Steam Lobby while using direct IP hosting.");
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning((object)("Could not clear the Steam Lobby state: " + ex.Message));
		}
	}

	private void StartIpClient(string value)
	{
		if (!TryParseEndpoint(value, out var host, out var port))
		{
			ShowSystemMessage("地址无效，请使用 IP:端口，例如 192.168.1.20:7777。");
			return;
		}
		if (operation != null || SteamInvitation.waitForExternalConnect)
		{
			ShowSystemMessage("正在切换联机会话，请稍候。");
			return;
		}
		defaultPort.Value = port;
		lastAddress.Value = value.Trim();
		BeginSessionOperation(SwitchToDirectSession(host, port));
	}

	private void ConfigureDirectClientKcp(NetworkManager manager, ushort port)
	{
		Transport transport = ((manager.transport != null) ? manager.transport : Transport.active);
		HorayKcpTransport horayKcpTransport = manager.GetComponent<HorayKcpTransport>();
		if (horayKcpTransport == null)
		{
			horayKcpTransport = manager.gameObject.AddComponent<HorayKcpTransport>();
		}
		if (transport == horayKcpTransport)
		{
			Transport[] components = manager.GetComponents<Transport>();
			foreach (Transport transport2 in components)
			{
				if (transport2 != null && transport2 != horayKcpTransport)
				{
					transport = transport2;
					break;
				}
			}
		}
		horayKcpTransport.Port = port;
		horayKcpTransport.enabled = true;
		directClientOriginalTransport = ((transport != horayKcpTransport) ? transport : null);
		directClientTransport = horayKcpTransport;
		directClientSession = true;
		manager.transport = horayKcpTransport;
		Transport.active = horayKcpTransport;
	}

	private void AbortDirectClient(NetworkManager manager, string reason)
	{
		try
		{
			if (manager != null && NetworkClient.active)
			{
				manager.StopClient();
			}
		}
		catch (Exception ex)
		{
			Logger.LogError("StopClient failed; continuing disconnect cleanup: " + ex);
		}
		try
		{
			if (manager != null && NetworkClient.active && networkManagerClientDisconnectedMethod != null)
				networkManagerClientDisconnectedMethod.Invoke(manager, null);
		}
		catch (Exception ex) { Logger.LogError("Disconnect callback failed: " + ex); }
		try
		{
			if (NetworkClient.active) NetworkClient.Shutdown();
		}
		catch (Exception ex) { Logger.LogError("Client shutdown failed: " + ex); }
		// Never switch transports under a still-live client.
		if (!NetworkClient.active) RestoreDirectClientTransport(reason);
	}

	private void RestoreDirectClientTransport(string reason)
	{
		if (!directClientSession && directClientTransport == null && directClientOriginalTransport == null)
		{
			return;
		}
		HorayKcpTransport horayKcpTransport = directClientTransport;
		Transport transport = directClientOriginalTransport;
		NetworkManager singleton = NetworkManager.singleton;
		directClientSession = false;
		directClientTransport = null;
		directClientOriginalTransport = null;
		if (transport != null)
		{
			transport.enabled = true;
			if (singleton != null && (singleton.transport == horayKcpTransport || singleton.transport == null))
			{
				singleton.transport = transport;
			}
			if (Transport.active == horayKcpTransport || Transport.active == null)
			{
				Transport.active = transport;
			}
		}
		if (horayKcpTransport != null && horayKcpTransport != transport)
		{
			horayKcpTransport.enabled = false;
		}
		Logger.LogInfo((object)(reason + " Original multiplayer transport restored."));
	}

	private bool TryParseEndpoint(string value, out string host, out ushort port)
	{
		host = null;
		port = 0;
		string text = (value ?? string.Empty).Trim();
		if (text.Length == 0)
		{
			return false;
		}
		if (!Uri.TryCreate("kcp://" + text, UriKind.Absolute, out var result) || string.IsNullOrWhiteSpace(result.Host))
		{
			return false;
		}
		int num = ((result.IsDefaultPort || result.Port < 1) ? ClampPort(defaultPort.Value) : result.Port);
		if (num < 1 || num > 65535)
		{
			return false;
		}
		host = result.Host;
		port = (ushort)num;
		return true;
	}

	private static bool TryReadPort(string value, out ushort port)
	{
		if (int.TryParse(value, out var result) && result >= 1 && result <= 65535)
		{
			port = (ushort)result;
			return true;
		}
		port = 0;
		return false;
	}

	private static int ClampPort(int value)
	{
		return Math.Max(1, Math.Min(65535, value));
	}

	private static string GetSelectedProfile()
	{
		if (OptionsBinding.Instance != null && OptionsBinding.Instance.Options != null)
		{
			return OptionsBinding.Instance.Options.GetString("SelectedProfile", SaveManager.defaultSlotName);
		}
		return SaveManager.defaultSlotName;
	}

	private void ShowSystemMessage(string message)
	{
		if (UIManager.Instance != null)
		{
			UI_SystemMessage element = UIManager.Instance.GetElement<UI_SystemMessage>();
			if (element != null)
			{
				element.Open(message, 4f);
				return;
			}
		}
		Logger.LogWarning((object)message);
	}

	private void WarnAboutTogetherIfPresent()
	{
		foreach (PluginInfo value in Chainloader.PluginInfos.Values)
		{
			if (value.Metadata.Name.IndexOf("SephiriaTogether", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				Logger.LogWarning((object)"SephiriaTogether is also loaded. Direct Connect changes the active transport only while switching to an IP session.");
				break;
			}
		}
	}
}




