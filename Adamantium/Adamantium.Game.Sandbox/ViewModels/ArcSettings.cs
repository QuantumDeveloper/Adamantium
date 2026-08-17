using Adamantium.Core;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry.Shapes;
using Adamantium.UI.Core.Media;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>The sector playground on the Shapes tab. A sector and a segment are the same ellipse the batch already draws
/// with a straight boundary added, so every combination these sliders reach comes out of one instanced pass - which is
/// worth having under a mouse rather than only in a test.
/// <para>The two closings differ in ways that are easy to state and easy to get wrong, so the stand shows them side by
/// side: a <see cref="EllipseType.Sector"/> closes through the CENTRE (and its stroke runs along both radii), while
/// <see cref="EllipseType.EdgeToEdge"/> closes by the CHORD - and stroked, it is an open arc with two ends and nothing
/// drawn across it.</para></summary>
public sealed class ArcSettings : PropertyChangedBase
{
    private double _startAngle = 30;
    private double _sweepAngle = 210;
    private double _strokeWidth = 6;
    private double _ringThickness = 0;
    private bool _sector = true;
    private double _boxWidth = 180;
    private double _boxHeight = 180;
    private double _strokeAlpha = 1.0;
    private bool _filled = true;

    /// <summary>Where the arc begins, in degrees of the ellipse's own parametric angle. 0 is the right edge, and UI space
    /// has y DOWN - so growing angles sweep toward the bottom.</summary>
    public double StartAngle { get => _startAngle; set => SetProperty(ref _startAngle, value); }

    /// <summary>How far it sweeps. 360 is a whole ellipse and needs no cut at all.</summary>
    public double SweepAngle { get => _sweepAngle; set => SetProperty(ref _sweepAngle, value); }

    public double StrokeWidth { get => _strokeWidth; set => SetProperty(ref _strokeWidth, value); }

    /// <summary>The outline's own brush, MUTATED rather than replaced when the alpha moves - recolouring re-bakes the
    /// instances that paint with it instead of re-recording the element.</summary>
    public SolidColorBrush StrokeBrush { get; } = new(new Color((byte)245, (byte)158, (byte)11, (byte)255));

    /// <summary>Outline alpha. On a CUT ellipse this is where the composite has to answer for itself: fill and stroke
    /// come out of one field, so the shared edge is a single layer - and the two radii of a sector, where the outline
    /// turns back on itself, must not come out darker than the arc.</summary>
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

    /// <summary>Leave a RING this thick instead of a solid shape - the thickness is GEOMETRY here, so the pen above stays
    /// free to outline the band. 0 is solid; anything else turns the wedge into a donut slice.</summary>
    public double RingThickness { get => _ringThickness; set => SetProperty(ref _ringThickness, value); }

    /// <summary>Sector (closed through the centre) or edge-to-edge (closed by the chord).</summary>
    public bool Sector
    {
        get => _sector;
        set { if (SetProperty(ref _sector, value)) RaisePropertyChanged(nameof(Kind)); }
    }

    /// <summary>Off leaves only the outline - which is how a ring gauge is drawn, and the case where the difference
    /// between a closed and an open contour is plain to see.</summary>
    public bool Filled
    {
        get => _filled;
        set { if (SetProperty(ref _filled, value)) RaisePropertyChanged(nameof(Fill)); }
    }

    /// <summary>The BOX the ellipse is inscribed in. Squash it and the cut has to follow the ellipse's OWN parametric
    /// angle rather than a circle's - the one thing about a sector that is easy to get wrong and easy to see.</summary>
    public double BoxWidth { get => _boxWidth; set => SetProperty(ref _boxWidth, value); }

    public double BoxHeight { get => _boxHeight; set => SetProperty(ref _boxHeight, value); }

    public EllipseType Kind => _sector ? EllipseType.Sector : EllipseType.EdgeToEdge;

    public Brush Fill => _filled ? FilledBrush : Brushes.Transparent;

    private static readonly SolidColorBrush FilledBrush = new(new Color((byte)37, (byte)99, (byte)235, (byte)160));
}
