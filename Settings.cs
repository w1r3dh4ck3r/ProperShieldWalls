using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace ProperShieldWalls
{
    public class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id => "ProperShieldWalls";
        public override string DisplayName => "Proper Shield Walls";
        public override string FolderName => "ProperShieldWalls";
        public override string FormatType => "json";

        // ── General ──

        [SettingPropertyBool(
            "Enabled",
            Order = 0,
            RequireRestart = false,
            HintText = "Master toggle. When enabled, melee attacks pass through friendly agents that are behind the attacker.")]
        [SettingPropertyGroup("General")]
        public bool Enabled { get; set; } = true;

        [SettingPropertyBool(
            "Player Only",
            Order = 1,
            RequireRestart = false,
            HintText = "If true, only the player's attacks pass through allies behind them. If false, all troops benefit (AI included).")]
        [SettingPropertyGroup("General")]
        public bool AffectPlayerOnly { get; set; } = false;

        [SettingPropertyFloatingInteger(
            "Behind Angle",
            60f, 180f, "#0",
            Order = 2,
            RequireRestart = false,
            HintText = "Degrees from forward. Friendlies beyond this angle are 'behind' and won't block attacks. 90 = sides+behind. 120 = wider cone. 180 = all friendlies.")]
        [SettingPropertyGroup("General")]
        public float BehindAngle { get; set; } = 100f;

        // ── Weapon Types / Polearms ──

        [SettingPropertyBool(
            "Enabled",
            Order = 0,
            RequireRestart = false,
            HintText = "Enable friendly bypass for polearms and spears.")]
        [SettingPropertyGroup("Weapon Types/Polearms / Spears", GroupOrder = 0)]
        public bool PolearmEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger(
            "Bypass Start %",
            0f, 100f, "#0",
            Order = 1,
            RequireRestart = false,
            HintText = "Attack progress % when bypass begins. 0 = from the very start of the swing/thrust.")]
        [SettingPropertyGroup("Weapon Types/Polearms / Spears", GroupOrder = 0)]
        public float PolearmBypassStart { get; set; } = 0f;

        [SettingPropertyFloatingInteger(
            "Bypass End %",
            0f, 100f, "#0",
            Order = 2,
            RequireRestart = false,
            HintText = "Attack progress % when bypass ends. 100 = through the entire swing/thrust.")]
        [SettingPropertyGroup("Weapon Types/Polearms / Spears", GroupOrder = 0)]
        public float PolearmBypassEnd { get; set; } = 50f;

        // ── Weapon Types / One-Handed Swords ──

        [SettingPropertyBool(
            "Enabled",
            Order = 0,
            RequireRestart = false,
            HintText = "Enable friendly bypass for one-handed swords.")]
        [SettingPropertyGroup("Weapon Types/One-Handed Swords", GroupOrder = 1)]
        public bool OneHandedSwordEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger(
            "Bypass Start %",
            0f, 100f, "#0",
            Order = 1,
            RequireRestart = false,
            HintText = "Attack progress % when bypass begins.")]
        [SettingPropertyGroup("Weapon Types/One-Handed Swords", GroupOrder = 1)]
        public float OneHandedSwordBypassStart { get; set; } = 0f;

        [SettingPropertyFloatingInteger(
            "Bypass End %",
            0f, 100f, "#0",
            Order = 2,
            RequireRestart = false,
            HintText = "Attack progress % when bypass ends.")]
        [SettingPropertyGroup("Weapon Types/One-Handed Swords", GroupOrder = 1)]
        public float OneHandedSwordBypassEnd { get; set; } = 50f;

        // ── Weapon Types / Two-Handed Swords ──

        [SettingPropertyBool(
            "Enabled",
            Order = 0,
            RequireRestart = false,
            HintText = "Enable friendly bypass for two-handed swords.")]
        [SettingPropertyGroup("Weapon Types/Two-Handed Swords", GroupOrder = 2)]
        public bool TwoHandedSwordEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger(
            "Bypass Start %",
            0f, 100f, "#0",
            Order = 1,
            RequireRestart = false,
            HintText = "Attack progress % when bypass begins.")]
        [SettingPropertyGroup("Weapon Types/Two-Handed Swords", GroupOrder = 2)]
        public float TwoHandedSwordBypassStart { get; set; } = 0f;

        [SettingPropertyFloatingInteger(
            "Bypass End %",
            0f, 100f, "#0",
            Order = 2,
            RequireRestart = false,
            HintText = "Attack progress % when bypass ends.")]
        [SettingPropertyGroup("Weapon Types/Two-Handed Swords", GroupOrder = 2)]
        public float TwoHandedSwordBypassEnd { get; set; } = 50f;

        // ── Weapon Types / One-Handed Axes ──

        [SettingPropertyBool(
            "Enabled",
            Order = 0,
            RequireRestart = false,
            HintText = "Enable friendly bypass for one-handed axes.")]
        [SettingPropertyGroup("Weapon Types/One-Handed Axes", GroupOrder = 3)]
        public bool OneHandedAxeEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger(
            "Bypass Start %",
            0f, 100f, "#0",
            Order = 1,
            RequireRestart = false,
            HintText = "Attack progress % when bypass begins.")]
        [SettingPropertyGroup("Weapon Types/One-Handed Axes", GroupOrder = 3)]
        public float OneHandedAxeBypassStart { get; set; } = 0f;

        [SettingPropertyFloatingInteger(
            "Bypass End %",
            0f, 100f, "#0",
            Order = 2,
            RequireRestart = false,
            HintText = "Attack progress % when bypass ends.")]
        [SettingPropertyGroup("Weapon Types/One-Handed Axes", GroupOrder = 3)]
        public float OneHandedAxeBypassEnd { get; set; } = 50f;

        // ── Weapon Types / Two-Handed Axes ──

        [SettingPropertyBool(
            "Enabled",
            Order = 0,
            RequireRestart = false,
            HintText = "Enable friendly bypass for two-handed axes.")]
        [SettingPropertyGroup("Weapon Types/Two-Handed Axes", GroupOrder = 4)]
        public bool TwoHandedAxeEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger(
            "Bypass Start %",
            0f, 100f, "#0",
            Order = 1,
            RequireRestart = false,
            HintText = "Attack progress % when bypass begins.")]
        [SettingPropertyGroup("Weapon Types/Two-Handed Axes", GroupOrder = 4)]
        public float TwoHandedAxeBypassStart { get; set; } = 0f;

        [SettingPropertyFloatingInteger(
            "Bypass End %",
            0f, 100f, "#0",
            Order = 2,
            RequireRestart = false,
            HintText = "Attack progress % when bypass ends.")]
        [SettingPropertyGroup("Weapon Types/Two-Handed Axes", GroupOrder = 4)]
        public float TwoHandedAxeBypassEnd { get; set; } = 50f;

        // ── Weapon Types / Maces ──

        [SettingPropertyBool(
            "Enabled",
            Order = 0,
            RequireRestart = false,
            HintText = "Enable friendly bypass for maces and blunt weapons.")]
        [SettingPropertyGroup("Weapon Types/Maces / Blunt", GroupOrder = 5)]
        public bool MaceEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger(
            "Bypass Start %",
            0f, 100f, "#0",
            Order = 1,
            RequireRestart = false,
            HintText = "Attack progress % when bypass begins.")]
        [SettingPropertyGroup("Weapon Types/Maces / Blunt", GroupOrder = 5)]
        public float MaceBypassStart { get; set; } = 0f;

        [SettingPropertyFloatingInteger(
            "Bypass End %",
            0f, 100f, "#0",
            Order = 2,
            RequireRestart = false,
            HintText = "Attack progress % when bypass ends.")]
        [SettingPropertyGroup("Weapon Types/Maces / Blunt", GroupOrder = 5)]
        public float MaceBypassEnd { get; set; } = 50f;

        // ── Weapon Types / Daggers ──

        [SettingPropertyBool(
            "Enabled",
            Order = 0,
            RequireRestart = false,
            HintText = "Enable friendly bypass for daggers and knives.")]
        [SettingPropertyGroup("Weapon Types/Daggers / Knives", GroupOrder = 6)]
        public bool DaggerEnabled { get; set; } = true;

        [SettingPropertyFloatingInteger(
            "Bypass Start %",
            0f, 100f, "#0",
            Order = 1,
            RequireRestart = false,
            HintText = "Attack progress % when bypass begins.")]
        [SettingPropertyGroup("Weapon Types/Daggers / Knives", GroupOrder = 6)]
        public float DaggerBypassStart { get; set; } = 0f;

        [SettingPropertyFloatingInteger(
            "Bypass End %",
            0f, 100f, "#0",
            Order = 2,
            RequireRestart = false,
            HintText = "Attack progress % when bypass ends.")]
        [SettingPropertyGroup("Weapon Types/Daggers / Knives", GroupOrder = 6)]
        public float DaggerBypassEnd { get; set; } = 50f;

        // ── Debug ──

        [SettingPropertyBool(
            "Enable Debug Messages",
            Order = 0,
            RequireRestart = false,
            HintText = "Show in-game messages when a friendly hit is bypassed.")]
        [SettingPropertyGroup("Debug")]
        public bool EnableDebug { get; set; } = false;
    }
}
