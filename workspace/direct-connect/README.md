# Sephiria Direct Connect 0.4.0（本地维护版）

基于本机已安装的 0.3.9 DLL 反编译建立的维护工程，不代表上游官方发布。插件 GUID 保持 `dev.dreamyao.sephiria.directconnect`，沿用原配置。只修改用户选定的建议 1、2、3、5；没有增加踢人功能。

## 本次改动

- 统一执行连接协程及嵌套协程，捕获异常并释放切换锁。每个等待阶段有独立超时；临时状态条显示当前阶段，并提供“取消连接”。
- 取消或失败发生在旧会话关闭之后时，清理客户端、恢复传输，再通过原版流程返回标题。旧会话尚未改动时保留当前世界。恢复本身也有超时，失败原因保留在可关闭的提示中。
- 去掉 Mod 的 0.2 / 0.3 秒固定等待。先等存档写入结束，再等网络关闭、Mirror 延迟离线场景任务、场景加载与淡出结束。原版 `SaveManager.Load/CreateNewTMP` 是同步操作，执行后核对存档对象和槽位绑定。
- 成功必须满足：连接建立、收到原版认证成功响应、Mirror 认证与 ready、本地玩家生成并获控制权、首次传送完成、镜头绑定、场景与淡出结束。单纯 `isConnected` 不再视为成功。
- 分别显示版本不一致、已有连接未清理、对局已开始或不符合重连条件等原版拒绝原因。
- 创建 IP 房间或更换端口之前，通过仍在工作的旧传输断开远端玩家，并调用原版 Mirror 清理入口移除其角色与连接记录。重复打开同一端口保留当前成员；换端口会断开成员，需要重新加入。
- 监听失败时恢复旧传输；先保存回调再停止旧传输，避免停止过程抛错后把原回调覆盖为空。无法恢复有效主机时进入统一标题恢复流程。

## 核对过的原版接口

核对来源为本机 `Sephiria_Data/Managed` 中的程序集，不依赖固定延时猜测：

- `NetworkManager.OnClientDisconnectInternal` 会延迟调用 `ClientChangeOfflineScene`；就绪检查包含 `IsInvoking` 和 `loadingSceneAsync`。
- `HorayNetworkAuthenticator.OnClientVersionResponseMessage` 提供认证结果与拒绝码；仍执行完整原方法。
- `PlayerSpawner.authorityStartPhase` 在首次传送回调 `CloseWorldMapOnMove` 中从 1 变为 2。该字段不存在时会在关闭旧会话之前停止连接并报兼容性错误。
- `HorayNetworkManager.GoToTitleScene` 会检查 `SteamInvitation.waitForExternalConnect`，因此恢复前释放该原版锁，同时用 Mod 自身操作状态防止重复点击。
- 当前游戏的 `EOSLobbyManager.WaitUntilReady` 是空协程，直连流程不再调用它。

## 构建和离线验证

从工作区根目录运行：

```powershell
$env:APPDATA = Join-Path (Get-Location) '.nuget-appdata'
dotnet build .\direct-connect\SephiriaDirectConnect.csproj -c Release --configfile .\NuGet.Config
dotnet run --project .\direct-connect\tests\Regression.csproj -c Release
```

已通过 Release 构建（0 警告、0 错误）与 10 项离线回归，覆盖阶段超时、取消、保留认证拒绝原因、恢复超时、嵌套协程异常传播与 finally 清理。测试不启动 Unity，也不建立实际网络连接。

## 安装与回滚

推荐在模组管理器中从 `MKfeel/SephiriaMods` 的 `catalog/mods.json` 安装“IP 直连”0.4.0。也可下载标准安装包，完全退出游戏后将其中的 `BepInEx` 目录合并到游戏目录。需要 BepInEx 5；安装前备份原来的 DLL，回滚时退出游戏并恢复备份。不要同时保留两个同 GUID 的插件 DLL。

DLL SHA-256：`9AC0FD64B9098BDDE7537C7F0B3B46229D0B6188EA8BEB8665B6292DB5E5BFFD`。

`Install-Local.ps1` 是原开发电脑的安装辅助脚本，只接受已核对的 0.3.9 基线；其他电脑推荐使用管理器。仓库不包含私人连接配置和开发电脑的回滚备份。构建时可用 `-p:GameDirectory=你的游戏目录` 指定引用路径。

## 待游戏验收

尚未启动或控制游戏。双机运行仍需确认：正常创建与加入；地址不可达时取消和超时后返回标题；版本不一致的具体提示；旧玩家途中重连与新玩家被拒绝；从 Steam 房间切换到 IP 后无残留成员；端口占用与换端口后可重新加入；返回标题后普通 Steam 联机仍可用。

同步原版方法在主线程执行期间不能被点击取消强行中断；超时与取消针对各异步等待阶段。原版返回标题协程内部的延时没有改动。

