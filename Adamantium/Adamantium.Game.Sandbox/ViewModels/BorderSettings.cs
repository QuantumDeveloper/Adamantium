using Adamantium.Core;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core.Media;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>The border playground on the Shapes tab: four side thicknesses and four corner radii, live. A border with
/// unequal sides or unequal corners used to leave the SDF batch for a tessellated ring; both now ride in the instance,
/// so every combination these eight sliders can make is drawn by the same instanced pass - which is what makes dragging
/// them worth watching rather than just reading about.</summary>
public sealed class BorderSettings : PropertyChangedBase
{
    private double _left = 2;
    private double _top = 10;
    private double _right = 2;
    private double _bottom = 10;

    private double _topLeft = 18;
    private double _topRight = 0;
    private double _bottomRight = 18;
    private double _bottomLeft = 0;

    public double Left { get => _left; set { if (SetProperty(ref _left, value)) RaisePropertyChanged(nameof(Thickness)); } }
    public double Top { get => _top; set { if (SetProperty(ref _top, value)) RaisePropertyChanged(nameof(Thickness)); } }
    public double Right { get => _right; set { if (SetProperty(ref _right, value)) RaisePropertyChanged(nameof(Thickness)); } }
    public double Bottom { get => _bottom; set { if (SetProperty(ref _bottom, value)) RaisePropertyChanged(nameof(Thickness)); } }

    public double TopLeft { get => _topLeft; set { if (SetProperty(ref _topLeft, value)) RaisePropertyChanged(nameof(Corners)); } }
    public double TopRight { get => _topRight; set { if (SetProperty(ref _topRight, value)) RaisePropertyChanged(nameof(Corners)); } }
    public double BottomRight { get => _bottomRight; set { if (SetProperty(ref _bottomRight, value)) RaisePropertyChanged(nameof(Corners)); } }
    public double BottomLeft { get => _bottomLeft; set { if (SetProperty(ref _bottomLeft, value)) RaisePropertyChanged(nameof(Corners)); } }

    // The border's own ALPHA, which is where this stand earns its keep. A ring drawn as its own shape blends the outline
    // it shares with the fill twice, and every corner - where two edges meet - counts a third time: translucency turns
    // that arithmetic into a picture. Here fill and ring come out of ONE field as complementary coverages, so the ring is
    // a single layer at any alpha, and the CORNER must stay the same tone as the straight run beside it. The flat swatch
    // next to the stand is the reference: same colour, same alpha, no border arithmetic at all.
    //
    // The brush is MUTATED rather than replaced, which is also the interesting path: recolouring a brush re-bakes the
    // instances that paint with it (AffectsPaint) instead of re-recording the element.
    public SolidColorBrush BorderBrush { get; } = new(new Color((byte)245, (byte)158, (byte)11, (byte)128));

    /// <summary>Same colour and alpha as the border, with nothing drawn under or over it - drag the alpha and the
    /// border's corner has to keep matching this.</summary>
    public SolidColorBrush SwatchBrush { get; } = new(new Color((byte)245, (byte)158, (byte)11, (byte)128));

    private double _alpha = 0.5;

    public double Alpha
    {
        get => _alpha;
        set
        {
            if (!SetProperty(ref _alpha, value)) return;

            var a = (byte)MathHelper.Clamp(value * 255.0, 0.0, 255.0);
            var c = BorderBrush.Color;
            BorderBrush.Color = new Color(c.R, c.G, c.B, a);
            SwatchBrush.Color = new Color(c.R, c.G, c.B, a);
        }
    }

    /// <summary>Thickness(left, top, right, bottom) - what the Border binds its BorderThickness to.</summary>
    public Thickness Thickness => new(_left, _top, _right, _bottom);

    /// <summary>CornerRadius(topLeft, topRight, bottomRight, bottomLeft) - the order the engine states corners in.</summary>
    public CornerRadius Corners => new(_topLeft, _topRight, _bottomRight, _bottomLeft);
}
