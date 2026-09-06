using System;

namespace Adamantium.Mathematics
{
    /// <summary>A real number carried as an unevaluated pair of doubles, <see cref="Hi"/> + <see cref="Lo"/>, where Lo is
    /// exactly what Hi could not hold. That buys roughly 32 significant digits against a double's 16.
    /// <para>It exists for iterations that feed their own rounding error forward. A deep-zoom fractal is the case that
    /// forced it: z = z² + c is chaotic, so a rounding error at one step is amplified by every step after it, and a few
    /// hundred steps of plain double arithmetic drift by ~1e-14 - wider than the whole visible frame at high zoom.</para>
    /// <para>Every operation here is exact to the pair. A sum keeps its own rounding residue, which is recoverable from
    /// the sum itself; a product's residue is not, and is recovered instead by splitting each factor into halves narrow
    /// enough that their products are exact. Without that the pair would be theatre - exact addition around an inexact
    /// multiply.</para></summary>
    public readonly struct DoubleDouble : IEquatable<DoubleDouble>
    {
        /// <summary>The leading part - the value a plain double would have held on its own.</summary>
        public readonly double Hi;

        /// <summary>The trailing part - exactly what <see cref="Hi"/> could not represent. Always far smaller than Hi;
        /// the true value is Hi + Lo, which is deliberately never evaluated.</summary>
        public readonly double Lo;

        /// <summary>Builds a pair from a leading and a trailing part. Callers normally want <see cref="Of"/> instead:
        /// this one trusts that <paramref name="lo"/> really is small relative to <paramref name="hi"/>.</summary>
        public DoubleDouble(double hi, double lo)
        {
            Hi = hi;
            Lo = lo;
        }

        /// <summary>The pair holding exactly this double.</summary>
        public static DoubleDouble Of(double value) => new DoubleDouble(value, 0.0);

        /// <summary>The pair holding exactly this double.</summary>
        public static implicit operator DoubleDouble(double value) => Of(value);

        /// <summary>The nearest double to the pair, losing the trailing part. Explicit, because that loss is the whole
        /// thing the pair exists to avoid.</summary>
        public static explicit operator double(DoubleDouble value) => value.Hi + value.Lo;

        /// <summary>Sum, exact to the pair.</summary>
        public static DoubleDouble operator +(DoubleDouble a, DoubleDouble b)
        {
            var s = a.Hi + b.Hi;
            var t = s - a.Hi;
            return Renormalise(s, ((a.Hi - (s - t)) + (b.Hi - t)) + (a.Lo + b.Lo));
        }

        /// <summary>Negation.</summary>
        public static DoubleDouble operator -(DoubleDouble a) => new DoubleDouble(-a.Hi, -a.Lo);

        /// <summary>Difference, exact to the pair.</summary>
        public static DoubleDouble operator -(DoubleDouble a, DoubleDouble b) => a + (-b);

        /// <summary>Product, exact to the pair.</summary>
        public static DoubleDouble operator *(DoubleDouble a, DoubleDouble b)
        {
            var p = a.Hi * b.Hi;
            Split(a.Hi, out var ah, out var al);
            Split(b.Hi, out var bh, out var bl);
            var e = ((ah * bh - p) + ah * bl + al * bh) + al * bl;
            return Renormalise(p, e + (a.Hi * b.Lo + a.Lo * b.Hi));
        }

        /// <summary>Equality of both parts. Two pairs holding the same real number are equal only when normalised the
        /// same way, so this is identity of the representation, not of the value.</summary>
        public bool Equals(DoubleDouble other) => Hi.Equals(other.Hi) && Lo.Equals(other.Lo);

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is DoubleDouble other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                return (Hi.GetHashCode() * 397) ^ Lo.GetHashCode();
            }
        }

        /// <inheritdoc/>
        public override string ToString() => $"{Hi:R} + {Lo:R}";

        private static DoubleDouble Renormalise(double hi, double lo)
        {
            var s = hi + lo;
            return new DoubleDouble(s, lo - (s - hi));
        }

        private static void Split(double value, out double hi, out double lo)
        {
            var t = 134217729.0 * value;   // 2^27 + 1: leaves each half narrow enough for their products to be exact
            hi = t - (t - value);
            lo = value - hi;
        }
    }
}
