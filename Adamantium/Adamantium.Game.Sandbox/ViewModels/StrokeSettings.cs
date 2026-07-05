using System;
using System.Globalization;
using Adamantium.Core;
using Adamantium.Core.TypeParsing;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core.Media;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Shared stroke ("кайма") parameters for the Layout demo. ONE instance is referenced by every ColorRect tile
/// AND by the slider bindings, so a slider change mutates the single object and only the realized tiles (virtualization)
/// re-read it - no per-item propagation across thousands of tiles. Bound via the nested path <c>Stroke.StrokeWidth</c>
/// (BindingExpression walks dotted paths and observes the leaf object).</summary>
public sealed class StrokeSettings : PropertyChangedBase
{
    private double _strokeWidth = 8;
    public double StrokeWidth { get => _strokeWidth; set => SetProperty(ref _strokeWidth, value); }

    private double _dashOffset;
    public double DashOffset { get => _dashOffset; set => SetProperty(ref _dashOffset, value); }

    private double _trimStart;
    public double TrimStart { get => _trimStart; set => SetProperty(ref _trimStart, value); }

    private double _trimEnd = 1.0;
    public double TrimEnd { get => _trimEnd; set => SetProperty(ref _trimEnd, value); }

    // Uniform corner radius for the rectangle tiles/preview. The slider binds the scalar Corner; the shape's CornerRadius
    // property (a CornerRadius struct, not a double) reads the derived value.
    private double _corner = 4;
    public double Corner { get => _corner; set { if (SetProperty(ref _corner, value)) RaisePropertyChanged(nameof(CornerRadius)); } }
    public CornerRadius CornerRadius => new(_corner);

    // Cap shape for dash-piece ends AND trim ends. Slider 0..5 covers all six PenLineCaps (0 flat, 1 square, 2 convex
    // round, 3 convex triangle, 4 concave triangle, 5 concave round). Set the Cap slider's Maximum to 5 to reach them.
    private double _capValue = 2;
    public double CapValue { get => _capValue; set { if (SetProperty(ref _capValue, value)) RaisePropertyChanged(nameof(Cap)); } }
    public PenLineCap Cap => (int)Math.Round(_capValue) switch
    {
        1 => PenLineCap.Square,
        2 => PenLineCap.ConvexRound,
        3 => PenLineCap.ConvexTriangle,
        4 => PenLineCap.ConcaveTriangle,
        5 => PenLineCap.ConcaveRound,
        _ => PenLineCap.Flat,
    };

    // Corner join: slider 0 = miter (sharp), 1 = bevel (chamfer), 2 = round. Visible on a sharp rectangle (Corner = 0).
    private double _joinValue = 2;
    public double JoinValue { get => _joinValue; set { if (SetProperty(ref _joinValue, value)) RaisePropertyChanged(nameof(Join)); } }
    public PenLineJoin Join => _joinValue >= 1.5 ? PenLineJoin.Round : _joinValue >= 0.5 ? PenLineJoin.Bevel : PenLineJoin.Miter;

    // Stroke colour by HUE (0..360, full saturation/value) - one slider covers the whole spectrum. The shapes bind their
    // Stroke to StrokeBrush.
    private double _hue = 32;
    public double Hue { get => _hue; set { if (SetProperty(ref _hue, value)) RaisePropertyChanged(nameof(StrokeBrush)); } }
    public Brush StrokeBrush => TypeParser.Parse<Brush>(HueToHex(_hue));

    private static string HueToHex(double hue)
    {
        double h = ((hue % 360) + 360) % 360 / 60.0;
        double x = 1 - Math.Abs(h % 2 - 1);
        double r = 0, g = 0, b = 0;
        switch ((int)h)
        {
            case 0: r = 1; g = x; break;
            case 1: r = x; g = 1; break;
            case 2: g = 1; b = x; break;
            case 3: g = x; b = 1; break;
            case 4: r = x; b = 1; break;
            default: r = 1; b = x; break;
        }
        return $"#{(int)(r * 255):X2}{(int)(g * 255):X2}{(int)(b * 255):X2}";
    }

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
