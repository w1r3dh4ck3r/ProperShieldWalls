using System;
using System.Globalization;

namespace ProperShieldWalls
{
    /// <summary>
    /// Bucketing and key construction for the live-arc census.
    ///
    /// Pure by design: no TaleWorlds and no MCM types, because ProperShieldWalls.Tests source-links
    /// this file and its csproj requires source-linked files to stay game-free. The adapter that
    /// reads Agent/AttackCollisionData lives in Diagnostics, which cannot be unit tested.
    ///
    /// Everything is bucketed rather than recorded raw. The key IS the dictionary identity, so an
    /// unbucketed field (a raw weapon length, a raw rank) would produce a near-unique key per event
    /// and turn an aggregate census back into the per-event log storm it exists to avoid.
    /// </summary>
    internal static class LiveArcCensus
    {
        /// <summary>
        /// Bucket for a live-arc collision between two agents in DIFFERENT formations. File/rank
        /// indices from GetFormationFileAndRankInfo are per-formation, so comparing them across
        /// formations is a coincidence, not a position -- see Diagnostics.RecordLiveArc (F3).
        /// </summary>
        internal const string OtherFormationBucket = "other-formation";

        /// <summary>
        /// Rank 0/1/2 are reported exactly because they are the ones the feature is about; deeper
        /// ranks collapse. A detached unit reports -1 from GetFormationFileAndRankInfo and must NOT
        /// be silently bucketed as rank 0 — that would invent front-rankers that do not exist.
        /// </summary>
        internal static string RankBucket(int rankIndex)
        {
            if (rankIndex < 0) return "detached";
            if (rankIndex == 0) return "0";
            if (rankIndex == 1) return "1";
            if (rankIndex == 2) return "2";
            return "3+";
        }

        /// <summary>
        /// Reach buckets. The boundary that matters is ~200: below it a weapon cannot plausibly
        /// reach the enemy front rank from rank 2 over roughly 1 m of rank spacing.
        /// </summary>
        internal static string LengthBucket(int weaponLength)
        {
            if (weaponLength < 120) return "<120";
            if (weaponLength < 200) return "120-199";
            if (weaponLength < 280) return "200-279";
            return "280+";
        }

        /// <summary>
        /// Mirrors the three-way mapping already used by WindupTransparencyPatch.Describe. It must
        /// stay three-way: folding Invalid into Swing would corrupt the spec's "majority Swing"
        /// decision-rule row, which is one of the four outcomes that decide what Stage 2 becomes.
        /// </summary>
        internal static string StrikeLabel(int strikeType)
        {
            if (strikeType == 1) return "Thrust";
            if (strikeType == 0) return "Swing";
            return "Invalid";
        }

        internal static string BuildKey(
            int rankIndex, string weaponClassName, int weaponLength,
            int strikeType, string attackDirection,
            bool isAlternativeAttack, string relativePosition)
        {
            string weapon = string.IsNullOrEmpty(weaponClassName) ? "unarmed" : weaponClassName;
            string direction = string.IsNullOrEmpty(attackDirection) ? "?" : attackDirection;
            string rel = string.IsNullOrEmpty(relativePosition) ? "unknown" : relativePosition;

            return string.Format(
                CultureInfo.InvariantCulture,
                "rank={0,-8} wpn={1,-20} len={2,-8} strike={3,-7} dir={4} alt={5} rel={6}",
                RankBucket(rankIndex),
                weapon,
                LengthBucket(weaponLength),
                StrikeLabel(strikeType),
                direction,
                isAlternativeAttack ? 1 : 0,
                rel);
        }

        /// <summary>
        /// Buckets the victim's position relative to the attacker, using the SAME
        /// GetFormationFileAndRankInfo idiom (and -1-means-detached contract) as the attacker side.
        ///
        /// Honesty check: same-file-and-lower-rank is a PARTIAL discriminator for "the victim was
        /// in front of the attacker's blade" -- it says nothing about facing, so a victim standing
        /// shoulder-to-shoulder but turned sideways still buckets as "front". It does not settle
        /// whether forward transparency would actually have helped; it only narrows the population
        /// worth asking that question about.
        /// </summary>
        internal static string RelativePosition(int attackerFile, int attackerRank, int victimFile, int victimRank)
        {
            if (attackerFile < 0 || attackerRank < 0 || victimFile < 0 || victimRank < 0) return "unknown";
            if (attackerFile != victimFile) return "other-file";
            if (victimRank < attackerRank) return "front";
            if (victimRank == attackerRank) return "same-rank";
            return "behind";
        }

        /// <summary>
        /// True for any weapon class whose name contains "Polearm" -- OneHandedPolearm,
        /// TwoHandedPolearm, and LowGripPolearm all match the substring test.
        /// Javelin is deliberately NOT counted here: what we are measuring is reach, which a javelin lacks.
        /// </summary>
        internal static bool IsPolearmClass(string weaponClassName)
        {
            if (string.IsNullOrEmpty(weaponClassName)) return false;
            return weaponClassName.IndexOf("Polearm", StringComparison.Ordinal) >= 0;
        }
    }
}
