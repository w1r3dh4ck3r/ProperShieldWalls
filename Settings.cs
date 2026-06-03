using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;

namespace ProperShieldWalls
{
    public class Settings : AttributeGlobalSettings<Settings>
    {
        public override string Id          => "ProperShieldWalls";
        public override string DisplayName => "Proper Shield Walls";
        public override string FolderName  => "ProperShieldWalls";
        public override string FormatType  => "json";

        [SettingPropertyBool("Enabled", Order = 0, RequireRestart = false,
            HintText = "Enable othismos shield wall contact mechanics.")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool Enabled { get; set; } = true;

        [SettingPropertyFloatingInteger("Engagement Distance", 2f, 10f, "#0.0",
            Order = 1, RequireRestart = false,
            HintText = "Formation centre-to-centre distance (metres) at which two opposing shield walls lock. Lower = tighter trigger.")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public float EngagementDistance { get; set; } = 5f;

        [SettingPropertyInteger("Min Agents Per Side", 1, 20,
            Order = 2, RequireRestart = false,
            HintText = "Minimum agents in each formation before othismos can engage.")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public int MinAgentsPerSide { get; set; } = 3;

        [SettingPropertyFloatingInteger("Stamina Break Threshold", 0f, 1f, "#0.00",
            Order = 3, RequireRestart = false,
            HintText = "Average front-rank stamina ratio (0–1) below which the engagement breaks. Requires StaminaSystem mod. Default 0.25 = 25% stamina.")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public float StaminaBreakThreshold { get; set; } = 0.25f;

        [SettingPropertyBool("Enable Debug Messages", Order = 0, RequireRestart = false,
            HintText = "Show in-game messages for othismos state transitions (PreLock, Locked, Breaking).")]
        [SettingPropertyGroup("Debug", GroupOrder = 99)]
        public bool EnableDebug { get; set; } = false;
    }
}
