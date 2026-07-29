using System;
using System.Globalization;

namespace Adamantium.UI.Controls.Panels;

/// <summary>How much of its row a pane takes: either a fixed number of pixels, or a weight in what is left over.
/// <para>ONE number with a mode, deliberately - not a share plus a pixel hint. Two numbers for one size have to be kept
/// in step through every split, move, rebuild and divider drag, and every one of those is a place they drift: a pane
/// whose share said half while its hint said 160 looked right until the hint stopped applying, and then jumped. This is
/// the same shape a Grid length has, for the same reason.</para></summary>
public enum PaneUnit
{
    /// <summary>A weight in whatever is left after the fixed panes have taken theirs (a Grid's star).</summary>
    Star,

    /// <summary>Exactly this many pixels, along the row's own axis.</summary>
    Pixel
}

/// <summary>A pane's length in its row - see <see cref="PaneUnit"/>.</summary>
public readonly struct PaneLength : IEquatable<PaneLength>
{
    public PaneLength(double value, PaneUnit unit = PaneUnit.Star)
    {
        Value = double.IsNaN(value) || value < 0 ? 0 : value;
        Unit = unit;
    }

    public double Value { get; }

    public PaneUnit Unit { get; }

    public bool IsPixel => Unit == PaneUnit.Pixel;

    public bool IsStar => Unit == PaneUnit.Star;

    /// <summary>One share of the leftovers - what a pane takes when nobody said anything about it.</summary>
    public static PaneLength Star => new(1, PaneUnit.Star);

    public static PaneLength Pixels(double value) => new(value, PaneUnit.Pixel);

    public static PaneLength Stars(double weight) => new(weight, PaneUnit.Star);

    /// <summary>Parses <c>"240"</c> (pixels), <c>"*"</c>, <c>"2*"</c> - the same spelling a Grid length uses, so markup
    /// says the same thing in both places.</summary>
    public static PaneLength Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Star;

        text = text.Trim();
        if (text == "*") return Star;
        if (text.EndsWith('*'))
        {
            var weight = text[..^1];
            return weight.Length == 0
                ? Star
                : Stars(double.Parse(weight, CultureInfo.InvariantCulture));
        }

        return Pixels(double.Parse(text, CultureInfo.InvariantCulture));
    }

    public bool Equals(PaneLength other) => Unit == other.Unit && Value.Equals(other.Value);

    public override bool Equals(object obj) => obj is PaneLength other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Value, (int)Unit);

    public static bool operator ==(PaneLength left, PaneLength right) => left.Equals(right);

    public static bool operator !=(PaneLength left, PaneLength right) => !left.Equals(right);

    public override string ToString() => IsPixel
        ? Value.ToString(CultureInfo.InvariantCulture)
        : Value == 1 ? "*" : Value.ToString(CultureInfo.InvariantCulture) + "*";
}
