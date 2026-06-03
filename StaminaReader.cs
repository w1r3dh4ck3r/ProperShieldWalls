using System;
using System.Reflection;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Behaviours
{
    // Soft dependency on StaminaSystem mod — PSW works without it; stamina-break is disabled if absent.
    // Uses reflection so StaminaSystem.dll is never required at compile or load time.
    internal static class StaminaReader
    {
        private static readonly Type       _behaviorType;
        private static readonly MethodInfo _getStamina; // static GetStaminaForAgent(Agent) : float
        private static readonly MethodInfo _calcMax;    // instance CalculateMaxStamina(Agent) : float

        static StaminaReader()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name != "StaminaSystem") continue;
                _behaviorType = asm.GetType("StaminaSystem.StaminaMissionBehavior");
                if (_behaviorType == null) break;
                var flags = BindingFlags.Public | BindingFlags.NonPublic;
                _getStamina = _behaviorType.GetMethod("GetStaminaForAgent",
                    flags | BindingFlags.Static, null, new[] { typeof(Agent) }, null);
                _calcMax = _behaviorType.GetMethod("CalculateMaxStamina",
                    flags | BindingFlags.Instance, null, new[] { typeof(Agent) }, null);
                break;
            }
            SubModule.Log($"[PSW] StaminaReader: {(_behaviorType != null ? "StaminaSystem found" : "StaminaSystem not found — stamina-break disabled")}");
        }

        internal static bool IsAvailable => _getStamina != null;

        // Finds the StaminaMissionBehavior instance from the running mission.
        internal static MissionBehavior FindInstance()
        {
            if (_behaviorType == null || Mission.Current == null) return null;
            foreach (var b in Mission.Current.MissionBehaviors)
                if (b.GetType() == _behaviorType) return b;
            return null;
        }

        // Returns stamina as 0–1 ratio. Falls back to 1.0 (full) if StaminaSystem absent or on error.
        internal static float GetStaminaRatio(Agent agent, MissionBehavior instance)
        {
            if (!IsAvailable || agent == null) return 1f;
            try
            {
                float current = (float)_getStamina.Invoke(null, new object[] { agent });
                float max = (_calcMax != null && instance != null)
                    ? (float)_calcMax.Invoke(instance, new object[] { agent })
                    : 100f;
                if (max <= 0f) return 1f;
                float ratio = current / max;
                return ratio < 0f ? 0f : (ratio > 1f ? 1f : ratio);
            }
            catch { return 1f; }
        }
    }
}
