using Adamantium.Core;
using Adamantium.Mathematics;
using Adamantium.UI.Core.Media;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>The polygon playground on the Shapes tab. One shape, one number: three corners is a triangle, six a hexagon,
/// and enough of them is a circle - so dragging the slider walks the whole family the UI actually needs (ticks, chevrons,
/// diamonds, dice pips, hex tiles), and every stop of it is one instanced draw.</summary>
public sealed class PolygonSettings : PropertyChangedBase
{
    private double _corners = 3;
    private double _ringThickness = 0;
    private double _strokeWidth = 0;
    private double _startAngle = 0;
    private double _boxWidth = 180;
    private double _boxHeight = 180;
    private double _strokeAlpha = 1.0;

    /// <summary>How many corners. A double because sliders are doubles; the shape rounds it and clamps below three.</summary>
    public double Corners { get => _corners; set { if (SetProperty(ref _corners, value)) RaisePropertyChanged(nameof(CornerCount)); } }

    public int CornerCount => (int)_corners;

    /// <summary>Hollow it out - a hollow triangle is a chevron, and it is GEOMETRY, so the pen below stays free.</summary>
    public double RingThickness { get => _ringThickness; set => SetProperty(ref _ringThickness, value); }

    public double StrokeWidth { get => _strokeWidth; set => SetProperty(ref _strokeWidth, value); }

    /// <summary>The outline's own brush, MUTATED rather than replaced when the alpha moves - recolouring re-bakes the
    /// instances that paint with it instead of re-recording the element.</summary>
    public SolidColorBrush StrokeBrush { get; } = new(new Color((byte)248, (byte)250, (byte)252, (byte)255));

    /// <summary>Outline alpha. A translucent outline is the honest test of the composite: fill and stroke come out of ONE
    /// field, so the shared edge must be a single layer - if the two were blended separately it would darken exactly
    /// there, and at a corner, where the outline turns, a third time.</summary>
    public double StrokeAlpha
    {
        get => _strokeAlpha;
        set
        {
            if (!SetProperty(ref _strokeAlpha, value)) return;

            var c = StrokeBrush.Color;
            StrokeBrush.Color = new Color(c.R, c.G, c.B, (byte)MathHelper.Clamp(value * 255.0, 0.0, 255.0));
        }
    }

    /// <summary>Where corner 0 sits, in degrees. A triangle points right at 0 and stands on its base at 90 - and turning
    /// it must not resize it, which is the part a squashed box makes visible.</summary>
    public double StartAngle { get => _startAngle; set => SetProperty(ref _startAngle, value); }

    /// <summary>The BOX the shape is inscribed in. Squash it and the polygon has to follow the box's aspect, not stay
    /// round - which is why the width and height are under the same hand as the corner count.</summary>
    public double BoxWidth { get => _boxWidth; set => SetProperty(ref _boxWidth, value); }

    public double BoxHeight { get => _boxHeight; set => SetProperty(ref _boxHeight, value); }
}
