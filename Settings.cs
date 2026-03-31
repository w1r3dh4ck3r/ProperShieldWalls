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

        // ── Weapon Toggles ──

        [SettingPropertyBool(
            "Polearms / Spears",
            Order = 0,
            RequireRestart = false,
            HintText = "Enable friendly bypass for polearms and spears.")]
        [SettingPropertyGroup("Weapon Types")]
        public bool PolearmEnabled { get; set; } = true;

        [SettingPropertyBool(
            "One-Handed Swords",
            Order = 1,
            RequireRestart = false,
            HintText = "Enable friendly bypass for one-handed swords.")]
        [SettingPropertyGroup("Weapon Types")]
        public bool OneHandedSwordEnabled { get; set; } = true;

        [SettingPropertyBool(
            "Two-Handed Swords",
            Order = 2,
            RequireRestart = false,
            HintText = "Enable friendly bypass for two-handed swords.")]
        [SettingPropertyGroup("Weapon Types")]
        public bool TwoHandedSwordEnabled { get; set; } = true;

        [SettingPropertyBool(
            "One-Handed Axes",
            Order = 3,
            RequireRestart = false,
            HintText = "Enable friendly bypass for one-handed axes.")]
        [SettingPropertyGroup("Weapon Types")]
        public bool OneHandedAxeEnabled { get; set; } = true;

        [SettingPropertyBool(
            "Two-Handed Axes",
            Order = 4,
            RequireRestart = false,
            HintText = "Enable friendly bypass for two-handed axes.")]
        [SettingPropertyGroup("Weapon Types")]
        public bool TwoHandedAxeEnabled { get; set; } = true;

        [SettingPropertyBool(
            "Maces / Blunt",
            Order = 5,
            RequireRestart = false,
            HintText = "Enable friendly bypass for maces and blunt weapons.")]
        [SettingPropertyGroup("Weapon Types")]
        public bool MaceEnabled { get; set; } = true;

        [SettingPropertyBool(
            "Daggers / Knives",
            Order = 6,
            RequireRestart = false,
            HintText = "Enable friendly bypass for daggers and knives.")]
        [SettingPropertyGroup("Weapon Types")]
        public bool DaggerEnabled { get; set; } = true;

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
