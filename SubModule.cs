using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MCM.Abstractions.Base.Global;
using ProperShieldWalls.Behaviours;
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
            mission.AddMissionBehavior(new CrowdStateBehavior());
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

        // How many times a given fault key is logged before it goes silent for the rest of the
        // session. Keeps a repeating fault from becoming an unthrottled per-tick/per-agent log
        // storm (the exact class of bug that previously caused ~100k/session assert-storm hitches).
        private const int ErrorThrottleCap = 3;

        // Keyed by fault identity (patch + exception type), not by the exception's Message text,
        // so that N different faults get N buckets but the SAME fault repeating every tick/agent
        // collapses into one bucket instead of growing this dictionary without bound.
        private static readonly Dictionary<string, int> _errorThrottleCounts = new Dictionary<string, int>();

        /// <summary>
        /// Error-path-only logging for catch blocks in hot per-tick/per-agent paths. Emits the
        /// first <see cref="ErrorThrottleCap"/> occurrences of a given <paramref name="key"/>
        /// normally, then one final "suppressed" line, then stays silent for that key for the
        /// rest of the session. The happy path (no exceptions) never calls this, so it costs
        /// nothing when nothing is wrong.
        ///
        /// Main-thread only: called exclusively from catch blocks inside Harmony patches on
        /// Agent.OnAIInputSet / MeleeHitCallback / MissionMainAgentController.ControlTick, all of
        /// which run on Bannerlord's main simulation thread. No lock is taken because nothing
        /// else can touch _errorThrottleCounts concurrently.
        /// </summary>
        internal static void LogErrorThrottled(string key, string message)
        {
            int count;
            _errorThrottleCounts.TryGetValue(key, out count);
            count++;
            _errorThrottleCounts[key] = count;

            if (count <= ErrorThrottleCap)
            {
                Log(message);
            }
            else if (count == ErrorThrottleCap + 1)
            {
                Log(string.Format("[PSW] further '{0}' errors suppressed for this session.", key));
            }
            // else: already announced suppression for this key; stay silent.
        }

        /// <summary>
        /// Clears the per-key error-log throttle counters. Called by CrowdStateBehavior at mission
        /// start and end. Must reset per mission: an error that storms and self-suppresses in one
        /// battle must not stay permanently silent for the rest of the session.
        /// </summary>
        internal static void ResetErrorThrottle()
        {
            _errorThrottleCounts.Clear();
        }
    }
}
