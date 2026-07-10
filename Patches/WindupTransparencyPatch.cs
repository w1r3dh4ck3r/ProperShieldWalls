using System;
using System.Globalization;
using HarmonyLib;
using MCM.Abstractions.Base.Global;
using TaleWorlds.MountAndBlade;

namespace ProperShieldWalls.Patches
{
    /// <summary>
    /// A friendly hit landing during an attack's wind-up costs nothing: no friendly-fire stun,
    /// no Bounced/Staggered weapon reaction, no shield clang, no blow. The sweep continues.
    ///
    /// Mechanism (verified against the v1.4.7 decompile). Mission.MeleeHitCallback wraps its
    /// entire penalty block in `if (colReaction != MeleeCollisionReaction.ContinueChecking)`
    /// (Mission.cs:5305). Everything that stops an attack on a friendly contact lives inside it:
    ///   - the attacker's friendly-fire stun (`AttackerStunPeriod = StunPeriodAttackerFriendlyFire`)
    ///   - MissionCombatMechanicsHelper.DecideWeaponCollisionReaction (called at Mission.cs:5376):
    ///       * IsColliderAgent && StrikeType==Thrust && HitWithStartOfTheAnimation -> Staggered
    ///       * InflictedDamage <= 0                                                -> Bounced
    ///         (a friendly hit ALWAYS lands here: damage is zeroed just above, at Mission.cs:5360)
    /// Setting ContinueChecking and returning true makes the original skip that whole block, so
    /// none of those reactions is ever assigned. Vanilla itself uses this path to let kicks and
    /// bashes (IsAlternativeAttack) pass through friendlies.
    ///
    /// DO NOT return false. A prefix returning false suppresses OTHER mods' prefixes on this same
    /// method — RBMCombat, RBMAI, RealisticCombatSounds and XorberaxLegacy all patch it — and would
    /// also skip the method's trailing sound-alarm block.
    ///
    /// Priority.High(600) sorts this ahead of RBMCombat's prefix (Normal=400), which rewrites
    /// collisionData for CollidedWithShieldOnBack. Both return true, so the ContinueChecking we
    /// write survives into the original.
    ///
    /// NOTE: Mission.MeleeHitCallback carries [MBCallback]. Patching Agent.OnAIInputSet (also
    /// [MBCallback]) folds every character into a spike; patching this one has not, but that is
    /// an observation, not a guarantee. If hit reactions misbehave, suspect this patch first.
    /// </summary>
    [HarmonyPatch(typeof(Mission), "MeleeHitCallback")]
    internal static class WindupTransparencyPatch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.High)]
        public static bool Prefix(
            ref AttackCollisionData collisionData,
            Agent attacker,
            Agent victim,
            ref MeleeCollisionReaction colReaction)
        {
            try
            {
                var settings = GlobalSettings<Settings>.Instance;
                if (settings == null || !settings.Enabled || !settings.WindupTransparency) return true;

                string rejectedBecause = Classify(ref collisionData, attacker, victim, settings);

                // Counted for every agent, so the mission report measures the feature rather than
                // just the player's own swings. "world-hit" and "enemy" are the overwhelming
                // majority and say nothing about friendly handling, so they stay out of the tally.
                if (rejectedBecause != "world-hit" && rejectedBecause != "enemy")
                    Diagnostics.RecordWindup(rejectedBecause);

                // Log before acting, and log rejections too. A hit turned away at a guard used to
                // leave no trace, which made "we never saw the collision" and "we saw it and
                // declined it" look identical in a battle log. Per-hit lines are scoped to the
                // player's own attacks so one skirmish produces a readable file rather than a storm.
                if (attacker != null && attacker.IsMainAgent && Diagnostics.Enabled)
                    Diagnostics.Write(Describe(ref collisionData, attacker, victim, rejectedBecause));

                if (rejectedBecause != null) return true;

                colReaction = MeleeCollisionReaction.ContinueChecking;

                var mission = Mission.Current;
                if (mission != null)
                    CrowdState.Stamp(attacker.Index, mission.CurrentTime, settings.CrowdedDuration);

                return true;
            }
            catch (Exception ex)
            {
                // Key = patch name + exception type, not ex.Message: this runs on every melee
                // collision, so a repeating fault must collapse into one throttled bucket instead
                // of logging (or allocating a dictionary entry) once per collision forever.
                SubModule.LogErrorThrottled(
                    "WindupTransparencyPatch:" + ex.GetType().Name,
                    "[PSW] WindupTransparencyPatch error: " + ex.Message);
                return true;
            }
        }

        /// <summary>
        /// Returns null when the hit should be made transparent, otherwise the name of the guard
        /// that turned it away. The names are written verbatim into the diagnostic log.
        /// </summary>
        private static string Classify(
            ref AttackCollisionData collisionData, Agent attacker, Agent victim, Settings settings)
        {
            if (attacker == null || victim == null) return "world-hit";
            if (!collisionData.IsColliderAgent) return "not-collider-agent";
            if (ReferenceEquals(attacker, victim)) return "self-hit";
            if (!victim.IsHuman) return "victim-not-human";

            // Team is a free managed field; IsFriendOf is a native call. Short-circuit on the
            // common case before paying for the interop.
            if (attacker.Team != victim.Team && !attacker.IsFriendOf(victim)) return "enemy";

            bool windup =
                (collisionData.CollisionHitResultFlags & CombatHitResultFlags.HitWithStartOfTheAnimation) != 0
                || collisionData.AttackProgress < settings.WindupThreshold;

            // A live strike arc: an ally in front still stops the blade.
            if (!windup) return "live-arc";

            return null;
        }

        private static string Describe(
            ref AttackCollisionData cd, Agent attacker, Agent victim, string rejectedBecause)
        {
            var mission = Mission.Current;
            float t = (mission != null) ? mission.CurrentTime : 0f;

            // Computed independently of Classify: the not-collider-agent guard rejects before the
            // friend check runs, so without this a reject line could not confirm the victim was an
            // ally — exactly the case the repro is meant to establish.
            string friend = "?";
            if (attacker != null && victim != null)
                friend = attacker.IsFriendOf(victim) ? "1" : "0";

            return string.Format(
                CultureInfo.InvariantCulture,
                "[PSW] t={0:0.00} dir={1} strike={2} prog={3:0.000} flags={4} collider={5} shieldBack={6} " +
                "blockedShield={7} result={8} altAttack={9} victim={10} friend={11} -> {12}",
                t,
                cd.AttackDirection,
                cd.StrikeType == 1 ? "Thrust" : (cd.StrikeType == 0 ? "Swing" : "Invalid"),
                cd.AttackProgress,
                cd.CollisionHitResultFlags,
                cd.IsColliderAgent ? 1 : 0,
                cd.CollidedWithShieldOnBack ? 1 : 0,
                cd.AttackBlockedWithShield ? 1 : 0,
                cd.CollisionResult,
                cd.IsAlternativeAttack ? 1 : 0,
                victim == null ? "none" : (victim.IsHuman ? "human" : "mount"),
                friend,
                rejectedBecause == null ? "BYPASS" : ("reject:" + rejectedBecause));
        }
    }
}
