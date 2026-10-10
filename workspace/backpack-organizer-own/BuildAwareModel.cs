using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace SephiriaBackpackOrganizer
{
    public partial class InventorySorter
    {
        private static readonly string ModelDirectory = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location), "model");
        private static readonly Encoding Utf8 = new UTF8Encoding(false);
        private static bool ModelAssemblyVerified;
        private static Dictionary<int, string> ModelItemClasses;
        private sealed class BuildModelRequest
        {
            internal string Json, RequestId, WeaponToken, InputToken;
            internal JObject Result;
            internal double Before, After;
            internal readonly HashSet<int> IgnoredItemIds = new HashSet<int>();
        }
        private static WeaponControllerSimple LocalWeaponController(GridInventory inv) => inv != null && inv.UnitAvatar != null ? inv.UnitAvatar.GetComponent<WeaponControllerSimple>() : null;
        private static string WeaponToken(GridInventory inv)
        {
            var controller = LocalWeaponController(inv);
            var weapon = controller != null ? controller.currentWeapon : null;
            return weapon == null ? "" : weapon.GetInstanceID() + ":" + weapon.entityId + ":" + (int)weapon.weaponType + ":" + controller.currentWeaponForm;
        }
        private bool BuildWeaponUnchanged(SearchContext ctx) => ctx.buildModel == null || ctx.buildModel.WeaponToken == WeaponToken(ctx.inv);
        private static int MatrixValue(IDictionary<ItemPosition, int> matrix, ItemPosition position) => matrix.TryGetValue(position, out var value) ? value : 0;
        private static JObject Primitives(object source)
        {
            var json = new JObject();
            foreach (var field in source.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var type = field.FieldType;
                if (type.IsPrimitive || type == typeof(string) || (type.IsArray && (type.GetElementType().IsPrimitive || type.GetElementType() == typeof(string))))
                {
                    var value = field.GetValue(source);
                    if (value != null) json[field.Name] = JToken.FromObject(value);
                }
            }
            return json;
        }
        // Ask the game's parser about the actual instance (custom tablets included).
        // Unsatisfied out-of-grid conditions must remain present in the snapshot.
        private static JObject NativePattern(SearchContext ctx, StoneTablet tablet, ItemPosition origin, int rotation)
        {
            int Cell(int x, int y) => x < 0 || x >= ctx.width || y < 0 || y * ctx.width + x >= ctx.storage ? -1 : y * ctx.width + x;
            var effects = new JArray(); var conditions = new JArray();
            foreach (var token in ParseQuerySafe(tablet, origin, rotation, ctx.width, ctx.height, ctx.storage, false))
            {
                var effect = new StoneTablet.AdditionEffectData(token);
                int cell = Cell(effect.position.x, effect.position.y);
                if (cell < 0) continue;
                string value = null;
                switch (effect.effectType)
                {
                    case StoneTablet.EffectType.IncreaseConstLevel: value = effect.levelParam.ToString(System.Globalization.CultureInfo.InvariantCulture); break;
                    case StoneTablet.EffectType.Disable: value = "X"; break;
                    case StoneTablet.EffectType.IgnoreCriteria: value = "IGNORECRITERIA"; break;
                    case StoneTablet.EffectType.MultiplyConstLevel: value = "MUL/" + effect.levelParam; break;
                    case StoneTablet.EffectType.None: break;
                    default: throw new InvalidOperationException("流派模型暂不支持石板效果：" + effect.effectType);
                }
                if (value != null) effects.Add(new JObject { ["cell"] = cell, ["value"] = value });
            }
            foreach (var token in ParseQuerySafe(tablet, origin, rotation, ctx.width, ctx.height, ctx.storage, true))
            {
                var condition = new StoneTablet.AdditionCriteriaData(token);
                string value = null;
                switch (condition.effectType)
                {
                    case StoneTablet.CriteriaType.AnyItem: value = "ITEM"; break;
                    case StoneTablet.CriteriaType.OnlyCharm: value = "CHARM"; break;
                    case StoneTablet.CriteriaType.Placed: value = "PLACED"; break;
                    case StoneTablet.CriteriaType.None: break;
                    default: throw new InvalidOperationException("流派模型暂不支持石板条件：" + condition.effectType);
                }
                if (value != null) conditions.Add(new JObject { ["cell"] = Cell(condition.position.x, condition.position.y), ["value"] = value });
            }
            return new JObject { ["effects"] = effects, ["conditions"] = conditions };
        }
        private static JObject NativeStats(UnitAvatar avatar)
        {
            var result = new JObject();
            var statuses = JArray.Parse(File.ReadAllText(Path.Combine(ModelDirectory, "data", "statuses.json"), Utf8));
            foreach (var entry in statuses)
            {
                string id = (string)entry["id"];
                var entity = StatusDatabase.GetStatusEntity(id);
                if (entity == null) continue;
                string[] parts = entity.className.Split('/');
                int value;
                if (parts.Length == 2 && parts[0] == "StatusInstance_Custom") value = avatar.customStats.TryGetValue(parts[1].ToUpperInvariant(), out var v) ? v : 0;
                else if (parts.Length == 2 && parts[0] == "StatusInstance_CustomAmp") value = avatar.GetCustomStatAmpUnsafe(parts[1].ToUpperInvariant());
                else if (NativeStatKeys.ByStatusClass.TryGetValue(entity.className, out var rawKey)) value = avatar.customStats.TryGetValue(rawKey, out var v) ? v : 0;
                else continue;
                if (id == "PHYSICAL_DAMAGE" || id == "FIRE_DAMAGE" || id == "ICE_DAMAGE" || id == "LIGHTNING_DAMAGE") value -= 20;
                result[id] = value;
            }
            result["MAX_MP"] = avatar.maxMp - 50;
            result["MAX_HP"] = avatar.maxHp - 70;
            result["FINAL_HP"] = avatar.finalMaxHp;
            result["MOVE_SPEED"] = (avatar.moveSpeedMultiplier - 1f) * 100f;
            var highestField = avatar.GetType().GetField("highestElementalBonus", BindingFlags.Public | BindingFlags.Instance);
            if (highestField != null) result["HIGHEST_ELEMENTAL_DAMAGE"] = Convert.ToInt32(highestField.GetValue(avatar));
            return result;
        }
        private static string ModelInputToken(SearchContext ctx)
        {
            var b = new StringBuilder(WeaponToken(ctx.inv));
            for (int c = 0; c < ctx.storage; c++)
            {
                var p = ctx.inv.IdxToPos(c);
                b.Append('|').Append(MatrixValue(ctx.inv.levelMatrix, p)).Append(',').Append(MatrixValue(ctx.inv.multiplyLevelMatrix, p)).Append(',').Append(MatrixValue(ctx.inv.disableMatrix, p)).Append(',').Append(MatrixValue(ctx.inv.ignoreCriteriaMatrix, p));
            }
            foreach (var item in ctx.items)
                if (item.isCharm) b.Append('|').Append(item.slot.instanceID).Append(':').Append(item.slot.charm.DisplayedLevel).Append(':').Append(item.slot.charm.IsEffectEnabled);
            return b.ToString();
        }
        private void CaptureBuildModel(SearchContext ctx)
        {
            if (!plugin.BuildAwareEnabled.Value) return;
            if (!File.Exists(Path.Combine(ModelDirectory, "worker.cjs")) || !File.Exists(Path.Combine(ModelDirectory, "runtime", "node.exe"))) throw new InvalidOperationException("流派模型文件不完整，请重新安装完整包。");
            if (!ModelAssemblyVerified)
            {
                var catalog = JObject.Parse(File.ReadAllText(Path.Combine(ModelDirectory, "data", "catalog.json"), Utf8));
                using (var stream = File.OpenRead(typeof(GridInventory).Assembly.Location))
                using (var sha = SHA256.Create())
                    if (!string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), (string)catalog["manifest"]["assemblySha256"], StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("游戏程序集已变化，需更新流派模型数据；保留原布局。");
                ModelItemClasses = ((JArray)catalog["items"]).ToDictionary(d => (int)d["id"], d => (string)d["class"]);
                ModelAssemblyVerified = true;
            }
            var controller = LocalWeaponController(ctx.inv); var weapon = controller != null ? controller.currentWeapon : null;
            if (weapon == null) throw new InvalidOperationException("武器尚未同步，请稍后按 F8。");
            if (ctx.inv.globalActiveValue <= 0) throw new InvalidOperationException("背包效果尚未启用，请稍后按 F8。");
            if (ctx.inv.UnitAvatar.GetCustomStatUnsafe("ARRANGEMENTBONUS") != 0) throw new InvalidOperationException("暂未建模当前排列奖励，保留原布局。");
            var request = new BuildModelRequest { RequestId = Guid.NewGuid().ToString("N"), WeaponToken = WeaponToken(ctx.inv), InputToken = ModelInputToken(ctx) };
            var cells = new JArray(); var definitions = new JArray(); var patterns = new JObject(); var effects = new JArray();
            var nativeLevel = new JArray(); var nativeMultiply = new JArray(); var nativeDisable = new JArray(); var nativeIgnore = new JArray();
            var addedDefinitions = new HashSet<int>();
            for (int c = 0; c < ctx.storage; c++)
            {
                var position = ctx.inv.IdxToPos(c); var slot = ctx.original[c];
                nativeLevel.Add(MatrixValue(ctx.inv.levelMatrix, position)); nativeMultiply.Add(MatrixValue(ctx.inv.multiplyLevelMatrix, position));
                nativeDisable.Add(MatrixValue(ctx.inv.disableMatrix, position)); nativeIgnore.Add(MatrixValue(ctx.inv.ignoreCriteriaMatrix, position));
                if (!slot.hasItem) { cells.Add(JValue.CreateNull()); continue; }
                var info = ctx.itemByInstance[slot.instanceID];
                string actualClass = info.isCharm ? slot.charm.GetType().Name : info.isStele ? "StoneTablet" : "";
                bool ignored = !ModelItemClasses.TryGetValue(slot.entityID, out var modeledClass) || modeledClass != actualClass ||
                    info.isCharm && slot.charm.GetType().Assembly != typeof(Charm_Basic).Assembly ||
                    info.isStele && slot.tablet.GetType().Assembly != typeof(StoneTablet).Assembly;
                if (ignored) request.IgnoredItemIds.Add(slot.entityID);
                if (!ignored && !info.isCharm && !info.isStele) throw new InvalidOperationException("主背包含有模型不支持的物品：" + slot.entityID);
                int mark = info.manualPriorityRank == 1 ? 2 : info.manualPriorityRank == 2 ? 1 : info.manualPriorityRank;
                cells.Add(new JObject { ["uid"] = slot.instanceID.ToString(), ["id"] = slot.entityID, ["rotation"] = slot.rotation, ["enchant"] = info.enchant, ["mark"] = mark, ["locked"] = false, ["nativeActive"] = info.isCharm && slot.charm.IsEffectEnabled });
                if (addedDefinitions.Add(slot.entityID))
                {
                    var d = new JObject { ["id"] = slot.entityID, ["class"] = actualClass, ["rarity"] = (int)info.rarity,
                        ["kind"] = info.isCharm ? "artifact" : info.isStele ? "tablet" : "misc", ["name"] = slot.charm != null ? slot.charm.name : slot.entityID.ToString(), ["modelIgnored"] = ignored };
                    if (info.isCharm)
                    {
                        var charm = slot.charm; var primitives = ignored ? new JObject() : Primitives(charm);
                        d["maxLevel"] = charm.maxLevel; d["unique"] = charm.isUniqueEffect; d["weapon"] = charm.isWeaponRelatedCharm ? new JValue((int)charm.relatedWeapon) : JValue.CreateNull();
                        d["criteria"] = charm.criteria != null ? charm.criteria.GetType().Name : "";
                        d["categories"] = new JArray(charm.GetItemCategory()); d["mechanics"] = primitives; d["curves"] = primitives.DeepClone();
                        d["attackable"] = info.isAttackable;
                        var statField = charm.GetType().GetField("stats", BindingFlags.Public | BindingFlags.Instance);
                        if (!ignored && statField != null) d["stats"] = JToken.FromObject(statField.GetValue(charm));
                        effects.Add(new JObject { ["uid"] = slot.instanceID.ToString(), ["level"] = charm.DisplayedLevel, ["active"] = charm.IsEffectEnabled });
                    }
                    else d["rotatable"] = info.tabletRotatable && plugin.AllowTabletRotation.Value;
                    definitions.Add(d);
                }
                else if (info.isCharm) effects.Add(new JObject { ["uid"] = slot.instanceID.ToString(), ["level"] = slot.charm.DisplayedLevel, ["active"] = slot.charm.IsEffectEnabled });
                if (info.isStele)
                {
                    var byCell = new JArray();
                    for (int target = 0; target < ctx.storage; target++)
                    {
                        var rotations = new JArray();
                        for (int r = 0; r < 4; r++) rotations.Add(NativePattern(ctx, slot.tablet, ctx.inv.IdxToPos(target), r));
                        byCell.Add(rotations);
                    }
                    patterns[slot.instanceID.ToString()] = byCell;
                }
            }
            var fixedPatterns = new JArray();
            foreach (var engraving in ctx.inv.engravings)
            {
                if (engraving == null) throw new InvalidOperationException("铭刻尚未同步");
                var position = new ItemPosition(engraving.xIdx, engraving.yIdx);
                fixedPatterns.Add(new JObject { ["cell"] = ctx.inv.PosToIdx(position), ["pattern"] = NativePattern(ctx, engraving, position, engraving.rotation) });
            }
            var counts = new JObject(); foreach (var pair in ctx.inv.currentSetEffectCount) counts[pair.Key] = pair.Value;
            var compass = new JArray(); foreach (var pair in ctx.compassTargetByInstance) compass.Add(new JArray(pair.Key.ToString(), pair.Value.ToString()));
            var paper = new JArray(); foreach (var target in ctx.paperLocks.Targets) paper.Add(new JObject { ["uid"] = target.Instance.ToString(), ["categories"] = new JArray(target.Categories) });
            var pins = new JArray(); foreach (var pair in ctx.paperLocks.PinnedCells) pins.Add(new JArray(pair.Key, pair.Value == 0 ? JValue.CreateNull() : new JValue(pair.Value.ToString())));
            var rows = new JArray(); foreach (var info in ctx.items.Where(i => i.isRowLocked && !i.isCyclicRowCategory)) rows.Add(new JObject { ["uid"] = info.slot.instanceID.ToString(), ["row"] = info.lockRow, ["cycle"] = info.lockRowCycle });
            var snapshot = new JObject
            {
                ["protocol"] = 1, ["requestId"] = request.RequestId, ["width"] = ctx.width, ["capacity"] = ctx.storage,
                ["profile"] = plugin.BuildProfile.Value, ["seed"] = Environment.TickCount, ["budgetMs"] = plugin.SearchTimeBudgetMs.Value,
                ["weapon"] = new JObject { ["entityId"] = weapon.entityId, ["name"] = WeaponDatabase.FindWeaponById(weapon.entityId)?.Name ?? "", ["type"] = (int)weapon.weaponType, ["form"] = controller.currentWeaponForm, ["attackWeight"] = weapon.attackWeightPerSwing },
                ["cells"] = cells, ["definitions"] = definitions, ["patterns"] = patterns, ["fixedPatterns"] = fixedPatterns,
                ["nativeCounts"] = counts, ["nativeEffects"] = effects, ["nativeStatsSchema"] = 2, ["nativeStats"] = NativeStats(ctx.inv.UnitAvatar),
                ["movement"] = new JObject { ["slowPercent"] = (ctx.inv.UnitAvatar.GetMoveSpeedMultiplier() - ctx.inv.UnitAvatar.moveSpeedMultiplier) * 100f },
                ["resonance"] = CaptureResonance(ctx), ["skipResonance"] = resonanceAttempted,
                ["uniqueOrder"] = new JArray(ctx.items.Where(i => i.isCharm && i.slot.charm.IsUniqueEffectRegistered).Concat(ctx.items.Where(i => !i.isCharm || !i.slot.charm.IsUniqueEffectRegistered)).Select(i => i.slot.instanceID.ToString())),
                ["matrices"] = new JObject { ["level"] = nativeLevel, ["multiply"] = nativeMultiply, ["disable"] = nativeDisable, ["ignore"] = nativeIgnore },
                ["locks"] = new JObject { ["compass"] = compass, ["paper"] = paper, ["pins"] = pins, ["rows"] = rows }
            };
            request.Json = snapshot.ToString(Formatting.None); ctx.buildModel = request;
            if (request.IgnoredItemIds.Count > 0) Plugin.Log.LogInfo("模组物品不参与流派收益建模；保留占格及手动标记，entity=" + string.Join(",", request.IgnoredItemIds));
        }
        private List<Slot> RunBuildModel(SearchContext ctx, List<Slot> original, out double before, out double after)
        {
            before = after = 0;
            var request = ctx.buildModel;
            var start = new ProcessStartInfo
            {
                FileName = Path.Combine(ModelDirectory, "runtime", "node.exe"), Arguments = "--max-old-space-size=256 worker.cjs", WorkingDirectory = ModelDirectory,
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Utf8, StandardErrorEncoding = Utf8
            };
            using (var process = new Process { StartInfo = start })
            {
                try
                {
                    process.Start();
                    try { process.PriorityClass = ProcessPriorityClass.BelowNormal; } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
                    var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
                    var payload = Utf8.GetBytes(request.Json);
                    process.StandardInput.BaseStream.Write(payload, 0, payload.Length); process.StandardInput.Close();
                    while (!process.WaitForExit(50))
                        if (ctx.cancelled) { try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } process.WaitForExit(); return CloneSlots(original); }
                    var result = JObject.Parse(output.GetAwaiter().GetResult());
                    if (!(bool?)result["ok"] ?? true) throw new InvalidOperationException((string)result["error"] ?? error.GetAwaiter().GetResult());
                    if ((int?)result["protocol"] != 1 || (string)result["requestId"] != request.RequestId) throw new InvalidOperationException("流派模型返回了其他请求的结果");
                    request.Result = result;
                    if ((string)result["phase"] == "resonance" && (resonanceAttempted || JObject.Parse(request.Json)["resonance"]?.Type != JTokenType.Object))
                        throw new InvalidOperationException("未请求共鸣石激活阶段");
                    foreach (string phase in new[] { "before", "after" })
                        if (!(result[phase]?["level"] is JArray levels) || levels.Count != original.Count || !(result[phase]?["active"] is JArray active) || active.Count != original.Count)
                            throw new InvalidOperationException("流派模型预测格数不一致");
                    var returned = result["cells"] as JArray; if (returned == null || returned.Count != original.Count) throw new InvalidOperationException("流派模型返回格数不一致");
                    var byUid = original.Where(s => s.hasItem).ToDictionary(s => s.instanceID.ToString()); var seen = new HashSet<string>(); var layout = new List<Slot>();
                    foreach (var item in returned)
                    {
                        if (item.Type == JTokenType.Null) { layout.Add(Slot.Empty()); continue; }
                        string uid = (string)item["uid"]; int rotation = (int)item["rotation"];
                        if (!seen.Add(uid) || !byUid.TryGetValue(uid, out var source) || (int)item["id"] != source.entityID || rotation < 0 || rotation > 3) throw new InvalidOperationException("流派模型返回实例不一致");
                        var info = ctx.itemByInstance[source.instanceID];
                        if ((!info.isStele || !info.tabletRotatable || !plugin.AllowTabletRotation.Value) && rotation != source.rotation) throw new InvalidOperationException("流派模型返回非法旋转");
                        var slot = source.Clone(); slot.rotation = rotation; layout.Add(slot);
                    }
                    if (!SameItems(original, layout) || !PaperLocksSatisfied(ctx, layout) || !CompassBindingsSatisfied(ctx, layout)) throw new InvalidOperationException("流派模型结果违反物品或绑定约束");
                    foreach (var info in ctx.items.Where(i => i.isRowLocked && !i.isCyclicRowCategory))
                        if (!IsAllowedLockedRow(info, layout.FindIndex(s => s.hasItem && s.instanceID == info.slot.instanceID) / ctx.width))
                            throw new InvalidOperationException("流派模型结果违反行锁定");
                    before = request.Before = (double)result["before"]["total"]; after = request.After = (double)result["after"]["total"];
                    bool normalPhase = (string)result["phase"] == "normal";
                    if (!BuildLayoutAcceptance.Accepts(BuildResultObjective(ctx, layout, result["after"]), BuildResultObjective(ctx, original, result["before"]),
                        normalPhase ? result["after"]["freeGain"]?.ToObject<FreeGainProof>() : null,
                        normalPhase ? result["before"]["freeGain"]?.ToObject<FreeGainProof>() : null))
                        throw new InvalidOperationException("流派模型完整目标下降，保留原布局");
                    ctx.annealEvaluations = (int)result["evaluations"]; ctx.annealStarts = 8; ctx.annealStartsCompleted = (int)result["completedStarts"]; ctx.searchBudgetReached = (bool)result["budgetReached"];
                    Plugin.Log.LogInfo("当前武器 " + JObject.Parse(request.Json)["weapon"].ToString(Formatting.None) + "；流派识别 " + result["detection"].ToString(Formatting.None));
                    Plugin.Log.LogInfo("整理阶段 " + (string)result["phase"] + "；共鸣石检查 " + result["resonance"]?.ToString(Formatting.None));
                    Plugin.Log.LogInfo("流派模型代理评分 " + before + " -> " + after + "；完整目标 " + result["before"]["objective"].ToString(Formatting.None) + " -> " + result["after"]["objective"].ToString(Formatting.None));
                    Plugin.Log.LogInfo("流派模型耗时(ms) " + result["timings"]?.ToString(Formatting.None) + "；重复计算缓存 " + result["cache"]?.ToString(Formatting.None));
                    if (result["freeGains"] is JObject freeGains) Plugin.Log.LogInfo("同分无损属性提升 " + freeGains.ToString(Formatting.None));
                    foreach (string phase in new[] { "before", "after" })
                        Plugin.Log.LogInfo("流派模型 " + phase + " 属性 " + result[phase]["stats"]?.ToString(Formatting.None) + "；MP " + result[phase]["resource"]?.ToString(Formatting.None));
                    if (result["after"]["unsupported"] is JArray unsupported && unsupported.Count > 0)
                        Plugin.Log.LogInfo("流派模型仍未覆盖的动态效果 " + unsupported.ToString(Formatting.None));
                    if (result["ignoredItems"] is JArray ignoredItems && ignoredItems.Count > 0)
                        Plugin.Log.LogInfo("已忽略模组物品的自定义收益；按玩家标记整理 " + ignoredItems.ToString(Formatting.None));
                    if (plugin.ExportBuildSnapshot.Value)
                    {
                        try
                        {
                            string folder = Path.Combine(ModelDirectory, "snapshots"); Directory.CreateDirectory(folder);
                            File.WriteAllText(Path.Combine(folder, "last-input.json"), request.Json, Utf8); File.WriteAllText(Path.Combine(folder, "last-result.json"), result.ToString(Formatting.Indented), Utf8);
                        }
                        catch (IOException ex) { Plugin.Log.LogWarning("快照导出失败（不影响整理）：" + ex.Message); }
                        catch (UnauthorizedAccessException ex) { Plugin.Log.LogWarning("快照导出失败（不影响整理）：" + ex.Message); }
                    }
                    return layout;
                }
                finally { try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } }
            }
        }
        private static bool BuildPredictionMatches(PendingEnhancedSort state, List<Slot> layout)
        {
            var result = state.ctx.buildModel?.Result;
            if (result == null || state.rollingBack) return true;
            var prediction = result["after"];
            for (int c = 0; c < layout.Count; c++)
                if (layout[c].charm != null && !state.ctx.buildModel.IgnoredItemIds.Contains(layout[c].entityID) && (layout[c].charm.DisplayedLevel != (int)prediction["level"][c] || layout[c].charm.IsEffectEnabled != (bool)prediction["active"][c])) return false;
            var counts = prediction["counts"] as JObject;
            if (counts == null) return false;
            foreach (var expected in counts.Properties())
                if ((state.inv.currentSetEffectCount.TryGetValue(expected.Name, out var actual) ? actual : 0) != (int)expected.Value) return false;
            foreach (var actual in state.inv.currentSetEffectCount)
                if (counts[actual.Key] == null && actual.Value != 0) return false;
            return true;
        }
        private static LayoutObjective BuildResultObjective(SearchContext ctx, List<Slot> slots, JToken prediction)
        {
            var score = new LayoutObjective { Ordinary = (double)prediction["total"] };
            for (int c = 0; c < slots.Count; c++)
            {
                var slot = slots[c]; if (!slot.hasItem) continue;
                if (slot.instanceID != ctx.original[c].instanceID || slot.rotation != ctx.original[c].rotation) score.Stable--;
                var info = ctx.itemByInstance[slot.instanceID];
                if (info.isCharm) score.Add((ItemMark)info.manualPriorityRank, (bool)prediction["active"][c], (int)prediction["level"][c], info.maxLevel);
            }
            return score;
        }
    }
}
