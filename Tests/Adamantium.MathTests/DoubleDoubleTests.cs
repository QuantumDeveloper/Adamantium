using System;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.MathTests
{
    /// <summary>What the pair is for, measured rather than restated: it must keep digits a double drops, and it must stay
    /// accurate through an iteration that feeds its own rounding error forward.</summary>
    [TestFixture]
    public class DoubleDoubleTests
    {
        // The plainest statement of the point: a double cannot hold 1 and 1e-20 at once, and loses the small one
        // completely. The pair keeps it, and hands it back on subtraction.
        [Test]
        public void ASumKeepsWhatADoubleWouldDrop()
        {
            Assert.That((1.0 + 1e-20) - 1.0, Is.EqualTo(0.0), "a double is expected to lose it - that is the premise");

            var sum = DoubleDouble.Of(1.0) + DoubleDouble.Of(1e-20);
            Assert.That(sum.Hi, Is.EqualTo(1.0));
            Assert.That((double)(sum - DoubleDouble.Of(1.0)), Is.EqualTo(1e-20).Within(1e-36));
        }

        // 1e16 has a step of 2, so adding 1 lands exactly between two doubles and a plain sum throws the 1 away. Here it
        // has to survive as the trailing part, unrounded.
        [Test]
        public void TheTrailingPartHoldsTheExactRemainder()
        {
            var sum = DoubleDouble.Of(1e16) + DoubleDouble.Of(1.0);

            Assert.That(sum.Hi, Is.EqualTo(1e16));
            Assert.That(sum.Lo, Is.EqualTo(1.0));
        }

        // A product's discarded bits cannot be recovered from the product, only from a fused multiply-add. 0.1 is not
        // exact in binary, so squaring it must leave a residue - a zero here means the multiply is rounding silently and
        // the pair is only exact around addition.
        [Test]
        public void AProductKeepsItsResidue()
        {
            var square = DoubleDouble.Of(0.1) * DoubleDouble.Of(0.1);

            Assert.That(square.Hi, Is.EqualTo(0.1 * 0.1));
            Assert.That(square.Lo, Is.Not.EqualTo(0.0), "the multiply threw its residue away");
        }

        /// <summary>The case the type was written for. z = z² + c is chaotic here, so one step's rounding is amplified by
        /// every step after it; c = -1.5 is exact in both binary and decimal, so the two arithmetics iterate the SAME
        /// number and the comparison measures precision alone. Decimal carries ~28 digits and stands in for the truth.</summary>
        [Test]
        public void AChaoticIterationStaysAccurateWhereADoubleDrifts()
        {
            const int steps = 20;
            const decimal c = -1.5m;

            var truth = 0m;
            var plain = 0.0;
            var pair = DoubleDouble.Of(0.0);

            for (var i = 0; i < steps; i++)
            {
                truth = truth * truth + c;
                plain = plain * plain + (double)c;
                pair = pair * pair + DoubleDouble.Of((double)c);
            }

            var plainError = Math.Abs(plain - (double)truth);
            var pairError = Math.Abs((double)pair - (double)truth);

            // No absolute bound on the pair: decimal carries ~28 digits, so it cannot witness accuracy far past its own.
            // The comparison against the double is the real claim, and it is not limited that way.
            Assert.That(plainError, Is.GreaterThan(1e-15), "a double is expected to have drifted by now - that is the premise");
            Assert.That(pairError, Is.LessThan(plainError / 1e6));
        }

        // Ordinary arithmetic still has to come out right - a type that is only accurate is no use if it is also wrong.
        [Test]
        public void OrdinaryArithmeticAgreesWithADouble()
        {
            var sum = DoubleDouble.Of(2.5) + DoubleDouble.Of(0.25);
            var difference = DoubleDouble.Of(2.5) - DoubleDouble.Of(0.25);
            var product = DoubleDouble.Of(2.5) * DoubleDouble.Of(0.25);

            Assert.That((double)sum, Is.EqualTo(2.75));
            Assert.That((double)difference, Is.EqualTo(2.25));
            Assert.That((double)product, Is.EqualTo(0.625));
        }
    }
}
