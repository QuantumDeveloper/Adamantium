using System;
using Adamantium.Mathematics;

namespace Adamantium.UI.Controls;

/// <summary>How an item is turned and leaned, about the middle of its own box.
/// <para>ONE thing and not two, because a rotation and a skew are the same kind of statement about a shape - and two
/// of them kept apart would be two coordinate systems inside one item, which is how a drawing ends up with a shape
/// whose outline, hit test and frame each think it is somewhere else.</para>
/// <para>About the MIDDLE and not a corner: that is what turning something means to a hand, and it keeps the box the
/// transform is measured against the one the item already has.</para></summary>
public readonly struct CanvasTransform : IEquatable<CanvasTransform>
{
    public CanvasTransform(double angle, double skewX = 0, double skewY = 0)
    {
        Angle = angle;
        SkewX = skewX;
        SkewY = skewY;
    }

    /// <summary>Nothing turned and nothing leaned.</summary>
    public static CanvasTransform None => default;

    /// <summary>Degrees clockwise on the screen, which is the direction the plane's Y runs.</summary>
    public double Angle { get; init; }

    /// <summary>Degrees the horizontal leans by.</summary>
    public double SkewX { get; init; }

    /// <summary>Degrees the vertical leans by.</summary>
    public double SkewY { get; init; }

    /// <summary>Whether this says anything at all. Everything that costs something - a second pass over the points, a
    /// per-unit draw instead of a batched one - is skipped when it does not.</summary>
    public bool IsSomething => Angle != 0 || SkewX != 0 || SkewY != 0;

    /// <summary>Where a point of the item ends up, turned about <paramref name="about"/>.</summary>
    public Vector2 Apply(Vector2 point, Vector2 about)
    {
        if (!IsSomething) return point;

        var x = point.X - about.X;
        var y = point.Y - about.Y;

        // SKEW first, then the turn. The other order is a different transform - and this is the order every drawing
        // program states it in, so a number typed into a panel means what the person typing it expects.
        if (SkewX != 0) x += y * Math.Tan(SkewX * Math.PI / 180);
        if (SkewY != 0) y += x * Math.Tan(SkewY * Math.PI / 180);

        if (Angle != 0)
        {
            var radians = Angle * Math.PI / 180;
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);

            (x, y) = (x * cos - y * sin, x * sin + y * cos);
        }

        return new Vector2(about.X + x, about.Y + y);
    }

    /// <summary>Where a point on the SCREEN came from - the inverse. What hit-testing needs: a turned shape is asked
    /// about a point in the world, and the only way it can answer is to un-turn the point and ask its plain self.
    /// </summary>
    public Vector2 Undo(Vector2 point, Vector2 about)
    {
        if (!IsSomething) return point;

        var x = point.X - about.X;
        var y = point.Y - about.Y;

        if (Angle != 0)
        {
            var radians = -Angle * Math.PI / 180;
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);

            (x, y) = (x * cos - y * sin, x * sin + y * cos);
        }

        if (SkewY != 0) y -= x * Math.Tan(SkewY * Math.PI / 180);
        if (SkewX != 0) x -= y * Math.Tan(SkewX * Math.PI / 180);

        return new Vector2(about.X + x, about.Y + y);
    }

    public bool Equals(CanvasTransform other) =>
        Angle.Equals(other.Angle) && SkewX.Equals(other.SkewX) && SkewY.Equals(other.SkewY);

    public override bool Equals(object obj) => obj is CanvasTransform other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Angle, SkewX, SkewY);
}
