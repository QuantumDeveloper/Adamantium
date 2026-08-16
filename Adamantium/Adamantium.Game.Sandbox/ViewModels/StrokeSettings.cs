using System.Globalization;
using Adamantium.Core;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core.Media;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Shared stroke ("кайма") parameters for the Shapes-tab pen playground. ONE instance drives the stroke of
/// EVERY shape in the tab: thickness, dashes, trim, corner, cap/join (bound to <c>DropDown</c>s) and colour (bound to a
/// <c>ColorPicker</c>). Sliders/dropdowns/picker mutate this one object; each shape binds its stroke off <c>Stroke.*</c>.</summary>
public sealed class StrokeSettings : PropertyChangedBase
{
    private double _strokeWidth = 8;
    public double StrokeWidth { get => _strokeWidth; set => SetProperty(ref _strokeWidth, value); }

    // Alpha of the translucent-overlap stand. Worth dragging: at 1 the strokes take the plain single-pass path, and
    // anywhere below it they take the union path, so the two can be compared on the same shapes.
    private double _overlapAlpha = 0.5;
    public double OverlapAlpha { get => _overlapAlpha; set => SetProperty(ref _overlapAlpha, value); }

    // Curve tessellation density: number of points each Bézier/NURBS/B-spline is sampled into (evenly by arc length).
    // 0 = automatic (~3px spacing). The curves bind their Samples to this so the panel drives them all.
    private double _samples = 32;   // demo slider START only; the real default lives on CurveBase.Samples (32)
    public double Samples { get => _samples; set => SetProperty(ref _samples, value); }

    // --- NURBS playground (the demo NURBS has 6 control points) --------------------------------------------------------
    // Weight of the two TOP control points (the peak). >1 pulls the NURBS toward them; =1 = a plain B-spline. This is the
    // "R" (rational) that a B-spline lacks - drag it and the curve bulges toward the peak.
    private double _nurbsWeight = 1;
    public double NurbsWeight
    {
        get => _nurbsWeight;
        set { if (SetProperty(ref _nurbsWeight, value)) RaisePropertyChanged(nameof(NurbsWeights)); }
    }
    public System.Collections.Generic.IReadOnlyList<double> NurbsWeights => [1, 1, _nurbsWeight, _nurbsWeight, 1, 1];

    // Piecewise-polynomial degree of the NURBS (a B-spline concept too): higher = smoother, pulls further from the
    // control polygon. The plain B-spline shape stays fixed for comparison.
    private double _nurbsDegree = 3;
    public double NurbsDegree { get => _nurbsDegree; set => SetProperty(ref _nurbsDegree, value); }

    private bool _nurbsUniform;   // false = non-uniform/clamped knots (smooth, reaches endpoints); true = uniform (floats)
    public bool NurbsUniform { get => _nurbsUniform; set => SetProperty(ref _nurbsUniform, value); }

    private double _dashOffset;
    public double DashOffset { get => _dashOffset; set => SetProperty(ref _dashOffset, value); }

    private double _trimStart;
    public double TrimStart { get => _trimStart; set => SetProperty(ref _trimStart, value); }

    private double _trimEnd = 1.0;
    public double TrimEnd { get => _trimEnd; set => SetProperty(ref _trimEnd, value); }

    // Uniform corner radius for the rectangle. The slider binds the scalar Corner; the shape's CornerRadius property
    // (a CornerRadius struct, not a double) reads the derived value.
    private double _corner = 4;
    public double Corner { get => _corner; set { if (SetProperty(ref _corner, value)) RaisePropertyChanged(nameof(CornerRadius)); } }
    public CornerRadius CornerRadius => new(_corner);

    // FOUR separate caps, because they are four separate properties and conflating them is what hid the bugs: the demo
    // used to bind all of them to one setting, so the interesting cases - a dash cap that differs from the line cap,
    // a start that differs from an end - could not be produced at all.
    //
    // The rule they follow: Start/End belong to the whole stroke and exist ONLY at its two real ends; every other dash
    // end takes its own dash cap; a CLOSED, untrimmed contour has no real ends at all, so it is dash caps everywhere.
    // The two dash caps set differently make each dash an arrow (a concave bite behind a convex tip).
    private PenLineCap _dashStartCap = PenLineCap.ConvexRound;
    public PenLineCap DashStartCap { get => _dashStartCap; set => SetProperty(ref _dashStartCap, value); }

    private PenLineCap _dashEndCap = PenLineCap.ConvexRound;
    public PenLineCap DashEndCap { get => _dashEndCap; set => SetProperty(ref _dashEndCap, value); }

    private PenLineCap _startCap = PenLineCap.Flat;
    public PenLineCap StartCap { get => _startCap; set => SetProperty(ref _startCap, value); }

    private PenLineCap _endCap = PenLineCap.Flat;
    public PenLineCap EndCap { get => _endCap; set => SetProperty(ref _endCap, value); }

    // Stretch the pattern so a CLOSED contour holds a whole number of periods. Off, the leftover lands where the
    // contour closes - one odd dash at the seam, which for a rounded rect is the bottom-right corner. It is the seam,
    // not the corner: there is nothing to fix in the corner code, and this is the switch that shows it.
    private bool _dashFit;
    public bool DashFit { get => _dashFit; set => SetProperty(ref _dashFit, value); }

    // Corner join. Bound two-way to a DropDown (EnumType=PenLineJoin): Miter / Bevel / Round.
    private PenLineJoin _join = PenLineJoin.Round;
    public PenLineJoin Join { get => _join; set => SetProperty(ref _join, value); }

    // Stroke colour, driven by a ColorPicker. StrokeBrush is ONE cached brush every shape binds its Stroke to - a colour
    // change mutates its Color in place (AffectsPaint re-bakes the users), NOT a new brush per read. Creating a fresh
    // SolidColorBrush (an AdamantiumComponent) on every read churned the property system and deadlocked the render thread
    // (which reads brush colours under a per-component Monitor lock) against the pump thread on a colour change.
    private readonly SolidColorBrush _strokeBrush = new(new Color(255, 136, 0));   // orange, matching the old default hue 32
    private Color _selectedColor = new(255, 136, 0);
    public Color SelectedColor { get => _selectedColor; set { if (SetProperty(ref _selectedColor, value)) _strokeBrush.Color = value; } }
    public Brush StrokeBrush => _strokeBrush;

    // Dash on-length / gap drive the pattern as a symbolic glyph string (reliable to bind vs a live collection). On == 0
    // = solid (empty symbols); otherwise a repeating "Dash" whose width/gap come from DashGlyphs.
    private double _dashOn;
    public double DashOn
    {
        get => _dashOn;
        set { if (SetProperty(ref _dashOn, value)) { RaisePropertyChanged(nameof(DashSymbols)); RaisePropertyChanged(nameof(DashGlyphs)); } }
    }

    private double _dashGap = 8;
    public double DashGap
    {
        get => _dashGap;
        set { if (SetProperty(ref _dashGap, value)) RaisePropertyChanged(nameof(DashGlyphs)); }
    }

    public string DashSymbols => _dashOn > 0 ? "Dash" : string.Empty;

    public string DashGlyphs =>
        $"Dash={_dashOn.ToString(CultureInfo.InvariantCulture)}, Gap={_dashGap.ToString(CultureInfo.InvariantCulture)}";
}
