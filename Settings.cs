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
            HintText = "Master switch. Turn off to restore vanilla melee collision entirely.")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool Enabled { get; set; } = true;

        [SettingPropertyBool("Windup Transparency", Order = 1, RequireRestart = false,
            HintText = "A friendly hit during your attack's wind-up costs nothing: no stun, no bounce, no shield clang. The swing continues.")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool WindupTransparency { get; set; } = true;

        [SettingPropertyBool("Cramped Attack Gating", Order = 2, RequireRestart = false,
            HintText = "When packed in among friendlies, horizontal swings become overheads. Requires Windup Transparency to be on.")]
        [SettingPropertyGroup("General", GroupOrder = 0)]
        public bool CrampedAttackGating { get; set; } = true;

        [SettingPropertyFloatingInteger("Windup Threshold", 0f, 0.6f, "#0.00",
            Order = 3, RequireRestart = false,
            HintText = "Attack progress (0-1) below which a friendly hit counts as wind-up. Higher = more of the swing passes through allies.")]
        [SettingPropertyGroup("Tuning", GroupOrder = 1)]
        public float WindupThreshold { get; set; } = 0.25f;

        [SettingPropertyFloatingInteger("Crowded Duration", 0.5f, 6f, "#0.0",
            Order = 4, RequireRestart = false,
            HintText = "Seconds an agent stays flagged as crowded after its wind-up clips a friendly.")]
        [SettingPropertyGroup("Tuning", GroupOrder = 1)]
        public float CrowdedDuration { get; set; } = 2f;

        [SettingPropertyBool("Diagnostic Logging", Order = 0, RequireRestart = false,
            HintText = "Log every friendly hit: strike type, hit-result flags, attack progress. Use to tune Windup Threshold. Very noisy.")]
        [SettingPropertyGroup("Debug", GroupOrder = 99)]
        public bool DiagnosticLogging { get; set; } = false;
    }
}
