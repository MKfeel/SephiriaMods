using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SephiriaItemLabTweaks
{
	internal sealed class EnchantToggleResult
	{
		internal bool Success { get; }

		internal bool Restored { get; }

		internal int Changed { get; }

		internal int Total { get; }

		internal string Message { get; }

		internal EnchantToggleResult(bool success, bool restored, int changed, int total, string message)
		{
			Success = success;
			Restored = restored;
			Changed = changed;
			Total = total;
			Message = message;
		}
	}
	internal static class EnchantToggleService
	{
		private static Dictionary<int, int>? _snapshot;

		private static object? _snapshotInventory;

		internal static bool HasSnapshot => _snapshot != null;

		internal static void ClearSnapshot()
		{
			_snapshot = null;
			_snapshotInventory = null;
		}

		internal static EnchantToggleResult Toggle()
		{
			try
			{
				return ToggleCore();
			}
			catch (Exception ex)
			{
				Plugin.ModLogger.LogError((object)$"神器附魔操作发生未处理异常：{ex}");
				return Failure("神器附魔失败：" + ex.GetBaseException().Message);
			}
		}

		private static EnchantToggleResult ToggleCore()
		{
			if (!GameAccess.TryGetLocalContext(out object _, out object inventory, out string error))
			{
				return Failure(error);
			}
			GridInventory gridInventory = (inventory as GridInventory) ?? throw new InvalidCastException("背包对象类型异常：" + inventory.GetType().FullName);
			if (_snapshot != null)
			{
				if (_snapshotInventory != (object)gridInventory)
				{
					ClearSnapshot();
					return Failure("游戏会话已经变化，旧附魔快照已丢弃；请再次点击。");
				}
				Dictionary<int, int> snapshot = _snapshot;
				try
				{
					int changed = ApplyLevels(gridInventory, snapshot);
					ClearSnapshot();
					return new EnchantToggleResult(success: true, restored: true, changed, snapshot.Count, $"已将 {snapshot.Count} 件神器恢复到原附魔等级。");
				}
				catch (Exception ex)
				{
					Plugin.ModLogger.LogError((object)ex);
					return Failure("还原失败，快照仍然保留，可再次尝试。");
				}
			}
			Dictionary<int, int> dictionary = new Dictionary<int, int>();
			Dictionary<int, int> dictionary2 = new Dictionary<int, int>();
			DungeonManager dungeon = (GameAccess.GetDungeonManager() as DungeonManager) ?? throw new InvalidCastException("DungeonManager 对象类型异常。");
			Plugin.ModLogger.LogInfo((object)$"开始扫描背包，格子记录数：{gridInventory.inventoryMatrix.Count}。");
			foreach (KeyValuePair<ItemPosition, NewItemOwnInstance> item2 in gridInventory.inventoryMatrix)
			{
				NewItemOwnInstance value = item2.Value;
				Charm_Basic charm = value.Charm;
				if (charm == null)
				{
					continue;
				}
				int maxLevel = charm.maxLevel;
				if (maxLevel > 0)
				{
					int instanceID = value.InstanceID;
					if (instanceID != int.MinValue && !dictionary.ContainsKey(instanceID))
					{
						int value2 = ReadEnchantLevel(dungeon, instanceID);
						dictionary.Add(instanceID, value2);
						dictionary2.Add(instanceID, maxLevel);
					}
				}
			}
			if (dictionary.Count == 0)
			{
				foreach (KeyValuePair<ItemPosition, Charm_Basic> charm2 in gridInventory.charms)
				{
					Charm_Basic value3 = charm2.Value;
					int maxLevel2 = value3.maxLevel;
					NewItemOwnInstance item = value3.Item;
					if (item != null && maxLevel2 > 0)
					{
						int instanceID2 = item.InstanceID;
						if (instanceID2 != int.MinValue && !dictionary.ContainsKey(instanceID2))
						{
							int value4 = ReadEnchantLevel(dungeon, instanceID2);
							dictionary.Add(instanceID2, value4);
							dictionary2.Add(instanceID2, maxLevel2);
						}
					}
				}
			}
			Plugin.ModLogger.LogInfo((object)$"检测到 {dictionary.Count} 件可附魔神器。");
			if (dictionary.Count == 0)
			{
				return Failure("背包中没有可附魔的神器。");
			}
			try
			{
				int changed2 = ApplyLevels(gridInventory, dictionary2);
				_snapshot = dictionary;
				_snapshotInventory = gridInventory;
				return new EnchantToggleResult(success: true, restored: false, changed2, dictionary.Count, $"已将 {dictionary.Count} 件神器附魔到满；再次点击可还原。");
			}
			catch (Exception ex2)
			{
				Plugin.ModLogger.LogError((object)ex2);
				try
				{
					ApplyLevels(gridInventory, dictionary);
				}
				catch (Exception ex3)
				{
					Plugin.ModLogger.LogError((object)ex3);
				}
				ClearSnapshot();
				return Failure("满附魔失败，已尝试恢复原状态。");
			}
		}

		private static int ApplyLevels(GridInventory inventory, IReadOnlyDictionary<int, int> targets)
		{
			DungeonManager dungeonManager = (GameAccess.GetDungeonManager() as DungeonManager) ?? throw new InvalidCastException("DungeonManager 对象类型异常。");
			bool flag = false;
			int num = 0;
			try
			{
				ReflectionUtil.Invoke(inventory, "GetPermission");
				flag = true;
				foreach (KeyValuePair<int, int> target in targets)
				{
					int num2 = ReadEnchantLevel(dungeonManager, target.Key);
					int num3 = Math.Max(0, target.Value);
					while (num2 < num3)
					{
						dungeonManager.EnchantOnServer(target.Key);
						num2++;
						num++;
					}
					while (num2 > num3)
					{
						dungeonManager.DisenchantOnServer(target.Key);
						num2--;
						num++;
					}
				}
			}
			finally
			{
				if (flag)
				{
					ReflectionUtil.Invoke(inventory, "ReleasePermission");
				}
			}
			return num;
		}

		private static int ReadEnchantLevel(DungeonManager dungeon, int instanceId)
		{
			if (!int.TryParse(dungeon.GetGlobalItemStatValue(instanceId, "Enchant"), out var result))
			{
				return 0;
			}
			return Math.Max(0, result);
		}

		private static EnchantToggleResult Failure(string message)
		{
			return new EnchantToggleResult(success: false, restored: false, 0, 0, message);
		}
	}
	internal static class GameAccess
	{
		internal static bool TryGetLocalContext(out object avatar, out object inventory, out string error)
		{
			avatar = null;
			inventory = null;
			Type type = ReflectionUtil.FindType("Mirror.NetworkServer");
			if (type == null || !ReflectionUtil.ToBool(ReflectionUtil.ReadStatic(type, "active")))
			{
				error = "仅可在单人游戏或本地主机中使用。";
				return false;
			}
			object obj = ReflectionUtil.ReadStatic(type, "connections");
			int num = ((obj == null) ? (-1) : ReflectionUtil.ToInt(ReflectionUtil.Read(obj, "Count"), -1));
			if (num < 0 && obj is ICollection collection)
			{
				num = collection.Count;
			}
			if (num > 1)
			{
				error = "多人房间中禁止修改游戏状态。";
				return false;
			}
			Type type2 = ReflectionUtil.FindType("PlayerAvatar");
			if (type2 == null)
			{
				error = "找不到 PlayerAvatar 类型。";
				return false;
			}
			UnityEngine.Object[] array = UnityEngine.Object.FindObjectsByType(type2, FindObjectsSortMode.None);
			object obj2 = null;
			int num2 = int.MinValue;
			UnityEngine.Object[] array2 = array;
			foreach (UnityEngine.Object obj3 in array2)
			{
				if (obj3 is Component component && component.gameObject.activeInHierarchy)
				{
					int num3 = 0;
					if (ReflectionUtil.ToBool(ReflectionUtil.Read(obj3, "isLocalPlayer")))
					{
						num3 += 100;
					}
					if (ReflectionUtil.ToBool(ReflectionUtil.Read(obj3, "isOwned")))
					{
						num3 += 50;
					}
					if (ReflectionUtil.ToBool(ReflectionUtil.Read(obj3, "hasAuthority")))
					{
						num3 += 25;
					}
					if (ReflectionUtil.ToBool(ReflectionUtil.Read(obj3, "isServer")))
					{
						num3 += 5;
					}
					if (num3 > num2)
					{
						obj2 = obj3;
						num2 = num3;
					}
				}
			}
			if (obj2 == null || num2 <= 5)
			{
				error = "本地玩家尚未就绪。";
				return false;
			}
			object obj4 = ReflectionUtil.Read(obj2, "Inventory");
			if (obj4 == null)
			{
				error = "本地玩家背包尚未就绪。";
				return false;
			}
			avatar = obj2;
			inventory = obj4;
			error = string.Empty;
			return true;
		}

		internal static object GetDungeonManager()
		{
			return ReflectionUtil.ReadStatic(ReflectionUtil.FindType("DungeonManager") ?? throw new InvalidOperationException("找不到 DungeonManager 类型。"), "Instance") ?? throw new InvalidOperationException("DungeonManager 尚未就绪。");
		}
	}
	[HarmonyPatch]
	internal static class BuildCharacterToolsPatch
	{
		private static MethodBase TargetMethod()
		{
			Type type = AccessTools.TypeByName("SephiriaItemLab.UI.DebugToolDrawer") ?? throw new TypeLoadException("SephiriaItemLab.UI.DebugToolDrawer");
			return AccessTools.Method(type, "BuildCharacterTools", (Type[])null, (Type[])null) ?? throw new MissingMethodException(type.FullName, "BuildCharacterTools");
		}

		private static void Postfix(object __instance)
		{
			try
			{
				ItemLabUiInjector.Inject(__instance);
			}
			catch (Exception arg)
			{
				Plugin.ModLogger.LogError((object)$"Failed to inject Item Lab tools: {arg}");
			}
		}
	}
	internal static class ItemLabUiInjector
	{
		private static string _sapphireInput = string.Empty;

		internal static void Inject(object drawer)
		{
			bool chinese = IsChinese();
			var content = (Transform)(ReflectionUtil.Read(drawer, "_contentRoot")
				?? throw new InvalidOperationException("Item Lab content root is missing."));
			// Resolve the same localized title as Item Lab; never rely on a fixed row index.
			var localization = ReflectionUtil.FindType("SephiriaItemLab.Localization.ModLocalization")
				?? throw new TypeLoadException("Item Lab localization is missing.");
			var title = (string)ReflectionUtil.InvokeStatic(localization, "Text", "原生测试面板", "NATIVE TEST PANELS");
			Transform anchor = null;
			foreach (Transform child in content)
			{
				var label = child.GetComponent<TMP_Text>();
				if (child.gameObject.activeSelf && label != null && label.text == title)
				{
					anchor = child;
					break;
				}
			}
			int firstAdded = content.childCount;
			try
			{
				AddControls(drawer, chinese);
			}
			finally
			{
				// Move only this injection's rows, including when an optional control fails.
				var added = new List<Transform>();
				for (int i = firstAdded; i < content.childCount; i++) added.Add(content.GetChild(i));
				if (anchor != null)
				{
					int index = anchor.GetSiblingIndex();
					foreach (var row in added) row.SetSiblingIndex(index++);
				}
				else Plugin.ModLogger.LogWarning("未找到原生测试面板标题，背包与资源保留在页尾。");
			}
		}

		private static void AddControls(object drawer, bool chinese)
		{
			ReflectionUtil.Invoke(drawer, "AddSection", chinese ? "背包与资源" : "INVENTORY & RESOURCES");
			RectTransform rectTransform = (RectTransform)(ReflectionUtil.Invoke(drawer, "CreateRow", 18f) ?? throw new InvalidOperationException("CreateRow returned null."));
			rectTransform.gameObject.name = "ItemLabTweaks_EnchantRow";
			ReflectionUtil.Invoke(drawer, "AddCompactLabel", rectTransform, chinese ? "神器附魔" : "Artifact enchant", 66f);
			Button enchantButton = null;
			Action action = delegate
			{
				try
				{
					Plugin.ModLogger.LogInfo((object)"神器满附魔按钮已触发。");
					EnchantToggleResult enchantToggleResult = EnchantToggleService.Toggle();
					Plugin.ModLogger.Log((LogLevel)(enchantToggleResult.Success ? 8 : 4), (object)enchantToggleResult.Message);
					if (enchantButton != null)
					{
						SetButtonText(enchantButton, EnchantButtonLabel(chinese));
					}
				}
				catch (Exception arg)
				{
					Plugin.ModLogger.LogError((object)$"神器附魔按钮回调异常：{arg}");
				}
			};
			enchantButton = (Button)(ReflectionUtil.Invoke(drawer, "AddButtonTo", rectTransform, EnchantButtonLabel(chinese), action, 18f, 42f, null) ?? throw new InvalidOperationException("CreateOfficialButton returned null."));
			RectTransform rectTransform2 = (RectTransform)(ReflectionUtil.Invoke(drawer, "CreateRow", 18f) ?? throw new InvalidOperationException("CreateRow returned null."));
			rectTransform2.gameObject.name = "ItemLabTweaks_SapphireRow";
			ReflectionUtil.Invoke(drawer, "AddCompactLabel", rectTransform2, chinese ? "蓝宝石" : "Sapphire", 66f);
			if (SapphireService.TryGetCurrent(out var amount))
			{
				_sapphireInput = amount.ToString();
			}
			Action<string> action2 = delegate(string value)
			{
				_sapphireInput = value ?? string.Empty;
			};
			TMP_InputField input = (TMP_InputField)(ReflectionUtil.Invoke(drawer, "CreateInput", rectTransform2, _sapphireInput, action2) ?? throw new InvalidOperationException("CreateInput returned null."));
			input.SetTextWithoutNotify(_sapphireInput);
			(input.gameObject.GetComponent<LayoutElement>() ?? input.gameObject.AddComponent<LayoutElement>()).preferredWidth = 52f;
			Button setButton = null;
			Action action3 = delegate
			{
				int actualAmount;
				string message;
				bool flag = SapphireService.TrySet(_sapphireInput, out actualAmount, out message);
				Plugin.ModLogger.Log((LogLevel)(flag ? 8 : 4), (object)message);
				if (flag)
				{
					_sapphireInput = actualAmount.ToString();
					input.SetTextWithoutNotify(_sapphireInput);
				}
				if (setButton != null)
				{
					SetButtonText(setButton, (!flag) ? (chinese ? "设置失败" : "FAILED") : (chinese ? "已设置" : "SET ✓"));
				}
			};
			setButton = (Button)(ReflectionUtil.Invoke(drawer, "AddButtonTo", rectTransform2, chinese ? "设置" : "SET", action3, 18f, 42f, null) ?? throw new InvalidOperationException("CreateOfficialButton returned null."));
			TalentPanel.Add(drawer, chinese);
		}

		private static string EnchantButtonLabel(bool chinese)
		{
			if (EnchantToggleService.HasSnapshot)
			{
				if (!chinese)
				{
					return "RESTORE";
				}
				return "还原";
			}
			if (!chinese)
			{
				return "MAX";
			}
			return "满级";
		}

		private static void SetButtonText(Button button, string text)
		{
			TMP_Text componentInChildren = button.GetComponentInChildren<TMP_Text>(includeInactive: true);
			if (componentInChildren != null)
			{
				componentInChildren.text = text;
			}
		}

		private static bool IsChinese()
		{
			Type type = ReflectionUtil.FindType("SephiriaItemLab.Localization.ModLocalization");
			return ReflectionUtil.ToBool((type == null) ? null : ReflectionUtil.ReadStatic(type, "IsSimplifiedChinese"));
		}
	}
	[BepInPlugin("com.codex.sephiria.itemlab-tweaks", "Sephiria Item Lab Tweaks", "1.1.0")]
	[BepInDependency(ItemLabGuid)]
	[BepInProcess("Sephiria.exe")]
	public sealed class Plugin : BaseUnityPlugin
	{
		public const string PluginGuid = "com.codex.sephiria.itemlab-tweaks";

		public const string PluginName = "Sephiria Item Lab Tweaks";

		public const string PluginVersion = "1.1.0";

		public const string ItemLabGuid = "com.sephiria.itemlab";

		private Harmony? _harmony;

		internal static ManualLogSource ModLogger { get; private set; }

		private void Awake()
		{
			//IL_0011: Unknown result type (might be due to invalid IL or missing references)
			//IL_001b: Expected O, but got Unknown
			ModLogger = Logger;
			TalentService.Initialize(Config);
			_harmony = new Harmony("com.codex.sephiria.itemlab-tweaks");
			_harmony.PatchAll();
			TalentSPCompat.Initialize(_harmony);
			Logger.LogInfo("Sephiria Item Lab Tweaks 1.1.0 loaded; extra talents and SPMod compatibility integrated.");
		}

		private void Update() => TalentSPCompat.Poll();

		private void OnDestroy()
		{
			EnchantToggleService.ClearSnapshot();
			TalentSPCompat.Stop();
			Harmony? harmony = _harmony;
			if (harmony != null)
			{
				harmony.UnpatchSelf();
			}
			_harmony = null;
		}
	}
	internal static class ReflectionUtil
	{
		private const BindingFlags AllInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		private const BindingFlags AllStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

		internal static Type? FindType(string fullName)
		{
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			for (int i = 0; i < assemblies.Length; i++)
			{
				Type type = assemblies[i].GetType(fullName, throwOnError: false);
				if (type != null)
				{
					return type;
				}
			}
			return null;
		}

		internal static object? Read(object target, string name)
		{
			Type type = target.GetType();
			while (type != null)
			{
				PropertyInfo propertyInfo = type.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((PropertyInfo candidate) => candidate.Name == name && candidate.GetIndexParameters().Length == 0);
				if (propertyInfo != null && propertyInfo.GetIndexParameters().Length == 0)
				{
					return propertyInfo.GetValue(target, null);
				}
				FieldInfo fieldInfo = type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((FieldInfo candidate) => candidate.Name == name);
				if (fieldInfo != null)
				{
					return fieldInfo.GetValue(target);
				}
				type = type.BaseType;
			}
			return null;
		}

		internal static object? ReadStatic(Type type, string name)
		{
			PropertyInfo property = type.GetProperty(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			if (property != null && property.GetIndexParameters().Length == 0)
			{
				return property.GetValue(null, null);
			}
			return type.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
		}

		internal static bool Write(object target, string name, object? value)
		{
			Type type = target.GetType();
			while (type != null)
			{
				PropertyInfo propertyInfo = type.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((PropertyInfo candidate) => candidate.Name == name && candidate.GetIndexParameters().Length == 0);
				if (propertyInfo != null && propertyInfo.CanWrite)
				{
					propertyInfo.SetValue(target, ConvertValue(value, propertyInfo.PropertyType), null);
					return true;
				}
				FieldInfo fieldInfo = type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault((FieldInfo candidate) => candidate.Name == name);
				if (fieldInfo != null)
				{
					fieldInfo.SetValue(target, ConvertValue(value, fieldInfo.FieldType));
					return true;
				}
				type = type.BaseType;
			}
			return false;
		}

		internal static object? Invoke(object target, string methodName, params object?[] args)
		{
			MethodInfo methodInfo = FindCompatibleMethod(target.GetType(), methodName, args, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new MissingMethodException(target.GetType().FullName, methodName);
			return methodInfo.Invoke(target, PrepareArguments(methodInfo, args));
		}

		internal static object? InvokeStatic(Type type, string methodName, params object?[] args)
		{
			MethodInfo methodInfo = FindCompatibleMethod(type, methodName, args, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) ?? throw new MissingMethodException(type.FullName, methodName);
			return methodInfo.Invoke(null, PrepareArguments(methodInfo, args));
		}

		internal static IEnumerable<object> Enumerate(object? value)
		{
			if (!(value is IEnumerable enumerable))
			{
				yield break;
			}
			foreach (object item in enumerable)
			{
				if (item != null)
				{
					yield return item;
				}
			}
		}

		internal static int ToInt(object? value, int fallback = 0)
		{
			if (value == null)
			{
				return fallback;
			}
			try
			{
				return Convert.ToInt32(value);
			}
			catch
			{
				return fallback;
			}
		}

		internal static bool ToBool(object? value)
		{
			if (value == null)
			{
				return false;
			}
			try
			{
				return Convert.ToBoolean(value);
			}
			catch
			{
				return false;
			}
		}

		private static MethodInfo? FindCompatibleMethod(Type type, string name, object?[] args, BindingFlags flags)
		{
			Type type2 = type;
			while (type2 != null)
			{
				foreach (MethodInfo item in from candidate in type2.GetMethods(flags)
					where candidate.Name == name
					select candidate)
				{
					ParameterInfo[] parameters = item.GetParameters();
					if (parameters.Length != args.Length)
					{
						continue;
					}
					bool flag = true;
					for (int num = 0; num < parameters.Length; num++)
					{
						object obj = args[num];
						Type parameterType = parameters[num].ParameterType;
						if (obj == null)
						{
							if (parameterType.IsValueType && Nullable.GetUnderlyingType(parameterType) == null)
							{
								flag = false;
								break;
							}
						}
						else if (!parameterType.IsInstanceOfType(obj) && !CanConvert(obj, parameterType))
						{
							flag = false;
							break;
						}
					}
					if (flag)
					{
						return item;
					}
				}
				type2 = type2.BaseType;
			}
			return null;
		}

		private static object?[] PrepareArguments(MethodInfo method, object?[] args)
		{
			ParameterInfo[] parameters = method.GetParameters();
			object[] array = new object[args.Length];
			for (int i = 0; i < args.Length; i++)
			{
				array[i] = ConvertValue(args[i], parameters[i].ParameterType);
			}
			return array;
		}

		private static bool CanConvert(object value, Type targetType)
		{
			try
			{
				ConvertValue(value, targetType);
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static object? ConvertValue(object? value, Type targetType)
		{
			if (value == null || targetType.IsInstanceOfType(value))
			{
				return value;
			}
			Type type = Nullable.GetUnderlyingType(targetType) ?? targetType;
			if (type.IsEnum)
			{
				if (!(value is string value2))
				{
					return Enum.ToObject(type, value);
				}
				return Enum.Parse(type, value2, ignoreCase: true);
			}
			return Convert.ChangeType(value, type);
		}
	}
	internal static class SapphireService
	{
		private const int MaximumSapphire = 999999999;

		internal static bool TryGetCurrent(out int amount)
		{
			amount = 0;
			if (!GameAccess.TryGetLocalContext(out object avatar, out object _, out string _))
			{
				return false;
			}
			object obj = ReflectionUtil.Read(avatar, "localDataStorage");
			if (obj == null)
			{
				return false;
			}
			amount = ReflectionUtil.ToInt(ReflectionUtil.Invoke(obj, "GetSapphire"));
			return true;
		}

		internal static bool TrySet(string text, out int actualAmount, out string message)
		{
			actualAmount = 0;
			if (!int.TryParse(text, out var result) || result < 0 || result > 999999999)
			{
				message = $"请输入 0 到 {999999999} 之间的整数。";
				return false;
			}
			if (!GameAccess.TryGetLocalContext(out object avatar, out object _, out string error))
			{
				message = error;
				return false;
			}
			try
			{
				object obj = ReflectionUtil.Read(avatar, "localDataStorage") ?? throw new InvalidOperationException("PlayerLocalDataStorage 尚未就绪。");
				int num = ReflectionUtil.ToInt(ReflectionUtil.Read(ReflectionUtil.Read(avatar, "spawner") ?? throw new InvalidOperationException("PlayerSpawner 尚未就绪。"), "sapphireInRun"));
				int num2 = ReflectionUtil.ToInt(ReflectionUtil.Read(obj, "sapphireUseInRun"));
				long num3 = (long)result - (long)num + num2;
				int num4;
				if (num3 >= 0)
				{
					num4 = (num3 > 999999999) ? 999999999 : (int)num3;
				}
				else
				{
					num4 = 0;
					int num5 = checked(num - result);
					if (!ReflectionUtil.Write(obj, "sapphireUseInRun", num5))
					{
						throw new MissingMemberException(obj.GetType().FullName, "sapphireUseInRun");
					}
				}
				if (!ReflectionUtil.Write(obj, "Networksapphire", num4))
				{
					ReflectionUtil.Invoke(obj, "set_Networksapphire", num4);
				}
				Type type = ReflectionUtil.FindType("SaveManager") ?? throw new InvalidOperationException("找不到 SaveManager 类型。");
				ReflectionUtil.Invoke(ReflectionUtil.ReadStatic(type, "Current") ?? throw new InvalidOperationException("当前存档尚未就绪。"), "SetInt", "Sapphire", num4);
				ReflectionUtil.InvokeStatic(type, "Save", true, false);
				actualAmount = ReflectionUtil.ToInt(ReflectionUtil.Invoke(obj, "GetSapphire"));
				message = $"蓝宝石数量已设置为 {actualAmount}。";
				return true;
			}
			catch (Exception ex)
			{
				Plugin.ModLogger.LogError((object)ex);
				message = "设置蓝宝石失败，详细信息已写入 BepInEx 日志。";
				return false;
			}
		}
	}
}
