using System.ComponentModel.DataAnnotations;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Which gradient the live stand is driving. All three take the same stops and the same spread and
/// interpolation - what differs is the geometry the ramp runs along, which is exactly what the stand is for.</summary>
public enum GradientKind
{
    /// <summary>The ramp runs along a line between two points.</summary>
    [Display(Name = "Linear")]
    Linear,

    /// <summary>The ramp runs outward from a centre.</summary>
    [Display(Name = "Radial")]
    Radial,

    /// <summary>The ramp is swept around a centre.</summary>
    [Display(Name = "Conic")]
    Conic
}
