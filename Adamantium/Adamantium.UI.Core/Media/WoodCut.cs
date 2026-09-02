namespace Adamantium.UI.Core.Media;

/// <summary>How the board was SAWN out of the log - which is the whole of why one piece of timber shows arches and the
/// next shows dead straight lines, even when both are oak.
///
/// <para>A tree's rings are concentric cylinders about its core, and a board is a plane cut through them. The figure on
/// its face is therefore not a property of the wood at all: it is where that plane crossed the cylinders. So these are
/// not four patterns - they are one pattern seen from four places, which is why the shader draws them all from the same
/// distance-to-the-core and differs only in where the core is.</para></summary>
public enum WoodCut
{
    /// <summary>PLAIN SAWN, the cheap cut and the common one: the plane runs beside the core without meeting it, so it
    /// slices the cylinders lengthways and the rings open into the nested arches - the "cathedral" - that most people
    /// picture when they picture wood.</summary>
    Flat,

    /// <summary>QUARTER SAWN: the plane passes THROUGH the core, cutting every ring square on, so they land as narrow
    /// evenly spaced lines running the length of the board. Wasteful of the log and prized for it - it is the striped
    /// oak of furniture and instrument tops.</summary>
    Quarter,

    /// <summary>END GRAIN: the plane is across the trunk, so the cylinders show as what they are - concentric rings
    /// about the core. A butcher's block, or the end of a beam.</summary>
    End,

    /// <summary>BURL: a growth of dormant buds where the grain has no direction left at all and the rings knot around
    /// each other. The one figure that is not a clean cut through an orderly log.</summary>
    Burl
}
