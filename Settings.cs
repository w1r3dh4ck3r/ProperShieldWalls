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

        [SettingPropertyBool(
            "Enabled",
            Order = 0,
            RequireRestart = false,
            HintText = "Master toggle. When enabled, polearm/spear thrusts pass through friendly agents.")]
        [SettingPropertyGroup("General")]
        public bool Enabled { get; set; } = true;

        [SettingPropertyBool(
            "Player Only",
            Order = 1,
            RequireRestart = false,
            HintText = "If true, only the player's thrusts pass through allies. If false, all troops benefit (AI included).")]
        [SettingPropertyGroup("General")]
        public bool AffectPlayerOnly { get; set; } = false;

        [SettingPropertyBool(
            "Enable Debug Messages",
            Order = 0,
            RequireRestart = false,
            HintText = "Show in-game messages when a friendly polearm hit is bypassed.")]
        [SettingPropertyGroup("Debug")]
        public bool EnableDebug { get; set; } = false;
    }
}
