using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MCM.Abstractions.Base.Global;

namespace ProperShieldWalls
{
    /// <summary>
    /// File-backed diagnostic sink and per-mission metrics.
    ///
    /// Debug.Print is captured by nothing on this machine, and the on-screen InformationManager
    /// feed scrolls away faster than it can be read mid-battle. The one instrument that has to
    /// survive a real fight therefore writes to disk.
    ///
    /// The counters exist because each of the mod's three features is SILENT when it works. A
    /// no-op looks exactly like a success. They are aggregated per feature rather than logged per
    /// event: MeleeHitCallback and OnAIInputSet both run at collision/tick rate, so a per-event
    /// line is a log storm, and a raw tick count ("13230 remaps") measures effort, not effect.
    /// </summary>
    internal static class Diagnostics
    {
        private const int MaxLinesPerMission = 400;

        /// <summary>
        /// Two remaps on the same agent closer together than this belong to the same swing.
        /// OnAIInputSet fires every AI decision tick, so without this every counter measures
        /// tick rate instead of behaviour.
        /// </summary>
        private const float RemapEventGapSeconds = 0.5f;

        private static string _path;
        private static int _lines;
        private static bool _capAnnounced;
        private static bool _sinkBroken;
        private static bool _pathAnnounced;

        // --- Cramped attack gating (AI only) ---
        private static int _remapTicks;
        private static int _remapEvents;
        private static float[] _lastRemapTime = new float[256];
        private static readonly HashSet<int> _remappedAgents = new HashSet<int>();

        // --- Wind-up transparency ---
        private static int _windupBypassed;
        private static readonly Dictionary<string, int> _windupRejects = new Dictionary<string, int>();

        // --- Friendly block passthrough ---
        private static int _friendlyBlocksNeutralised;

        internal static bool Enabled
        {
            get
            {
                var settings = GlobalSettings<Settings>.Instance;
                return settings != null && settings.DiagnosticLogging;
            }
        }

        internal static void Reset()
        {
            _lines = 0;
            _capAnnounced = false;
            _remapTicks = 0;
            _remapEvents = 0;
            _lastRemapTime = new float[256];
            _remappedAgents.Clear();
            _windupBypassed = 0;
            _windupRejects.Clear();
            _friendlyBlocksNeutralised = 0;
        }

        /// <summary>Counted unconditionally: these are ints, and gating them on a settings read would cost more.</summary>
        internal static void RecordRemap(int agentIndex, float now)
        {
            _remapTicks++;
            if (agentIndex < 0) return;

            EnsureCapacity(ref _lastRemapTime, agentIndex);
            if (now - _lastRemapTime[agentIndex] > RemapEventGapSeconds)
                _remapEvents++;

            _lastRemapTime[agentIndex] = now;
            _remappedAgents.Add(agentIndex);
        }

        internal static void RecordWindup(string rejectedBecause)
        {
            if (rejectedBecause == null)
            {
                _windupBypassed++;
                return;
            }

            int n;
            _windupRejects.TryGetValue(rejectedBecause, out n);
            _windupRejects[rejectedBecause] = n + 1;
        }

        internal static void RecordFriendlyBlockNeutralised()
        {
            _friendlyBlocksNeutralised++;
        }

        /// <summary>
        /// One report per mission, per feature. This is the artefact that answers "is it working";
        /// the per-hit lines above it only explain WHY when a number looks wrong.
        /// </summary>
        internal static void WriteMissionReport()
        {
            // Deliberately bypasses the per-hit line cap. The report is the whole point of the
            // instrument; a noisy battle must not be the reason it goes missing.
            Append("[PSW] ==== mission report ====");
            Append(string.Format(CultureInfo.InvariantCulture,
                "[PSW]  windup transparency : {0} friendly hits made transparent{1}",
                _windupBypassed, _windupBypassed == 0 ? "   <-- FEATURE NEVER FIRED" : ""));

            foreach (var kv in _windupRejects)
                Append(string.Format(CultureInfo.InvariantCulture,
                    "[PSW]      rejected {0,-20} x{1}", kv.Key, kv.Value));

            Append(string.Format(CultureInfo.InvariantCulture,
                "[PSW]  friendly blocks     : {0} neutralised (ally shield no longer halts the swing){1}",
                _friendlyBlocksNeutralised, _friendlyBlocksNeutralised == 0 ? "   <-- FEATURE NEVER FIRED" : ""));

            Append(string.Format(CultureInfo.InvariantCulture,
                "[PSW]  cramped gating (AI) : {0} swings remapped across {1} agents ({2} input ticks){3}",
                _remapEvents, _remappedAgents.Count, _remapTicks,
                _remapEvents == 0 ? "   <-- FEATURE NEVER FIRED" : ""));
            Append("[PSW] ========================");
        }

        /// <summary>
        /// Announces the resolved log path on-screen, once per session. The path comes from
        /// Environment.SpecialFolder.MyDocuments as the *game* process resolves it, which can be
        /// OneDrive-redirected independently of where the game keeps its Configs. If the file ever
        /// lands somewhere unexpected, an absent log would read as "the feature never fired"
        /// rather than "you are reading the wrong file". Say where it went instead of assuming.
        /// </summary>
        private static void AnnouncePathOnce()
        {
            if (_pathAnnounced) return;
            _pathAnnounced = true;
            SubModule.Log("[PSW] diagnostic log: " + (_path ?? "<unresolved>"));
        }

        /// <summary>Capped per-hit line. Use for anything that can fire once per collision or tick.</summary>
        internal static void Write(string line)
        {
            if (_lines >= MaxLinesPerMission)
            {
                if (_capAnnounced) return;
                _capAnnounced = true;
                line = string.Format(
                    "[PSW] per-hit log cap reached ({0} lines); counters keep running, report still follows.",
                    MaxLinesPerMission);
            }
            _lines++;
            Append(line);
        }

        /// <summary>Uncapped. Reserved for the bounded, once-per-mission report.</summary>
        private static void Append(string line)
        {
            if (_sinkBroken) return;

            try
            {
                if (_path == null)
                {
                    string dir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "Mount and Blade II Bannerlord");
                    Directory.CreateDirectory(dir);
                    _path = Path.Combine(dir, "PSW_diag.log");
                }

                File.AppendAllText(_path, line + Environment.NewLine, Encoding.UTF8);
                AnnouncePathOnce();
            }
            catch
            {
                // A diagnostic must never take the game down. Latch off rather than retry
                // once per collision for the rest of the mission.
                _sinkBroken = true;
            }
        }

        private static void EnsureCapacity(ref float[] buffer, int index)
        {
            if (index < buffer.Length) return;

            var grown = new float[CrowdState.ComputeNewSize(buffer.Length, index)];
            Array.Copy(buffer, grown, buffer.Length);
            buffer = grown;
        }
    }
}
