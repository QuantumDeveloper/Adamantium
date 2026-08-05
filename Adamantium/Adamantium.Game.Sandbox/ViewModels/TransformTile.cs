using Adamantium.Core;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>One tile of the Transforms tab's grid: a fill colour plus the affine transform THIS tile is drawn under.
/// Each tile carries its own angle/shear (the panel spreads them across the grid) so the batch is asked to draw many
/// DIFFERENT matrices at once - which is the thing being demonstrated.</summary>
public sealed class TransformTile : PropertyChangedBase
{
    public string Color { get; init; }

    private double _angle;
    public double Angle { get => _angle; set => SetProperty(ref _angle, value); }

    private double _skewX;
    public double SkewX { get => _skewX; set => SetProperty(ref _skewX, value); }

    private double _skewY;
    public double SkewY { get => _skewY; set => SetProperty(ref _skewY, value); }

    private double _scale = 1.0;
    public double Scale { get => _scale; set => SetProperty(ref _scale, value); }
}
