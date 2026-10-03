using System.Collections.Generic;

namespace SephiriaBackpackOrganizer
{
    // Verified against the installed StatusInstance_*.ApplyStatusInner methods.
    // Status IDs are not necessarily the keys stored in UnitAvatar.customStats.
    internal static class NativeStatKeys
    {
        internal static readonly Dictionary<string, string> ByStatusClass = new Dictionary<string, string>
        {
            ["StatusInstance_AttackSpeed"] = "ATTACKSPEED",
            ["StatusInstance_BasicAttackDamage"] = "BASICATTACKDAMAGEBONUS",
            ["StatusInstance_Critical"] = "CRITICAL",
            ["StatusInstance_CriticalDamageRate"] = "CRITICALDAMAGEBONUS",
            ["StatusInstance_DashAttackDamage"] = "DASHATTACKDAMAGEBONUS",
            ["StatusInstance_DashRecoverySpeed"] = "DASHRECOVERY",
            ["StatusInstance_Defense"] = "DAMAGEREDUCTION",
            ["StatusInstance_Evasion"] = "EVASION",
            ["StatusInstance_FinalDamage"] = "ALLDAMAGEBONUS",
            ["StatusInstance_FinalMP"] = "FINALMP",
            ["StatusInstance_FinalWeaponDamage"] = "FINALWEAPONDAMAGE",
            ["StatusInstance_PhysicalDamage"] = "PHYSICALDAMAGE",
            ["StatusInstance_FireDamage"] = "FIREDAMAGE",
            ["StatusInstance_IceDamage"] = "ICEDAMAGE",
            ["StatusInstance_LightningDamage"] = "LIGHTNINGDAMAGE",
            ["StatusInstance_MagicCritical"] = "MAGICCRITICAL",
            ["StatusInstance_MPRegen"] = "MPREGEN",
            ["StatusInstance_MPSteal"] = "MPSTEAL",
            ["StatusInstance_SpecialAttackDamage"] = "SPECIALATTACKDAMAGEBONUS",
            ["StatusInstance_TrueDamage"] = "TRUEDAMAGE",
            ["StatusInstance_CooldownRecoverySpeed"] = "COOLDOWNRECOVERYSPEED"
        };
    }
}
