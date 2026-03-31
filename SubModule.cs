using System;
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

            try
            {
                _harmony = new Harmony("com.propershieldwalls.patch");
                _harmony.PatchAll();
                Log("Proper Shield Walls v1.0.0 loaded.");
            }
            catch (Exception ex)
            {
                Log($"ERROR loading patches: {ex.Message}\n{ex.StackTrace}");
            }
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            InformationManager.DisplayMessage(
                new InformationMessage(
                    "[PSW] Proper Shield Walls active — polearm thrusts pass through allies",
                    Colors.Green
                )
            );
        }

        protected override void OnSubModuleUnloaded()
        {
            _harmony?.UnpatchAll("com.propershieldwalls.patch");
            base.OnSubModuleUnloaded();
        }

        internal static void Log(string message)
        {
            TaleWorlds.Library.Debug.Print($"[ProperShieldWalls] {message}", 0,
                TaleWorlds.Library.Debug.DebugColor.Yellow);
        }
    }
}
