using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;
namespace SephiriaHiddenRoomHints { public sealed partial class HiddenRoomHintPlugin {
		private bool WasInstantKillPressed()
		{
			if (instantKillKey.Value == KeyCode.None)
			{
				return false;
			}
			Key? key = MapInputSystemKey(instantKillKey.Value);
			return key.HasValue && Keyboard.current != null && Keyboard.current[key.Value].wasPressedThisFrame;
		}

		private static Key? MapInputSystemKey(KeyCode keyCode)
		{
			if (1 == 0)
			{
			}
			Key? result = keyCode switch
			{
				KeyCode.F1 => Key.F1, 
				KeyCode.F2 => Key.F2, 
				KeyCode.F3 => Key.F3, 
				KeyCode.F4 => Key.F4, 
				KeyCode.F5 => Key.F5, 
				KeyCode.F6 => Key.F6, 
				KeyCode.F7 => Key.F7, 
				KeyCode.F8 => Key.F8, 
				KeyCode.F9 => Key.F9, 
				KeyCode.F10 => Key.F10, 
				KeyCode.F11 => Key.F11, 
				KeyCode.F12 => Key.F12, 
				_ => null, 
			};
			if (1 == 0)
			{
			}
			return result;
		}

		private void ForceFirstFloorHiddenRoom()
		{
			if (!forceHiddenRoomOnFirstFloor.Value || !NetworkServer.active || DungeonManager.Instance == null)
			{
				return;
			}
			var array = FloorGenerator.FloorGenerators;
			foreach (var generator in array)
			{
				if (!(generator is EnhancedProceduralFloorGenerator enhancedProceduralFloorGenerator) || !enhancedProceduralFloorGenerator || enhancedProceduralFloorGenerator.GenerateSuccess)
				{
					continue;
				}
				FloorData dataOnServer = enhancedProceduralFloorGenerator.DataOnServer;
				if (dataOnServer == null || !IsFirstFloor(dataOnServer) || !DungeonManager.Instance.generatedFloors.TryGetValue(enhancedProceduralFloorGenerator.guid, out var value))
				{
					continue;
				}
				int hiddenRoomCount = Math.Max(1, value.hiddenRoomCount);
				if (value.hiddenRoomCount < hiddenRoomCount)
				{
					value.hiddenRoomCount = hiddenRoomCount;
					if (dataOnServer != value)
					{
						dataOnServer.hiddenRoomCount = hiddenRoomCount;
					}
					base.Logger.LogInfo("Test mode: forced a hidden room on first floor '" + dataOnServer.name + "' (" + dataOnServer.guid + ").");
				}
			}
		}

		private static bool IsFirstFloor(FloorData data)
		{
			StageEntity stageEntity = DungeonManager.Instance.FindStage(data.stageName);
			return stageEntity != null && stageEntity.firstFloor != null && data.globalX == 0 && string.Equals(data.name, stageEntity.firstFloor.name, StringComparison.Ordinal);
		}

		private void InstantKillAllEnemies()
		{
			if (!NetworkServer.active)
			{
				ShowTestNotice("秒杀功能需要在单机或房主端使用。", Color.orange);
				return;
			}
			int num = 0;
			UnitAvatar[] array = UnityEngine.Object.FindObjectsByType<UnitAvatar>(FindObjectsSortMode.None);
			foreach (UnitAvatar unitAvatar in array)
			{
				if ((bool)unitAvatar && (!(unitAvatar is PlayerAvatar) && !unitAvatar.IsDead))
				{
					unitAvatar.ForceDie();
					num++;
				}
			}
			ShowTestNotice($"测试秒杀：已处理 {num} 个非玩家单位。", Color.cyan);
		}

		private void ShowTestNotice(string notice, Color color)
		{
			if (showSystemMessage.Value && UIManager.Instance != null)
			{
				UI_SystemMessage element = UIManager.Instance.GetElement<UI_SystemMessage>();
				if (element != null)
				{
					element.Open(notice, 5f);
				}
			}
			if (GameLogWriter.Instance != null)
			{
				GameLogWriter.Instance.WriteLog(notice, color);
			}
			screenNotice = notice;
			screenNoticeUntil = Time.unscaledTime + 7f;
			base.Logger.LogInfo(notice);
		}


} }
