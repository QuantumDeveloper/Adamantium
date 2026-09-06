using System.ComponentModel.DataAnnotations;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Which stand this tab is showing. The tab used to run a section per subject down one long page, so comparing
/// two of them meant scrolling past everything in between; one slot and this selector puts them in the same place.
/// <para>Named for STANDS, not brushes: the aura and shadow live here too, and they are an effect drawn outside the
/// element rather than a fill.</para></summary>
public enum LiveStand
{
    /// <summary>Linear, radial and conic ramps. The one stand with no live panel yet - its rows of swatches ARE its
    /// content, which is still better than hiding them behind a switch that belongs to no subject at all.</summary>
    [Display(Name = "Gradients")]
    Gradients,

    /// <summary>A ramp between four corner colours, interpolated across the shape.</summary>
    [Display(Name = "Mesh gradient")]
    Mesh,

    /// <summary>A procedural two-colour pattern, evaluated per fragment.</summary>
    [Display(Name = "Pattern")]
    Pattern,

    /// <summary>Procedural noise fields.</summary>
    [Display(Name = "Noise")]
    Noise,

    /// <summary>An escape-time fractal, iterated per fragment.</summary>
    [Display(Name = "Fractal")]
    Fractal,

    /// <summary>A picture, tiled or fitted.</summary>
    [Display(Name = "Image")]
    Image,

    /// <summary>One picture that keeps its corners at any size.</summary>
    [Display(Name = "Nine-slice")]
    NineSlice,

    /// <summary>A drawing as a fill: viewbox, viewport, stretch, tiling.</summary>
    [Display(Name = "Drawing")]
    Drawing,

    /// <summary>A real, live element as the fill.</summary>
    [Display(Name = "Visual")]
    Visual,

    /// <summary>A surface that reacts to what is behind it.</summary>
    [Display(Name = "Material")]
    Material,

    /// <summary>Not a fill at all: a band drawn OUTSIDE the element, as a glow or a shadow.</summary>
    [Display(Name = "Aura / Shadow")]
    Aura
}
