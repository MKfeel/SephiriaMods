using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SephiriaHoldSpecial
{
    [BepInPlugin(Id, "Sephiria Hold Special", "1.0.1")]
    [BepInProcess("Sephiria.exe")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "feel.sephiria.holdspecial";
        internal static Plugin Instance = null!;
        internal static ConfigEntry<bool> Enabled = null!;
        internal static ConfigEntry<float> Interval = null!;
        private Harmony? harmony;

        private void Awake()
        {
            Instance = this;
            Enabled = Config.Bind("General", "Enabled", true, "按住特殊攻击键自动释放；剑盾需同时按住普攻，单独按住仍为防御。");
            Interval = Config.Bind("General", "RetryInterval", 0.12f,
                new ConfigDescription("尝试下一次攻击的最小间隔（秒），不改变原生攻速、冷却或资源消耗。", new AcceptableValueRange<float>(0.08f, 1f)));
            try
            {
                InputAccess.Validate();
                harmony = new Harmony(Id);
                harmony.PatchAll(typeof(Plugin).Assembly);
                Logger.LogInfo("Sephiria Hold Special 1.0.1 loaded. Bloodletting/Reassemble: native manual input only. Shield: hold both attacks; greatsword: full charge; minigun/sheath: preserve hold/stance.");
            }
            catch (Exception ex)
            {
                harmony?.UnpatchSelf();
                Logger.LogError("Hold Special disabled: game contract/patch failed. " + ex);
                enabled = false;
            }
        }

        private void OnDisable() => HoldSession.Stop();
        private void OnDestroy() { HoldSession.Stop(); harmony?.UnpatchSelf(); }
        internal static bool Active => Instance && Instance.isActiveAndEnabled && Enabled.Value;
        internal static void Fail(Exception ex)
        {
            try { HoldSession.Stop(); } catch { HoldSession.Forget(); }
            Instance.enabled = false;
            Instance.Logger.LogError("Hold Special stopped after an unexpected error: " + ex);
        }
    }

    internal static class InputAccess
    {
        internal static readonly AccessTools.FieldRef<PlayerInputController, PlayerAvatar> Avatar = AccessTools.FieldRefAccess<PlayerInputController, PlayerAvatar>("avatar");
        internal static readonly AccessTools.FieldRef<PlayerInputController, WeaponControllerSimple> WeaponController = AccessTools.FieldRefAccess<PlayerInputController, WeaponControllerSimple>("weaponController");
        internal static readonly AccessTools.FieldRef<PlayerInputController, IntegratedActionController> Actions = AccessTools.FieldRefAccess<PlayerInputController, IntegratedActionController>("integratedActionController");
        internal static readonly AccessTools.FieldRef<PlayerInputController, bool> Hovered = AccessTools.FieldRefAccess<PlayerInputController, bool>("isAnyUIHovered");
        internal static readonly Func<PlayerInputController, bool> ScreenReady = AccessTools.MethodDelegate<Func<PlayerInputController, bool>>(AccessTools.Method(typeof(PlayerInputController), "ValidateScreenFader_PlayerMove"));
        internal static readonly Func<PlayerInputController, Vector2> Aim = AccessTools.MethodDelegate<Func<PlayerInputController, Vector2>>(AccessTools.Method(typeof(PlayerInputController), "GetAimedPosition"));
        internal static readonly AccessTools.FieldRef<WeaponSimple_GreatSword, bool> ChargeReady = AccessTools.FieldRefAccess<WeaponSimple_GreatSword, bool>("sweepRequest");
        internal static readonly AccessTools.FieldRef<WeaponSimple_SwordAndShield, bool> Sweeping = AccessTools.FieldRefAccess<WeaponSimple_SwordAndShield, bool>("isSweepUsing");
        internal static readonly AccessTools.FieldRef<WeaponSimple_Katana_New, bool> SmashReady = AccessTools.FieldRefAccess<WeaponSimple_Katana_New, bool>("smashReadyStateEnabled");
        internal static readonly AccessTools.FieldRef<WeaponSimple_Katana_New, bool> SmashPending = AccessTools.FieldRefAccess<WeaponSimple_Katana_New, bool>("smashAttackRequest");
        internal static void Validate() { _ = Avatar; _ = ScreenReady; _ = ChargeReady; }

        internal static bool Valid(PlayerInputController input)
        {
            if (!Plugin.Active || !input || !input.isActiveAndEnabled || !Application.isFocused || Time.timeScale <= 0f || input.BlockAvatarInput) return false;
            var avatar = Avatar(input);
            return avatar && avatar.isLocalPlayer && !avatar.IsDead && WeaponController(input) &&
                input.playerInput && input.playerInput.isActiveAndEnabled &&
                (!UIManager.Instance || UIManager.Instance.CurrentControlStack == null) && ScreenReady(input);
        }

        internal static bool SlotIs(IntegratedActionController actions, int index, QuickSlotType type) =>
            actions && actions.quickSlotsWeapon != null && actions.quickSlotsWeapon.Length > index &&
            actions.quickSlotsWeapon[index] != null && actions.quickSlotsWeapon[index].Type == type;
    }

    internal static class HoldSession
    {
        private static PlayerInputController? input;
        private static PlayerAvatar? avatar;
        private static WeaponControllerSimple? controller;
        private static WeaponSimple? weapon;
        private static InputAction? special;
        private static InputAction? basic;
        private static readonly RepeatCycle cycle = new RepeatCycle();
        private static int armFrame = -1;
        private static bool specialHeld;
        private static bool mouseSpecial;
        private static bool remoteChargeReady;

        internal static bool RequiresManualSpecial(WeaponSimple candidate)
        {
            // Reassemble uses the native transform flag. Bloodletting attaches its
            // effect to that same transformation through the BoneBlood addon.
            if (!(candidate is WeaponSimple_GreatSword sword)) return false;
            if (sword.specialAttackToTransform) return true;
            if (sword.addons != null)
                foreach (var addon in sword.addons)
                    if (addon is WeaponAddonGreatsword_BoneBlood) return true;
            return false;
        }

        internal static void ChargeEvent(WeaponSimple_GreatSword source, bool complete)
        {
            if (weapon == source) remoteChargeReady = complete;
        }

        internal static void BasicInput(InputAction.CallbackContext context) { basic = context.action; }

        internal static void SpecialInput(PlayerInputController source, InputAction.CallbackContext context)
        {
            var action = context.action;
            if (action == null || !action.IsPressed()) { Stop(); return; }
            if (input || armFrame == Time.frameCount || !action.WasPressedThisFrame() || !InputAccess.Valid(source)) return;
            if (!InputAccess.SlotIs(InputAccess.Actions(source), 1, QuickSlotType.Weapon_Special)) return;
            bool mouse = context.control?.device is Mouse;
            if (mouse && InputAccess.Hovered(source)) return;
            var player = InputAccess.Avatar(source);
            var current = InputAccess.WeaponController(source).currentWeapon;
            if (!current || RequiresManualSpecial(current)) return;
            input = source;
            avatar = player;
            controller = InputAccess.WeaponController(source);
            weapon = current;
            special = action;
            specialHeld = true;
            remoteChargeReady = false;
            mouseSpecial = mouse;
            armFrame = Time.frameCount;
            cycle.Begin(Time.time, Plugin.Interval.Value);
        }

        internal static void Forget()
        {
            input = null; avatar = null; controller = null; weapon = null; special = null;
            specialHeld = false;
            remoteChargeReady = false;
        }

        internal static void Stop()
        {
            // Release only the same weapon; never send an old release into a new loadout.
            var oldController = controller;
            var oldWeapon = weapon;
            bool release = specialHeld;
            Forget();
            if (release && oldController && oldWeapon && oldController.currentWeapon == oldWeapon && !RequiresManualSpecial(oldWeapon))
                oldController.SubAttackButtonUp();
        }

        internal static void Tick(PlayerInputController source)
        {
            if (input != source || !input) return;
            if (!avatar || !controller || !weapon || controller.currentWeapon != weapon) { Forget(); return; }
            // An addon/form may change on the same weapon instance while held.
            // Forget without synthesizing Up: the player's original release remains in charge.
            if (RequiresManualSpecial(weapon)) { Forget(); return; }
            if (!InputAccess.Valid(source) || special == null || !special.enabled || !special.IsPressed() ||
                !InputAccess.SlotIs(InputAccess.Actions(source), 1, QuickSlotType.Weapon_Special) ||
                (mouseSpecial && InputAccess.Hovered(source))) { Stop(); return; }
            bool busy = controller.currentWeaponSwing != -1 || controller.IsCastAnimationRunning;
            cycle.Observe(busy);
            // Movement-input locks also occur during weapon actions. The native special
            // input only checks CanMove, so do not mistake an attack's movement lock for a stun.
            if (!avatar.CanMove) return;
            float now = Time.time;
            float interval = Plugin.Interval.Value;

            if (weapon is WeaponSimple_GreatSword sword)
            {
                // Timer.Update resets the native timer on completion, so the UI ratio
                // is NOT a reliable full-charge signal. Clients observe the native completion RPC.
                bool full = sword.isServer ? InputAccess.ChargeReady(sword) : remoteChargeReady;
                if (cycle.CanReleaseCharge(sword.sweepSwing, full) && specialHeld)
                {
                    controller.SubAttackButtonUp();
                    specialHeld = false;
                    remoteChargeReady = false;
                    cycle.ReleasedCharge(now, interval);
                    return;
                }
                if (sword.sweepSwing) return;
                // currentWeaponSwing == 20 is the native follow-up whirlwind window.
                if (controller.currentWeaponSwing == 20)
                {
                    if (cycle.Ready(now, false)) SendSpecial(now, interval);
                    return;
                }
                if (cycle.Ready(now, busy) && (sword.moneyWhirlwind || avatar.MP >= sword.SweepCost)) SendSpecial(now, interval);
                return;
            }

            if (weapon is WeaponSimple_SwordAndShield shield)
            {
                // SubFire alone always remains guard. Never create a synthetic held basic attack.
                if (basic == null || !basic.enabled || !basic.IsPressed() || avatar.activeMagicCastModeClientside ||
                    !InputAccess.SlotIs(InputAccess.Actions(source), 0, QuickSlotType.Weapon_Basic) ||
                    !shield.isSweepAvailable || !shield.isGuardAvailable ||
                    !controller.animator.GetBool(AnimHashContainer.Instance.GuardHash) ||
                    (shield.isServer && InputAccess.Sweeping(shield))) return;
                if (shield.chargedSweep && controller.chargingRatioForUI < 1f) return;
                if (!cycle.Ready(now, busy)) return;
                avatar.AttackButtonDown(InputAccess.Aim(source) - (Vector2)controller.transform.position);
                cycle.Sent(now, interval, true);
                return;
            }

            if (weapon is WeaponSimple_Crossbow bow)
            {
                if (!cycle.Ready(now, busy) || avatar.MP < bow.SpecialAttackCost) return;
                if (bow.specialAttackType == WeaponSimple_Crossbow.ESpecialAttackType.Minigun)
                {
                    // Recover a press rejected while busy/low on MP, without interrupting warmup or firing.
                    if (!bow.isMinigunFiring) SendSpecial(now, 0.5f, false);
                    return;
                }
                if (bow.specialAttackType == WeaponSimple_Crossbow.ESpecialAttackType.FastReload &&
                    (bow.isReloading || (bow.ammoInCurrentMagazine >= bow.currentMagazineCapacity &&
                    bow.remainingMagazineCount >= bow.defaultMagazineCount))) return;
                if (bow.specialAttackType == WeaponSimple_Crossbow.ESpecialAttackType.IceBuff &&
                    bow.isServer && !bow.iceBuffCoolDownTimer.Check()) return;
                if (bow.specialAttackType == WeaponSimple_Crossbow.ESpecialAttackType.AmmoCompression &&
                    (bow.hasCompressedAmmo || bow.isReloading || bow.ammoInCurrentMagazine <= 1)) return;
                SendSpecial(now, Mathf.Max(interval, 0.25f), bow.specialAttackType == WeaponSimple_Crossbow.ESpecialAttackType.FireBullet);
                return;
            }

            if (weapon is WeaponSimple_Katana katana)
            {
                // Sheath is a TOGGLE, not an attack. Repeating it continuously unsheathes the blade.
                if (katana.sheathActionType == WeaponSimple_Katana.ESheathActionType.Sheath || katana.isSheathAnimationRunning) return;
                if (!cycle.Ready(now, busy)) return;
                if (katana.sheathActionType != WeaponSimple_Katana.ESheathActionType.Eclipse && avatar.MP < katana.SpecialAttackCost) return;
                SendSpecial(now, interval, katana.sheathActionType != WeaponSimple_Katana.ESheathActionType.Eclipse);
                return;
            }

            if (weapon is WeaponSimple_Katana_New newer)
            {
                // This prototype accepts a follow-up only inside a native ready/attack window.
                if (!newer.isServer || InputAccess.SmashPending(newer) ||
                    (!busy && !InputAccess.SmashReady(newer)) || avatar.MP < newer.SpecialAttackCost) return;
                if (cycle.Ready(now, false)) SendSpecial(now, interval);
                return;
            }

            if (weapon is WeaponSimple_Dagger || weapon is WeaponSimple_QuartterStaff)
            {
                if (cycle.Ready(now, busy)) SendSpecial(now, interval);
            }
            // Bow/Staff/Golem have no SubAttack override in this game build. Leave them native.
        }

        private static void SendSpecial(float now, float interval, bool expectAnimation = true)
        {
            if (!avatar || !input || !controller || !weapon || RequiresManualSpecial(weapon)) return;
            avatar.SubAttackButtonDown(InputAccess.Aim(input) - (Vector2)controller.transform.position);
            specialHeld = true;
            cycle.Sent(now, interval, expectAnimation);
        }
    }

    [HarmonyPatch(typeof(PlayerInputController), "HandleOnSubFire")]
    internal static class SpecialInputPatch
    {
        private static void Postfix(PlayerInputController __instance, InputAction.CallbackContext input)
        {
            try { HoldSession.SpecialInput(__instance, input); } catch (Exception ex) { Plugin.Fail(ex); }
        }
    }
    [HarmonyPatch(typeof(PlayerInputController), "HandleOnFire")]
    internal static class BasicInputPatch
    {
        private static void Prefix(InputAction.CallbackContext input) => HoldSession.BasicInput(input);
    }
    [HarmonyPatch(typeof(PlayerInputController), "Update")]
    internal static class TickPatch
    {
        private static void Postfix(PlayerInputController __instance)
        {
            try { HoldSession.Tick(__instance); } catch (Exception ex) { Plugin.Fail(ex); }
        }
    }
    [HarmonyPatch(typeof(PlayerInputController), "OnDisable")]
    internal static class DisablePatch
    {
        private static void Prefix() => HoldSession.Stop();
    }
    [HarmonyPatch(typeof(PlayerInputController), "OnApplicationFocus")]
    internal static class FocusPatch
    {
        private static void Prefix(bool focusStatus) { if (!focusStatus) HoldSession.Stop(); }
    }
    [HarmonyPatch(typeof(WeaponSimple_GreatSword), "UserCode_RpcCreateCompleteChargeFx")]
    internal static class ChargeCompletePatch
    {
        private static void Postfix(WeaponSimple_GreatSword __instance) => HoldSession.ChargeEvent(__instance, true);
    }
    [HarmonyPatch(typeof(WeaponSimple_GreatSword), "UserCode_RpcCreateChargingFx")]
    internal static class ChargeStartPatch
    {
        private static void Postfix(WeaponSimple_GreatSword __instance) => HoldSession.ChargeEvent(__instance, false);
    }
    [HarmonyPatch(typeof(WeaponSimple_GreatSword), "UserCode_RpcDestroyChargingFx")]
    internal static class ChargeEndPatch
    {
        private static void Postfix(WeaponSimple_GreatSword __instance) => HoldSession.ChargeEvent(__instance, false);
    }
}
