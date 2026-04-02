using System;
using System.Reflection;
using HarmonyLib;
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
            int applied = 0;
            int failed = 0;

            // Patch each type individually so one failure doesn't block others
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (type.GetCustomAttribute<HarmonyPatch>() == null)
                    continue;

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

            Log($"[PSW] Proper Shield Walls v1.1.0 loaded. Patches: {applied} OK, {failed} failed.");
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            InformationManager.DisplayMessage(
                new InformationMessage(
                    "[PSW] Proper Shield Walls active — melee weapons pass through allies",
                    Colors.Green
                )
            );
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);
            mission.AddMissionBehavior(new ShieldWallBehaviour());
        }

        protected override void OnSubModuleUnloaded()
        {
            _harmony?.UnpatchAll("com.propershieldwalls.patch");
            base.OnSubModuleUnloaded();
        }

        internal static void Log(string message)
        {
            Debug.Print(message);
        }
    }
}
