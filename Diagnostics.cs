using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MCM.Abstractions.Base.Global;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

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

        // --- Live-arc census (Stage 1 measurement: who is the live-arc guard turning away?) ---
        private static readonly Dictionary<string, int> _liveArcCensus = new Dictionary<string, int>();
        private static LiveArcAggregate _liveArcAggregate = new LiveArcAggregate();

        // --- Friendly block passthrough ---
        private static int _friendlyBlocksNeutralised;

        // --- Shield rotation ---
        private static int _rotationSwaps;
        private static int _rotationShieldlessFront;
        private static int _rotationFormations;
        private static int _rotationSkippedDetached;
        private static int _rotationErrors;
        private static int _rotationSweepsWithSwaps;
        private static int _rotationMaxSwapsInOneSweep;

        /// <summary>
        /// Keyed by a formatted (order, spacing, interval, eligible) string so distinct combinations
        /// are counted once with a hit count — NOT one entry per sweep, which runs 2x/second and
        /// would be a log storm.
        /// </summary>
        private static readonly Dictionary<string, int> _formationCensus = new Dictionary<string, int>();

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
            _liveArcCensus.Clear();
            _liveArcAggregate = new LiveArcAggregate();
            _friendlyBlocksNeutralised = 0;
            _rotationSwaps = 0;
            _rotationShieldlessFront = 0;
            _rotationFormations = 0;
            _rotationSkippedDetached = 0;
            _rotationErrors = 0;
            _rotationSweepsWithSwaps = 0;
            _rotationMaxSwapsInOneSweep = 0;
            _formationCensus.Clear();
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

        /// <summary>
        /// One live-arc rejection. Reads rank/weapon off the attacker and rank off the victim here
        /// rather than at the call site, so the [MBCallback] patch stays a one-liner.
        ///
        /// Counted unconditionally, matching RecordWindup: only the report WRITE is gated on the
        /// DiagnosticLogging setting. The path is already filtered to friendly collisions by the
        /// patch's own `enemy` guard, so the volume is bounded (~1071/mission as measured 2026-07-10).
        ///
        /// Fix 1 (alternative attacks): AttackCollisionData.IsAlternativeAttack is read and passed
        /// through into both the key and the aggregate, NOT filtered here — the exclusion from the
        /// §5 percentage rows is a read-time decision made in LiveArcAggregate, so the raw census
        /// still shows every alternative attack that reached this guard.
        ///
        /// Fix 2 (relative position): the victim's file/rank is read with the SAME idiom and the
        /// SAME -1-detached contract as the attacker's, inside the same try/catch, so a failed read
        /// degrades to "unknown" rather than dropping the sample.
        /// </summary>
        internal static void RecordLiveArc(Agent attacker, Agent victim, ref AttackCollisionData cd)
        {
            if (attacker == null) return;

            int attackerFileIndex = -1;
            int rankIndex = -1;
            int victimFileIndex = -1;
            int victimRankIndex = -1;
            string weaponClassName = null;
            int weaponLength = 0;

            try
            {
                // Same idiom (and the same -1 detached contract) as ShieldRotationBehavior.
                attacker.GetFormationFileAndRankInfo(out attackerFileIndex, out rankIndex);

                if (victim != null)
                    victim.GetFormationFileAndRankInfo(out victimFileIndex, out victimRankIndex);

                // Same idiom as AttackGatePatches.CanSwing: null when unarmed.
                WeaponComponentData weapon = attacker.WieldedWeapon.CurrentUsageItem;
                if (weapon != null)
                {
                    weaponClassName = weapon.WeaponClass.ToString();
                    // WeaponLength is an int on v1.4.7. If the build reports a type mismatch here,
                    // wrap it: (int)weapon.WeaponLength — do not change the census signature.
                    weaponLength = weapon.WeaponLength;
                }
            }
            catch
            {
                // A diagnostic must never take the game down, and this runs per collision. Record
                // what we managed to read rather than dropping the sample entirely; a key with
                // wpn=unarmed / rel=unknown is still a countable event.
            }

            string relativePosition = LiveArcCensus.RelativePosition(
                attackerFileIndex, rankIndex, victimFileIndex, victimRankIndex);
            bool isAlternativeAttack = cd.IsAlternativeAttack;

            string key = LiveArcCensus.BuildKey(
                rankIndex,
                weaponClassName,
                weaponLength,
                cd.StrikeType,               // raw; LiveArcCensus.StrikeLabel maps it three ways
                cd.AttackDirection.ToString(),
                isAlternativeAttack,
                relativePosition);

            int n;
            _liveArcCensus.TryGetValue(key, out n);
            _liveArcCensus[key] = n + 1;

            _liveArcAggregate.Add(rankIndex, weaponClassName, weaponLength, cd.StrikeType,
                isAlternativeAttack, relativePosition);
        }

        internal static void RecordFriendlyBlockNeutralised()
        {
            _friendlyBlocksNeutralised++;
        }

        internal static void RecordShieldSwap()
        {
            _rotationSwaps++;
        }

        /// <summary>A man holding rank 0 (front rank / outer ring) with no shield — the thing we exist to fix.</summary>
        internal static void RecordShieldlessFront()
        {
            _rotationShieldlessFront++;
        }

        /// <summary>
        /// Counted separately because it is the feature's most likely silent failure: if melee detaches
        /// men from their formation, every candidate is skipped and the result is indistinguishable from
        /// "the feature never fired". This number tells the two apart.
        /// </summary>
        internal static void RecordRotationSkippedDetached()
        {
            _rotationSkippedDetached++;
        }

        internal static void RecordRotationFormation()
        {
            _rotationFormations++;
        }

        /// <summary>An exception escaped Sweep() and was caught by OnMissionTick's catch block.</summary>
        internal static void RecordRotationError()
        {
            _rotationErrors++;
        }

        /// <summary>
        /// Called once per formation per sweep with the total swaps that sweep performed. Discriminates
        /// a settling formation (swaps taper to zero) from churn (the formation never stops swapping).
        /// </summary>
        internal static void RecordFormationSweepResult(int swapsThisSweep)
        {
            if (swapsThisSweep > 0)
                _rotationSweepsWithSwaps++;

            if (swapsThisSweep > _rotationMaxSwapsInOneSweep)
                _rotationMaxSwapsInOneSweep = swapsThisSweep;
        }

        /// <summary>
        /// Every formation the sweep examines, whether or not it passes the eligibility gate — the
        /// point is to see what we SKIP. unitCount is deliberately excluded from the key: it changes
        /// as men die and would explode the dictionary, so it is not tracked at all.
        /// </summary>
        internal static void RecordFormationCensus(string orderName, int unitSpacing, float interval, int unitCount, bool eligible)
        {
            string key = string.Format(CultureInfo.InvariantCulture,
                "{0,-12} spacing={1} interval={2:0.000} eligible={3}",
                orderName, unitSpacing, interval, eligible ? 1 : 0);

            int n;
            _formationCensus.TryGetValue(key, out n);
            _formationCensus[key] = n + 1;
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
            Append("[PSW]  config: " + DescribeConfig());
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
            Append(string.Format(CultureInfo.InvariantCulture,
                "[PSW]  shield rotation     : {0} swaps across {1} formation-sweeps ({2} shieldless front-rankers seen, {3} skipped as detached){4}",
                _rotationSwaps, _rotationFormations, _rotationShieldlessFront, _rotationSkippedDetached,
                _rotationSwaps == 0 ? "   <-- FEATURE NEVER FIRED" : ""));

            bool churning = _rotationSweepsWithSwaps * 2 > _rotationFormations && _rotationFormations > 20;
            Append(string.Format(CultureInfo.InvariantCulture,
                "[PSW]      churn check: {0} of {1} formation-sweeps emitted swaps (max {2} in one sweep){3}",
                _rotationSweepsWithSwaps, _rotationFormations, _rotationMaxSwapsInOneSweep,
                churning ? "   <-- CHURNING? formation is not settling" : ""));

            if (_rotationErrors > 0)
                Append(string.Format(CultureInfo.InvariantCulture,
                    "[PSW]      errors caught: {0}   <-- SWEEP IS THROWING", _rotationErrors));

            Append("[PSW]      formation census:");
            if (_formationCensus.Count == 0)
            {
                Append("[PSW]        (no formations seen at all)");
            }
            else
            {
                foreach (var kv in _formationCensus)
                    Append(string.Format(CultureInfo.InvariantCulture,
                        "[PSW]        {0}  x{1}", kv.Key, kv.Value));
            }

            Append("[PSW]      live-arc aggregate (pre-registered §5 answers):");
            int windupLiveArc;
            _windupRejects.TryGetValue("live-arc", out windupLiveArc);
            foreach (var line in _liveArcAggregate.Render(windupLiveArc))
                Append("[PSW]        " + line);

            Append("[PSW]      live-arc census (who the guard turned away):");
            if (_liveArcCensus.Count == 0)
            {
                Append("[PSW]        (no live-arc rejections seen at all)");
            }
            else
            {
                foreach (var kv in _liveArcCensus)
                    Append(string.Format(CultureInfo.InvariantCulture,
                        "[PSW]        {0}  x{1}", kv.Key, kv.Value));
            }

            Append("[PSW] ========================");
        }

        /// <summary>
        /// The settings the mission actually ran under, stamped into its own report. Every toggle is
        /// RequireRestart=false, so a test campaign flips them between missions into one appended
        /// log; without this the numbers cannot be attributed to a configuration after the fact.
        /// </summary>
        private static string DescribeConfig()
        {
            var s = GlobalSettings<Settings>.Instance;
            if (s == null) return "<unresolved>";

            return string.Format(CultureInfo.InvariantCulture,
                "enabled={0} windup={1} cramped={2} blockPass={3} threshold={4:0.00} crowdedDur={5:0.0} rotate={6} rotInterval={7:0.0}",
                s.Enabled ? 1 : 0,
                s.WindupTransparency ? 1 : 0,
                s.CrampedAttackGating ? 1 : 0,
                s.FriendlyBlockPassthrough ? 1 : 0,
                s.WindupThreshold,
                s.CrowdedDuration,
                s.ShieldRotation ? 1 : 0,
                s.RotationInterval);
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
