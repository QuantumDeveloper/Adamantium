using Adamantium.Mathematics;
using Adamantium.UI.Core.Media;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>A named metal, as the numbers that actually distinguish one from another. Steel, aluminium, chrome, gold
/// and copper are ONE material in the engine - they differ in reflectance at normal incidence, in how polished they
/// are, and in how coarse the grinding is - so they belong here, on the stand, rather than as five entries in
/// <see cref="MaterialType"/> that would each mean "the same shader with other numbers".</summary>
public sealed class MetalPreset
{
    private MetalPreset(string name, Color colour, double roughness, double grain)
    {
        Name = name;
        Colour = colour;
        Roughness = roughness;
        Grain = grain;
    }

    public string Name { get; }

    /// <summary>F0: what the metal reflects head-on, which for a conductor IS its colour.</summary>
    public Color Colour { get; }

    public double Roughness { get; }

    /// <summary>How coarse the grinding is. A polished face has almost none; a satin one wears it openly.</summary>
    public double Grain { get; }

    public override string ToString() => Name;

    /// <summary>The catalogue. The colours are the measured reflectances every renderer quotes for these metals, not
    /// tastes - which is why gold is warm and dark rather than yellow-bright.</summary>
    public static MetalPreset[] All { get; } =
    [
        new("Polished steel", new Color(196, 199, 202, 255), 0.08, 22),
        new("Brushed steel", new Color(190, 193, 197, 255), 0.30, 4),
        new("Aluminium", new Color(232, 234, 236, 255), 0.16, 10),
        new("Chrome", new Color(214, 220, 225, 255), 0.03, 30),
        new("Gold", new Color(255, 200, 108, 255), 0.12, 24),
        new("Copper", new Color(245, 158, 118, 255), 0.14, 20)
    ];
}
