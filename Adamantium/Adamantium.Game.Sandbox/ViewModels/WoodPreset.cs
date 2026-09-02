using Adamantium.Mathematics;
using Adamantium.UI.Core.Media;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>A named timber, as the numbers that actually distinguish one from another - the same arrangement the metals
/// use, and for the same reason: oak, walnut and pine are ONE material in the engine, differing in the two colours of
/// their growth, in how far apart the rings sit and in what they are finished with.
///
/// <para>What separates the species is mostly CONTRAST, not hue: pine is pale wood with almost black summer bands, while
/// walnut is dark wood whose bands barely show. A single "wood colour" cannot say that, which is why there are two.</para>
/// </summary>
public sealed class WoodPreset
{
    private WoodPreset(string name, Color early, Color late, double ringScale, double gloss)
    {
        Name = name;
        Early = early;
        Late = late;
        RingScale = ringScale;
        Gloss = gloss;
    }

    public string Name { get; }

    /// <summary>Spring growth: the broad pale band that makes up most of a ring.</summary>
    public Color Early { get; }

    /// <summary>Summer growth: the narrow dense band that closes it.</summary>
    public Color Late { get; }

    /// <summary>Device pixels per ring - how old and how fast-grown the tree reads as.</summary>
    public double RingScale { get; }

    /// <summary>The finish, as roughness: oiled is nearly matte, lacquered is nearly a mirror.</summary>
    public double Gloss { get; }

    public override string ToString() => Name;

    public static WoodPreset[] All { get; } =
    [
        new("Oak", new Color(198, 158, 106, 255), new Color(120, 78, 42, 255), 9, 0.35),
        new("Walnut", new Color(122, 84, 58, 255), new Color(64, 40, 26, 255), 11, 0.28),
        new("Pine", new Color(226, 190, 134, 255), new Color(150, 96, 48, 255), 7, 0.40),
        new("Mahogany", new Color(150, 78, 52, 255), new Color(94, 42, 28, 255), 13, 0.20),
        new("Maple", new Color(226, 202, 162, 255), new Color(190, 158, 112, 255), 8, 0.25),
        new("Ebony", new Color(58, 46, 40, 255), new Color(26, 20, 18, 255), 10, 0.18)
    ];
}
