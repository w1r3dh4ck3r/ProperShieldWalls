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
            int rankIndex, string weaponClassName, int weaponLength, int strikeType, string attackDirection)
        {
            string weapon = string.IsNullOrEmpty(weaponClassName) ? "unarmed" : weaponClassName;
            string direction = string.IsNullOrEmpty(attackDirection) ? "?" : attackDirection;

            return string.Format(
                CultureInfo.InvariantCulture,
                "rank={0,-8} wpn={1,-20} len={2,-8} strike={3,-7} dir={4}",
                RankBucket(rankIndex),
                weapon,
                LengthBucket(weaponLength),
                StrikeLabel(strikeType),
                direction);
        }
    }
}
