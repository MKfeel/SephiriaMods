using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SephiriaBackpackOrganizer
{
	internal enum ManualBindDirection
	{
		None,
		Right,
		Left,
		Up,
		Down,
		Both
	}
    internal static class DirectionBindingManager
    {
        internal static ManualBindDirection GetDirection(int id) => ManualBindDirection.None;
        internal static void Clear() { }
        internal static void PruneAndSnapshot(HashSet<int> ids) { }
    }

	public enum SortMode
	{
		Vanilla,
		Enhanced
	}
	public partial class InventorySorter
	{
		private sealed class Slot
		{
			public bool hasItem;

			public int instanceID;

			public int entityID;

			public sbyte quantity;

			public Charm_Basic charm;

			public StoneTablet tablet;

			public int rotation;

			public Slot Clone()
			{
				return new Slot
				{
					hasItem = hasItem,
					instanceID = instanceID,
					entityID = entityID,
					quantity = quantity,
					charm = charm,
					tablet = tablet,
					rotation = rotation
				};
			}

			public static Slot Empty()
			{
				return new Slot();
			}
		}

		private enum CharmPositionKind
		{
			None,
			Top,
			Bottom,
			Side,
			Inside,
			Outlined,
			BothSidesEmpty,
			BothSideCharm,
			NeighborsFull,
			NearMagicBook,
			FullHp
		}

		private sealed class ItemInfo
		{
			public int index;

			public Slot slot;

			public bool isStele;

			public bool isCharm;

			public bool weaponOk = true;

			public bool tabletRotatable;

			public int steleImportance;

			public int manualPriorityRank;

			public bool isBurden;

			public bool isPlanetCategory;

			public bool excludeFromPlanetCluster;

			public bool isPlanetModule;

			public bool isHarmonyCrystal;

			public bool isDedicationBadge;

			public bool isDedicationCompanion;

			public bool isHourglass;

			public bool isMagicBook;

			public float magicCd;

			public bool isRayShard;

			public int magicMpCost;

			public bool isCompass;

			public bool isAttackable;

			public bool isWhitePaper;

			public bool isBelt;

			public bool lowLevelValue;

			public int minDesiredLevel;

			public float levelScoreFactor = 1f;

			public HashSet<string> comboCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			public CharmPositionKind kind;

			public EItemRarity rarity;

			public int priority = 4;

			public bool preferIgnoreCells;

			public bool isRowLocked;

			public int lockRow;

			public int lockRowCycle;

			public bool isCyclicRowCategory;

			public string originalRowCategory;

			public string targetRowCategory;

			public bool isEternalEclipse;

			public bool isOpposingScale;

			public int enchant;

			public int maxLevel;

			public ItemEntity entity;
		}

		private sealed class CompassChain
		{
			public int originalRootCell;

			public readonly List<int> instanceIDs = new List<int>();
		}

		private sealed class WhitePaperComboTarget
		{
			public string category;

			public string displayName;

			public int baseCount;

			public int cap;
		}

		private struct EffectEntry
		{
			public int cell;

			public byte kind;

			public int value;
		}

		private struct ConditionEntry
		{
			public int cell;

			public StoneTablet.CriteriaType type;
		}

		private sealed class StelePattern
		{
			public int cell;

			public int rotation;

			public List<EffectEntry> effects = new List<EffectEntry>();

			public List<ConditionEntry> conditions = new List<ConditionEntry>();

			public bool hasPlacedCondition;
		}

		private sealed class SearchContext
		{
            public volatile bool cancelled;
			public GridInventory inv;

			public int storage;

			public int width;

			public int height;

			public int[] baseLevel;

			public List<Slot> original;

			public List<ItemInfo> items = new List<ItemInfo>();

			public List<ItemInfo> steles = new List<ItemInfo>();

			public List<ItemInfo> charms = new List<ItemInfo>();

			public List<ItemInfo> whitePapers = new List<ItemInfo>();

			public List<ItemInfo> burdens = new List<ItemInfo>();

			public List<ItemInfo> others = new List<ItemInfo>();

			public Dictionary<int, Dictionary<int, StelePattern>> stelePatterns;

			public Dictionary<int, ItemInfo> itemByInstance = new Dictionary<int, ItemInfo>();

			public Dictionary<int, int> compassTargetByInstance = new Dictionary<int, int>();

			public Dictionary<int, int> hourglassTargetByInstance = new Dictionary<int, int>();

			public Dictionary<int, int> rayShardTargetByInstance = new Dictionary<int, int>();

			public Dictionary<int, int> directionBindingTargetByInstance = new Dictionary<int, int>();

			public Dictionary<int, ManualBindDirection> directionBindingDirByInstance = new Dictionary<int, ManualBindDirection>();

			public Dictionary<int, int> bothLeftByInstance = new Dictionary<int, int>();

			public Dictionary<int, int> bothRightByInstance = new Dictionary<int, int>();

			public List<CompassChain> compassChains = new List<CompassChain>();

			public HashSet<int> compassChainInstances = new HashSet<int>();

			public Dictionary<int, int> compassPositionScratch = new Dictionary<int, int>();

			public HashSet<int> compassReservedScratch = new HashSet<int>();

			public int[] compassRootScratch = new int[0];

			public List<WhitePaperComboTarget> whitePaperTargets = new List<WhitePaperComboTarget>();

			public int[] whitePaperAssignmentScratch = new int[0];

			public bool hasBelt;

			public int[] mysticFactor = new int[0];

			public int frostCount;

			public int flameSwordCount;

			public int glacierCount;

			public int emberCount;

			public int mysticCount;

			public int mysticActiveCells;

			public int[] cellLevel = new int[0];

			public bool[] disabled = new bool[0];

			public bool[] ignore = new bool[0];

			public bool[] compassPairedScratch = new bool[0];

			public int[] itemIndexScratch = new int[0];

			public int[] emptyIndexScratch = new int[0];

			public readonly List<int> moveScratchA = new List<int>();

			public readonly List<int> moveScratchB = new List<int>();

			public readonly List<int> moveScratchC = new List<int>();

			public readonly List<int> moveScratchD = new List<int>();

			public readonly HashSet<int> instanceSetScratch = new HashSet<int>();

			public int annealEvaluations;

			public int annealStarts;

			public int annealStartsCompleted;

			public bool searchBudgetReached;

			public int manualPriorityCount;
		}

		private sealed class SearchOutcome
		{
			public List<Slot> layout;

			public double beforeScore;

			public double bestScore;
		}

		private sealed class PendingEnhancedSort
		{
            public string diagnosticId = Guid.NewGuid().ToString("N").Substring(0, 12);
            public LayoutObjective diagnosticBefore;
            public int marksRevision;
            public int inventoryRevision;
			public GridInventory inv;

			public List<Slot> original;

			public SearchContext ctx;

			public float beforeGameScore;

			public Stopwatch stopwatch;

			public SearchOutcome outcome;

			public List<Slot> target;

			public List<Slot> expected;

			public readonly List<(int a, int b)> swaps = new List<(int, int)>();

			public readonly List<(int pos, int count)> rotations = new List<(int, int)>();

			public int swapIndex;

			public int rotationIndex;

			public int rotationRemaining;

			public bool applying;

			public bool rollingBack;

			public bool awaitingObservedState;

			public readonly Stopwatch acknowledgement = new Stopwatch();

			public int swapsPerFrame;

			public int rotationClicksPerFrame;

			public double frameBudgetMs;

			public int acknowledgementTimeoutMs;
		}



		private static readonly string[] CyclicRowCategories = new string[4] { "STURDY", "EMBER", "GLACIER", "MAGITECH" };

		private static readonly string[] CyclicRowCategoryNames = new string[4] { "坚固", "余烬", "冰川", "魔法科技" };

		private readonly Plugin plugin;

		private bool busy;

		private bool requestPending;
        private float requestedAt;
        private List<Slot> readySnapshot;
        private int readyFrame;
        private GridInventory readyInventory;

		private Task<SearchOutcome> pendingSearch;

		private PendingEnhancedSort pendingEnhanced;

		private static readonly ItemPosition[] Neighbor8 = new ItemPosition[8]
		{
			new ItemPosition(-1, 0),
			new ItemPosition(1, 0),
			new ItemPosition(0, -1),
			new ItemPosition(0, 1),
			new ItemPosition(-1, -1),
			new ItemPosition(1, -1),
			new ItemPosition(-1, 1),
			new ItemPosition(1, 1)
		};

		internal const string EclipseFrostCategory = "FROST";

		internal const string EclipseFlameSwordCategory = "FLAMESWORD";

		internal const string ScaleGlacierCategory = "GLACIER";

		internal const string ScaleEmberCategory = "EMBER";

		public bool Busy => busy;

		public void ResetSessionClock()
		{
			requestPending = false; readySnapshot = null; readyInventory = null;
            if (pendingEnhanced != null) pendingEnhanced.ctx.cancelled = true;
		}

		public InventorySorter(Plugin plugin)
		{
			this.plugin = plugin;
		}

		private static CharmPositionKind GetPositionKind(Charm_Basic charm)
		{
			if (charm == null || charm.criteria == null)
			{
				return CharmPositionKind.None;
			}
			if (charm.criteria is CharmActivateCriteria_TopInInventory)
			{
				return CharmPositionKind.Top;
			}
			if (charm.criteria is CharmActivateCriteria_BottomInInventory)
			{
				return CharmPositionKind.Bottom;
			}
			if (charm.criteria is CharmActivateCriteria_SideEnd)
			{
				return CharmPositionKind.Side;
			}
			if (charm.criteria is CharmActivateCriteria_Inside)
			{
				return CharmPositionKind.Inside;
			}
			if (charm.criteria is CharmActivateCriteria_Outlined)
			{
				return CharmPositionKind.Outlined;
			}
			if (charm.criteria is CharmActivateCriteria_BothSidesAreEmpty)
			{
				return CharmPositionKind.BothSidesEmpty;
			}
			if (charm.criteria is CharmActivateCriteria_BothSideCharm)
			{
				return CharmPositionKind.BothSideCharm;
			}
			if (charm.criteria is CharmActivateCriteria_NeighborsAreFull)
			{
				return CharmPositionKind.NeighborsFull;
			}
			if (charm.criteria is CharmActivateCriteria_Near8MagicBook)
			{
				return CharmPositionKind.NearMagicBook;
			}
			return CharmPositionKind.None;
		}

		private static bool IsSatisfyingCell(CharmPositionKind kind, int x, int y, int index, int storage, int width)
		{
			switch (kind)
			{
			case CharmPositionKind.Top:
				return y == 0;
			case CharmPositionKind.Bottom:
				return index >= storage - 6;
			case CharmPositionKind.Side:
				if (x != 0)
				{
					return x >= width - 1;
				}
				return true;
			case CharmPositionKind.Inside:
				if (x > 0 && y > 0 && x < width - 1)
				{
					return index + 7 <= storage - 1;
				}
				return false;
			case CharmPositionKind.Outlined:
				if (x > 0 && y > 0 && x < width - 1)
				{
					return index >= storage - 6;
				}
				return true;
			default:
				return true;
			}
		}

		private static int KindPriority(CharmPositionKind kind)
		{
			switch (kind)
			{
			case CharmPositionKind.Top:
			case CharmPositionKind.Bottom:
			case CharmPositionKind.Side:
				return 3;
			case CharmPositionKind.Inside:
			case CharmPositionKind.Outlined:
				return 2;
			default:
				return 1;
			}
		}

		private static List<StoneTablet.AdditionMetadata> ParseQuerySafe(StoneTablet tablet, ItemPosition origin, int rotation, int width, int height, int storage, bool condition)
		{
			try
			{
				string text = (condition ? tablet.GetConditionQuery(tablet.instanceID) : tablet.GetQuery(tablet.instanceID));
				if (string.IsNullOrEmpty(text))
				{
					return new List<StoneTablet.AdditionMetadata>();
				}
				List<StoneTablet.HumanReadableAddition> humanReadable;
				return StoneTablet.ParseQuery(text, width, height, storage, origin, rotation, out humanReadable);
			}
			catch
			{
				return new List<StoneTablet.AdditionMetadata>();
			}
		}

		private static bool InBounds(int x, int y, int width, int height)
		{
			if (x >= 0 && y >= 0 && x < width)
			{
				return y < height;
			}
			return false;
		}

		private static Slot At(List<Slot> slots, int x, int y, int width, int storage)
		{
			if (x < 0 || y < 0 || x >= width)
			{
				return null;
			}
			int num = y * width + x;
			if (num < 0 || num >= storage || num >= slots.Count)
			{
				return null;
			}
			return slots[num];
		}

		private SearchContext BuildContext(GridInventory inv, List<Slot> original)
		{
			int currentInventoryStorage = inv.CurrentInventoryStorage;
			int width = inv.Width;
			int height = inv.GetHeight(currentInventoryStorage);
			SearchContext searchContext = new SearchContext
			{
				inv = inv,
				storage = currentInventoryStorage,
				width = width,
				height = height,
				original = original,
				baseLevel = new int[currentInventoryStorage],
				cellLevel = new int[currentInventoryStorage],
				disabled = new bool[currentInventoryStorage],
				ignore = new bool[currentInventoryStorage],
				compassPairedScratch = new bool[currentInventoryStorage],
				itemIndexScratch = new int[currentInventoryStorage],
				emptyIndexScratch = new int[currentInventoryStorage],
				mysticFactor = new int[currentInventoryStorage]
			};
			HashSet<int> hashSet = new HashSet<int>();
			for (int i = 0; i < original.Count; i++)
			{
				Slot slot = original[i];
				if (slot != null && slot.hasItem && slot.charm is not null)
				{
					hashSet.Add(slot.instanceID);
				}
			}
			foreach (var live in inv.inventoryMatrix.Values) ManualPriorityManager.Observe(live);
            Dictionary<int, int> dictionary = ManualPriorityManager.PruneAndSnapshot(hashSet);
			searchContext.manualPriorityCount = dictionary.Count;
			DirectionBindingManager.PruneAndSnapshot(hashSet);
			for (int j = 0; j < original.Count && j < currentInventoryStorage; j++)
			{
				Slot slot2 = original[j];
				if (slot2 == null || !slot2.hasItem)
				{
					continue;
				}
				ItemInfo itemInfo = new ItemInfo
				{
					index = j,
					slot = slot2,
					isStele = (slot2.tablet is not null),
					isCharm = (slot2.charm is not null)
				};
				dictionary.TryGetValue(slot2.instanceID, out itemInfo.manualPriorityRank);
				if (itemInfo.isStele)
				{
					itemInfo.steleImportance = SteleImportance(slot2.tablet);
				}
				if (itemInfo.isCharm)
				{
					itemInfo.kind = GetPositionKind(slot2.charm);
					itemInfo.maxLevel = slot2.charm.maxLevel;
					itemInfo.isPlanetModule = slot2.charm is Charm_PlanetModule;
					itemInfo.isHarmonyCrystal = slot2.charm is Charm_NearLevelDamage;
					itemInfo.isDedicationBadge = slot2.charm is Charm_CompanionChaos;
					itemInfo.isDedicationCompanion = slot2.charm is ICompanionCharm;
					itemInfo.isHourglass = slot2.charm is Charm_RightSpellCooldownHelper;
					itemInfo.isMagicBook = slot2.charm is Charm_Magic;
					if (itemInfo.isMagicBook && slot2.charm is Charm_Magic charm_Magic && charm_Magic.ContainedMagic != null)
					{
						itemInfo.magicCd = charm_Magic.ContainedMagic.cooldownTime;
						if (charm_Magic.ContainedMagic.mpCostsByLevel != null)
						{
							int num = 0;
							int[] mpCostsByLevel = charm_Magic.ContainedMagic.mpCostsByLevel;
							foreach (int num2 in mpCostsByLevel)
							{
								if (num2 > num)
								{
									num = num2;
								}
							}
							itemInfo.magicMpCost = num;
						}
					}
					itemInfo.isEternalEclipse = slot2.charm is Charm_FireIceWeapon;
					itemInfo.isOpposingScale = slot2.charm is Charm_FireIce;
					itemInfo.isCompass = slot2.charm is Charm_UpCharmDamage;
					itemInfo.isAttackable = slot2.charm is IAttackableCharm attackableCharm && attackableCharm.IsAttackableCharm();
					itemInfo.isWhitePaper = slot2.charm is Charm_WhitePaper;
					itemInfo.isCyclicRowCategory = slot2.charm is Charm_3Elemental_ByRow;
					itemInfo.isBelt = slot2.charm is Charm_WoodenBox;
					if (slot2.charm.isWeaponRelatedCharm)
					{
						WeaponControllerSimple weaponController = slot2.charm.WeaponController;
						itemInfo.weaponOk = weaponController != null && weaponController.currentWeapon != null && weaponController.currentWeapon.weaponType == slot2.charm.relatedWeapon;
					}
				}
				try
				{
					itemInfo.entity = ItemDatabase.FindItemById(slot2.entityID);
					if (itemInfo.entity != null)
					{
						itemInfo.rarity = itemInfo.entity.rarity;
						if (itemInfo.entity.categories != null)
						{
							itemInfo.isPlanetCategory = itemInfo.entity.categories.Contains("PLANET");
							if (itemInfo.isPlanetCategory && itemInfo.entity.aName != null && !string.IsNullOrEmpty(itemInfo.entity.aName.key))
							{
								string[] array = plugin.PlanetClusterExcludedItems.Value.Split(new char[2] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
								foreach (string text in array)
								{
									if (itemInfo.entity.aName.key.Trim() == text.Trim())
									{
										itemInfo.excludeFromPlanetCluster = true;
										break;
									}
								}
							}
						}
						if (itemInfo.entity.aName != null && !string.IsNullOrEmpty(itemInfo.entity.aName.key))
						{
							string[] array = plugin.BurdenItemKeys.Value.Split(new char[2] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
							foreach (string text2 in array)
							{
								if (itemInfo.entity.aName.key.Trim() == text2.Trim())
								{
									itemInfo.isBurden = true;
									break;
								}
							}
							array = plugin.BeltItems.Value.Split(new char[2] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
							foreach (string token in array)
							{
								if (MatchesItemKey(itemInfo, token))
								{
									itemInfo.isBelt = true;
									break;
								}
							}
							if (plugin.PriorityEnable.Value)
							{
								itemInfo.priority = RarityToPriority(itemInfo.rarity);
                                if (MatchesItemKey(itemInfo, "Item_IncreaseGoldDropRate_Name")) itemInfo.priority = 2;
								array = plugin.PriorityFixedItems.Value.Split(new char[2] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
								foreach (string text3 in array)
								{
									if (itemInfo.entity.aName.key.Trim() == text3.Trim())
									{
										itemInfo.priority = 1;
										break;
									}
								}
								array = plugin.PriorityLowValueItems.Value.Split(new char[2] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
								foreach (string token2 in array)
								{
									if (MatchesItemKey(itemInfo, token2))
									{
										itemInfo.lowLevelValue = true;
										itemInfo.priority = 4;
										itemInfo.levelScoreFactor = Mathf.Clamp(plugin.LowValueLevelFactor.Value, 0f, 1f);
										break;
									}
								}
								array = plugin.PriorityMinLevelItems.Value.Split(new char[2] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
								for (int k = 0; k < array.Length; k++)
								{
									string[] array2 = array[k].Split(new char[1] { '=' });
									if (array2.Length == 2 && MatchesItemKey(itemInfo, array2[0]) && int.TryParse(array2[1].Trim(), out var result) && result > 0)
									{
										itemInfo.minDesiredLevel = result;
										itemInfo.priority = 1;
										break;
									}
								}
								array = plugin.ForcedPriorityItems.Value.Split(new char[2] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
								for (int k = 0; k < array.Length; k++)
								{
									string[] array3 = array[k].Split(new char[1] { ':' });
									if (array3.Length == 2 && itemInfo.entity.aName.key.Trim() == array3[0].Trim() && int.TryParse(array3[1], out var result2) && result2 >= 1 && result2 <= 4)
									{
										itemInfo.priority = result2;
										break;
									}
								}
								array = plugin.IgnoreCellPreferredItems.Value.Split(new char[2] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
								foreach (string text4 in array)
								{
									if (itemInfo.entity.aName.key.Trim() == text4.Trim())
									{
										itemInfo.preferIgnoreCells = true;
										break;
									}
								}
								array = plugin.RowLockedItems.Value.Split(new char[2] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
								foreach (string text5 in array)
								{
									if (itemInfo.entity.aName.key.Trim() == text5.Trim())
									{
										itemInfo.isRowLocked = true;
										itemInfo.lockRow = j / width;
										break;
									}
								}
								array = plugin.HarmonyCrystalItems.Value.Split(new char[2] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
								foreach (string text6 in array)
								{
									if (itemInfo.entity.aName.key.Trim() == text6.Trim())
									{
										itemInfo.isHarmonyCrystal = true;
										break;
									}
								}
								array = plugin.DedicationBadgeItems.Value.Split(new char[2] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
								foreach (string text7 in array)
								{
									if (itemInfo.entity.aName.key.Trim() == text7.Trim())
									{
										itemInfo.isDedicationBadge = true;
										break;
									}
								}
								array = plugin.HourglassItems.Value.Split(new char[2] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
								foreach (string text8 in array)
								{
									if (itemInfo.entity.aName.key.Trim() == text8.Trim())
									{
										itemInfo.isHourglass = true;
										break;
									}
								}
								array = plugin.RayShardItems.Value.Split(new char[2] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
								foreach (string token3 in array)
								{
									if (MatchesItemKey(itemInfo, token3))
									{
										itemInfo.isRayShard = true;
										break;
									}
								}
								array = plugin.EclipseItems.Value.Split(new char[2] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
								foreach (string text9 in array)
								{
									if (itemInfo.entity.aName.key.Trim() == text9.Trim())
									{
										itemInfo.isEternalEclipse = true;
										break;
									}
								}
							}
						}
					}
				}
				catch
				{
				}
				if (itemInfo.isCharm && !itemInfo.isWhitePaper)
				{
					try
					{
						foreach (string item in slot2.charm.GetItemCategory())
						{
							if (!string.IsNullOrEmpty(item))
							{
								itemInfo.comboCategories.Add(item);
							}
						}
					}
					catch
					{
					}
					if (itemInfo.comboCategories.Count == 0 && itemInfo.entity != null && itemInfo.entity.categories != null)
					{
						foreach (string category in itemInfo.entity.categories)
						{
							if (!string.IsNullOrEmpty(category))
							{
								itemInfo.comboCategories.Add(category);
							}
						}
					}
				}
				try
				{
					if (int.TryParse((DungeonManager.Instance != null) ? DungeonManager.Instance.GetGlobalItemStatValue(slot2.instanceID, "Enchant") : "", out var result3) && result3 != 0)
					{
						itemInfo.enchant = result3;
					}
				}
				catch
				{
				}
				if (itemInfo.manualPriorityRank > 0)
				{
					// Explicit intent supersedes inferred low-value and burden behavior.
                    itemInfo.priority = itemInfo.manualPriorityRank <= 2 ? 1 : 4;
                    itemInfo.levelScoreFactor = itemInfo.manualPriorityRank <= 2 ? 1f : 0f;
                    itemInfo.isBurden = false;
                    itemInfo.minDesiredLevel = 0;
				}
				searchContext.items.Add(itemInfo);
				searchContext.itemByInstance[slot2.instanceID] = itemInfo;
				if (itemInfo.isBelt)
				{
					searchContext.hasBelt = true;
				}
				if (itemInfo.isWhitePaper)
				{
					searchContext.whitePapers.Add(itemInfo);
				}
				if (itemInfo.isBurden)
				{
					searchContext.burdens.Add(itemInfo);
				}
				else if (itemInfo.isStele)
				{
					searchContext.steles.Add(itemInfo);
				}
				else if (itemInfo.isCharm)
				{
					searchContext.charms.Add(itemInfo);
				}
				else
				{
					searchContext.others.Add(itemInfo);
				}
			}
			ConfigureCyclicRowCategories(searchContext);
			CaptureCompassBindings(searchContext, original);
			// Natural spell pairs are soft preferences, not locked original neighbors.


			if (plugin.PriorityEnable.Value && plugin.CompassTargetForcedHigh.Value)
			{
				foreach (int value3 in searchContext.compassTargetByInstance.Values)
				{
					if (searchContext.itemByInstance.TryGetValue(value3, out var value) && value != null)
					{
						value.priority = 1;
					}
				}
			}
			BuildWhitePaperTargets(searchContext);
			searchContext.stelePatterns = new Dictionary<int, Dictionary<int, StelePattern>>();
			foreach (ItemInfo stele in searchContext.steles)
			{
				int num3 = ((!(stele.tabletRotatable = DungeonManager.IsTabletRotatable(stele.slot.instanceID, stele.slot.tablet.isRotatable))) ? 1 : 4);
				Dictionary<int, StelePattern> dictionary2 = new Dictionary<int, StelePattern>();
				for (int l = 0; l < currentInventoryStorage; l++)
				{
					ItemPosition origin = inv.IdxToPos(l);
					for (int m = 0; m < num3; m++)
					{
						dictionary2[l * 4 + m] = BuildStelePattern(searchContext, stele.slot.tablet, origin, m);
					}
				}
				searchContext.stelePatterns[stele.slot.instanceID] = dictionary2;
			}
			for (int n = 0; n < currentInventoryStorage; n++)
			{
				searchContext.mysticFactor[n] = 1;
			}
			if (plugin.MysticEnable.Value)
			{
				searchContext.mysticCount = 0;
				try
				{
					foreach (KeyValuePair<string, int> item2 in inv.currentSetEffectCount)
					{
						if (string.Equals(item2.Key, plugin.MysticCategory.Value, StringComparison.OrdinalIgnoreCase))
						{
							searchContext.mysticCount = item2.Value;
							break;
						}
					}
				}
				catch
				{
				}
				if (searchContext.mysticCount <= 0)
				{
					foreach (ItemInfo item3 in searchContext.items)
					{
						if (item3.isCharm && item3.entity != null && item3.entity.categories != null && item3.entity.categories.Any((string c) => string.Equals(c, plugin.MysticCategory.Value, StringComparison.OrdinalIgnoreCase)))
						{
							searchContext.mysticCount++;
						}
					}
				}
				int num4 = ((searchContext.mysticCount >= 5) ? 4 : ((searchContext.mysticCount >= 2) ? 1 : 0));
				searchContext.mysticActiveCells = 0;
				if (num4 > 0 && inv.mysticPositions != null && inv.mysticPositions.Count > 0)
				{
					int num5 = Math.Max(1, (int)plugin.MysticMultiplier.Value);
					int num6 = Math.Min(num4, inv.mysticPositions.Count);
					for (int num7 = 0; num7 < num6; num7++)
					{
						ItemPosition pos = inv.mysticPositions[num7];
						int num8 = inv.PosToIdx(pos);
						if (num8 >= 0 && num8 < currentInventoryStorage)
						{
							searchContext.mysticFactor[num8] = num5;
							searchContext.mysticActiveCells++;
						}
					}
				}
			}
			int[] array4 = ComputeSteleContribution(searchContext, original);
			for (int num9 = 0; num9 < currentInventoryStorage; num9++)
			{
				int num10 = 0;
				if (inv.levelMatrix.TryGetValue(inv.IdxToPos(num9), out var value2))
				{
					num10 = value2;
				}
				int num11 = 0;
				if (original[num9] != null && original[num9].hasItem)
				{
					foreach (ItemInfo item4 in searchContext.items)
					{
						if (item4.index == num9)
						{
							num11 = item4.enchant;
							break;
						}
					}
				}
				int num12 = searchContext.mysticFactor[num9];
				searchContext.baseLevel[num9] = ((num12 > 1) ? (num10 / num12) : num10) - array4[num9] - num11;
			}
			foreach (ItemInfo item5 in searchContext.items)
			{
				if (!(item5.entity == null) && item5.entity.categories != null)
				{
					if (item5.entity.categories.Contains("FROST"))
					{
						searchContext.frostCount++;
					}
					if (item5.entity.categories.Contains("FLAMESWORD"))
					{
						searchContext.flameSwordCount++;
					}
					if (item5.entity.categories.Contains("GLACIER"))
					{
						searchContext.glacierCount++;
					}
					if (item5.entity.categories.Contains("EMBER"))
					{
						searchContext.emberCount++;
					}
				}
			}
			return searchContext;
		}

		private static void ConfigureCyclicRowCategories(SearchContext ctx)
		{
			List<ItemInfo> list = ctx.charms.FindAll((ItemInfo item) => item.isCyclicRowCategory);
			if (list.Count == 0)
			{
				return;
			}
			int[] array = new int[CyclicRowCategories.Length];
			try
			{
				foreach (KeyValuePair<string, int> item in ctx.inv.currentSetEffectCount)
				{
					for (int num = 0; num < CyclicRowCategories.Length; num++)
					{
						if (string.Equals(item.Key, CyclicRowCategories[num], StringComparison.OrdinalIgnoreCase))
						{
							array[num] = item.Value;
							break;
						}
					}
				}
			}
			catch
			{
			}
			foreach (ItemInfo whitePaper in ctx.whitePapers)
			{
				if (!(whitePaper.slot.charm is Charm_WhitePaper charm_WhitePaper))
				{
					continue;
				}
				try
				{
					foreach (string item2 in charm_WhitePaper.assignedCategory)
					{
						for (int num2 = 0; num2 < CyclicRowCategories.Length; num2++)
						{
							if (string.Equals(item2, CyclicRowCategories[num2], StringComparison.OrdinalIgnoreCase))
							{
								array[num2] = Math.Max(0, array[num2] - 1);
								break;
							}
						}
					}
				}
				catch
				{
				}
			}
			foreach (ItemInfo item3 in list)
			{
				int num3 = item3.index / ctx.width % CyclicRowCategories.Length;
				array[num3] = Math.Max(0, array[num3] - 1);
			}
			int[] array2 = new int[CyclicRowCategories.Length];
			foreach (ItemInfo item4 in ctx.items)
			{
				if (!item4.isCharm || item4.isWhitePaper || item4.isCyclicRowCategory)
				{
					continue;
				}
				foreach (string comboCategory in item4.comboCategories)
				{
					for (int num4 = 0; num4 < CyclicRowCategories.Length; num4++)
					{
						if (string.Equals(comboCategory, CyclicRowCategories[num4], StringComparison.OrdinalIgnoreCase))
						{
							array2[num4]++;
							break;
						}
					}
				}
			}
			for (int num5 = 0; num5 < array.Length; num5++)
			{
				array[num5] = Math.Max(array[num5], array2[num5]);
			}
			int num6 = list[0].index / ctx.width % CyclicRowCategories.Length;
			int bestIndex = -1;
			int num7 = int.MinValue;
			for (int num8 = 0; num8 < CyclicRowCategories.Length; num8++)
			{
				if (num8 * ctx.width < ctx.storage && (array[num8] > num7 || (array[num8] == num7 && num8 == num6 && bestIndex != num6)))
				{
					bestIndex = num8;
					num7 = array[num8];
				}
			}
			if (bestIndex < 0)
			{
				bestIndex = num6;
			}
			foreach (ItemInfo item5 in list)
			{
				int num9 = item5.index / ctx.width % CyclicRowCategories.Length;
				item5.originalRowCategory = CyclicRowCategories[num9];
				item5.targetRowCategory = CyclicRowCategories[bestIndex];
				item5.isRowLocked = true;
				item5.lockRow = bestIndex;
				item5.lockRowCycle = CyclicRowCategories.Length;
				item5.comboCategories.Clear();
				item5.comboCategories.Add(item5.targetRowCategory);
			}
			string text = string.Join("/", (from row in Enumerable.Range(0, ctx.height)
				where row % CyclicRowCategories.Length == bestIndex && row * ctx.width < ctx.storage
				select (row + 1).ToString()).ToArray());
			Plugin.Log.LogInfo($"凯尔萨德尼钥匙：坚固{array[0]} 余烬{array[1]} 冰川{array[2]} 魔法科技{array[3]}" + " -> 选择" + CyclicRowCategoryNames[bestIndex] + "，允许第" + text + "行。");
		}

		private static bool IsAllowedLockedRow(ItemInfo item, int row)
		{
			if (item == null || !item.isRowLocked)
			{
				return true;
			}
			if (item.lockRowCycle > 0)
			{
				return row % item.lockRowCycle == item.lockRow;
			}
			return row == item.lockRow;
		}

		private static void CaptureCompassBindings(SearchContext ctx, List<Slot> original)
		{
			for (int i = ctx.width; i < ctx.storage && i < original.Count; i++)
			{
				Slot slot = original[i];
				if (slot != null && slot.hasItem && ctx.itemByInstance.TryGetValue(slot.instanceID, out var value) && value != null && value.isCompass && DirectionBindingManager.GetDirection(slot.instanceID) == ManualBindDirection.None)
				{
					Slot slot2 = original[i - ctx.width];
					if (slot2 != null && slot2.hasItem && !(slot2.charm is null) && ctx.itemByInstance.TryGetValue(slot2.instanceID, out var value2) && value2 != null && (value2.isAttackable || value2.isCompass))
					{
						ctx.compassTargetByInstance[slot.instanceID] = slot2.instanceID;
					}
				}
			}
			if (ctx.compassTargetByInstance.Count == 0)
			{
				return;
			}
			Dictionary<int, int> dictionary = new Dictionary<int, int>();
			foreach (KeyValuePair<int, int> item in ctx.compassTargetByInstance)
			{
				dictionary[item.Value] = item.Key;
			}
			HashSet<int> hashSet = new HashSet<int>();
			foreach (KeyValuePair<int, int> item2 in ctx.compassTargetByInstance)
			{
				int value3 = item2.Value;
				if (!ctx.compassTargetByInstance.ContainsKey(value3))
				{
					AddCompassChain(ctx, value3, dictionary, hashSet);
				}
			}
			foreach (KeyValuePair<int, int> item3 in ctx.compassTargetByInstance)
			{
				if (!hashSet.Contains(item3.Key))
				{
					AddCompassChain(ctx, item3.Value, dictionary, hashSet);
				}
			}
			ctx.compassRootScratch = new int[ctx.compassChains.Count];
		}







		private static void AddCompassChain(SearchContext ctx, int rootInstanceID, Dictionary<int, int> compassBelowTarget, HashSet<int> visitedCompasses)
		{
			if (ctx.itemByInstance.TryGetValue(rootInstanceID, out var value) && value != null)
			{
				CompassChain compassChain = new CompassChain
				{
					originalRootCell = value.index
				};
				compassChain.instanceIDs.Add(rootInstanceID);
				ctx.compassChainInstances.Add(rootInstanceID);
				int key = rootInstanceID;
				int value2;
				while (compassBelowTarget.TryGetValue(key, out value2) && visitedCompasses.Add(value2))
				{
					compassChain.instanceIDs.Add(value2);
					ctx.compassChainInstances.Add(value2);
					key = value2;
				}
				if (compassChain.instanceIDs.Count > 1)
				{
					ctx.compassChains.Add(compassChain);
				}
			}
		}

		private static void BuildWhitePaperTargets(SearchContext ctx)
		{
			if (ctx.whitePapers.Count == 0)
			{
				return;
			}
			Dictionary<string, int> dictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			foreach (ItemInfo item in ctx.items)
			{
				if (!item.isCharm || item.isWhitePaper)
				{
					continue;
				}
				foreach (string comboCategory in item.comboCategories)
				{
					dictionary[comboCategory] = ((!dictionary.TryGetValue(comboCategory, out var value)) ? 1 : (value + 1));
				}
			}
			Dictionary<string, int> dictionary2 = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			try
			{
				foreach (KeyValuePair<string, int> item2 in ctx.inv.currentSetEffectCount)
				{
					dictionary2[item2.Key] = item2.Value;
				}
			}
			catch
			{
			}
			foreach (ItemInfo whitePaper in ctx.whitePapers)
			{
				if (!(whitePaper.slot.charm is Charm_WhitePaper charm_WhitePaper))
				{
					continue;
				}
				try
				{
					foreach (string item3 in charm_WhitePaper.assignedCategory)
					{
						if (dictionary2.TryGetValue(item3, out var value2))
						{
							dictionary2[item3] = Math.Max(0, value2 - 1);
						}
					}
				}
				catch
				{
				}
			}
			foreach (ItemInfo item4 in ctx.items)
			{
				if (item4.isCyclicRowCategory && !string.IsNullOrEmpty(item4.originalRowCategory) && dictionary2.TryGetValue(item4.originalRowCategory, out var value3))
				{
					dictionary2[item4.originalRowCategory] = Math.Max(0, value3 - 1);
				}
			}
			foreach (KeyValuePair<string, int> item5 in dictionary)
			{
				dictionary2[item5.Key] = (dictionary2.TryGetValue(item5.Key, out var value4) ? Math.Max(value4, item5.Value) : item5.Value);
			}
			foreach (KeyValuePair<string, int> item6 in dictionary2)
			{
				if (item6.Value >= 2 && dictionary.TryGetValue(item6.Key, out var value5) && value5 >= 2)
				{
					string displayName;
					int comboEffectCap = GetComboEffectCap(ctx, item6.Key, out displayName);
					if (comboEffectCap > item6.Value)
					{
						ctx.whitePaperTargets.Add(new WhitePaperComboTarget
						{
							category = item6.Key,
							displayName = displayName,
							baseCount = item6.Value,
							cap = comboEffectCap
						});
					}
				}
			}
			ctx.whitePaperTargets.Sort(delegate(WhitePaperComboTarget a, WhitePaperComboTarget b)
			{
				int num = b.baseCount.CompareTo(a.baseCount);
				if (num != 0)
				{
					return num;
				}
				int num2 = (a.cap - a.baseCount).CompareTo(b.cap - b.baseCount);
				return (num2 != 0) ? num2 : string.Compare(a.category, b.category, StringComparison.OrdinalIgnoreCase);
			});
			ctx.whitePaperAssignmentScratch = new int[ctx.whitePaperTargets.Count];
		}

		private static int GetComboEffectCap(SearchContext ctx, string category, out string displayName)
		{
			displayName = category;
			int num = 0;
			try
			{
				ItemCategoryEntity itemCategoryEntity = ItemDatabase.FindItemCategory(category);
				if (itemCategoryEntity == null)
				{
					return 0;
				}
				if (!string.IsNullOrEmpty(itemCategoryEntity.Name))
				{
					displayName = itemCategoryEntity.Name;
				}
				if (itemCategoryEntity.setStatus != null)
				{
					ItemCategoryEntity.SetTarget[] setStatus = itemCategoryEntity.setStatus;
					foreach (ItemCategoryEntity.SetTarget setTarget in setStatus)
					{
						if (setTarget != null)
						{
							num = Math.Max(num, setTarget.itemCount);
						}
					}
				}
				ComboEffectBase value = null;
				try
				{
					ctx.inv.lastAppliedComboEffects.TryGetValue(category, out value);
				}
				catch
				{
				}
				if (value == null && itemCategoryEntity.comboEffectPrefab != null)
				{
					value = itemCategoryEntity.comboEffectPrefab.GetComponent<ComboEffectBase>();
				}
				if (value != null)
				{
					if (value.addStatByCombo != null)
					{
						ComboEffectBase.ComboStat[] addStatByCombo = value.addStatByCombo;
						foreach (ComboEffectBase.ComboStat comboStat in addStatByCombo)
						{
							if (comboStat != null)
							{
								num = Math.Max(num, comboStat.comboCount);
							}
						}
					}
					try
					{
						List<ComboEffectElement> list = value.RequestComboData(ctx.inv.UnitAvatar);
						if (list != null)
						{
							foreach (ComboEffectElement item in list)
							{
								if (item != null)
								{
									num = Math.Max(num, item.comboCount);
								}
							}
						}
					}
					catch
					{
					}
				}
			}
			catch
			{
				return 0;
			}
			return num;
		}

		private static StelePattern BuildStelePattern(SearchContext ctx, StoneTablet tablet, ItemPosition origin, int rotation)
		{
			StelePattern stelePattern = new StelePattern
			{
				cell = ctx.inv.PosToIdx(new ItemPosition(origin.x, origin.y)),
				rotation = rotation
			};
			foreach (StoneTablet.AdditionMetadata item in ParseQuerySafe(tablet, origin, rotation, ctx.width, ctx.height, ctx.storage, condition: false))
			{
				StoneTablet.AdditionEffectData additionEffectData = new StoneTablet.AdditionEffectData(item);
				if (additionEffectData.position.x >= 0 && additionEffectData.position.y >= 0 && additionEffectData.position.x < ctx.width && additionEffectData.position.y < ctx.height)
				{
					int cell = ctx.inv.PosToIdx(new ItemPosition(additionEffectData.position.x, additionEffectData.position.y));
					switch (additionEffectData.effectType)
					{
					case StoneTablet.EffectType.IncreaseConstLevel:
						stelePattern.effects.Add(new EffectEntry
						{
							cell = cell,
							kind = 0,
							value = additionEffectData.levelParam
						});
						break;
					case StoneTablet.EffectType.Disable:
						stelePattern.effects.Add(new EffectEntry
						{
							cell = cell,
							kind = 1
						});
						break;
					case StoneTablet.EffectType.IgnoreCriteria:
						stelePattern.effects.Add(new EffectEntry
						{
							cell = cell,
							kind = 2
						});
						break;
					case StoneTablet.EffectType.MultiplyConstLevel:
						stelePattern.effects.Add(new EffectEntry
						{
							cell = cell,
							kind = 3,
							value = additionEffectData.levelParam
						});
						break;
					}
				}
			}
			foreach (StoneTablet.AdditionMetadata item2 in ParseQuerySafe(tablet, origin, rotation, ctx.width, ctx.height, ctx.storage, condition: true))
			{
				StoneTablet.AdditionCriteriaData additionCriteriaData = new StoneTablet.AdditionCriteriaData(item2);
				if (additionCriteriaData.effectType != StoneTablet.CriteriaType.None && additionCriteriaData.position.x >= 0 && additionCriteriaData.position.y >= 0 && additionCriteriaData.position.x < ctx.width && additionCriteriaData.position.y < ctx.height)
				{
					int cell2 = ctx.inv.PosToIdx(new ItemPosition(additionCriteriaData.position.x, additionCriteriaData.position.y));
					stelePattern.conditions.Add(new ConditionEntry
					{
						cell = cell2,
						type = additionCriteriaData.effectType
					});
					if (additionCriteriaData.effectType == StoneTablet.CriteriaType.Placed)
					{
						stelePattern.hasPlacedCondition = true;
					}
				}
			}
			return stelePattern;
		}

		private static bool ConditionsOk(StelePattern pattern, List<Slot> slots)
		{
			if (pattern.conditions.Count == 0)
			{
				return true;
			}
			bool flag = true;
			bool flag2 = false;
			bool flag3 = false;
			foreach (ConditionEntry condition in pattern.conditions)
			{
				Slot slot = ((condition.cell >= 0 && condition.cell < slots.Count) ? slots[condition.cell] : null);
				bool flag4;
				switch (condition.type)
				{
				case StoneTablet.CriteriaType.AnyItem:
					flag4 = slot?.hasItem ?? false;
					break;
				case StoneTablet.CriteriaType.OnlyCharm:
					flag4 = slot != null && slot.hasItem && slot.charm is not null;
					break;
				case StoneTablet.CriteriaType.Placed:
					flag4 = true;
					flag2 = true;
					flag3 |= condition.cell == pattern.cell;
					break;
				default:
					flag4 = true;
					break;
				}
				flag = flag && flag4;
			}
			if (!flag)
			{
				return false;
			}
			if (flag2 && !flag3)
			{
				return false;
			}
			return true;
		}

		private static void ApplyPatternEffects(SearchContext ctx, List<Slot> slots, StelePattern pattern, int[] level, bool[] disabled, bool[] ignore)
		{
			if (!ConditionsOk(pattern, slots))
			{
				return;
			}
			foreach (EffectEntry effect in pattern.effects)
			{
				if (effect.cell < 0 || effect.cell >= ctx.storage)
				{
					continue;
				}
				switch (effect.kind)
				{
				case 0:
					level[effect.cell] += effect.value;
					break;
				case 1:
					if (disabled != null)
					{
						disabled[effect.cell] = true;
					}
					break;
				case 2:
					if (ignore != null)
					{
						ignore[effect.cell] = true;
					}
					break;
				}
			}
			foreach (EffectEntry effect2 in pattern.effects)
			{
				if (effect2.kind == 3 && effect2.cell >= 0 && effect2.cell < ctx.storage)
				{
					level[effect2.cell] *= effect2.value;
				}
			}
		}

		private static int[] ComputeSteleContribution(SearchContext ctx, List<Slot> slots)
		{
			int[] array = new int[ctx.storage];
			int[] array2 = new int[ctx.storage];
			for (int i = 0; i < ctx.storage; i++)
			{
				array2[i] = 1;
			}
			for (int j = 0; j < ctx.storage; j++)
			{
				Slot slot = slots[j];
				if (slot == null || !slot.hasItem || slot.tablet is null || !ctx.stelePatterns.TryGetValue(slot.instanceID, out var value) || !value.TryGetValue(j * 4 + slot.rotation, out var value2) || !ConditionsOk(value2, slots))
				{
					continue;
				}
				foreach (EffectEntry effect in value2.effects)
				{
					if (effect.cell >= 0 && effect.cell < ctx.storage)
					{
						if (effect.kind == 0)
						{
							array[effect.cell] += effect.value;
						}
						else if (effect.kind == 3)
						{
							array2[effect.cell] *= effect.value;
						}
					}
				}
			}
			for (int k = 0; k < ctx.storage; k++)
			{
				array[k] *= array2[k];
			}
			return array;
		}

		private static bool HasItemAt(List<Slot> slots, int x, int y, int width, int height)
		{
			if (!InBounds(x, y, width, height))
			{
				return false;
			}
			int num = y * width + x;
			if (num >= 0 && num < slots.Count && slots[num] != null)
			{
				return slots[num].hasItem;
			}
			return false;
		}

		private static bool HasCharmAt(List<Slot> slots, int x, int y, int width, int height)
		{
			if (!InBounds(x, y, width, height))
			{
				return false;
			}
			int num = y * width + x;
			if (num >= 0 && num < slots.Count && slots[num] != null && slots[num].hasItem)
			{
				return slots[num].charm is not null;
			}
			return false;
		}

		private static bool CriteriaSatisfied(SearchContext ctx, ItemInfo info, List<Slot> slots, bool ignored, int cell)
		{
			if (ignored)
			{
				return true;
			}
			int num = cell % ctx.width;
			int num2 = cell / ctx.width;
			switch (info.kind)
			{
			case CharmPositionKind.Top:
				return num2 == 0;
			case CharmPositionKind.Bottom:
				return cell >= ctx.storage - 6;
			case CharmPositionKind.Side:
				if (num != 0)
				{
					return num >= ctx.width - 1;
				}
				return true;
			case CharmPositionKind.Inside:
				if (num > 0 && num2 > 0 && num < ctx.width - 1)
				{
					return cell + 7 <= ctx.storage - 1;
				}
				return false;
			case CharmPositionKind.Outlined:
				if (num > 0 && num2 > 0 && num < ctx.width - 1)
				{
					return cell >= ctx.storage - 6;
				}
				return true;
			case CharmPositionKind.BothSidesEmpty:
				if (num > 0 && num < ctx.width - 1 && !HasItemAt(slots, num - 1, num2, ctx.width, ctx.height))
				{
					return !HasItemAt(slots, num + 1, num2, ctx.width, ctx.height);
				}
				return false;
			case CharmPositionKind.BothSideCharm:
				if (num > 0 && num < ctx.width - 1 && HasCharmAt(slots, num - 1, num2, ctx.width, ctx.height))
				{
					return HasCharmAt(slots, num + 1, num2, ctx.width, ctx.height);
				}
				return false;
			case CharmPositionKind.NeighborsFull:
			{
				for (int j = 0; j < 8; j++)
				{
					if (!HasItemAt(slots, num + Neighbor8[j].x, num2 + Neighbor8[j].y, ctx.width, ctx.height))
					{
						return false;
					}
				}
				return true;
			}
			case CharmPositionKind.NearMagicBook:
			{
				for (int i = 0; i < 8; i++)
				{
					if (HasCharmAt(slots, num + Neighbor8[i].x, num2 + Neighbor8[i].y, ctx.width, ctx.height) && slots[num2 * ctx.width + (num + Neighbor8[i].x)].charm is Charm_Magic)
					{
						return true;
					}
				}
				return false;
			}
			default:
				return true;
			}
		}

		private static bool IsCompassPaired(SearchContext ctx, List<Slot> slots, int cell, Slot compassSlot)
		{
			int x = cell % ctx.width;
			int num = cell / ctx.width;
			Slot slot = At(slots, x, num - 1, ctx.width, ctx.storage);
			if (slot == null || !slot.hasItem || slot.charm is null)
			{
				return false;
			}
			if (ctx.compassTargetByInstance.TryGetValue(compassSlot.instanceID, out var value))
			{
				return slot.instanceID == value;
			}
			if (ctx.itemByInstance.TryGetValue(slot.instanceID, out var value2) && value2 != null)
			{
				if (!value2.isCompass)
				{
					return value2.isAttackable;
				}
				return true;
			}
			return false;
		}

		private static int CountBrokenCompassBindings(SearchContext ctx, List<Slot> slots)
		{
			if (ctx.compassTargetByInstance.Count == 0)
			{
				return 0;
			}
			int num = 0;
			int num2 = 0;
			int num3 = Math.Min(ctx.storage, slots?.Count ?? 0);
			for (int i = 0; i < num3; i++)
			{
				Slot slot = slots[i];
				if (slot != null && slot.hasItem && ctx.compassTargetByInstance.TryGetValue(slot.instanceID, out var value))
				{
					num2++;
					int num4 = i - ctx.width;
					if (num4 < 0 || num4 >= num3 || slots[num4] == null || !slots[num4].hasItem || slots[num4].instanceID != value)
					{
						num++;
					}
				}
			}
			return num + (ctx.compassTargetByInstance.Count - num2);
		}

		private static bool CompassBindingsSatisfied(SearchContext ctx, List<Slot> slots)
		{
			return CountBrokenCompassBindings(ctx, slots) == 0;
		}

		private static int CountBrokenHourglassRayShardBindings(SearchContext ctx, List<Slot> slots)
		{
			int num = ctx.hourglassTargetByInstance.Count + ctx.rayShardTargetByInstance.Count;
			if (slots == null || num == 0)
			{
				return 0;
			}
			int num2 = 0;
			int num3 = 0;
			int num4 = Math.Min(ctx.storage, slots.Count);
			for (int i = 0; i < num4; i++)
			{
				Slot slot = slots[i];
				if (slot == null || !slot.hasItem)
				{
					continue;
				}
				int num5 = i % ctx.width;
				int y = i / ctx.width;
				int value2;
				if (ctx.hourglassTargetByInstance.TryGetValue(slot.instanceID, out var value))
				{
					num3++;
					Slot slot2 = At(slots, num5 + 1, y, ctx.width, ctx.storage);
					if (slot2 == null || !slot2.hasItem || slot2.instanceID != value)
					{
						num2++;
					}
				}
				else if (ctx.rayShardTargetByInstance.TryGetValue(slot.instanceID, out value2))
				{
					num3++;
					Slot slot3 = At(slots, num5 - 1, y, ctx.width, ctx.storage);
					if (slot3 == null || !slot3.hasItem || slot3.instanceID != value2)
					{
						num2++;
					}
				}
			}
			return num2 + (num - num3);
		}

		private static bool HourglassRayShardBindingsSatisfied(SearchContext ctx, List<Slot> slots)
		{
			return CountBrokenHourglassRayShardBindings(ctx, slots) == 0;
		}

		private static int CountBrokenDirectionBindings(SearchContext ctx, List<Slot> slots)
		{
			int num = ctx.directionBindingTargetByInstance.Count + ctx.bothLeftByInstance.Count + ctx.bothRightByInstance.Count;
			if (slots == null || num == 0)
			{
				return 0;
			}
			int num2 = 0;
			int num3 = 0;
			int num4 = Math.Min(ctx.storage, slots.Count);
			for (int i = 0; i < num4; i++)
			{
				Slot slot = slots[i];
				if (slot == null || !slot.hasItem)
				{
					continue;
				}
				int num5 = i % ctx.width;
				int num6 = i / ctx.width;
				if (ctx.bothLeftByInstance.TryGetValue(slot.instanceID, out var value))
				{
					num3++;
					Slot slot2 = At(slots, num5 - 1, num6, ctx.width, ctx.storage);
					if (slot2 == null || !slot2.hasItem || slot2.instanceID != value)
					{
						num2++;
					}
				}
				if (ctx.bothRightByInstance.TryGetValue(slot.instanceID, out var value2))
				{
					num3++;
					Slot slot3 = At(slots, num5 + 1, num6, ctx.width, ctx.storage);
					if (slot3 == null || !slot3.hasItem || slot3.instanceID != value2)
					{
						num2++;
					}
				}
				if (!ctx.directionBindingTargetByInstance.TryGetValue(slot.instanceID, out var value3))
				{
					continue;
				}
				num3++;
				if (!ctx.directionBindingDirByInstance.TryGetValue(slot.instanceID, out var value4))
				{
					num2++;
					continue;
				}
				Slot slot4 = null;
				switch (value4)
				{
				case ManualBindDirection.Right:
					slot4 = At(slots, num5 + 1, num6, ctx.width, ctx.storage);
					break;
				case ManualBindDirection.Left:
					slot4 = At(slots, num5 - 1, num6, ctx.width, ctx.storage);
					break;
				case ManualBindDirection.Up:
					slot4 = At(slots, num5, num6 - 1, ctx.width, ctx.storage);
					break;
				case ManualBindDirection.Down:
					slot4 = At(slots, num5, num6 + 1, ctx.width, ctx.storage);
					break;
				}
				if (slot4 == null || !slot4.hasItem || slot4.instanceID != value3)
				{
					num2++;
				}
			}
			return num2 + (num - num3);
		}

		private static bool DirectionBindingsSatisfied(SearchContext ctx, List<Slot> slots)
		{
			return CountBrokenDirectionBindings(ctx, slots) == 0;
		}

		private static bool WhitePaperMatchesTarget(SearchContext ctx, List<Slot> slots, int paperCell, int targetIndex)
		{
			if (paperCell < 0 || paperCell >= ctx.storage || targetIndex < 0 || targetIndex >= ctx.whitePaperTargets.Count)
			{
				return false;
			}
			int num = paperCell % ctx.width;
			if (num <= 0 || num >= ctx.width - 1)
			{
				return false;
			}
			int index = paperCell - 1;
			int num2 = paperCell + 1;
			if (num2 >= ctx.storage)
			{
				return false;
			}
			Slot slot = slots[index];
			Slot slot2 = slots[num2];
			if (slot == null || slot2 == null || !slot.hasItem || !slot2.hasItem || slot.charm is null || slot2.charm is null || !ctx.itemByInstance.TryGetValue(slot.instanceID, out var value) || value == null || !ctx.itemByInstance.TryGetValue(slot2.instanceID, out var value2) || value2 == null || value.isWhitePaper || value2.isWhitePaper)
			{
				return false;
			}
			string category = ctx.whitePaperTargets[targetIndex].category;
			if (value.comboCategories.Contains(category))
			{
				return value2.comboCategories.Contains(category);
			}
			return false;
		}

		private static int RefreshWhitePaperAssignments(SearchContext ctx, List<Slot> slots)
		{
			int[] whitePaperAssignmentScratch = ctx.whitePaperAssignmentScratch;
			Array.Clear(whitePaperAssignmentScratch, 0, whitePaperAssignmentScratch.Length);
			int num = 0;
			for (int i = 0; i < ctx.storage && i < slots.Count; i++)
			{
				Slot slot = slots[i];
				if (slot == null || !slot.hasItem || !ctx.itemByInstance.TryGetValue(slot.instanceID, out var value) || value == null || !value.isWhitePaper)
				{
					continue;
				}
				bool flag = false;
				for (int j = 0; j < ctx.whitePaperTargets.Count; j++)
				{
					if (WhitePaperMatchesTarget(ctx, slots, i, j))
					{
						whitePaperAssignmentScratch[j]++;
						flag = true;
					}
				}
				if (!flag)
				{
					num++;
				}
			}
			return num;
		}

		private double EvaluateWhitePaperSynergy(SearchContext ctx, List<Slot> slots)
		{
			if (plugin.WhitePaperComboBonus.Value <= 0f || ctx.whitePaperTargets.Count == 0)
			{
				return 0.0;
			}
			int num = RefreshWhitePaperAssignments(ctx, slots);
			double num2 = plugin.WhitePaperComboBonus.Value;
			double num3 = 0.0;
			for (int i = 0; i < ctx.whitePaperTargets.Count; i++)
			{
				WhitePaperComboTarget whitePaperComboTarget = ctx.whitePaperTargets[i];
				int num4 = ctx.whitePaperAssignmentScratch[i];
				int val = Math.Max(0, whitePaperComboTarget.cap - whitePaperComboTarget.baseCount);
				int num5 = Math.Min(num4, val);
				double num6 = ((ctx.whitePaperTargets.Count > 0) ? (4.0 * (double)(ctx.whitePaperTargets.Count - i) / (double)ctx.whitePaperTargets.Count) : 0.0);
				for (int j = 0; j < num5; j++)
				{
					int num7 = whitePaperComboTarget.baseCount + j + 1;
					num3 += num2 * ((double)whitePaperComboTarget.baseCount * 10.0 + num6);
					if (num7 == whitePaperComboTarget.cap)
					{
						num3 += num2 * 5.0;
					}
				}
				if (num4 > num5)
				{
					num3 -= num2 * 20.0 * (double)(num4 - num5);
				}
			}
			return num3 - num2 * 20.0 * (double)num;
		}

		private double EvaluateLayout(SearchContext ctx, List<Slot> slots)
		{
			int num = Math.Min(ctx.storage, slots?.Count ?? 0);
			int[] cellLevel = ctx.cellLevel;
			bool[] disabled = ctx.disabled;
			bool[] ignore = ctx.ignore;
			Array.Copy(ctx.baseLevel, cellLevel, num);
			Array.Clear(disabled, 0, num);
			Array.Clear(ignore, 0, num);
			for (int i = 0; i < num; i++)
			{
				Slot slot = slots[i];
				if (slot != null && slot.hasItem && !(slot.tablet is null) && ctx.stelePatterns.TryGetValue(slot.instanceID, out var value) && value.TryGetValue(i * 4 + slot.rotation, out var value2))
				{
					ApplyPatternEffects(ctx, slots, value2, cellLevel, disabled, ignore);
				}
			}
			double num2 = 0.0;
			int num3 = CountBrokenCompassBindings(ctx, slots);
			if (num3 > 0)
			{
				num2 -= (double)num3 * 1000000000.0;
			}
			int num4 = CountBrokenHourglassRayShardBindings(ctx, slots);
			if (num4 > 0)
			{
				num2 -= (double)num4 * 1000000000.0;
			}
			int num5 = CountBrokenDirectionBindings(ctx, slots);
			if (num5 > 0)
			{
				num2 -= (double)num5 * 1000000000.0;
			}
			int num6 = int.MaxValue;
			bool flag = ctx.burdens.Count > 0 && plugin.BurdenPenalty.Value > 0f;
			if (flag)
			{
				for (int j = 0; j < num; j++)
				{
					if (cellLevel[j] < num6)
					{
						num6 = cellLevel[j];
					}
				}
			}
			bool[] array = null;
			if (plugin.CompassBonus.Value > 0f)
			{
				array = ctx.compassPairedScratch;
				Array.Clear(array, 0, num);
				for (int k = 0; k < num; k++)
				{
					Slot slot2 = slots[k];
					if (slot2 != null && slot2.hasItem && !(slot2.charm is null) && ctx.itemByInstance.TryGetValue(slot2.instanceID, out var value3) && value3 != null && value3.isCompass)
					{
						array[k] = IsCompassPaired(ctx, slots, k, slot2);
					}
				}
			}
			for (int l = 0; l < num; l++)
			{
				Slot slot3 = slots[l];
				if (slot3 == null || !slot3.hasItem || slot3.charm is null || !ctx.itemByInstance.TryGetValue(slot3.instanceID, out var value4) || value4 == null)
				{
					continue;
				}
				if (value4.isBurden)
				{
					if (flag)
					{
						double num7 = cellLevel[l] - num6;
						if (num7 > 0.0)
						{
							num2 -= (double)plugin.BurdenPenalty.Value * num7;
						}
					}
					continue;
				}
				int num8 = (cellLevel[l] + value4.enchant) * ctx.mysticFactor[l];
				bool num9 = !disabled[l] && num8 >= 0 && CriteriaSatisfied(ctx, value4, slots, ignore[l], l) && value4.weaponOk;
				if (!IsAllowedLockedRow(value4, l / ctx.width))
				{
					num2 -= 100000000.0;
				}
				if (value4.isEternalEclipse && ctx.frostCount != ctx.flameSwordCount)
				{
					int num10 = l % ctx.width;
					bool flag2 = ctx.frostCount > ctx.flameSwordCount;
					if ((flag2 && num10 < ctx.width / 2) || (!flag2 && num10 >= ctx.width / 2))
					{
						num2 -= 100000000.0;
					}
				}
				if (value4.isOpposingScale)
				{
					int num11 = l % ctx.width;
					bool flag3 = num11 == 0;
					bool flag4 = num11 == ctx.width - 1;
					if (ctx.glacierCount > ctx.emberCount)
					{
						if (!flag4)
						{
							num2 -= 100000000.0;
						}
					}
					else if (ctx.glacierCount < ctx.emberCount)
					{
						if (!flag3)
						{
							num2 -= 100000000.0;
						}
					}
					else if (!flag3 && !flag4)
					{
						num2 -= 100000000.0;
					}
				}
				if (KindPriority(value4.kind) >= 2)
				{
					bool flag5 = IsSatisfyingCell(x: l % ctx.width, y: l / ctx.width, kind: value4.kind, index: l, storage: ctx.storage, width: ctx.width);
					if (value4.preferIgnoreCells)
					{
						if (ignore[l])
						{
							num2 += 5000.0;
						}
						else if (flag5)
						{
							num2 += 500.0;
						}
					}
					else if (flag5 && !ignore[l])
					{
						num2 += 500.0;
					}
				}
				if (num9)
				{
					int num12 = Mathf.Clamp(num8, 0, value4.maxLevel);
					double num13 = (double)(num12 * 10000) * PriorityWeight(value4.priority) * (double)value4.levelScoreFactor;
					if (value4.isCompass && array != null && !array[l])
					{
						num13 *= (double)plugin.CompassUnpairedFactor.Value;
					}
					num2 += num13 + 1000.0;
					if (num8 > value4.maxLevel)
					{
						num2 += (double)(num8 - value4.maxLevel);
					}
					if (value4.minDesiredLevel > 0 && num12 < value4.minDesiredLevel)
					{
						num2 -= (double)(value4.minDesiredLevel - num12) * 10000.0 * PriorityWeight(value4.priority);
					}
				}
				else
				{
					num2 -= 750.0;
				}
				if (num8 < 0)
				{
					num2 -= (double)(250 * -num8);
				}
			}
			if (plugin.PlanetBonus.Value > 0f)
			{
				for (int m = 0; m < num; m++)
				{
					Slot slot4 = slots[m];
					if (slot4 == null || !slot4.hasItem || slot4.charm is null || !ctx.itemByInstance.TryGetValue(slot4.instanceID, out var value5) || value5 == null || !value5.isPlanetModule)
					{
						continue;
					}
					int num14 = m % ctx.width;
					int num15 = m / ctx.width;
					if (disabled[m] || (cellLevel[m] + value5.enchant) * ctx.mysticFactor[m] < 0 || !CriteriaSatisfied(ctx, value5, slots, ignore[m], m))
					{
						continue;
					}
					int num16 = 0;
					for (int n = 0; n < 8; n++)
					{
						int num17 = num14 + Neighbor8[n].x;
						int num18 = num15 + Neighbor8[n].y;
						Slot slot5 = At(slots, num17, num18, ctx.width, ctx.storage);
						if (slot5 != null && slot5.hasItem && ctx.itemByInstance.TryGetValue(slot5.instanceID, out var value6) && value6 != null && value6.isPlanetCategory && !value6.excludeFromPlanetCluster)
						{
							int num19 = num18 * ctx.width + num17;
							if (!disabled[num19] && (cellLevel[num19] + value6.enchant) * ctx.mysticFactor[num19] >= 0)
							{
								num16++;
							}
						}
					}
					if (num16 > 0)
					{
						num2 += (double)(plugin.PlanetBonus.Value * (float)num16);
					}
				}
			}
			if (plugin.HarmonyLevelBonus.Value > 0f)
			{
				for (int num20 = 0; num20 < num; num20++)
				{
					Slot slot6 = slots[num20];
					if (slot6 == null || !slot6.hasItem || slot6.charm is null || !ctx.itemByInstance.TryGetValue(slot6.instanceID, out var value7) || value7 == null || !value7.isHarmonyCrystal || disabled[num20] || (cellLevel[num20] + value7.enchant) * ctx.mysticFactor[num20] < 0 || !CriteriaSatisfied(ctx, value7, slots, ignore[num20], num20) || !value7.weaponOk)
					{
						continue;
					}
					int num21 = num20 % ctx.width;
					int num22 = num20 / ctx.width;
					int num23 = 0;
					for (int num24 = 0; num24 < 8; num24++)
					{
						int num25 = num21 + Neighbor8[num24].x;
						int num26 = num22 + Neighbor8[num24].y;
						Slot slot7 = At(slots, num25, num26, ctx.width, ctx.storage);
						if (slot7 != null && slot7.hasItem && !(slot7.charm is null) && ctx.itemByInstance.TryGetValue(slot7.instanceID, out var value8) && value8 != null && !value8.isBurden)
						{
							int num27 = num26 * ctx.width + num25;
							int num28 = Mathf.Clamp((cellLevel[num27] + value8.enchant) * ctx.mysticFactor[num27], 0, value8.maxLevel);
							num23 += num28;
						}
					}
					if (num23 > 0)
					{
						num2 += (double)(plugin.HarmonyLevelBonus.Value * (float)num23);
					}
				}
			}
			if (plugin.DedicationCompanionBonus.Value > 0f)
			{
				for (int num29 = 0; num29 < num; num29++)
				{
					Slot slot8 = slots[num29];
					if (slot8 == null || !slot8.hasItem || slot8.charm is null || !ctx.itemByInstance.TryGetValue(slot8.instanceID, out var value9) || value9 == null || !value9.isDedicationBadge || disabled[num29] || (cellLevel[num29] + value9.enchant) * ctx.mysticFactor[num29] < 0 || !CriteriaSatisfied(ctx, value9, slots, ignore[num29], num29) || !value9.weaponOk)
					{
						continue;
					}
					int num30 = num29 / ctx.width;
					int num31 = 0;
					int num32 = num30 * ctx.width;
					int num33 = Math.Min(num, num32 + ctx.width);
					for (int num34 = num32; num34 < num33; num34++)
					{
						if (num34 != num29)
						{
							Slot slot9 = slots[num34];
							if (slot9 != null && slot9.hasItem && !(slot9.charm is null) && ctx.itemByInstance.TryGetValue(slot9.instanceID, out var value10) && value10 != null && value10.isDedicationCompanion)
							{
								num31++;
							}
						}
					}
					if (num31 > 0)
					{
						num2 += (double)(plugin.DedicationCompanionBonus.Value * (float)num31);
					}
				}
			}
			if (plugin.HourglassBonus.Value > 0f)
			{
				for (int num35 = 0; num35 < num; num35++)
				{
					Slot slot10 = slots[num35];
					if (slot10 != null && slot10.hasItem && !(slot10.charm is null) && ctx.itemByInstance.TryGetValue(slot10.instanceID, out var value11) && value11 != null && value11.isHourglass && !disabled[num35] && (cellLevel[num35] + value11.enchant) * ctx.mysticFactor[num35] >= 0 && CriteriaSatisfied(ctx, value11, slots, ignore[num35], num35) && value11.weaponOk)
					{
						int num36 = num35 % ctx.width;
						int y = num35 / ctx.width;
						Slot slot11 = At(slots, num36 + 1, y, ctx.width, ctx.storage);
						if (slot11 != null && slot11.hasItem && !(slot11.charm is null) && ctx.itemByInstance.TryGetValue(slot11.instanceID, out var value12) && value12 != null && value12.isMagicBook)
						{
							num2 += (double)(plugin.HourglassBonus.Value * Math.Max(0f, value12.magicCd));
						}
					}
				}
			}
			if (plugin.RayShardBonus.Value > 0f)
			{
				for (int num37 = 0; num37 < num; num37++)
				{
					Slot slot12 = slots[num37];
					if (slot12 != null && slot12.hasItem && !(slot12.charm is null) && ctx.itemByInstance.TryGetValue(slot12.instanceID, out var value13) && value13 != null && value13.isRayShard && !disabled[num37] && (cellLevel[num37] + value13.enchant) * ctx.mysticFactor[num37] >= 0 && CriteriaSatisfied(ctx, value13, slots, ignore[num37], num37) && value13.weaponOk)
					{
						int num38 = num37 % ctx.width;
						int y2 = num37 / ctx.width;
						Slot slot13 = At(slots, num38 - 1, y2, ctx.width, ctx.storage);
						if (slot13 != null && slot13.hasItem && !(slot13.charm is null) && ctx.itemByInstance.TryGetValue(slot13.instanceID, out var value14) && value14 != null && value14.isMagicBook)
						{
							num2 += (double)(plugin.RayShardBonus.Value * (float)Math.Max(0, value14.magicMpCost));
						}
					}
				}
			}
			if (ctx.hasBelt && plugin.BeltRowBonus.Value > 0f)
			{
				for (int num39 = 0; num39 < num; num39++)
				{
					Slot slot14 = slots[num39];
					if (slot14 == null || !slot14.hasItem || slot14.charm is null || !ctx.itemByInstance.TryGetValue(slot14.instanceID, out var value15) || value15 == null || !value15.isBelt || disabled[num39] || (cellLevel[num39] + value15.enchant) * ctx.mysticFactor[num39] < 0 || !CriteriaSatisfied(ctx, value15, slots, ignore[num39], num39) || !value15.weaponOk)
					{
						continue;
					}
					int num40 = 0;
					int num41 = Math.Min(num, ctx.width);
					for (int num42 = 0; num42 < num41; num42++)
					{
						Slot slot15 = slots[num42];
						if (slot15 != null && slot15.hasItem && slot15.charm is not null && ctx.itemByInstance.TryGetValue(slot15.instanceID, out var value16) && value16 != null && !value16.isBurden)
						{
							num40++;
						}
					}
					num2 += (double)(plugin.BeltRowBonus.Value * (float)num40);
					break;
				}
			}
			if (plugin.CompassBonus.Value > 0f)
			{
				for (int num43 = 0; num43 < num; num43++)
				{
					Slot slot16 = slots[num43];
					if (slot16 == null || !slot16.hasItem || slot16.charm is null || !ctx.itemByInstance.TryGetValue(slot16.instanceID, out var value17) || value17 == null || !value17.isCompass)
					{
						continue;
					}
					int x = num43 % ctx.width;
					int num44 = num43 / ctx.width;
					Slot slot17 = At(slots, x, num44 - 1, ctx.width, ctx.storage);
					if (slot17 != null && slot17.hasItem && slot17.charm is not null && IsCompassPaired(ctx, slots, num43, slot16))
					{
						double num45 = 1.0;
						if (ctx.itemByInstance.TryGetValue(slot17.instanceID, out var value18) && value18 != null)
						{
							num45 = PriorityWeight(value18.priority);
						}
						num2 += (double)plugin.CompassBonus.Value * num45;
					}
				}
			}
			return num2 + EvaluateWhitePaperSynergy(ctx, slots);
		}

		private static bool MatchesItemKey(ItemInfo info, string token)
		{
			token = token.Trim();
			if (token.Length == 0)
			{
				return false;
			}
			if (info.entity != null && info.entity.aName != null && string.Equals(info.entity.aName.key, token, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			if (info.slot != null && info.slot.charm is not null && string.Equals(info.slot.charm.GetType().Name, token, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			return false;
		}

		private int RarityToPriority(EItemRarity rarity)
		{
			return rarity switch
			{
				EItemRarity.Legend => Mathf.Clamp(plugin.PriorityLegend.Value, 1, 4),
				EItemRarity.Eternal => Mathf.Clamp(plugin.PriorityEternal.Value, 1, 4),
				EItemRarity.Rare => Mathf.Clamp(plugin.PriorityRare.Value, 1, 4),
				EItemRarity.Uncommon => Mathf.Clamp(plugin.PriorityUncommon.Value, 1, 4),
				_ => Mathf.Clamp(plugin.PriorityCommon.Value, 1, 4),
			};
		}

		private double PriorityWeight(int priority)
		{
			if (!plugin.PriorityEnable.Value)
			{
				return 1.0;
			}
			return priority switch
			{
				1 => plugin.PriorityWeight1.Value,
				2 => plugin.PriorityWeight2.Value,
				3 => plugin.PriorityWeight3.Value,
				_ => plugin.PriorityWeight4.Value,
			};
		}

		private void LogLayoutGrid(SearchContext ctx, List<Slot> slots, string tag)
		{
			EvaluateLayout(ctx, slots);
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(tag + " 布局图(等级/物品)：");
			for (int i = 0; i < ctx.height; i++)
			{
				for (int j = 0; j < ctx.width; j++)
				{
					int num = i * ctx.width + j;
					if (num >= ctx.storage)
					{
						break;
					}
					int num2 = ctx.cellLevel[num];
					char c = '.';
					Slot slot = slots[num];
					if (slot != null && slot.hasItem)
					{
						c = ((!(slot.tablet is not null)) ? ((!(slot.charm is not null)) ? 'o' : ((!ctx.itemByInstance.TryGetValue(slot.instanceID, out var value) || value == null) ? 'c' : ((!value.isBurden) ? ((!value.isPlanetModule) ? ((!value.isCyclicRowCategory) ? ((!value.isWhitePaper) ? ((!value.isCompass) ? ((!value.isPlanetCategory) ? 'c' : 'P') : 'C') : 'W') : 'K') : 'M') : 'B'))) : 'T');
					}
					stringBuilder.Append($"[{num2,2}{c}]");
				}
				stringBuilder.Append(" | ");
			}
			Plugin.Log.LogInfo(stringBuilder.ToString());
		}

		private static void LogLayoutAnalysis(SearchContext ctx, List<Slot> slots, string tag)
		{
			int width = ctx.width;
			for (int i = 0; i < ctx.storage; i++)
			{
				Slot slot = slots[i];
				if (slot == null || !slot.hasItem || !ctx.itemByInstance.TryGetValue(slot.instanceID, out var value) || value == null)
				{
					continue;
				}
				int num = i % width;
				int num2 = i / width;
				if (value.isPlanetModule)
				{
					int num3 = 0;
					string text = "";
					for (int j = 0; j < 8; j++)
					{
						int num4 = num + Neighbor8[j].x;
						int num5 = num2 + Neighbor8[j].y;
						Slot slot2 = At(slots, num4, num5, width, ctx.storage);
						if (slot2 != null && slot2.hasItem && ctx.itemByInstance.TryGetValue(slot2.instanceID, out var value2) && value2 != null && value2.isPlanetCategory)
						{
							num3++;
							text += $"({num4},{num5}) ";
						}
					}
					Plugin.Log.LogInfo($"{tag} 望远镜@{num},{num2}：相邻行星 {num3} 颗 {text.Trim()}");
				}
				if (value.isHarmonyCrystal)
				{
					int num6 = 0;
					string text2 = "";
					for (int k = 0; k < 8; k++)
					{
						int num7 = num + Neighbor8[k].x;
						int num8 = num2 + Neighbor8[k].y;
						Slot slot3 = At(slots, num7, num8, width, ctx.storage);
						if (slot3 != null && slot3.hasItem && slot3.charm is not null && ctx.itemByInstance.TryGetValue(slot3.instanceID, out var value3) && value3 != null)
						{
							int num9 = num8 * width + num7;
							int num10 = Mathf.Clamp((ctx.cellLevel[num9] + value3.enchant) * ctx.mysticFactor[num9], 0, value3.maxLevel);
							num6 += num10;
							text2 += $"({num7},{num8}:{num10}) ";
						}
					}
					Plugin.Log.LogInfo($"{tag} 和谐之晶@{num},{num2}：周围8格等级和={num6} {text2.Trim()}");
				}
				if (value.isDedicationBadge)
				{
					int num11 = 0;
					string text3 = "";
					for (int l = 0; l < width; l++)
					{
						int num12 = num2 * width + l;
						if (num12 < ctx.storage && num12 != i)
						{
							Slot slot4 = slots[num12];
							if (slot4 != null && slot4.hasItem && ctx.itemByInstance.TryGetValue(slot4.instanceID, out var value4) && value4 != null && value4.isDedicationCompanion)
							{
								num11++;
								text3 += $"({l},{num2}) ";
							}
						}
					}
					Plugin.Log.LogInfo($"{tag} 奉献徽章@{num},{num2}：同行同伴 {num11} 个 {text3.Trim()}");
				}
				if (value.isHourglass)
				{
					Slot slot5 = At(slots, num + 1, num2, width, ctx.storage);
					if (slot5 != null && slot5.hasItem && slot5.charm is Charm_Magic)
					{
						float num13 = 0f;
						if (ctx.itemByInstance.TryGetValue(slot5.instanceID, out var value5) && value5 != null)
						{
							num13 = value5.magicCd;
						}
						Plugin.Log.LogInfo($"{tag} 沙漏@{num},{num2}：右边魔法书 CD={num13:F1}s");
					}
					else
					{
						Plugin.Log.LogInfo($"{tag} 沙漏@{num},{num2}：右边无魔法书（未配对）");
					}
				}
				if (value.isRayShard)
				{
					Slot slot6 = At(slots, num - 1, num2, width, ctx.storage);
					if (slot6 != null && slot6.hasItem && slot6.charm is Charm_Magic)
					{
						int num14 = 0;
						if (ctx.itemByInstance.TryGetValue(slot6.instanceID, out var value6) && value6 != null)
						{
							num14 = value6.magicMpCost;
						}
						Plugin.Log.LogInfo($"{tag} 雷伊星碎片@{num},{num2}：左侧魔法书耗蓝={num14}");
					}
					else
					{
						Plugin.Log.LogInfo($"{tag} 雷伊星碎片@{num},{num2}：左侧无魔法书（未配对）");
					}
				}
				if (value.isEternalEclipse)
				{
					string text4;
					if (ctx.frostCount == ctx.flameSwordCount)
					{
						text4 = "无限制（冰霜武具=太阳剑）";
					}
					else
					{
						bool flag = ctx.frostCount > ctx.flameSwordCount;
						text4 = string.Format("{0}（冰霜武具{1} > 太阳剑{2} = {3}）", flag ? "右三列" : "左三列", ctx.frostCount, ctx.flameSwordCount, flag ? "是" : "否");
					}
					string text5 = ((num < ctx.width / 2) ? "左三列" : "右三列");
					Plugin.Log.LogInfo($"{tag} 永恒蚀@{num},{num2}（{text5}）：{text4}");
				}
				if (value.isOpposingScale)
				{
					string text6 = ((ctx.glacierCount > ctx.emberCount) ? $"最右列（冰川{ctx.glacierCount} > 余烬{ctx.emberCount}）" : ((ctx.glacierCount >= ctx.emberCount) ? $"最左/最右列（冰川{ctx.glacierCount} = 余烬{ctx.emberCount}）" : $"最左列（冰川{ctx.glacierCount} < 余烬{ctx.emberCount}）"));
					string text7 = ((num == 0) ? "最左列" : ((num == ctx.width - 1) ? "最右列" : "中间"));
					Plugin.Log.LogInfo($"{tag} 对立之秤@{num},{num2}（{text7}）：{text6}");
				}
				if (value.isBelt)
				{
					int num15 = 0;
					for (int m = 0; m < Math.Min(ctx.storage, width); m++)
					{
						Slot slot7 = slots[m];
						if (slot7 != null && slot7.hasItem && slot7.charm is not null && ctx.itemByInstance.TryGetValue(slot7.instanceID, out var value7) && value7 != null && !value7.isBurden)
						{
							num15++;
						}
					}
					Plugin.Log.LogInfo($"{tag} 腰带@{num},{num2}：第一行神器 {num15} 件");
				}
				if (value.isCyclicRowCategory)
				{
					int num16 = num2 % CyclicRowCategories.Length;
					string text8 = CyclicRowCategoryNames[num16];
					string text9 = (string.Equals(CyclicRowCategories[num16], value.targetRowCategory, StringComparison.OrdinalIgnoreCase) ? "目标✓" : ("目标应为" + value.targetRowCategory));
					Plugin.Log.LogInfo($"{tag} 凯尔萨德尼钥匙@{num},{num2}：第{num2 + 1}行={text8} {text9}");
				}
				if (value.isCompass)
				{
					string text10 = "空";
					string text11 = "";
					string text12 = "";
					Slot slot8 = At(slots, num, num2 - 1, width, ctx.storage);
					if (slot8 != null && slot8.hasItem && slot8.charm is not null)
					{
						text10 = ((slot8.charm is Charm_UpCharmDamage) ? "指北针" : ((slot8.charm is IAttackableCharm attackableCharm && attackableCharm.IsAttackableCharm()) ? "伤害类" : "其他"));
						if (ctx.itemByInstance.TryGetValue(slot8.instanceID, out var value8) && value8 != null)
						{
							text11 = $" P{value8.priority}";
						}
					}
					if (ctx.compassTargetByInstance.TryGetValue(slot.instanceID, out var value9))
					{
						text12 = ((slot8 != null && slot8.hasItem && slot8.instanceID == value9) ? " 原目标✓" : $" 原目标绑定异常(期望实例{value9})");
					}
					Plugin.Log.LogInfo($"{tag} 罗盘@{num},{num2}：上方={text10}{text11}{text12}");
				}
				if (value.isWhitePaper)
				{
					StringBuilder stringBuilder = new StringBuilder();
					for (int n = 0; n < ctx.whitePaperTargets.Count; n++)
					{
						if (WhitePaperMatchesTarget(ctx, slots, i, n))
						{
							WhitePaperComboTarget whitePaperComboTarget = ctx.whitePaperTargets[n];
							stringBuilder.Append($" {whitePaperComboTarget.displayName}({whitePaperComboTarget.baseCount}→{Math.Min(whitePaperComboTarget.cap, whitePaperComboTarget.baseCount + 1)}/{whitePaperComboTarget.cap})");
						}
					}
					Plugin.Log.LogInfo((stringBuilder.Length > 0) ? $"{tag} 白纸@{num},{num2}：复制连击{stringBuilder}" : $"{tag} 白纸@{num},{num2}：左右未形成可补位的同连击");
				}
				if (value.isBurden)
				{
					int num17 = ctx.cellLevel[i];
					Plugin.Log.LogInfo(string.Format("{0} 负担@{1},{2}：格等级={3}（{4}）", tag, num, num2, num17, (num17 < 0) ? "负格✓" : "非负格"));
				}
			}
		}

		private static void LogItemIdentification(SearchContext ctx)
		{
			int num = 0;
			int num2 = 0;
			int num3 = 0;
			int num4 = 0;
			int num5 = 0;
			int num6 = 0;
			int num7 = 0;
			int num8 = 0;
			int num9 = 0;
			int num10 = 0;
			int num11 = 0;
			int num12 = 0;
			int num13 = 0;
			int num14 = 0;
			int num15 = 0;
			int num16 = 0;
			int num17 = 0;
			int num18 = 0;
			int num19 = 0;
			int[] array = new int[5];
			int[] array2 = new int[5];
			foreach (ItemInfo item in ctx.items)
			{
				if (item.isBurden)
				{
					num3++;
				}
				if (item.isPlanetModule)
				{
					num++;
				}
				if (item.isCompass)
				{
					num2++;
				}
				if (item.isAttackable && !item.isCompass)
				{
					num4++;
				}
				if (item.isPlanetCategory)
				{
					num5++;
				}
				if (item.enchant != 0)
				{
					num6++;
					num7 += item.enchant;
				}
				if (item.isDedicationBadge)
				{
					num10++;
				}
				if (item.isDedicationCompanion)
				{
					num11++;
				}
				if (item.isHourglass)
				{
					num12++;
				}
				if (item.isMagicBook)
				{
					num13++;
				}
				if (item.isRayShard)
				{
					num16++;
				}
				if (item.isEternalEclipse)
				{
					num14++;
				}
				if (item.isOpposingScale)
				{
					num15++;
				}
				if (item.isBelt)
				{
					num17++;
				}
				if (item.lowLevelValue)
				{
					num18++;
				}
				if (item.minDesiredLevel > 0)
				{
					num19++;
				}
				if (!item.isCharm || item.isBurden)
				{
					continue;
				}
				array[(int)item.rarity]++;
				if (item.priority >= 1 && item.priority <= 4)
				{
					array2[item.priority]++;
				}
				if (item.slot.charm.isWeaponRelatedCharm)
				{
					num8++;
					WeaponControllerSimple weaponController = item.slot.charm.WeaponController;
					if (weaponController != null && weaponController.currentWeapon != null && weaponController.currentWeapon.weaponType == item.slot.charm.relatedWeapon)
					{
						num9++;
					}
				}
			}
			Plugin.Log.LogInfo($"识别：存储{ctx.storage}格({ctx.width}x{ctx.height}) 石板{ctx.steles.Count} 护符{ctx.charms.Count}" + $" 望远镜{num} 罗盘{num2}(锁定原目标{ctx.compassTargetByInstance.Count})" + $" 伤害类{num4} 行星类{num5} 负担{num3}" + $" 神秘{ctx.mysticCount}个/×2地块{ctx.mysticActiveCells}格" + $" 附魔{num6}件(+{num7}) 武器相关{num8}(匹配{num9})" + $" 奉献徽章{num10} 同伴{num11}" + $" 沙漏{num12} 魔法书{num13} 雷伊星碎片{num16} 白纸{ctx.whitePapers.Count}" + $" 永恒蚀{num14}(冰霜武具{ctx.frostCount}/太阳剑{ctx.flameSwordCount})" + $" 对立之秤{num15}(冰川{ctx.glacierCount}/余烬{ctx.emberCount})" + $" 腰带{num17} 低等级价值{num18} 最低等级目标{num19}" + $" 优先级[P1:{array2[1]} P2:{array2[2]} P3:{array2[3]} P4:{array2[4]}]" + $" 稀有度[普通{array[0]} 优秀{array[1]} 稀有{array[2]} 传说{array[3]} 永恒{array[4]}]");
			StringBuilder stringBuilder = new StringBuilder();
			foreach (ItemInfo charm in ctx.charms)
			{
				string text = ((charm.slot != null && charm.slot.charm is not null) ? charm.slot.charm.GetType().Name : "?");
				string text2 = ((charm.entity != null && charm.entity.aName != null) ? charm.entity.aName.key : "?");
				stringBuilder.Append(" [" + text2 + "|" + text + "]");
			}
			if (stringBuilder.Length > 0)
			{
				Plugin.Log.LogInfo($"护符清单（key|类名）:{stringBuilder}");
			}
			if (ctx.whitePapers.Count <= 0)
			{
				return;
			}
			if (ctx.whitePaperTargets.Count == 0)
			{
				Plugin.Log.LogInfo("白纸：未找到同时具备两件神器且尚未达到最高档位的连击。");
				return;
			}
			StringBuilder stringBuilder2 = new StringBuilder();
			int num20 = Math.Min(5, ctx.whitePaperTargets.Count);
			for (int i = 0; i < num20; i++)
			{
				WhitePaperComboTarget whitePaperComboTarget = ctx.whitePaperTargets[i];
				stringBuilder2.Append($" [{whitePaperComboTarget.displayName}:{whitePaperComboTarget.baseCount}/{whitePaperComboTarget.cap}]");
			}
			Plugin.Log.LogInfo($"白纸补位候选（按优先级）:{stringBuilder2}");
		}

		private List<Slot> BuildSmartStart(SearchContext ctx)
		{
			int storage = ctx.storage;
			Slot[] array = new Slot[storage];
			bool[] array2 = new bool[storage];
			List<ItemInfo> list = new List<ItemInfo>(ctx.steles);
			list.Sort((ItemInfo a, ItemInfo b) => b.steleImportance.CompareTo(a.steleImportance));
			HashSet<int> hashSet = new HashSet<int>();
			foreach (ItemInfo item in list)
			{
				int num = -1;
				int num2 = item.slot.rotation;
				float num3 = float.MinValue;
				int num4 = ((!item.tabletRotatable) ? 1 : 4);
				for (int num5 = 0; num5 < storage; num5++)
				{
					if (array2[num5])
					{
						continue;
					}
					for (int num6 = 0; num6 < num4; num6++)
					{
						if (ctx.stelePatterns.TryGetValue(item.slot.instanceID, out var value) && value.TryGetValue(num5 * 4 + num6, out var value2))
						{
							float num7 = EvaluateStelePattern(ctx, value2, array, array2, hashSet);
							if (num7 > num3)
							{
								num3 = num7;
								num = num5;
								num2 = num6;
							}
						}
					}
				}
				if (num < 0)
				{
					continue;
				}
				Slot slot = item.slot.Clone();
				slot.rotation = num2;
				array[num] = slot;
				array2[num] = true;
				if (!ctx.stelePatterns.TryGetValue(item.slot.instanceID, out var value3) || !value3.TryGetValue(num * 4 + num2, out var value4))
				{
					continue;
				}
				foreach (EffectEntry effect in value4.effects)
				{
					if (effect.cell >= 0 && effect.cell < storage)
					{
						hashSet.Add(effect.cell);
					}
				}
			}
			List<Slot> list2 = SlotsFromArray(array, storage);
			EvaluateLayout(ctx, list2);
			List<ItemInfo> list3 = new List<ItemInfo>(ctx.charms);
			List<ItemInfo> list4 = list3.FindAll((ItemInfo x) => KindPriority(x.kind) >= 2);
			list4.Sort(delegate(ItemInfo a, ItemInfo b)
			{
				int num42 = KindPriority(b.kind).CompareTo(KindPriority(a.kind));
				if (num42 != 0)
				{
					return num42;
				}
				num42 = CompareManualPriority(a, b);
				return (num42 == 0) ? a.priority.CompareTo(b.priority) : num42;
			});
			foreach (ItemInfo item2 in list4)
			{
				int num8 = FindBestCharmCell(ctx, item2, array, array2, list2);
				if (num8 < 0)
				{
					num8 = FirstFree(array2);
				}
				if (num8 >= 0)
				{
					array[num8] = item2.slot.Clone();
					array2[num8] = true;
					list3.Remove(item2);
					list2 = SlotsFromArray(array, storage);
					EvaluateLayout(ctx, list2);
				}
			}
			foreach (ItemInfo item3 in list3.FindAll((ItemInfo x) => x.isRowLocked))
			{
				int num9 = FindBestCharmCell(ctx, item3, array, array2, list2);
				if (num9 < 0)
				{
					num9 = FirstFreeForLockedItem(array2, item3, ctx);
				}
				if (num9 >= 0)
				{
					array[num9] = item3.slot.Clone();
					array2[num9] = true;
					list3.Remove(item3);
					list2 = SlotsFromArray(array, storage);
					EvaluateLayout(ctx, list2);
				}
			}
			int num10 = -1;
			ItemInfo itemInfo = list3.Find((ItemInfo x) => x.isPlanetModule);
			if (itemInfo != null)
			{
				int num11 = FindBestCharmCell(ctx, itemInfo, array, array2, list2);
				if (num11 >= 0)
				{
					array[num11] = itemInfo.slot.Clone();
					array2[num11] = true;
					num10 = num11;
					list3.Remove(itemInfo);
					list2 = SlotsFromArray(array, storage);
					EvaluateLayout(ctx, list2);
				}
			}
			HashSet<int> hashSet2 = new HashSet<int>();
			foreach (ItemInfo item4 in list3.FindAll((ItemInfo x) => x.isHarmonyCrystal))
			{
				int num12 = FindBestCharmCell(ctx, item4, array, array2, list2);
				if (num12 < 0)
				{
					num12 = FirstFree(array2);
				}
				if (num12 < 0)
				{
					continue;
				}
				array[num12] = item4.slot.Clone();
				array2[num12] = true;
				list3.Remove(item4);
				list2 = SlotsFromArray(array, storage);
				EvaluateLayout(ctx, list2);
				int num13 = num12 % ctx.width;
				int num14 = num12 / ctx.width;
				for (int num15 = 0; num15 < 8; num15++)
				{
					int num16 = num13 + Neighbor8[num15].x;
					int num17 = num14 + Neighbor8[num15].y;
					if (InBounds(num16, num17, ctx.width, ctx.height))
					{
						int num18 = num17 * ctx.width + num16;
						if (num18 >= 0 && num18 < ctx.storage && !array2[num18])
						{
							hashSet2.Add(num18);
						}
					}
				}
			}
			int dedicationRow = -1;
			foreach (ItemInfo item5 in list3.FindAll((ItemInfo x) => x.isDedicationBadge))
			{
				int num19 = FindBestCharmCell(ctx, item5, array, array2, list2);
				if (num19 < 0)
				{
					num19 = FirstFree(array2);
				}
				if (num19 >= 0)
				{
					array[num19] = item5.slot.Clone();
					array2[num19] = true;
					dedicationRow = num19 / ctx.width;
					list3.Remove(item5);
					list2 = SlotsFromArray(array, storage);
					EvaluateLayout(ctx, list2);
				}
			}
			HashSet<int> hashSet3 = new HashSet<int>();
			foreach (ItemInfo item6 in list3.FindAll((ItemInfo x) => x.isHourglass))
			{
				int num20 = FindBestCharmCell(ctx, item6, array, array2, list2);
				if (num20 < 0)
				{
					num20 = FirstFree(array2);
				}
				if (num20 >= 0)
				{
					array[num20] = item6.slot.Clone();
					array2[num20] = true;
					list3.Remove(item6);
					list2 = SlotsFromArray(array, storage);
					EvaluateLayout(ctx, list2);
					int num21 = num20 % ctx.width;
					int num22 = num20 / ctx.width * ctx.width + (num21 + 1);
					if (num21 + 1 < ctx.width && num22 >= 0 && num22 < ctx.storage && !array2[num22])
					{
						hashSet3.Add(num22);
					}
				}
			}
			HashSet<int> hashSet4 = new HashSet<int>();
			foreach (ItemInfo item7 in list3.FindAll((ItemInfo x) => x.isRayShard))
			{
				int num23 = FindBestCharmCell(ctx, item7, array, array2, list2);
				if (num23 < 0)
				{
					num23 = FirstFree(array2);
				}
				if (num23 >= 0)
				{
					array[num23] = item7.slot.Clone();
					array2[num23] = true;
					list3.Remove(item7);
					list2 = SlotsFromArray(array, storage);
					EvaluateLayout(ctx, list2);
					int num24 = num23 % ctx.width;
					int num25 = num23 / ctx.width * ctx.width + (num24 - 1);
					if (num24 - 1 >= 0 && num25 >= 0 && num25 < ctx.storage && !array2[num25])
					{
						hashSet4.Add(num25);
					}
				}
			}
			if (num10 >= 0)
			{
				int num26 = num10 % ctx.width;
				int num27 = num10 / ctx.width;
				foreach (ItemInfo item8 in list3.FindAll((ItemInfo x) => x.isPlanetCategory && !x.excludeFromPlanetCluster))
				{
					int num28 = -1;
					float num29 = float.MinValue;
					for (int num30 = 0; num30 < 8; num30++)
					{
						int num31 = num26 + Neighbor8[num30].x;
						int num32 = num27 + Neighbor8[num30].y;
						if (!InBounds(num31, num32, ctx.width, ctx.height))
						{
							continue;
						}
						int num33 = num32 * ctx.width + num31;
						if (num33 >= 0 && num33 < ctx.storage && !array2[num33] && ctx.cellLevel[num33] >= 0 && !ctx.disabled[num33])
						{
							float num34 = (float)ctx.cellLevel[num33] * 100f;
							if (num34 > num29)
							{
								num29 = num34;
								num28 = num33;
							}
						}
					}
					int num35 = ((num28 >= 0) ? num28 : FindBestCharmCell(ctx, item8, array, array2, list2, hashSet2, dedicationRow));
					if (num35 < 0)
					{
						num35 = FirstFree(array2);
					}
					if (num35 >= 0)
					{
						array[num35] = item8.slot.Clone();
						array2[num35] = true;
						list3.Remove(item8);
						list2 = SlotsFromArray(array, storage);
						EvaluateLayout(ctx, list2);
					}
				}
			}
			foreach (ItemInfo item9 in list3.FindAll((ItemInfo x) => x.isCompass))
			{
				int num36 = (ctx.compassTargetByInstance.ContainsKey(item9.slot.instanceID) ? (-1) : FindCompassTargetCell(ctx, array, array2));
				if (num36 < 0)
				{
					num36 = FindBestCharmCell(ctx, item9, array, array2, list2, hashSet2, dedicationRow);
				}
				if (num36 < 0)
				{
					num36 = FirstFree(array2);
				}
				if (num36 >= 0)
				{
					array[num36] = item9.slot.Clone();
					array2[num36] = true;
					list3.Remove(item9);
					list2 = SlotsFromArray(array, storage);
					EvaluateLayout(ctx, list2);
				}
			}
			list3.Sort(delegate(ItemInfo a, ItemInfo b)
			{
				int num42 = CompareManualPriority(a, b);
				if (num42 != 0)
				{
					return num42;
				}
				num42 = a.priority.CompareTo(b.priority);
				return (num42 != 0) ? num42 : b.rarity.CompareTo(a.rarity);
			});
			foreach (ItemInfo item10 in list3)
			{
				int num37 = FindBestCharmCell(ctx, item10, array, array2, list2, hashSet2, dedicationRow, hashSet3, hashSet4);
				if (num37 < 0)
				{
					num37 = FirstFree(array2);
				}
				if (num37 >= 0)
				{
					array[num37] = item10.slot.Clone();
					array2[num37] = true;
					list2 = SlotsFromArray(array, storage);
					EvaluateLayout(ctx, list2);
				}
			}
			foreach (ItemInfo other in ctx.others)
			{
				int num38 = FirstFree(array2);
				if (num38 >= 0)
				{
					array[num38] = other.slot.Clone();
					array2[num38] = true;
				}
			}
			foreach (ItemInfo burden in ctx.burdens)
			{
				int num39 = FindWorstCell(ctx, array, array2);
				if (num39 < 0)
				{
					num39 = FirstFree(array2);
				}
				if (num39 >= 0)
				{
					array[num39] = burden.slot.Clone();
					array2[num39] = true;
				}
			}
			List<Slot> list5 = SlotsFromArray(array, storage);
			if (!RestoreCompassBindings(ctx, list5))
			{
				Plugin.Log.LogWarning("智能初始布局无法恢复指北针原目标绑定，已回退整理前布局。");
				return CloneSlots(ctx.original);
			}
			if (ctx.whitePaperTargets.Count > 0 && plugin.WhitePaperComboBonus.Value > 0f)
			{
				EvaluateLayout(ctx, list5);
				System.Random rng = new System.Random((ctx.storage * 397) ^ (ctx.items.Count * 7919));
				int num40 = Math.Max(4, ctx.whitePapers.Count * 4);
				for (int num41 = 0; num41 < num40; num41++)
				{
					if (TryWhitePaperMove(ctx, list5, rng))
					{
						RestoreCompassBindings(ctx, list5);
						EvaluateLayout(ctx, list5);
					}
				}
			}
			return list5;
		}

		private int FindCompassTargetCell(SearchContext ctx, Slot[] result, bool[] occupied)
		{
			int result2 = -1;
			float num = float.MinValue;
			for (int i = 0; i < ctx.storage; i++)
			{
				if (occupied[i])
				{
					continue;
				}
				int num2 = i % ctx.width;
				int num3 = i / ctx.width;
				int num4 = num2;
				int num5 = num3 - 1;
				if (num5 < 0)
				{
					continue;
				}
				int num6 = num5 * ctx.width + num4;
				Slot slot = null;
				if (num6 >= 0 && num6 < ctx.storage)
				{
					slot = result[num6];
				}
				if (slot != null && slot.hasItem && slot.charm is not null && ctx.itemByInstance.TryGetValue(slot.instanceID, out var value) && value != null && (value.isCompass || value.isAttackable))
				{
					float num7 = 1f;
					if (ctx.itemByInstance.TryGetValue(slot.instanceID, out var value2) && value2 != null)
					{
						num7 = (float)PriorityWeight(value2.priority);
					}
					float num8 = (float)ctx.cellLevel[i] * 100f * num7 - (ctx.disabled[i] ? 500f : 0f);
					if (num8 > num)
					{
						num = num8;
						result2 = i;
					}
				}
			}
			return result2;
		}

		private static int SteleImportance(StoneTablet tablet)
		{
			if (tablet == null)
			{
				return 0;
			}
			try
			{
				string query = tablet.GetQuery(tablet.instanceID);
				if (string.IsNullOrEmpty(query))
				{
					return 0;
				}
				int num = 0;
				string[] array = query.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
				for (int i = 0; i < array.Length; i++)
				{
					string[] array2 = array[i].Split(new char[1] { ' ' });
					if (array2.Length >= 2 && int.TryParse(array2[array2.Length - 1], out var result) && result < 0)
					{
						num++;
					}
				}
				return num;
			}
			catch
			{
				return 0;
			}
		}

		private static float EvaluateStelePattern(SearchContext ctx, StelePattern pattern, Slot[] result, bool[] occupied, HashSet<int> steleEffectCells = null)
		{
			if (!ConditionsOk(pattern, SlotsFromArray(result, ctx.storage)))
			{
				return -1000f;
			}
			float num = 0f;
			if (steleEffectCells != null && pattern.cell >= 0 && steleEffectCells.Contains(pattern.cell))
			{
				num -= 100f;
			}
			foreach (EffectEntry effect in pattern.effects)
			{
				bool flag = effect.cell >= 0 && effect.cell < ctx.storage && (occupied[effect.cell] || result[effect.cell] != null);
				switch (effect.kind)
				{
				case 0:
					num += (float)effect.value * 10f;
					if (effect.value < 0 && !flag)
					{
						num -= 160f;
					}
					else if (effect.value > 0 && flag)
					{
						num -= 60f;
					}
					break;
				case 2:
					num += 25f;
					break;
				case 1:
					num += 1f;
					break;
				}
			}
			return num;
		}

		private static int FindBestCharmCell(SearchContext ctx, ItemInfo charm, Slot[] result, bool[] occupied, List<Slot> slotsNow, HashSet<int> harmonyNeighbors = null, int dedicationRow = -1, HashSet<int> hourglassRightCells = null, HashSet<int> rayShardLeftCells = null)
		{
			int num = -1;
			float num2 = float.MinValue;
			int num3 = -1;
			float num4 = float.MinValue;
			int num5 = -1;
			float num6 = float.MinValue;
			bool flag = KindPriority(charm.kind) >= 2;
			bool flag2 = charm.isEternalEclipse && ctx.frostCount != ctx.flameSwordCount;
			bool flag3 = ctx.frostCount > ctx.flameSwordCount;
			bool isOpposingScale = charm.isOpposingScale;
			bool flag4 = ctx.glacierCount > ctx.emberCount;
			bool flag5 = ctx.glacierCount < ctx.emberCount;
			for (int i = 0; i < ctx.storage; i++)
			{
				if (occupied[i])
				{
					continue;
				}
				int num7 = i % ctx.width;
				int num8 = i / ctx.width;
				if (!IsAllowedLockedRow(charm, num8) || (charm.isHourglass && num7 == ctx.width - 1) || (charm.isRayShard && num7 == 0) || (flag2 && ((flag3 && num7 < ctx.width / 2) || (!flag3 && num7 >= ctx.width / 2))))
				{
					continue;
				}
				if (isOpposingScale)
				{
					bool flag6 = num7 == 0 || num7 == ctx.width - 1;
					if ((flag4 && num7 != ctx.width - 1) || (flag5 && num7 != 0) || (!flag4 && !flag5 && !flag6))
					{
						continue;
					}
				}
				bool flag7 = ctx.ignore[i];
				bool flag8 = IsSatisfyingCell(charm.kind, num7, num8, i, ctx.storage, ctx.width);
				float num9 = (float)ctx.cellLevel[i] * 100f * (float)ctx.mysticFactor[i] - (ctx.disabled[i] ? 500f : 0f);
				if (ctx.hasBelt && num8 == 0)
				{
					num9 += 6000f;
				}
				if (harmonyNeighbors != null && harmonyNeighbors.Contains(i))
				{
					num9 += 8000f;
				}
				if (dedicationRow >= 0 && num8 == dedicationRow)
				{
					num9 += 6000f;
				}
				if (charm.isMagicBook && hourglassRightCells != null && hourglassRightCells.Contains(i))
				{
					num9 += 8000f + charm.magicCd * 4000f;
				}
				if (charm.isMagicBook && rayShardLeftCells != null && rayShardLeftCells.Contains(i))
				{
					num9 += 8000f + (float)charm.magicMpCost * 4000f;
				}
				if (ctx.mysticFactor[i] > 1)
				{
					num9 += 20000f;
				}
				if (flag)
				{
					if (charm.preferIgnoreCells)
					{
						if (flag7)
						{
							num9 += 5000f;
						}
						else if (flag8)
						{
							num9 += 500f;
						}
					}
					else if (flag8 && !flag7)
					{
						num9 += 500f;
					}
				}
				if (num9 > num6)
				{
					num6 = num9;
					num5 = i;
				}
				if (flag8 && !flag7 && num9 > num2)
				{
					num2 = num9;
					num = i;
				}
				if (flag7 && num9 > num4)
				{
					num4 = num9;
					num3 = i;
				}
			}
			if (flag)
			{
				if (charm.preferIgnoreCells)
				{
					if (num3 >= 0)
					{
						return num3;
					}
					if (num >= 0)
					{
						return num;
					}
				}
				else
				{
					if (num >= 0)
					{
						return num;
					}
					if (num3 >= 0)
					{
						return num3;
					}
				}
			}
			if ((flag2 || isOpposingScale) && num5 < 0)
			{
				for (int j = 0; j < ctx.storage; j++)
				{
					if (!occupied[j])
					{
						float num10 = (float)ctx.cellLevel[j] * 100f * (float)ctx.mysticFactor[j] - (ctx.disabled[j] ? 500f : 0f);
						if (num10 > num6)
						{
							num6 = num10;
							num5 = j;
						}
					}
				}
			}
			return num5;
		}

		private static int FindWorstCell(SearchContext ctx, Slot[] result, bool[] occupied)
		{
			int result2 = -1;
			int num = int.MaxValue;
			for (int i = 0; i < ctx.storage; i++)
			{
				if (!occupied[i])
				{
					int num2 = ctx.cellLevel[i];
					if (num2 < num)
					{
						num = num2;
						result2 = i;
					}
				}
			}
			return result2;
		}

		private static int FirstFree(bool[] occupied)
		{
			for (int i = 0; i < occupied.Length; i++)
			{
				if (!occupied[i])
				{
					return i;
				}
			}
			return -1;
		}

		private static int FirstFreeForLockedItem(bool[] occupied, ItemInfo item, SearchContext ctx)
		{
			for (int i = 0; i < ctx.storage; i++)
			{
				if (!occupied[i] && IsAllowedLockedRow(item, i / ctx.width))
				{
					return i;
				}
			}
			return -1;
		}

		private static List<Slot> SlotsFromArray(Slot[] result, int storage)
		{
			List<Slot> list = new List<Slot>(storage);
			for (int i = 0; i < storage; i++)
			{
				list.Add((result[i] != null) ? result[i] : Slot.Empty());
			}
			return list;
		}

		private static void FillItemPositionMap(List<Slot> slots, Dictionary<int, int> positions)
		{
			positions.Clear();
			if (slots == null)
			{
				return;
			}
			for (int i = 0; i < slots.Count; i++)
			{
				Slot slot = slots[i];
				if (slot != null && slot.hasItem)
				{
					positions[slot.instanceID] = i;
				}
			}
		}

		private static bool CanPlaceCompassChain(SearchContext ctx, CompassChain chain, int rootCell, HashSet<int> reserved)
		{
			if (rootCell < 0 || rootCell >= ctx.storage)
			{
				return false;
			}
			int num = rootCell % ctx.width;
			for (int i = 0; i < chain.instanceIDs.Count; i++)
			{
				int num2 = rootCell + i * ctx.width;
				if (num2 < 0 || num2 >= ctx.storage || num2 % ctx.width != num || (reserved != null && reserved.Contains(num2)))
				{
					return false;
				}
				if (ctx.itemByInstance.TryGetValue(chain.instanceIDs[i], out var value) && value != null && !IsAllowedLockedRow(value, num2 / ctx.width))
				{
					return false;
				}
			}
			return true;
		}

		private static void SetCompassChainReservation(SearchContext ctx, CompassChain chain, int rootCell, HashSet<int> reserved, bool value)
		{
			for (int i = 0; i < chain.instanceIDs.Count; i++)
			{
				int item = rootCell + i * ctx.width;
				if (value)
				{
					reserved.Add(item);
				}
				else
				{
					reserved.Remove(item);
				}
			}
		}

		private static int FindClosestCompassChainRoot(SearchContext ctx, CompassChain chain, int currentRoot, int fallbackRoot, HashSet<int> reserved)
		{
			int num = ((currentRoot >= 0) ? currentRoot : fallbackRoot);
			int num2 = num % ctx.width;
			int num3 = num / ctx.width;
			int num4 = -1;
			int num5 = int.MaxValue;
			int num6 = int.MaxValue;
			for (int i = 0; i < ctx.storage; i++)
			{
				if (CanPlaceCompassChain(ctx, chain, i, reserved))
				{
					int num7 = Math.Abs(i % ctx.width - num2) + Math.Abs(i / ctx.width - num3);
					int num8 = ((i != fallbackRoot) ? 1 : 0);
					if (num7 < num5 || (num7 == num5 && num8 < num6) || (num7 == num5 && num8 == num6 && i < num4))
					{
						num4 = i;
						num5 = num7;
						num6 = num8;
					}
				}
			}
			return num4;
		}

		private static void SwapSlotsAndTrack(List<Slot> slots, Dictionary<int, int> positions, int a, int b)
		{
			if (a != b)
			{
				SwapSlots(slots, a, b);
				if (slots[a] != null && slots[a].hasItem)
				{
					positions[slots[a].instanceID] = a;
				}
				if (slots[b] != null && slots[b].hasItem)
				{
					positions[slots[b].instanceID] = b;
				}
			}
		}

		private static bool RestoreCompassBindings(SearchContext ctx, List<Slot> slots)
		{
			if (ctx.compassChains.Count == 0)
			{
				return true;
			}
			if (CompassBindingsSatisfied(ctx, slots))
			{
				return true;
			}
			Dictionary<int, int> compassPositionScratch = ctx.compassPositionScratch;
			FillItemPositionMap(slots, compassPositionScratch);
			HashSet<int> compassReservedScratch = ctx.compassReservedScratch;
			compassReservedScratch.Clear();
			int[] compassRootScratch = ctx.compassRootScratch;
			for (int i = 0; i < ctx.compassChains.Count; i++)
			{
				CompassChain compassChain = ctx.compassChains[i];
				if (!CanPlaceCompassChain(ctx, compassChain, compassChain.originalRootCell, compassReservedScratch))
				{
					return false;
				}
				compassRootScratch[i] = compassChain.originalRootCell;
				SetCompassChainReservation(ctx, compassChain, compassRootScratch[i], compassReservedScratch, value: true);
			}
			for (int j = 0; j < ctx.compassChains.Count; j++)
			{
				CompassChain compassChain2 = ctx.compassChains[j];
				SetCompassChainReservation(ctx, compassChain2, compassRootScratch[j], compassReservedScratch, value: false);
				int value;
				int currentRoot = (compassPositionScratch.TryGetValue(compassChain2.instanceIDs[0], out value) ? value : compassChain2.originalRootCell);
				int num = FindClosestCompassChainRoot(ctx, compassChain2, currentRoot, compassChain2.originalRootCell, compassReservedScratch);
				if (num < 0)
				{
					return false;
				}
				compassRootScratch[j] = num;
				SetCompassChainReservation(ctx, compassChain2, num, compassReservedScratch, value: true);
			}
			for (int k = 0; k < ctx.compassChains.Count; k++)
			{
				CompassChain compassChain3 = ctx.compassChains[k];
				for (int l = 0; l < compassChain3.instanceIDs.Count; l++)
				{
					int key = compassChain3.instanceIDs[l];
					if (!compassPositionScratch.TryGetValue(key, out var value2))
					{
						return false;
					}
					int b = compassRootScratch[k] + l * ctx.width;
					SwapSlotsAndTrack(slots, compassPositionScratch, value2, b);
				}
			}
			return CompassBindingsSatisfied(ctx, slots);
		}

		private static bool VerifyInventorySnapshot(GridInventory inv, List<Slot> captured)
		{
			try
			{
				int currentInventoryStorage = inv.CurrentInventoryStorage;
				HashSet<int> hashSet = new HashSet<int>();
				foreach (KeyValuePair<ItemPosition, NewItemOwnInstance> item in inv.inventoryMatrix)
				{
					if (item.Value != null && InventoryScope.IsMainCell(item.Key.x, item.Key.y, currentInventoryStorage))
					{
						hashSet.Add(item.Value.InstanceID);
					}
				}
				HashSet<int> hashSet2 = new HashSet<int>();
				foreach (Slot item2 in captured)
				{
					if (item2 != null && item2.hasItem)
					{
						hashSet2.Add(item2.instanceID);
					}
				}
				if (hashSet.Count != hashSet2.Count)
				{
					return false;
				}
				foreach (int item3 in hashSet)
				{
					if (!hashSet2.Contains(item3))
					{
						return false;
					}
				}
				return true;
			}
			catch
			{
				return false;
			}
		}

		private void StartRequestedSort()
		{
			if (busy)
			{
				Plugin.Log.LogWarning("整理正在进行中，请稍候再试。");
				return;
			}
			if (!NetworkClient.active)
			{
				Notify("未进入游戏会话，无法整理背包。");
				return;
			}
			NetworkIdentity localPlayer = NetworkClient.localPlayer;
			if (localPlayer == null)
			{
				Notify("未找到本地玩家，无法整理背包。");
				return;
			}
			PlayerAvatar component = localPlayer.GetComponent<PlayerAvatar>();
			if (component == null)
			{
				Notify("未找到玩家角色，无法整理背包。");
				return;
			}
			GridInventory inventory = component.Inventory;
			if (inventory == null)
			{
				Notify("背包不可用，无法整理。");
				return;
			}
			if (inventory.CurrentInventoryStorage <= 1 || inventory.charms.Count == 0)
			{
				Notify("整理完成");
				return;
			}
			busy = true;
			bool flag = false;
			try
			{
				switch (plugin.Mode.Value)
				{
				case SortMode.Vanilla:
					SortVanilla(inventory);
					break;
				case SortMode.Enhanced:
					flag = StartSortEnhanced(inventory);
					break;
				default:
					Plugin.Log.LogWarning($"未知整理模式 {plugin.Mode.Value}，使用内置整理。");
					SortVanilla(inventory);
					break;
				}
			}
			catch (Exception arg)
			{
				Plugin.Log.LogError($"整理背包异常: {arg}");
				Notify("整理背包失败，详见日志。");
			}
			finally
			{
				if (!flag)
				{
					busy = false;
				}
			}
		}

		private static int CompareManualPriority(ItemInfo a, ItemInfo b)
		{
			int num = a?.manualPriorityRank ?? 0;
			int num2 = b?.manualPriorityRank ?? 0;
			if (num == num2)
			{
				return 0;
			}
			if (num == 0)
			{
				return 1;
			}
			if (num2 == 0)
			{
				return -1;
			}
			return num.CompareTo(num2);
		}

		public void Poll()
        {
            if (pendingEnhanced == null && requestPending)
            {
                try { TryStartRequest(); }
                catch (Exception ex) { requestPending = false; Plugin.Log.LogError(ex); Notify("整理失败"); }
            }
			if (pendingEnhanced == null)
			{
				return;
			}
			PendingEnhancedSort pendingEnhancedSort = pendingEnhanced;
			try
			{
				if (!NetworkClient.active || pendingEnhancedSort.inv == null)
				{
					pendingEnhancedSort.ctx.cancelled = true;
                    if (pendingSearch != null && !pendingSearch.IsCompleted) return;
                    if (pendingSearch != null && pendingSearch.IsFaulted) Plugin.Log.LogWarning(pendingSearch.Exception);
                    CancelEnhancedSort(pendingEnhancedSort, "游戏会话已结束，本次整理已取消。", notifyFailure: false);
				}
				else if (pendingSearch != null)
				{
					if (pendingEnhancedSort.inventoryRevision != ManualPriorityManager.InventoryRevision || pendingEnhancedSort.marksRevision != ManualPriorityManager.Revision || IsDragging() || !LayoutsEquivalent(CaptureState(pendingEnhancedSort.inv), pendingEnhancedSort.original)) pendingEnhancedSort.ctx.cancelled = true;
                    if (pendingSearch.IsCompleted)
					{
						Task<SearchOutcome> task = pendingSearch;
						pendingSearch = null;
						var outcome = task.GetAwaiter().GetResult();
                        if (pendingEnhancedSort.ctx.cancelled) RestartRequest(pendingEnhancedSort);
                        else BeginApplyEnhanced(pendingEnhancedSort, outcome);
					}
				}
				else if (pendingEnhancedSort.applying)
				{
					AdvanceApplyEnhanced(pendingEnhancedSort);
				}
			}
			catch (Exception arg)
			{
				Plugin.Log.LogError($"后台整理搜索异常: {arg}");
				CancelEnhancedSort(pendingEnhancedSort, "整理背包失败，详见日志。", notifyFailure: true);
			}
		}

		private void SortVanilla(GridInventory inv)
		{
			float num = SafeScore(inv);
			bool flag;
			if (NetworkServer.active)
			{
				flag = inv.AutoArrangeInventoryForBestCharmLevels(plugin.VanillaIterations.Value, plugin.AllowTabletRotation.Value);
			}
			else
			{
				inv.RequestAutoArrangeInventoryForBestCharmLevels(plugin.VanillaIterations.Value, plugin.AllowTabletRotation.Value);
				flag = true;
			}
			if (!flag)
			{
				Notify("背包无需整理（无护符或已是最优）。");
				return;
			}
			float num2 = SafeScore(inv);
			if (float.IsNaN(num) || float.IsNaN(num2))
			{
				Notify("整理完成");
				return;
			}
			Plugin.Log.LogInfo($"内置整理完成：加成评分 {num:F0} -> {num2:F0}");
			Notify("整理完成");
		}

        private List<Slot> ComputeBestLayout(List<Slot> original, SearchContext ctx, out double beforeScore, out double bestScore)
        {
            return RunHybridSearch(ctx, original, out beforeScore, out bestScore);
        }

		private bool StartSortEnhanced(GridInventory inv)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			List<Slot> original = CaptureState(inv);
			if (!VerifyInventorySnapshot(inv, original))
			{
				Notify("背包状态未就绪，本次未整理（请稍后再试）。");
				return false;
			}
			SearchContext ctx = BuildContext(inv, original);
			if (plugin.VerboseDiagnostics.Value)
			{
				LogItemIdentification(ctx);
			}
			float beforeGameScore = SafeScore(inv);
			pendingEnhanced = new PendingEnhancedSort
			{
				inv = inv,
                marksRevision = ManualPriorityManager.Revision,
                inventoryRevision = ManualPriorityManager.InventoryRevision,
				original = original,
				ctx = ctx,
				beforeGameScore = beforeGameScore,
				stopwatch = stopwatch,
				swapsPerFrame = Math.Max(1, plugin.ApplySwapsPerFrame.Value),
				rotationClicksPerFrame = Math.Max(1, plugin.ApplyRotationClicksPerFrame.Value),
				frameBudgetMs = Math.Max(0.25, plugin.ApplyFrameBudgetMs.Value),
				acknowledgementTimeoutMs = Math.Max(250, plugin.ApplyAckTimeoutMs.Value)
			};
            LogSortDiagnostics(pendingEnhanced, original, "开始");
			pendingSearch = Task.Run(delegate
			{
				double beforeScore;
				double bestScore;
				List<Slot> layout = ComputeBestLayout(original, ctx, out beforeScore, out bestScore);
				return new SearchOutcome
				{
					layout = layout,
					beforeScore = beforeScore,
					bestScore = bestScore
				};
			});

			return true;
		}

		private void BeginApplyEnhanced(PendingEnhancedSort state, SearchOutcome outcome)
		{
			if (state == null || outcome == null || state.inv == null)
			{
				throw new InvalidOperationException("后台整理状态丢失");
			}
			GridInventory inv = state.inv;
			List<Slot> original = state.original;
			List<Slot> a = CaptureState(inv);
			if (state.marksRevision != ManualPriorityManager.Revision || !VerifyInventorySnapshot(inv, original) || !LayoutsEquivalent(a, original))
			{
				RestartRequest(state);
				return;
			}
			if (!SameItems(original, outcome.layout))
            {
                CancelEnhancedSort(state, "整理失败：搜索布局物品集合不一致", true); return;
            }
            state.outcome = outcome;
			state.target = CloneSlots(outcome.layout);
			BeginMovePlan(state, original, state.target, rollingBack: false);
		}

		private void BeginMovePlan(PendingEnhancedSort state, List<Slot> current, List<Slot> target, bool rollingBack)
		{
			state.swaps.Clear();
			state.rotations.Clear();
			BuildClientOps(current, target, state.swaps, state.rotations);
			state.expected = CloneSlots(current);
			state.target = CloneSlots(target);
			state.swapIndex = 0;
			state.rotationIndex = 0;
			state.rotationRemaining = 0;
			state.rollingBack = rollingBack;
			state.applying = true;
			state.awaitingObservedState = false;
			state.acknowledgement.Reset();
			if (state.swaps.Count == 0 && state.rotations.Count == 0)
			{
				CompleteApplyEnhanced(state);
			}
		}

		private void AdvanceApplyEnhanced(PendingEnhancedSort state)
		{
			if (state.awaitingObservedState)
			{
				if (!LayoutsEquivalent(CaptureState(state.inv), state.expected))
				{
					if (!state.acknowledgement.IsRunning)
					{
						state.acknowledgement.Restart();
					}
					if (state.acknowledgement.ElapsedMilliseconds >= state.acknowledgementTimeoutMs)
					{
						string text = (state.rollingBack ? "回滚" : "应用");
						CancelEnhancedSort(state, "整理" + text + "等待服务器确认超时，已停止继续操作。当前背包未被强制改写。", notifyFailure: true);
					}
					return;
				}
				state.awaitingObservedState = false;
				state.acknowledgement.Reset();
			}
            if (state.marksRevision != ManualPriorityManager.Revision || IsDragging() || !LayoutsEquivalent(CaptureState(state.inv), state.expected))
            {
                RestartRequest(state); return;
            }
			Stopwatch stopwatch = Stopwatch.StartNew();
			int num = 0;
			int num2 = 0;
			bool flag = false;
			while (state.swapIndex < state.swaps.Count && num < state.swapsPerFrame && (num == 0 || stopwatch.Elapsed.TotalMilliseconds < state.frameBudgetMs))
			{
				(int, int) tuple = state.swaps[state.swapIndex++];
				ItemPosition itemPosition = state.inv.IdxToPos(tuple.Item1);
				ItemPosition itemPosition2 = state.inv.IdxToPos(tuple.Item2);
				state.inv.Swap(itemPosition.x, itemPosition.y, itemPosition2.x, itemPosition2.y);
				SwapSlots(state.expected, tuple.Item1, tuple.Item2);
				num++;
				flag = true;
			}
			while (state.swapIndex >= state.swaps.Count && state.rotationIndex < state.rotations.Count && num2 < state.rotationClicksPerFrame && ((num == 0 && num2 == 0) || stopwatch.Elapsed.TotalMilliseconds < state.frameBudgetMs))
			{
				(int, int) tuple2 = state.rotations[state.rotationIndex];
				if (state.rotationRemaining <= 0)
				{
					state.rotationRemaining = tuple2.Item2;
				}
				ItemPosition pos = state.inv.IdxToPos(tuple2.Item1);
				state.inv.DoClickAction(pos);
				Slot slot = state.expected[tuple2.Item1];
				slot.rotation = (slot.rotation + 1) % 4;
				state.expected[tuple2.Item1] = slot;
				state.rotationRemaining--;
				num2++;
				flag = true;
				if (state.rotationRemaining == 0)
				{
					state.rotationIndex++;
				}
			}
			if (flag)
			{
				state.awaitingObservedState = true;
			}
			else if (state.swapIndex >= state.swaps.Count && state.rotationIndex >= state.rotations.Count && state.rotationRemaining == 0)
			{
				CompleteApplyEnhanced(state);
			}
		}

		private void CompleteApplyEnhanced(PendingEnhancedSort state)
		{
			List<Slot> list = CaptureState(state.inv);
			if (!LayoutsEquivalent(list, state.target))
			{
				if (!state.rollingBack && VerifyInventorySnapshot(state.inv, state.original))
				{
					Plugin.Log.LogWarning("应用后的物品/旋转布局与搜索目标不一致，正在分帧恢复整理前布局。");
					BeginMovePlan(state, list, state.original, rollingBack: true);
				}
				else
				{
					Plugin.Log.LogError("整理回滚后布局仍与整理前快照不一致，请保留日志并检查背包。");
					CancelEnhancedSort(state, "整理未能安全完成，请检查背包。", notifyFailure: true);
				}
				return;
			}
            LogSortDiagnostics(state, list, "结束");
			float num = SafeScore(state.inv);
			double num2 = EvaluateLayout(state.ctx, list);
			if (plugin.VerboseDiagnostics.Value)
			{
				LogLayoutGrid(state.ctx, list, state.rollingBack ? "回滚" : "整理");
				LogLayoutAnalysis(state.ctx, list, state.rollingBack ? "回滚" : "整理");
			}
			state.stopwatch.Stop();
			Plugin.Log.LogInfo($"增强整理完成 #{state.diagnosticId}（后台+分帧总耗时 {state.stopwatch.ElapsedMilliseconds}ms）：" + $"离线评分 {state.outcome.beforeScore:F0} -> {num2:F0}（本次最佳普通分 {state.outcome.bestScore:F0}）；" + $"游戏评分 {state.beforeGameScore:F0} -> {num:F0}；" + $"搜索 {state.ctx.annealEvaluations} 候选/启动 {state.ctx.annealStartsCompleted}/{state.ctx.annealStarts}" + string.Format("；{0}；布局 {1} 件", state.rollingBack ? "已安全回滚" : "落地校验一致", state.ctx.items.Count));
			state.applying = false;
			pendingEnhanced = null;
			pendingSearch = null;
			busy = false;
			Notify("整理完成");
		}

		private void CancelEnhancedSort(PendingEnhancedSort state, string reason, bool notifyFailure)
		{
			if (state != null && state.stopwatch != null && state.stopwatch.IsRunning)
			{
				state.stopwatch.Stop();
			}
			Plugin.Log.LogWarning(reason);
			pendingSearch = null;
			pendingEnhanced = null;
			busy = false;
			Notify(notifyFailure ? reason : "整理期间背包发生变化，本次结果已取消");
		}

		private static void ApplyMoves(GridInventory inv, List<(int a, int b)> swaps, List<(int pos, int count)> rots)
		{
			foreach (var swap in swaps)
			{
				int item = swap.a;
				int item2 = swap.b;
				ItemPosition itemPosition = inv.IdxToPos(item);
				ItemPosition itemPosition2 = inv.IdxToPos(item2);
				inv.Swap(itemPosition.x, itemPosition.y, itemPosition2.x, itemPosition2.y);
			}
			foreach (var rot in rots)
			{
				int item3 = rot.pos;
				int item4 = rot.count;
				ItemPosition pos = inv.IdxToPos(item3);
				for (int i = 0; i < item4; i++)
				{
					inv.DoClickAction(pos);
				}
			}
		}

		private static void ApplyByMoves(GridInventory inv, List<Slot> original, List<Slot> finalLayout)
		{
			List<(int, int)> swaps = new List<(int, int)>();
			List<(int, int)> rots = new List<(int, int)>();
			BuildClientOps(original, finalLayout, swaps, rots);
			ApplyMoves(inv, swaps, rots);
		}

		private static void BuildClientOps(List<Slot> current, List<Slot> target, List<(int, int)> swaps, List<(int, int)> rots)
		{
			int num = Math.Min(current.Count, target.Count);
			List<Slot> list = CloneSlots(current);
			Dictionary<int, int> dictionary = new Dictionary<int, int>();
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i].hasItem)
				{
					dictionary[list[i].instanceID] = i;
				}
			}
			for (int j = 0; j < num; j++)
			{
				bool hasItem = list[j].hasItem;
				bool hasItem2 = target[j].hasItem;
				int num2 = (hasItem ? list[j].instanceID : (-1));
				int num3 = (hasItem2 ? target[j].instanceID : (-1));
				if (hasItem == hasItem2 && num2 == num3)
				{
					continue;
				}
				if (hasItem2)
				{
					if (dictionary.TryGetValue(num3, out var value) && value != j)
					{
						swaps.Add((j, value));
						SwapLogical(list, dictionary, j, value);
					}
					continue;
				}
				int num4 = -1;
				for (int k = 0; k < list.Count; k++)
				{
					if (k != j && !list[k].hasItem)
					{
						num4 = k;
						break;
					}
				}
				if (num4 >= 0)
				{
					swaps.Add((j, num4));
					SwapLogical(list, dictionary, j, num4);
				}
			}
			for (int l = 0; l < num; l++)
			{
				Slot slot = target[l];
				Slot slot2 = list[l];
				if (slot.hasItem && slot.tablet is not null && slot2.hasItem && slot2.tablet is not null && slot.instanceID == slot2.instanceID && slot.rotation != slot2.rotation)
				{
					int num5 = (slot.rotation - slot2.rotation + 4) % 4;
					if (num5 > 0)
					{
						rots.Add((l, num5));
					}
				}
			}
		}

		private static void SwapLogical(List<Slot> logical, Dictionary<int, int> posOf, int a, int b)
		{
			Slot value = logical[a];
			logical[a] = logical[b];
			logical[b] = value;
			if (logical[a].hasItem)
			{
				posOf[logical[a].instanceID] = a;
			}
			if (logical[b].hasItem)
			{
				posOf[logical[b].instanceID] = b;
			}
		}





		private void Mutate(SearchContext ctx, List<Slot> slots, System.Random rng)
		{
			int[] itemIndexScratch = ctx.itemIndexScratch;
			int[] emptyIndexScratch = ctx.emptyIndexScratch;
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < slots.Count; i++)
			{
				if (slots[i].hasItem)
				{
					itemIndexScratch[num++] = i;
				}
				else
				{
					emptyIndexScratch[num2++] = i;
				}
			}
			if (num == 0 || (ctx.manualPriorityCount > 0 && rng.Next(100) < 16 && TryManualPriorityMove(ctx, slots, rng)))
			{
				return;
			}
			int num3 = rng.Next(100);
			if ((num3 < 12 && plugin.CriteriaMoveChance.Value > 0f && TryCriteriaMove(ctx, slots, rng)) || (num3 < 22 && plugin.PlanetBonus.Value > 0f && TryPlanetMove(ctx, slots, rng)) || (num3 < 32 && plugin.CompassBonus.Value > 0f && TryCompassMove(ctx, slots, rng)) || (num3 < 37 && ctx.burdens.Count > 0 && TryBurdenDump(ctx, slots, rng)) || (num3 < 45 && ctx.whitePaperTargets.Count > 0 && plugin.WhitePaperComboBonus.Value > 0f && TryWhitePaperMove(ctx, slots, rng)) || (num3 < 52 && plugin.HourglassBonus.Value > 0f && TryHourglassMove(ctx, slots, rng)) || (num3 < 58 && plugin.RayShardBonus.Value > 0f && TryRayShardMove(ctx, slots, rng)))
			{
				return;
			}
			if (num3 < 75 && num2 > 0)
			{
				int a = itemIndexScratch[rng.Next(num)];
				int b = emptyIndexScratch[rng.Next(num2)];
				SwapSlots(slots, a, b);
				return;
			}
			if (num3 < 92)
			{
				if (num >= 2)
				{
					int num4 = itemIndexScratch[rng.Next(num)];
					int num5 = itemIndexScratch[rng.Next(num)];
					if (num4 != num5)
					{
						SwapSlots(slots, num4, num5);
					}
				}
				return;
			}
			for (int j = 0; j < 8; j++)
			{
				int index = itemIndexScratch[rng.Next(num)];
				Slot slot = slots[index];
				if (slot.tablet is not null && ctx.itemByInstance.TryGetValue(slot.instanceID, out var value) && value != null && value.tabletRotatable)
				{
					slot.rotation = (slot.rotation + 1 + rng.Next(3)) % 4;
					return;
				}
			}
			if (num >= 2)
			{
				int num6 = itemIndexScratch[rng.Next(num)];
				int num7 = itemIndexScratch[rng.Next(num)];
				if (num6 != num7)
				{
					SwapSlots(slots, num6, num7);
				}
			}
		}

		private bool TryManualPriorityMove(SearchContext ctx, List<Slot> slots, System.Random rng)
		{
			List<int> moveScratchA = ctx.moveScratchA;
			moveScratchA.Clear();
			int num = 0;
			for (int i = 0; i < slots.Count; i++)
			{
				Slot slot = slots[i];
				if (slot != null && slot.hasItem && !(slot.charm is null) && ctx.itemByInstance.TryGetValue(slot.instanceID, out var value) && value != null && value.manualPriorityRank > 0 && value.manualPriorityRank <= 2)
				{
					moveScratchA.Add(i);
					num += Math.Max(1, 64 / (value.manualPriorityRank * value.manualPriorityRank));
				}
			}
			if (moveScratchA.Count == 0)
			{
				return false;
			}
			int num2 = rng.Next(Math.Max(1, num));
			int num3 = moveScratchA[0];
			ItemInfo itemInfo = null;
			foreach (int item in moveScratchA)
			{
				ItemInfo itemInfo2 = ctx.itemByInstance[slots[item].instanceID];
				num2 -= Math.Max(1, 64 / (itemInfo2.manualPriorityRank * itemInfo2.manualPriorityRank));
				if (num2 < 0)
				{
					num3 = item;
					itemInfo = itemInfo2;
					break;
				}
			}
			if (itemInfo == null)
			{
				itemInfo = ctx.itemByInstance[slots[num3].instanceID];
			}
			int num4 = (ctx.disabled[num3] ? (-536870912) : ((ctx.cellLevel[num3] + itemInfo.enchant) * ctx.mysticFactor[num3]));
			int num5 = -1;
			int num6 = num4;
			for (int j = 0; j < ctx.storage; j++)
			{
				if (j == num3 || !IsAllowedLockedRow(itemInfo, j / ctx.width) || ctx.disabled[j])
				{
					continue;
				}
				Slot slot2 = slots[j];
				if ((slot2 == null || !slot2.hasItem || !(slot2.tablet is not null)) && (slot2 == null || !slot2.hasItem || !ctx.itemByInstance.TryGetValue(slot2.instanceID, out var value2) || value2 == null || ((value2.manualPriorityRank <= 0 || value2.manualPriorityRank > itemInfo.manualPriorityRank) && IsAllowedLockedRow(value2, num3 / ctx.width))))
				{
					int num7 = (ctx.cellLevel[j] + itemInfo.enchant) * ctx.mysticFactor[j];
					if (num7 > num6)
					{
						num6 = num7;
						num5 = j;
					}
				}
			}
			if (num5 < 0)
			{
				return false;
			}
			SwapSlots(slots, num3, num5);
			return true;
		}

		private bool TryCriteriaMove(SearchContext ctx, List<Slot> slots, System.Random rng)
		{
			List<int> moveScratchA = ctx.moveScratchA;
			moveScratchA.Clear();
			for (int i = 0; i < slots.Count; i++)
			{
				if (slots[i].hasItem && ctx.itemByInstance.TryGetValue(slots[i].instanceID, out var value) && value != null && value.isCharm && KindPriority(value.kind) >= 2)
				{
					moveScratchA.Add(i);
				}
			}
			if (moveScratchA.Count == 0)
			{
				return false;
			}
			int num = moveScratchA[rng.Next(moveScratchA.Count)];
			ItemInfo value2;
			bool flag = ctx.itemByInstance.TryGetValue(slots[num].instanceID, out value2) && value2 != null && value2.preferIgnoreCells;
			CharmPositionKind kind = value2?.kind ?? CharmPositionKind.None;
			List<int> moveScratchB = ctx.moveScratchB;
			moveScratchB.Clear();
			for (int j = 0; j < ctx.storage; j++)
			{
				if (j != num)
				{
					int x = j % ctx.width;
					int y = j / ctx.width;
					bool flag2 = ctx.ignore[j];
					bool flag3 = IsSatisfyingCell(kind, x, y, j, ctx.storage, ctx.width);
					bool num2 = flag2 || flag3;
					ItemInfo value3;
					bool flag4 = slots[j].hasItem && ctx.itemByInstance.TryGetValue(slots[j].instanceID, out value3) && value3 != null && value3.isCharm && KindPriority(value3.kind) >= 2;
					if (num2 && !flag4 && (!flag || flag2) && (flag || flag3))
					{
						moveScratchB.Add(j);
					}
				}
			}
			if (moveScratchB.Count == 0 && flag)
			{
				for (int k = 0; k < ctx.storage; k++)
				{
					if (k != num)
					{
						int x2 = k % ctx.width;
						int y2 = k / ctx.width;
						ItemInfo value4;
						bool flag5 = slots[k].hasItem && ctx.itemByInstance.TryGetValue(slots[k].instanceID, out value4) && value4 != null && value4.isCharm && KindPriority(value4.kind) >= 2;
						if (IsSatisfyingCell(kind, x2, y2, k, ctx.storage, ctx.width) && !flag5)
						{
							moveScratchB.Add(k);
						}
					}
				}
			}
			if (moveScratchB.Count == 0)
			{
				return false;
			}
			SwapSlots(slots, num, moveScratchB[rng.Next(moveScratchB.Count)]);
			return true;
		}

		private bool TryPlanetMove(SearchContext ctx, List<Slot> slots, System.Random rng)
		{
			int num = -1;
			for (int i = 0; i < ctx.storage; i++)
			{
				Slot slot = slots[i];
				if (slot != null && slot.hasItem && ctx.itemByInstance.TryGetValue(slot.instanceID, out var value) && value != null && value.isPlanetModule)
				{
					num = i;
					break;
				}
			}
			if (num < 0)
			{
				return false;
			}
			int num2 = num % ctx.width;
			int num3 = num / ctx.width;
			List<int> moveScratchA = ctx.moveScratchA;
			moveScratchA.Clear();
			for (int j = 0; j < ctx.storage; j++)
			{
				Slot slot2 = slots[j];
				if (j != num && slot2 != null && slot2.hasItem && ctx.itemByInstance.TryGetValue(slot2.instanceID, out var value2) && value2 != null && value2.isPlanetCategory && !value2.excludeFromPlanetCluster)
				{
					int num4 = j % ctx.width;
					int num5 = j / ctx.width;
					if (Math.Abs(num4 - num2) > 1 || Math.Abs(num5 - num3) > 1)
					{
						moveScratchA.Add(j);
					}
				}
			}
			if (moveScratchA.Count == 0)
			{
				return false;
			}
			List<int> moveScratchB = ctx.moveScratchB;
			moveScratchB.Clear();
			for (int k = 0; k < 8; k++)
			{
				int num6 = num2 + Neighbor8[k].x;
				int num7 = num3 + Neighbor8[k].y;
				if (InBounds(num6, num7, ctx.width, ctx.height))
				{
					int num8 = num7 * ctx.width + num6;
					if (num8 >= 0 && num8 < ctx.storage && (slots[num8] == null || !slots[num8].hasItem))
					{
						moveScratchB.Add(num8);
					}
				}
			}
			if (moveScratchB.Count == 0)
			{
				return false;
			}
			SwapSlots(slots, moveScratchA[rng.Next(moveScratchA.Count)], moveScratchB[rng.Next(moveScratchB.Count)]);
			return true;
		}

		private bool TryCompassMove(SearchContext ctx, List<Slot> slots, System.Random rng)
		{
			if (ctx.compassChains.Count > 0 && rng.Next(100) < 70)
			{
				CompassChain compassChain = ctx.compassChains[rng.Next(ctx.compassChains.Count)];
				Dictionary<int, int> compassPositionScratch = ctx.compassPositionScratch;
				FillItemPositionMap(slots, compassPositionScratch);
				if (compassPositionScratch.TryGetValue(compassChain.instanceIDs[0], out var value))
				{
					HashSet<int> instanceSetScratch = ctx.instanceSetScratch;
					instanceSetScratch.Clear();
					for (int i = 0; i < compassChain.instanceIDs.Count; i++)
					{
						instanceSetScratch.Add(compassChain.instanceIDs[i]);
					}
					List<int> moveScratchA = ctx.moveScratchA;
					moveScratchA.Clear();
					for (int j = 0; j < ctx.storage; j++)
					{
						if (j != value && CanPlaceCompassChain(ctx, compassChain, j, null))
						{
							Slot slot = slots[j];
							if (slot == null || !slot.hasItem || !instanceSetScratch.Contains(slot.instanceID))
							{
								moveScratchA.Add(j);
							}
						}
					}
					if (moveScratchA.Count > 0)
					{
						SwapSlots(slots, value, moveScratchA[rng.Next(moveScratchA.Count)]);
						return true;
					}
				}
			}
			List<int> moveScratchA2 = ctx.moveScratchA;
			moveScratchA2.Clear();
			for (int k = 0; k < ctx.storage; k++)
			{
				Slot slot2 = slots[k];
				if (slot2 != null && slot2.hasItem && ctx.itemByInstance.TryGetValue(slot2.instanceID, out var value2) && value2 != null && value2.isCompass && !ctx.compassChainInstances.Contains(slot2.instanceID) && DirectionBindingManager.GetDirection(slot2.instanceID) == ManualBindDirection.None)
				{
					moveScratchA2.Add(k);
				}
			}
			if (moveScratchA2.Count == 0)
			{
				return false;
			}
			HashSet<int> instanceSetScratch2 = ctx.instanceSetScratch;
			instanceSetScratch2.Clear();
			for (int l = 0; l < moveScratchA2.Count; l++)
			{
				instanceSetScratch2.Add(moveScratchA2[l]);
			}
			List<int> moveScratchB = ctx.moveScratchB;
			List<int> moveScratchC = ctx.moveScratchC;
			moveScratchB.Clear();
			moveScratchC.Clear();
			for (int m = 0; m < ctx.storage; m++)
			{
				int num = m % ctx.width;
				int num2 = m / ctx.width;
				int num3 = (num2 - 1) * ctx.width + num;
				if (num2 > 0 && num3 >= 0 && num3 < ctx.storage && slots[num3] != null && slots[num3].hasItem && slots[num3].charm is not null && ctx.itemByInstance.TryGetValue(slots[num3].instanceID, out var value3) && value3 != null && (value3.isCompass || value3.isAttackable) && (slots[m] == null || !slots[m].hasItem || !(slots[m].charm is Charm_UpCharmDamage)))
				{
					moveScratchB.Add(m);
				}
				int num4 = (num2 + 1) * ctx.width + num;
				if (num2 < ctx.height - 1 && num4 < ctx.storage && instanceSetScratch2.Contains(num4) && (slots[m] == null || !slots[m].hasItem))
				{
					moveScratchC.Add(m);
				}
			}
			if (moveScratchB.Count > 0 && rng.Next(2) == 0)
			{
				int a = moveScratchA2[rng.Next(moveScratchA2.Count)];
				SwapSlots(slots, a, moveScratchB[rng.Next(moveScratchB.Count)]);
				return true;
			}
			if (moveScratchC.Count > 0)
			{
				List<int> moveScratchD = ctx.moveScratchD;
				moveScratchD.Clear();
				for (int n = 0; n < ctx.storage; n++)
				{
					Slot slot3 = slots[n];
					if (slot3 != null && slot3.hasItem && ctx.itemByInstance.TryGetValue(slot3.instanceID, out var value4) && value4 != null && value4.isAttackable && !value4.isCompass && !ctx.compassChainInstances.Contains(slot3.instanceID))
					{
						moveScratchD.Add(n);
					}
				}
				if (moveScratchD.Count > 0)
				{
					SwapSlots(slots, moveScratchD[rng.Next(moveScratchD.Count)], moveScratchC[rng.Next(moveScratchC.Count)]);
					return true;
				}
			}
			return false;
		}

		private static bool CanUseWhitePaperTriple(SearchContext ctx, List<Slot> slots, int rootCell, int paperInstanceID, int leftInstanceID, int rightInstanceID)
		{
			int num = rootCell / ctx.width;
			for (int i = 0; i < 3; i++)
			{
				int num2 = rootCell + i;
				if (num2 < 0 || num2 >= ctx.storage || num2 / ctx.width != num)
				{
					return false;
				}
				Slot slot = slots[num2];
				if (slot == null || !slot.hasItem)
				{
					continue;
				}
				if (ctx.compassChainInstances.Contains(slot.instanceID))
				{
					return false;
				}
				if (ctx.itemByInstance.TryGetValue(slot.instanceID, out var value) && value != null)
				{
					bool flag = slot.instanceID == paperInstanceID || slot.instanceID == leftInstanceID || slot.instanceID == rightInstanceID;
					if (value.isWhitePaper && slot.instanceID != paperInstanceID)
					{
						return false;
					}
					if (value.isRowLocked && !flag)
					{
						return false;
					}
				}
			}
			if (ctx.itemByInstance.TryGetValue(leftInstanceID, out var value2) && value2 != null && !IsAllowedLockedRow(value2, num))
			{
				return false;
			}
			if (ctx.itemByInstance.TryGetValue(rightInstanceID, out var value3) && value3 != null && !IsAllowedLockedRow(value3, num))
			{
				return false;
			}
			if (ctx.itemByInstance.TryGetValue(paperInstanceID, out var value4) && value4 != null && !IsAllowedLockedRow(value4, num))
			{
				return false;
			}
			return true;
		}

		private bool TryWhitePaperMove(SearchContext ctx, List<Slot> slots, System.Random rng)
		{
			if (ctx.whitePaperTargets.Count == 0 || plugin.WhitePaperComboBonus.Value <= 0f)
			{
				return false;
			}
			RefreshWhitePaperAssignments(ctx, slots);
			List<int> moveScratchA = ctx.moveScratchA;
			moveScratchA.Clear();
			for (int i = 0; i < ctx.storage; i++)
			{
				Slot slot = slots[i];
				if (slot != null && slot.hasItem && ctx.itemByInstance.TryGetValue(slot.instanceID, out var value) && value != null && value.isWhitePaper && !ctx.bothLeftByInstance.ContainsKey(slot.instanceID) && !ctx.bothRightByInstance.ContainsKey(slot.instanceID))
				{
					moveScratchA.Add(i);
				}
			}
			if (moveScratchA.Count == 0)
			{
				return false;
			}
			int num = rng.Next(moveScratchA.Count);
			for (int j = 0; j < moveScratchA.Count; j++)
			{
				int num2 = moveScratchA[(num + j) % moveScratchA.Count];
				int instanceID = slots[num2].instanceID;
				for (int k = 0; k < ctx.whitePaperTargets.Count; k++)
				{
					WhitePaperComboTarget whitePaperComboTarget = ctx.whitePaperTargets[k];
					bool flag = WhitePaperMatchesTarget(ctx, slots, num2, k);
					int num3 = ctx.whitePaperAssignmentScratch[k] - (flag ? 1 : 0);
					if (whitePaperComboTarget.baseCount + num3 >= whitePaperComboTarget.cap)
					{
						continue;
					}
					List<int> moveScratchB = ctx.moveScratchB;
					moveScratchB.Clear();
					for (int l = 0; l < ctx.storage; l++)
					{
						Slot slot2 = slots[l];
						if (slot2 != null && slot2.hasItem && slot2.instanceID != instanceID && !ctx.compassChainInstances.Contains(slot2.instanceID) && ctx.itemByInstance.TryGetValue(slot2.instanceID, out var value2) && value2 != null && !value2.isWhitePaper && value2.comboCategories.Contains(whitePaperComboTarget.category))
						{
							moveScratchB.Add(l);
						}
					}
					if (moveScratchB.Count < 2)
					{
						continue;
					}
					if (flag)
					{
						break;
					}
					int num4 = -1;
					int key = -1;
					int key2 = -1;
					float num5 = float.MinValue;
					int num6 = Math.Min(16, Math.Max(1, moveScratchB.Count * 2));
					for (int m = 0; m < num6; m++)
					{
						int num7 = rng.Next(moveScratchB.Count);
						int num8 = rng.Next(moveScratchB.Count - 1);
						if (num8 >= num7)
						{
							num8++;
						}
						int index = moveScratchB[num7];
						int index2 = moveScratchB[num8];
						int num9 = slots[index].instanceID;
						int num10 = slots[index2].instanceID;
						if (rng.Next(2) == 0)
						{
							int num11 = num9;
							num9 = num10;
							num10 = num11;
						}
						for (int n = 0; n < ctx.storage; n++)
						{
							if (n % ctx.width <= ctx.width - 3 && n + 2 < ctx.storage && CanUseWhitePaperTriple(ctx, slots, n, instanceID, num9, num10))
							{
								float num12 = (float)(ctx.cellLevel[n] + ctx.cellLevel[n + 2]) + (float)ctx.cellLevel[n + 1] * 0.25f;
								if (slots[n].hasItem && slots[n].instanceID == num9)
								{
									num12 += 10f;
								}
								if (slots[n + 1].hasItem && slots[n + 1].instanceID == instanceID)
								{
									num12 += 10f;
								}
								if (slots[n + 2].hasItem && slots[n + 2].instanceID == num10)
								{
									num12 += 10f;
								}
								if (num12 > num5)
								{
									num5 = num12;
									num4 = n;
									key = num9;
									key2 = num10;
								}
							}
						}
					}
					if (num4 >= 0)
					{
						Dictionary<int, int> compassPositionScratch = ctx.compassPositionScratch;
						FillItemPositionMap(slots, compassPositionScratch);
						SwapSlotsAndTrack(slots, compassPositionScratch, compassPositionScratch[key], num4);
						SwapSlotsAndTrack(slots, compassPositionScratch, compassPositionScratch[instanceID], num4 + 1);
						SwapSlotsAndTrack(slots, compassPositionScratch, compassPositionScratch[key2], num4 + 2);
						return true;
					}
				}
			}
			return false;
		}

		private bool TryHourglassMove(SearchContext ctx, List<Slot> slots, System.Random rng)
		{
			List<int> moveScratchA = ctx.moveScratchA;
			List<int> moveScratchB = ctx.moveScratchB;
			moveScratchA.Clear();
			moveScratchB.Clear();
			for (int i = 0; i < ctx.storage; i++)
			{
				Slot slot = slots[i];
				if (slot != null && slot.hasItem && ctx.itemByInstance.TryGetValue(slot.instanceID, out var value) && value != null)
				{
					if (value.isHourglass && !ctx.hourglassTargetByInstance.ContainsKey(slot.instanceID) && DirectionBindingManager.GetDirection(slot.instanceID) == ManualBindDirection.None)
					{
						moveScratchA.Add(i);
					}
					else if (value.isMagicBook)
					{
						moveScratchB.Add(i);
					}
				}
			}
			if (moveScratchA.Count == 0 || moveScratchB.Count == 0)
			{
				return false;
			}
			moveScratchB.Sort(delegate(int a, int b)
			{
				ItemInfo value3;
				float value2 = ((ctx.itemByInstance.TryGetValue(slots[a].instanceID, out value3) && value3 != null) ? value3.magicCd : 0f);
				ItemInfo value4;
				return ((ctx.itemByInstance.TryGetValue(slots[b].instanceID, out value4) && value4 != null) ? value4.magicCd : 0f).CompareTo(value2);
			});
			int num = moveScratchB[rng.Next(Math.Min(2, moveScratchB.Count))];
			int num2 = num % ctx.width;
			int num3 = num / ctx.width;
			if (num2 > 0)
			{
				int num4 = num3 * ctx.width + (num2 - 1);
				if (num4 >= 0 && num4 < ctx.storage && (slots[num4] == null || !slots[num4].hasItem || !(slots[num4].charm is Charm_RightSpellCooldownHelper)))
				{
					int num5 = moveScratchA[rng.Next(moveScratchA.Count)];
					if (num4 != num5)
					{
						SwapSlots(slots, num5, num4);
						return true;
					}
				}
			}
			int num6 = moveScratchA[rng.Next(moveScratchA.Count)];
			int num7 = num6 % ctx.width;
			int num8 = num6 / ctx.width;
			if (num7 + 1 < ctx.width)
			{
				int num9 = num8 * ctx.width + (num7 + 1);
				if (num9 >= 0 && num9 < ctx.storage && (slots[num9] == null || !slots[num9].hasItem || !(slots[num9].charm is Charm_Magic)) && num9 != num)
				{
					SwapSlots(slots, num, num9);
					return true;
				}
			}
			return false;
		}

		private bool TryRayShardMove(SearchContext ctx, List<Slot> slots, System.Random rng)
		{
			List<int> moveScratchA = ctx.moveScratchA;
			List<int> moveScratchB = ctx.moveScratchB;
			moveScratchA.Clear();
			moveScratchB.Clear();
			for (int i = 0; i < ctx.storage; i++)
			{
				Slot slot = slots[i];
				if (slot != null && slot.hasItem && ctx.itemByInstance.TryGetValue(slot.instanceID, out var value) && value != null)
				{
					if (value.isRayShard && !ctx.rayShardTargetByInstance.ContainsKey(slot.instanceID) && DirectionBindingManager.GetDirection(slot.instanceID) == ManualBindDirection.None)
					{
						moveScratchA.Add(i);
					}
					else if (value.isMagicBook)
					{
						moveScratchB.Add(i);
					}
				}
			}
			if (moveScratchA.Count == 0 || moveScratchB.Count == 0)
			{
				return false;
			}
			moveScratchB.Sort(delegate(int a, int b)
			{
				ItemInfo value3;
				int value2 = ((ctx.itemByInstance.TryGetValue(slots[a].instanceID, out value3) && value3 != null) ? value3.magicMpCost : 0);
				ItemInfo value4;
				return ((ctx.itemByInstance.TryGetValue(slots[b].instanceID, out value4) && value4 != null) ? value4.magicMpCost : 0).CompareTo(value2);
			});
			int num = moveScratchB[rng.Next(Math.Min(2, moveScratchB.Count))];
			int num2 = num % ctx.width;
			int num3 = num / ctx.width;
			if (num2 + 1 < ctx.width)
			{
				int num4 = num3 * ctx.width + (num2 + 1);
				if (num4 >= 0 && num4 < ctx.storage && (slots[num4] == null || !slots[num4].hasItem || !(slots[num4].charm is Charm_Magic)))
				{
					int num5 = moveScratchA[rng.Next(moveScratchA.Count)];
					if (num4 != num5)
					{
						SwapSlots(slots, num5, num4);
						return true;
					}
				}
			}
			int num6 = moveScratchA[rng.Next(moveScratchA.Count)];
			int num7 = num6 % ctx.width;
			int num8 = num6 / ctx.width;
			if (num7 - 1 >= 0)
			{
				int num9 = num8 * ctx.width + (num7 - 1);
				if (num9 >= 0 && num9 < ctx.storage && (slots[num9] == null || !slots[num9].hasItem || !(slots[num9].charm is Charm_Magic)) && num9 != num)
				{
					SwapSlots(slots, num, num9);
					return true;
				}
			}
			return false;
		}

		private bool TryBurdenDump(SearchContext ctx, List<Slot> slots, System.Random rng)
		{
			int num = -1;
			int num2 = int.MaxValue;
			for (int i = 0; i < ctx.storage; i++)
			{
				int num3 = ctx.cellLevel[i];
				if (num3 < num2)
				{
					num2 = num3;
					num = i;
				}
			}
			if (num < 0)
			{
				return false;
			}
			List<int> moveScratchA = ctx.moveScratchA;
			moveScratchA.Clear();
			for (int j = 0; j < ctx.storage; j++)
			{
				Slot slot = slots[j];
				if (slot != null && slot.hasItem && ctx.itemByInstance.TryGetValue(slot.instanceID, out var value) && value != null && value.isBurden)
				{
					moveScratchA.Add(j);
				}
			}
			if (moveScratchA.Count == 0)
			{
				return false;
			}
			int num4 = moveScratchA[rng.Next(moveScratchA.Count)];
			if (num4 != num)
			{
				SwapSlots(slots, num4, num);
				return true;
			}
			return false;
		}

		private static void SwapSlots(List<Slot> slots, int a, int b)
		{
			Slot value = slots[a];
			slots[a] = slots[b];
			slots[b] = value;
		}

		private static void ScrambleForSearch(SearchContext ctx, List<Slot> slots, System.Random rng)
		{
			List<int> list = new List<int>();
			for (int i = 0; i < slots.Count; i++)
			{
				if (slots[i].hasItem)
				{
					list.Add(i);
				}
			}
			for (int num = list.Count - 1; num > 0; num--)
			{
				int index = rng.Next(num + 1);
				if (list[num] != list[index])
				{
					SwapSlots(slots, list[num], list[index]);
				}
			}
			foreach (int item in list)
			{
				Slot slot = slots[item];
				if (slot.tablet is not null && rng.Next(2) == 0 && ctx.itemByInstance.TryGetValue(slot.instanceID, out var value) && value != null && value.tabletRotatable)
				{
					slot.rotation = rng.Next(4);
				}
			}
		}

		private static long CreateSearchDeadline(int budgetMs)
		{
			if (budgetMs <= 0)
			{
				return 0L;
			}
			long timestamp = Stopwatch.GetTimestamp();
			long val = (long)((double)budgetMs * (double)Stopwatch.Frequency / 1000.0);
			return timestamp + Math.Max(1L, val);
		}

		private static bool SearchDeadlineReached(long deadlineTicks)
		{
			if (deadlineTicks > 0)
			{
				return Stopwatch.GetTimestamp() >= deadlineTicks;
			}
			return false;
		}

		private static void CopySlots(List<Slot> src, List<Slot> dst)
		{
			if (src == null || dst == null || src.Count != dst.Count)
			{
				throw new ArgumentException("布局缓冲区大小不一致");
			}
			for (int i = 0; i < src.Count; i++)
			{
				Slot slot = src[i];
				Slot slot2 = dst[i];
				slot2.hasItem = slot.hasItem;
				slot2.instanceID = slot.instanceID;
				slot2.entityID = slot.entityID;
				slot2.quantity = slot.quantity;
				slot2.charm = slot.charm;
				slot2.tablet = slot.tablet;
				slot2.rotation = slot.rotation;
			}
		}

		private static bool LayoutsEquivalent(List<Slot> a, List<Slot> b)
		{
			if (a == null || b == null || a.Count != b.Count)
			{
				return false;
			}
			for (int i = 0; i < a.Count; i++)
			{
				Slot slot = a[i];
				Slot slot2 = b[i];
				if (slot == null || slot2 == null || slot.hasItem != slot2.hasItem)
				{
					return false;
				}
				if (slot.hasItem)
				{
					if (slot.instanceID != slot2.instanceID || slot.entityID != slot2.entityID || slot.quantity != slot2.quantity)
					{
						return false;
					}
					if ((slot.tablet is not null || slot2.tablet is not null) && (slot.tablet is null || slot2.tablet is null || ((slot.rotation - slot2.rotation) & 3) != 0))
					{
						return false;
					}
				}
			}
			return true;
		}

		private static List<Slot> CloneSlots(List<Slot> src)
		{
			List<Slot> list = new List<Slot>(src.Count);
			for (int i = 0; i < src.Count; i++)
			{
				list.Add(src[i].Clone());
			}
			return list;
		}

		private static List<Slot> CaptureState(GridInventory inv)
		{
			List<Slot> list = new List<Slot>(inv.CurrentInventoryStorage);
			for (int i = 0; i < inv.CurrentInventoryStorage; i++)
			{
				ItemPosition key = inv.IdxToPos(i);
				if (inv.inventoryMatrix.TryGetValue(key, out var value) && value != null)
				{
					list.Add(new Slot
					{
						hasItem = true,
						instanceID = value.InstanceID,
						entityID = value.EntityID,
						quantity = value.Quantity,
						charm = value.Charm,
						tablet = value.StoneTablet,
						rotation = ((value.StoneTablet != null) ? value.StoneTablet.rotation : 0)
					});
				}
				else
				{
					list.Add(Slot.Empty());
				}
			}
			return list;
		}

		private static float SafeScore(GridInventory inv)
		{
			if (!NetworkServer.active)
			{
				return float.NaN;
			}
			return inv.EvaluateCurrentAutoArrangeScore();
		}



		private static void Scramble(List<Slot> slots, System.Random rng)
		{
			List<int> list = new List<int>();
			for (int i = 0; i < slots.Count; i++)
			{
				if (slots[i].hasItem)
				{
					list.Add(i);
				}
			}
			for (int num = list.Count - 1; num > 0; num--)
			{
				int index = rng.Next(num + 1);
				if (list[num] != list[index])
				{
					SwapSlots(slots, list[num], list[index]);
				}
			}
			foreach (int item in list)
			{
				Slot slot = slots[item];
				if (slot.tablet is not null && rng.Next(2) == 0 && DungeonManager.IsTabletRotatable(slot.instanceID, slot.tablet.isRotatable))
				{
					slot.rotation = rng.Next(4);
				}
			}
		}

		private void Notify(string msg)
        {
            if (msg != "整理完成" && msg != "整理中…" && msg != "整理失败")
            {
                Plugin.Log.LogInfo(msg);
                if (msg.Contains("失败") || msg.Contains("超时") || msg.Contains("未能安全")) msg = "整理失败";
                else return;
            }
			Plugin.Log.LogInfo(msg);
			if (!plugin.ShowNotifications.Value)
			{
				return;
			}
			try
			{
				if (UIManager.Instance != null)
				{
					UIManager.Instance.GetElement<UI_SystemMessage>()?.Open(msg, 2.5f);
				}
			}
			catch (Exception ex)
			{
				Plugin.Log.LogDebug("游戏内提示显示失败: " + ex.Message);
			}
		}
	}

	internal sealed class ManualPriorityBadge : MonoBehaviour
	{
		private UI_NewInventoryIcon owner;

		private GameObject badgeRoot;

		private TextMeshProUGUI label;
        private MarkArrow arrow;

		internal static ManualPriorityBadge GetOrCreate(UI_NewInventoryIcon icon)
		{
			ManualPriorityBadge manualPriorityBadge = icon.GetComponent<ManualPriorityBadge>();
			if (manualPriorityBadge == null)
			{
				manualPriorityBadge = icon.gameObject.AddComponent<ManualPriorityBadge>();
			}
			manualPriorityBadge.owner = icon;
			manualPriorityBadge.EnsureVisual();
			return manualPriorityBadge;
		}

		private void EnsureVisual()
		{
			if (!(badgeRoot != null) && !(owner == null))
			{
				badgeRoot = new GameObject("ManualPriorityBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
				badgeRoot.transform.SetParent(owner.transform, worldPositionStays: false);
				badgeRoot.transform.SetAsLastSibling();
				RectTransform obj = (RectTransform)badgeRoot.transform;
				obj.anchorMin = new Vector2(0.03f, 0.02f);
				obj.anchorMax = new Vector2(0.48f, 0.25f);
				obj.pivot = new Vector2(0f, 0f);
				obj.offsetMin = Vector2.zero;
				obj.offsetMax = Vector2.zero;
				label = badgeRoot.GetComponent<TextMeshProUGUI>();
				if (owner.quantityText != null)
				{
					label.font = owner.quantityText.font;
				}
				var arrowObject = new GameObject("Arrow", typeof(RectTransform), typeof(UnityEngine.CanvasRenderer), typeof(MarkArrow));
                arrowObject.transform.SetParent(badgeRoot.transform, false);
                var arrowRect = (RectTransform)arrowObject.transform;
                arrowRect.anchorMin = Vector2.zero; arrowRect.anchorMax = Vector2.one; arrowRect.offsetMin = arrowRect.offsetMax = Vector2.zero;
                arrow = arrowObject.GetComponent<MarkArrow>(); arrow.raycastTarget = false;
                label.text = "";
				label.color = new Color(1f, 0.82f, 0.2f, 1f);
				label.fontStyle = FontStyles.Bold;
				label.alignment = TextAlignmentOptions.Center;
				label.enableAutoSizing = true;
				label.fontSizeMin = 4f;
				label.fontSizeMax = ((owner.quantityText != null) ? Math.Max(7f, owner.quantityText.fontSize * 0.58f) : 11f);
				label.raycastTarget = false;
				badgeRoot.SetActive(value: false);
			}
		}

		internal bool Refresh()
		{
			EnsureVisual();
			if (badgeRoot == null || owner == null)
			{
				return false;
			}
			Plugin instance = Plugin.Instance;
			NewItemOwnInstance item = owner.Item;
			int num = (item != null && owner.Inventory != null && owner.Inventory.isLocalPlayer) ? (int)ManualPriorityManager.Observe(item) : 0;
			bool flag = instance != null && instance.ManualPriorityEnabled.Value && instance.ShowManualPriorityBadge.Value && item != null && item.Charm != null && num > 0;
			badgeRoot.SetActive(flag);
			if (flag)
			{
				label.text = "";
                arrow.gameObject.SetActive(true);
                arrow.Down = num == 3 || num == 4;
                arrow.Double = num == 1 || num == 4;
                arrow.color = arrow.Down ? new Color(0.5f, 0.75f, 1f, 1f) : new Color(1f, 0.82f, 0.2f, 1f);
                arrow.SetVerticesDirty();
				badgeRoot.transform.SetAsLastSibling();
			}
			return flag;
		}
	}
	[HarmonyPatch(typeof(UI_NewInventoryIcon), "SetItemReference")]
	internal static class ManualPrioritySetItemPatch
	{
		private static void Postfix(UI_NewInventoryIcon __instance)
		{
			ManualPriorityBadge.GetOrCreate(__instance).Refresh();

		}
	}
	[HarmonyPatch(typeof(UI_NewInventoryIcon), "UpdateIcon")]
	internal static class ManualPriorityUpdateIconPatch
	{
		private static void Postfix(UI_NewInventoryIcon __instance)
		{
			ManualPriorityBadge.GetOrCreate(__instance).Refresh();

		}
	}
	[HarmonyPatch(typeof(UI_NewInventoryIcon), "OnEnable")]
	internal static class ManualPriorityEnablePatch
	{
		private static void Postfix(UI_NewInventoryIcon __instance)
		{
			ManualPriorityBadge.GetOrCreate(__instance).Refresh();

		}
	}

	[BepInPlugin("com.sephiria.backpack-organizer", "Sephiria Backpack Organizer", "2.5.4")]
	public class Plugin : BaseUnityPlugin
	{
		internal static Plugin Instance;

		internal static ManualLogSource Log;

		internal ConfigEntry<KeyboardShortcut> Hotkey;

		internal ConfigEntry<SortMode> Mode;

		internal ConfigEntry<bool> ShowNotifications;

		internal ConfigEntry<bool> AutoSortOnSessionStart;


		internal ConfigEntry<string> RowLockedItems;


		internal ConfigEntry<int> VanillaIterations;

		internal ConfigEntry<bool> AllowTabletRotation;





		internal ConfigEntry<int> SearchTimeBudgetMs;

		internal ConfigEntry<bool> VerboseDiagnostics;

		internal ConfigEntry<int> ApplySwapsPerFrame;

		internal ConfigEntry<int> ApplyRotationClicksPerFrame;

		internal ConfigEntry<float> ApplyFrameBudgetMs;

		internal ConfigEntry<int> ApplyAckTimeoutMs;

		internal ConfigEntry<bool> ManualPriorityEnabled;

		internal ConfigEntry<bool> ShowManualPriorityBadge;



		internal ConfigEntry<bool> EnableSmartStart;


		internal ConfigEntry<float> CriteriaWeight;

		internal ConfigEntry<float> NegativeWeight;

		internal ConfigEntry<float> CriteriaMoveChance;

		internal ConfigEntry<bool> PriorityEnable;

		internal ConfigEntry<int> PriorityCommon;

		internal ConfigEntry<int> PriorityUncommon;

		internal ConfigEntry<int> PriorityRare;

		internal ConfigEntry<int> PriorityLegend;

		internal ConfigEntry<int> PriorityEternal;

		internal ConfigEntry<string> PriorityFixedItems;

		internal ConfigEntry<string> ForcedPriorityItems;

		internal ConfigEntry<string> IgnoreCellPreferredItems;

		internal ConfigEntry<float> PriorityWeight1;

		internal ConfigEntry<float> PriorityWeight2;

		internal ConfigEntry<float> PriorityWeight3;

		internal ConfigEntry<float> PriorityWeight4;

		internal ConfigEntry<bool> CompassTargetForcedHigh;

		internal ConfigEntry<string> PriorityLowValueItems;

		internal ConfigEntry<float> LowValueLevelFactor;

		internal ConfigEntry<string> PriorityMinLevelItems;

		internal ConfigEntry<float> PlanetBonus;

		internal ConfigEntry<string> PlanetClusterExcludedItems;

		internal ConfigEntry<string> HarmonyCrystalItems;

		internal ConfigEntry<float> HarmonyLevelBonus;

		internal ConfigEntry<string> DedicationBadgeItems;

		internal ConfigEntry<float> DedicationCompanionBonus;

		internal ConfigEntry<string> HourglassItems;

		internal ConfigEntry<float> HourglassBonus;

		internal ConfigEntry<string> RayShardItems;

		internal ConfigEntry<float> RayShardBonus;

		internal ConfigEntry<string> EclipseItems;

		internal ConfigEntry<string> OpposingScaleItems;

		internal ConfigEntry<float> WhitePaperComboBonus;

		internal ConfigEntry<float> CompassBonus;

		internal ConfigEntry<float> CompassUnpairedFactor;

		internal ConfigEntry<float> BurdenPenalty;

		internal ConfigEntry<string> BeltItems;

		internal ConfigEntry<float> BeltRowBonus;

		internal ConfigEntry<string> BurdenItemKeys;

		internal ConfigEntry<bool> MysticEnable;

		internal ConfigEntry<string> MysticCategory;

		internal ConfigEntry<float> MysticMultiplier;

		private InventorySorter sorter;

		private Harmony harmony;

		private bool autoSortedThisSession;

		private bool lastSessionActive;

		internal bool IsSorting
		{
			get
			{
				if (sorter != null)
				{
					return sorter.Busy;
				}
				return false;
			}
		}

		private void Awake()
		{
			Instance = this;
			Log = base.Logger;
			Hotkey = base.Config.Bind("General", "Hotkey", new KeyboardShortcut(KeyCode.F8), "触发背包整理的快捷键（游戏使用新输入系统，插件会同时兼容新旧输入）");
			Mode = base.Config.Bind("General", "SortMode", SortMode.Enhanced, new ConfigDescription("整理模式：\nVanilla = 调用游戏内置的自动排列（任意模式可用，含联机作为客户端时）；\nEnhanced = 后台增强搜索，并通过分帧交换/旋转安全应用；主机与联机客户端均可用。", null));
			ShowNotifications = base.Config.Bind("General", "ShowNotifications", defaultValue: true, "是否在游戏内显示整理结果提示（屏幕系统消息）");
			AutoSortOnSessionStart = base.Config.Bind("General", "AutoSortOnSessionStart", defaultValue: false, "进入会话后自动整理一次背包（默认关闭，可在配置里开启）");
			RowLockedItems = base.Config.Bind("General", "RowLockedItems", "", "额外需固定在整理前原行的物品 LocalizedString key（逗号分隔）。凯尔萨德尼钥匙无需填写：插件会自动按当前最多的坚固/余烬/冰川/魔法科技羁绊选择周期行");
			VanillaIterations = base.Config.Bind("Vanilla", "MaxIterations", 30, new ConfigDescription("游戏内置自动排列的最大迭代次数（原版默认 4，越大效果越好但耗时略增）", new AcceptableValueRange<int>(1, 500)));
			AllowTabletRotation = base.Config.Bind("Vanilla", "AllowTabletRotation", defaultValue: true, "是否允许自动旋转石板以匹配加成覆盖范围");
			SearchTimeBudgetMs = base.Config.Bind("Enhanced", "SearchTimeBudgetMs", 0, new ConfigDescription("后台搜索时间预算（毫秒）；0=完成多起点搜索及局部精修（默认，质量优先）。正数达到预算后保留最佳方案。快照与预计算不计入预算。", new AcceptableValueRange<int>(0, 1000)));
			ApplySwapsPerFrame = base.Config.Bind("Apply", "SwapsPerFrame", 2, new ConfigDescription("每帧最多执行的背包交换次数。数值越低越平滑，默认 2", new AcceptableValueRange<int>(1, 10)));
			ApplyRotationClicksPerFrame = base.Config.Bind("Apply", "RotationClicksPerFrame", 4, new ConfigDescription("每帧最多执行的石板旋转点击次数。默认 4", new AcceptableValueRange<int>(1, 12)));
			ApplyFrameBudgetMs = base.Config.Bind("Apply", "FrameBudgetMs", 2f, new ConfigDescription("每帧应用整理操作的时间预算（毫秒），默认 2", new AcceptableValueRange<float>(0.25f, 10f)));
			ApplyAckTimeoutMs = base.Config.Bind("Apply", "NetworkAckTimeoutMs", 2000, new ConfigDescription("联机时等待服务器确认每批操作的最长毫秒数，默认 2000", new AcceptableValueRange<int>(250, 10000)));
			ManualPriorityEnabled = base.Config.Bind("ManualPriority", "Enabled", defaultValue: true, "允许用鼠标中键点击背包中的神器来设置手动优先级（每件独立循环：默认→max→↑→默认；Ctrl+中键：默认→-→↓→默认）");
			ShowManualPriorityBadge = base.Config.Bind("ManualPriority", "ShowBadge", defaultValue: true, "在已设置手动优先级的神器左下角显示透明小字 max / ↑ / - / ↓ 标记");
			VerboseDiagnostics = base.Config.Bind("Debug", "VerboseDiagnostics", defaultValue: false, "输出完整物品识别、布局网格和特殊机制分析。关闭可减少每次整理后的额外评分与日志开销");
			EnableSmartStart = base.Config.Bind("Smart", "EnableSmartStart", defaultValue: true, new ConfigDescription("启用智能初始布局：石板贪心摆位（正覆盖最大化、负等级推出背包外或压到非护符下）、受限护符优先放满足位置条件的格子或豁免格（解除限制的石板格）", null));
			CriteriaWeight = base.Config.Bind("Smart", "CriteriaWeight", 150f, new ConfigDescription("引导评分中位置条件满足/不满足的奖惩（0=关闭）", new AcceptableValueRange<float>(0f, 5000f)));
			NegativeWeight = base.Config.Bind("Smart", "NegativeWeight", 80f, new ConfigDescription("引导评分中护符站上负等级格子的额外惩罚（0=关闭）", new AcceptableValueRange<float>(0f, 2000f)));
			CriteriaMoveChance = base.Config.Bind("Smart", "CriteriaMoveChance", 0.15f, new ConfigDescription("退火中“受限护符定向跳转到满足条件格子”的移动概率（0=关闭）", new AcceptableValueRange<float>(0f, 1f)));
			PriorityEnable = base.Config.Bind("Priority", "Enable", defaultValue: true, new ConfigDescription("藏品优先级系统：1级(最高)→4级(最低)。默认：传说=1、羁绊(永恒)=1、稀有=2、高级=3、普通=4。高优先级藏品优先满足等级与位置需求，必要时低级藏品被牺牲进负格", null));
			PriorityCommon = base.Config.Bind("Priority", "Common", 4, new ConfigDescription("普通(Common)优先级（1最高~4最低）", new AcceptableValueRange<int>(1, 4)));
			PriorityUncommon = base.Config.Bind("Priority", "Uncommon", 3, new ConfigDescription("高级(Uncommon)优先级", new AcceptableValueRange<int>(1, 4)));
			PriorityRare = base.Config.Bind("Priority", "Rare", 2, new ConfigDescription("稀有(Rare)优先级", new AcceptableValueRange<int>(1, 4)));
			PriorityLegend = base.Config.Bind("Priority", "Legend", 1, new ConfigDescription("传说(Legend)优先级", new AcceptableValueRange<int>(1, 4)));
			PriorityEternal = base.Config.Bind("Priority", "Eternal", 1, new ConfigDescription("羁绊/永恒(Eternal)优先级", new AcceptableValueRange<int>(1, 4)));
			PriorityFixedItems = base.Config.Bind("Priority", "FixedHighPriorityItems", "Item_ColdLock_Name,Item_SweepRange_Name,Item_SweepRange_Enhanced_Name,Item_MiniBossFight_Name,Item_FrostRelicStack_Name", "强制最高优先级(1级)的特定藏品 LocalizedString key（逗号分隔）。默认：冰冷的锁、丢弃的金戒指、绝对戒指、红茶叶袋、冰星（效果重要，即使稀有度不高）");
			ForcedPriorityItems = base.Config.Bind("Priority", "ForcedPriorityItems", "Item_ScytheOfBerut_Name:3,Item_IceHammer_Name:2,Item_FaultfinderNeedle_Name:4,Item_IncreaseGoldDropRate_Name:2", "强制指定优先级的藏品（格式 key:优先级，逗号分隔多个；1最高~4最低，覆盖稀有度映射与强制1级配置）。默认：贝鲁特之镰降为3级（其效果依赖暴击溢出，权重不高）；暴风雪之锤升为2级；故障探测针降为4级（普通）；克里顿的印章（金币掉落率）设为2级");
			IgnoreCellPreferredItems = base.Config.Bind("Priority", "IgnoreCellPreferredItems", "Item_ColdLock_Name", "优先利用豁免格（IgnoreCriteria 解锁石板解除位置限制）的藏品 LocalizedString key（逗号分隔）。默认：冰冷的锁——只要背包里有解锁石板就尽可能站豁免格（可上高等级格），其次才考虑其自然限制位置。其他有位置限制的物品则相反：先考虑自然满足位置，不行再用豁免格");
			PriorityWeight1 = base.Config.Bind("Priority", "Weight1", 1.5f, new ConfigDescription("1级藏品等级分权重", new AcceptableValueRange<float>(0.5f, 3f)));
			PriorityWeight2 = base.Config.Bind("Priority", "Weight2", 1.25f, new ConfigDescription("2级藏品等级分权重", new AcceptableValueRange<float>(0.5f, 3f)));
			PriorityWeight3 = base.Config.Bind("Priority", "Weight3", 1.1f, new ConfigDescription("3级藏品等级分权重", new AcceptableValueRange<float>(0.5f, 3f)));
			PriorityWeight4 = base.Config.Bind("Priority", "Weight4", 1f, new ConfigDescription("4级藏品等级分权重", new AcceptableValueRange<float>(0.5f, 3f)));
			CompassTargetForcedHigh = base.Config.Bind("Priority", "CompassTargetForcedHigh", defaultValue: true, "被北向的金色针（指北针 Charm_UpCharmDamage）锁定的目标神器强制最高优先级(1级)：无论稀有度，优先拉满等级。整理前已经配对的针才生效");
			PriorityLowValueItems = base.Config.Bind("Priority", "LowLevelValueItems", "Item_ShadowEye_Name,Item_PlateArmor_Name", "等级价值低的藏品 LocalizedString key（逗号分隔，支持护符类名如 Charm_ShadowEye）：这些藏品升级收益极低，等级分按 LowValueLevelFactor 打折，且优先级降为4级（最晚选格）。默认：闪烁的眼睛、蜥蜴板甲（只保证启用，不追求高等级）");
			LowValueLevelFactor = base.Config.Bind("Priority", "LowLevelValueFactor", 0.1f, new ConfigDescription("低等级价值藏品的等级分保留比例（0=等级完全无价值，仍保证启用）", new AcceptableValueRange<float>(0f, 1f)));
			PriorityMinLevelItems = base.Config.Bind("Priority", "MinLevelItems", "Item_SuperPlanet_Name=2", "必须优先达到指定最低等级的藏品（格式 key=等级，逗号分隔多个）。命中后强制最高优先级(1级)，启用时有效等级低于目标每级额外扣分，保证优先拉满该等级。默认：谱子「银河」=2（Level2 效果关键）");
			PlanetBonus = base.Config.Bind("Synergy", "PlanetBonus", 40000f, new ConfigDescription("行星望远镜(Charm_PlanetModule)启用时，周围八格每颗启用行星藏品的加成奖励（0=关闭）。该值代表“行星聚拢到望远镜旁”的权重：应明显高于行星自身单级等级分(10000)才会让搜索优先聚拢", new AcceptableValueRange<float>(0f, 200000f)));
			PlanetClusterExcludedItems = base.Config.Bind("Synergy", "PlanetClusterExcludedItems", "Item_SuperPlanet_Name,Item_FlamePlanet_Name", "虽是行星分类但不参与望远镜聚簇的藏品 LocalizedString key（逗号分隔）。默认：乐谱银河（谱子「银河」）、红色行星观察日志——它们是行星但不需放在望远镜周围");
			HarmonyCrystalItems = base.Config.Bind("Synergy", "HarmonyCrystalItems", "Item_NearLevelDamage_Name", "和谐之晶类藏品 LocalizedString key（逗号分隔）：周围8格内神器每1级伤害放大+1%——整理会尽量把高等级护符聚到它周围8格（须启用才生效）");
			HarmonyLevelBonus = base.Config.Bind("Synergy", "HarmonyLevelBonus", 2000f, new ConfigDescription("和谐之晶周围8格每级护符有效等级的评分奖励（0=关闭）", new AcceptableValueRange<float>(0f, 50000f)));
			DedicationBadgeItems = base.Config.Bind("Synergy", "DedicationBadgeItems", "Item_CompanionChaos_Name", "奉献徽章类藏品 LocalizedString key（逗号分隔；默认类识别已覆盖奉献徽章）。效果：加成同一横排的同伴藏品");
			DedicationCompanionBonus = base.Config.Bind("Synergy", "DedicationCompanionBonus", 3000f, new ConfigDescription("奉献徽章同一横排内每个同伴藏品的评分奖励（0=关闭）。同伴按 ICompanionCharm 接口自动识别（金色手铃/迷你弩炮/灵魂粉末×3/采矿臂章等）", new AcceptableValueRange<float>(0f, 50000f)));
			HourglassItems = base.Config.Bind("Synergy", "HourglassItems", "", "发光的沙漏类藏品 LocalizedString key（逗号分隔；默认类识别已覆盖 Charm_RightSpellCooldownHelper）。效果：使右边一格的魔法书（Charm_Magic）CD 恢复速度 +30/60/100%（按沙漏等级）");
			HourglassBonus = base.Config.Bind("Synergy", "HourglassBonus", 6000f, new ConfigDescription("沙漏右边有魔法书时，按魔法书 CD 秒数的评分奖励（0=关闭）。CD 越长奖励越高 → 搜索会把沙漏放到 CD 最长的魔法书左边", new AcceptableValueRange<float>(0f, 50000f)));
			RayShardItems = base.Config.Bind("Synergy", "RayShardItems", "Item_DoubleMagic_Name", "雷伊星碎片类藏品 LocalizedString key（逗号分隔，也支持类名 token）。效果：放在耗蓝量最高的魔法书右侧（与沙漏方向相反）");
			RayShardBonus = base.Config.Bind("Synergy", "RayShardBonus", 4000f, new ConfigDescription("雷伊星碎片左侧有魔法书时，按魔法书耗蓝量的评分奖励（0=关闭）。耗蓝越高奖励越高 → 搜索会把碎片放到耗蓝最高的魔法书右侧", new AcceptableValueRange<float>(0f, 50000f)));
			EclipseItems = base.Config.Bind("Synergy", "EclipseItems", "", "永恒蚀类藏品 LocalizedString key（逗号分隔；默认类识别已覆盖 Charm_FireIceWeapon）。摆位规则：背包中冰霜武具(FROST)藏品多于太阳剑(FLAMESWORD)时放右三列，少于时放左三列，相等/皆无则不限");
			OpposingScaleItems = base.Config.Bind("Synergy", "OpposingScaleItems", "", "对立之秤类藏品 LocalizedString key（逗号分隔；默认类识别已覆盖 Charm_FireIce）。摆位规则：冰川(GLACIER)藏品多于余烬(EMBER)时放最右列，少于时放最左列，相等/皆无时只能最左或最右列");
			WhitePaperComboBonus = base.Config.Bind("Synergy", "WhitePaperComboBonus", 5000f, new ConfigDescription("白纸夹在两件同连击神器中间时的补位评分权重（0=关闭）。优先当前数量最大但尚未达到最高效果档位的连击；例如坚固 9/10 时优先用白纸补到 10", new AcceptableValueRange<float>(0f, 100000f)));
			CompassBonus = base.Config.Bind("Synergy", "CompassBonus", 12000f, new ConfigDescription("指北针(Charm_UpCharmDamage)维持整理前原目标时的评分奖励（0=不额外加分，原目标绑定仍始终生效）。整理前已配对的针会锁定同一个物品实例并随它移动；只有原先未配对的针才自动寻找伤害类藏品/指北针", new AcceptableValueRange<float>(0f, 50000f)));
			CompassUnpairedFactor = base.Config.Bind("Synergy", "CompassUnpairedFactor", 0.1f, new ConfigDescription("指北针未配对（上方无伤害类/指北针）时其等级分的保留比例。游戏里指北针效果只在配对时生效，未配对等级分应视为虚分（0.1=保留一成）", new AcceptableValueRange<float>(0f, 1f)));
			BeltItems = base.Config.Bind("Synergy", "BeltItems", "Item_Belt_Name", "多用途腰带类藏品 LocalizedString key（逗号分隔；类识别已覆盖 Charm_WoodenBox，木箱也会命中）。效果：背包最上行(y=0)每有一件神器，效果叠加一次——启用后整理会尽量把神器堆满第一行");
			BeltRowBonus = base.Config.Bind("Synergy", "BeltRowBonus", 2500f, new ConfigDescription("腰带启用时，第一行每件神器的评分奖励（0=关闭）", new AcceptableValueRange<float>(0f, 50000f)));
			BurdenPenalty = base.Config.Bind("Burden", "NegativeCellPenalty", 20000f, new ConfigDescription("负面藏品未待在负等级格子时的扣分（强制塞负格；0=关闭）", new AcceptableValueRange<float>(0f, 100000f)));
			BurdenItemKeys = base.Config.Bind("Burden", "ItemKeys", "Item_MindBurden_Name", "负面藏品识别：LocalizedString key（逗号分隔多个）。识别到的物品会被塞进背包最差的（负等级）格子。默认心之重担(Item_MindBurden_Name)");
			MysticEnable = base.Config.Bind("Mystic", "Enable", defaultValue: true, "神秘标签联动：神秘藏品≥2个时 1 个神秘地块等级×2，≥5个时共 4 个地块×2（ComboEffect_Mystic）。启用后插件会优先把高价值护符放到×2地块上");
			MysticCategory = base.Config.Bind("Mystic", "Category", "Mystic", "神秘标签名（游戏内分类 key，本地化显示为“神秘”）");
			MysticMultiplier = base.Config.Bind("Mystic", "Multiplier", 2f, new ConfigDescription("神秘地块等级倍率", new AcceptableValueRange<float>(1f, 10f)));
			sorter = new InventorySorter(this);
			harmony = new Harmony("com.sephiria.backpack-organizer");
			harmony.PatchAll(typeof(Plugin).Assembly);
			Log.LogInfo("Sephiria Backpack Organizer v2.5.4 已加载。" + $"按 [{Hotkey.Value}] 整理背包（当前模式: {Mode.Value}）");
		}

		private void Tick()
		{
			if (sorter == null)
			{
				return;
			}
			InventoryObserver.Poll();
            sorter.Poll();
			bool flag = NetworkClient.active && NetworkClient.localPlayer != null;
			if (flag != lastSessionActive)
			{
				lastSessionActive = flag;
				if (!flag)
				{
					sorter.ResetSessionClock();
					ManualPriorityManager.Clear();
					DirectionBindingManager.Clear();
				}
				Log.LogInfo($"会话状态变化: NetworkClient.active={NetworkClient.active}, " + "localPlayer=" + ((NetworkClient.localPlayer != null) ? "有" : "无"));
			}
			if (IsHotkeyDown(Hotkey.Value))
			{
				sorter.Sort();
			}
			else
			{
				if (!AutoSortOnSessionStart.Value)
				{
					return;
				}
				if (NetworkClient.active && NetworkClient.localPlayer != null)
				{
					if (!autoSortedThisSession)
					{
						PlayerAvatar component = NetworkClient.localPlayer.GetComponent<PlayerAvatar>();
						if (component != null && component.Inventory != null && component.Inventory.charms.Count > 0)
						{
							autoSortedThisSession = true;
							Log.LogInfo("检测到会话开始且背包有护符，自动整理…");
							sorter.Sort();
						}
					}
				}
				else
				{
					autoSortedThisSession = false;
				}
			}
		}

		private void Update()
		{
			Tick();
		}

		private static bool IsHotkeyDown(KeyboardShortcut ks)
		{
			KeyCode mainKey = ks.MainKey;
			if (mainKey == KeyCode.None)
			{
				return false;
			}
			Keyboard current = Keyboard.current;
			if (current != null)
			{
				Key? key = KeyCodeToInputSystemKey(mainKey);
				if (key.HasValue)
				{
					bool wasPressedThisFrame = current[key.Value].wasPressedThisFrame;
					if (wasPressedThisFrame && ks.Modifiers != null)
					{
						foreach (KeyCode modifier in ks.Modifiers)
						{
							Key? key2 = KeyCodeToInputSystemKey(modifier);
							if (key2.HasValue && !current[key2.Value].isPressed)
							{
								return false;
							}
						}
					}
					return wasPressedThisFrame;
				}
			}
			if (Input.GetKeyDown(mainKey))
			{
				return true;
			}
			return false;
		}

		private static Key? KeyCodeToInputSystemKey(KeyCode kc)
		{
			switch (kc)
			{
			case KeyCode.LeftControl:
				return Key.LeftCtrl;
			case KeyCode.RightControl:
				return Key.RightCtrl;
			case KeyCode.LeftAlt:
				return Key.LeftAlt;
			case KeyCode.RightAlt:
				return Key.RightAlt;
			case KeyCode.LeftMeta:
				return Key.LeftMeta;
			case KeyCode.RightMeta:
				return Key.RightMeta;
			case KeyCode.Return:
				return Key.Enter;
			case KeyCode.None:
				return null;
			default:
				try
				{
					return (Key)Enum.Parse(typeof(Key), kc.ToString(), ignoreCase: true);
				}
				catch
				{
					return null;
				}
			}
		}

		private void OnDestroy()
		{
            sorter?.ResetSessionClock();
            InventoryObserver.Detach();
			harmony?.UnpatchSelf();
			harmony = null;
			ManualPriorityManager.Clear();
			DirectionBindingManager.Clear();
			Instance = null;
		}
	}
	internal static class PluginInfo
	{
		public const string PLUGIN_GUID = "com.sephiria.backpack-organizer";

		public const string PLUGIN_NAME = "Sephiria Backpack Organizer";

		public const string PLUGIN_VERSION = "2.5.4";
	}
}




