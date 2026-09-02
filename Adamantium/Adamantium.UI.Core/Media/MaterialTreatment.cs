namespace Adamantium.UI.Core.Media;

/// <summary>What a material's shader DOES - the second of the two axes a <see cref="MaterialType"/> chooses on (the
/// first being where its picture comes from). Two materials share a treatment when they differ only in what is handed
/// to it: acrylic and mica are both frosted.
/// <para>A value rather than a flag because there are three of these now. It was a bool - "glass, or else frosted" -
/// which reads fine while there are exactly two and silently mis-sorts the third: everything that is not glass is not
/// therefore frosted.</para></summary>
public enum MaterialTreatment
{
    /// <summary>Blur, tint, grain over whatever picture the material brought - acrylic and mica.</summary>
    Frosted,

    /// <summary>The same picture bent like a lens, with a chromatic fringe and a bright rim - liquid glass.</summary>
    Glass,

    /// <summary>No picture at all: a lit surface, whose relief comes from a noise field and whose brightness comes from
    /// a grazing-angle sheen - velvet and the fabrics beside it.</summary>
    Sheen,

    /// <summary>The same lit surface with a metal's answer: a GGX lobe stretched along the grinding, reflecting a
    /// procedural studio environment rather than anything captured.</summary>
    Metal,

    /// <summary>A lit surface whose appearance is mostly FIGURE: annual rings drawn as colour, with the light doing
    /// no more than varnishing them. The odd one of the branch - the other two are lighting models over a plain
    /// colour, this one is a pattern that happens to be lit.</summary>
    Wood
}
