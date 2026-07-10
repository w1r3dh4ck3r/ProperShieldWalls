using System;
using System.Reflection;
using HarmonyLib;
using MCM.Abstractions.Base.Global;
// using ProperShieldWalls.Behaviours;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls
{
    public class SubModule : MBSubModuleBase
    {
        private Harmony _harmony;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            _harmony = new Harmony("com.propershieldwalls.patch");
            int applied = 0, failed = 0;

            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (type.GetCustomAttribute<HarmonyPatch>() == null) continue;
                try
                {
                    _harmony.CreateClassProcessor(type).Patch();
                    applied++;
                    Log($"[PSW] Patched: {type.Name}");
                }
                catch (Exception ex)
                {
                    failed++;
                    Log($"[PSW] FAILED to patch {type.Name}: {ex.Message}");
                }
            }

            Log($"[PSW] Proper Shield Walls v2.0.0 loaded. Patches: {applied} OK, {failed} failed.");
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            // mission.AddMissionBehavior(new CrowdStateBehavior());
        }

        protected override void OnSubModuleUnloaded()
        {
            _harmony?.UnpatchAll("com.propershieldwalls.patch");
            base.OnSubModuleUnloaded();
        }

        internal static void Log(string message)
        {
            Debug.Print(message);
            var settings = GlobalSettings<Settings>.Instance;
            if (settings != null && settings.DiagnosticLogging)
                InformationManager.DisplayMessage(new InformationMessage(message, Colors.Cyan));
        }
    }
}
