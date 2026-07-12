using System.Collections.Generic;
using ProperShieldWalls;
using Xunit;

namespace ProperShieldWalls.Tests
{
    public class ShieldRotationTests
    {
        /// <summary>Applies the planned swaps to a copy and returns the resulting layout.</summary>
        private static bool[] Apply(bool[] input)
        {
            var result = (bool[])input.Clone();
            foreach (var swap in ShieldRotation.PlanFileSwaps(input))
            {
                bool tmp = result[swap.A];
                result[swap.A] = result[swap.B];
                result[swap.B] = tmp;
            }
            return result;
        }

        [Fact]
        public void AlreadySorted_EmitsNoSwaps()
        {
            var plan = ShieldRotation.PlanFileSwaps(new[] { true, true, false, false });
            Assert.Empty(plan);
        }

        [Fact]
        public void ShieldlessAtFront_IsSwappedWithShieldedBehind()
        {
            // rank0 shieldless, rank1 shielded -> they must trade places.
            Assert.Equal(new[] { true, false }, Apply(new[] { false, true }));
        }

        [Fact]
        public void FullyReversed_PartitionsCompletely()
        {
            Assert.Equal(new[] { true, true, false, false }, Apply(new[] { false, false, true, true }));
        }

        [Fact]
        public void ShieldedManDeepInFile_ReachesFrontInOnePlan()
        {
            // The whole point: vanilla bubbles one rank per 0.5s tick. We do it in one pass.
            Assert.Equal(new[] { true, false, false, false }, Apply(new[] { false, false, false, true }));
        }

        [Fact]
        public void AllShielded_EmitsNoSwaps()
        {
            Assert.Empty(ShieldRotation.PlanFileSwaps(new[] { true, true, true }));
        }

        [Fact]
        public void NoneShielded_EmitsNoSwaps()
        {
            Assert.Empty(ShieldRotation.PlanFileSwaps(new[] { false, false, false }));
        }

        [Fact]
        public void SingleUnit_EmitsNoSwaps()
        {
            Assert.Empty(ShieldRotation.PlanFileSwaps(new[] { false }));
        }

        [Fact]
        public void Empty_EmitsNoSwaps()
        {
            Assert.Empty(ShieldRotation.PlanFileSwaps(new bool[0]));
        }

        [Fact]
        public void Null_EmitsNoSwaps()
        {
            Assert.Empty(ShieldRotation.PlanFileSwaps(null));
        }

        [Fact]
        public void PlanIsIdempotent_ReplanningAfterApplyEmitsNothing()
        {
            var settled = Apply(new[] { false, true, false, true });
            Assert.Empty(ShieldRotation.PlanFileSwaps(settled));
        }

        [Fact]
        public void EmitsMinimalSwaps_OneSwapPerMisplacedShieldedMan()
        {
            // Two shielded men behind two shieldless -> exactly two swaps, not four.
            var plan = ShieldRotation.PlanFileSwaps(new[] { false, false, true, true });
            Assert.Equal(2, plan.Count);
        }
    }
}
