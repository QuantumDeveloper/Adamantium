using System;
using System.Globalization;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>
/// An inline colour picker: a saturation/value square, a hue bar and an alpha bar (each dragged), plus a hex field and
/// numeric R/G/B/A fields - every surface bound to the same <see cref="SelectedColor"/>. HSV is the internal source of
/// truth (so the hue thumb doesn't jump when the colour hits grey/black, where hue is undefined in RGB); the RGBA/hex
/// fields and <see cref="SelectedColor"/> are derived from it and, when edited, fed back into it. The text fields bind
/// two-way to these properties from the template via <c>{Ancestor ColorPicker, ..., Mode=TwoWay}</c> (TemplateBinding is
/// one-way by design, like WPF - the Ancestor binding is the two-way relative-source path).
/// </summary>
public class ColorPicker : Control
{
    // HSV is the source of truth: hue 0..360, sat/val/alpha 0..1.
    private double _hue;
    private double _sat = 1;
    private double _val = 1;
    private double _alpha = 1;
    private bool _syncing;   // guards the colour <-> hsv <-> RGBA/hex fan-out from re-entering itself
    private DragTarget _drag = DragTarget.None;

    private Border _svArea;
    private Border _hueArea;
    private Border _alphaArea;
    private Border _alphaGradient;
    private Border _preview;
    private MeasurableUIComponent _svThumb;
    private MeasurableUIComponent _hueThumb;
    private MeasurableUIComponent _alphaThumb;
    private readonly SolidColorBrush _hueBrush = new(Colors.Red);   // SV square background = the pure hue
    private readonly SolidColorBrush _previewBrush = new(Colors.Red);   // preview swatch = the picked colour (with its alpha)
    // Alpha bar overlay: the current colour opaque (top) -> transparent (bottom), over the theme's checkerboard.
    private readonly LinearGradientBrush _alphaBrush = new(new GradientStopCollection(
        [new GradientStop(Colors.Red, 0), new GradientStop(Colors.Transparent, 1)]))
    { StartPoint = new Vector2(0, 0), EndPoint = new Vector2(0, 1) };

    private enum DragTarget
    {
        None,
        SaturationValue,
        Hue,
        Alpha
    }

    public static readonly AdamantiumProperty SelectedColorProperty = AdamantiumProperty.Register(nameof(SelectedColor),
        typeof(Color), typeof(ColorPicker),
        new PropertyMetadata(Colors.Red, PropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorChanged));

    public static readonly AdamantiumProperty HexProperty = AdamantiumProperty.Register(nameof(Hex),
        typeof(string), typeof(ColorPicker),
        new PropertyMetadata("#FFFF0000", PropertyMetadataOptions.BindsTwoWayByDefault, OnHexChanged));

    public static readonly AdamantiumProperty RedProperty = AdamantiumProperty.Register(nameof(Red),
        typeof(string), typeof(ColorPicker),
        new PropertyMetadata("255", PropertyMetadataOptions.BindsTwoWayByDefault, OnChannelChanged));

    public static readonly AdamantiumProperty GreenProperty = AdamantiumProperty.Register(nameof(Green),
        typeof(string), typeof(ColorPicker),
        new PropertyMetadata("0", PropertyMetadataOptions.BindsTwoWayByDefault, OnChannelChanged));

    public static readonly AdamantiumProperty BlueProperty = AdamantiumProperty.Register(nameof(Blue),
        typeof(string), typeof(ColorPicker),
        new PropertyMetadata("0", PropertyMetadataOptions.BindsTwoWayByDefault, OnChannelChanged));

    public static readonly AdamantiumProperty AlphaProperty = AdamantiumProperty.Register(nameof(Alpha),
        typeof(string), typeof(ColorPicker),
        new PropertyMetadata("255", PropertyMetadataOptions.BindsTwoWayByDefault, OnChannelChanged));

    /// <summary>The picked colour (ARGB). Two-way; also driven by the square/bars and the fields. The clean binding target.</summary>
    public Color SelectedColor { get => GetValue<Color>(SelectedColorProperty); set => SetValue(SelectedColorProperty, value); }

    /// <summary>The colour as <c>#AARRGGBB</c>. Two-way - edit the hex field to set the colour.</summary>
    public string Hex { get => GetValue<string>(HexProperty); set => SetValue(HexProperty, value); }

    /// <summary>Red channel as editable text 0..255. Two-way.</summary>
    public string Red { get => GetValue<string>(RedProperty); set => SetValue(RedProperty, value); }

    /// <summary>Green channel as editable text 0..255. Two-way.</summary>
    public string Green { get => GetValue<string>(GreenProperty); set => SetValue(GreenProperty, value); }

    /// <summary>Blue channel as editable text 0..255. Two-way.</summary>
    public string Blue { get => GetValue<string>(BlueProperty); set => SetValue(BlueProperty, value); }

    /// <summary>Alpha channel as editable text 0..255. Two-way.</summary>
    public string Alpha { get => GetValue<string>(AlphaProperty); set => SetValue(AlphaProperty, value); }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        DetachAreas();

        DetachParts();   // a template swap re-runs this; drop the old wiring first

        _svArea = GetTemplateChild("PART_SVArea") as Border;
        _hueArea = GetTemplateChild("PART_HueArea") as Border;
        _alphaArea = GetTemplateChild("PART_AlphaArea") as Border;
        _alphaGradient = GetTemplateChild("PART_AlphaGradient") as Border;
        _preview = GetTemplateChild("PART_Preview") as Border;
        _svThumb = GetTemplateChild("PART_SVThumb") as MeasurableUIComponent;
        _hueThumb = GetTemplateChild("PART_HueThumb") as MeasurableUIComponent;
        _alphaThumb = GetTemplateChild("PART_AlphaThumb") as MeasurableUIComponent;

        if (_svArea != null)
        {
            _svArea.Background = _hueBrush;
            _svArea.SizeChanged += OnAreaSizeChanged;
        }

        if (_hueArea != null)
        {
            _hueArea.SizeChanged += OnAreaSizeChanged;
        }

        if (_alphaArea != null)
        {
            _alphaArea.SizeChanged += OnAreaSizeChanged;
        }

        if (_alphaGradient != null)
        {
            _alphaGradient.Background = _alphaBrush;
        }

        if (_preview != null)
        {
            _preview.Background = _previewBrush;
        }

        // Bootstrap the whole fan-out from the current SelectedColor, then reflect it onto the fresh parts.
        ApplyColor(SelectedColor);
        Commit();
    }

    /// <summary>Let the template's parts go when the template does - see ScrollBar.OnRemoveTemplate.</summary>
    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();
        DetachParts();
    }

    private void DetachParts()
    {
        if (_svArea != null) _svArea.SizeChanged -= OnAreaSizeChanged;
        if (_hueArea != null) _hueArea.SizeChanged -= OnAreaSizeChanged;
        if (_alphaArea != null) _alphaArea.SizeChanged -= OnAreaSizeChanged;
        _svArea = null;
        _hueArea = null;
        _alphaArea = null;
    }

    private void DetachAreas()
    {
        if (_svArea != null)
        {
            _svArea.SizeChanged -= OnAreaSizeChanged;
        }

        if (_hueArea != null)
        {
            _hueArea.SizeChanged -= OnAreaSizeChanged;
        }

        if (_alphaArea != null)
        {
            _alphaArea.SizeChanged -= OnAreaSizeChanged;
        }
    }

    private void OnAreaSizeChanged(object sender, SizeChangedEventArgs e) => UpdateThumbs();

    // ---- Pointer drag over the three areas -----------------------------------------------------------------------

    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(sender, e);
        _drag = HitArea(e);
        if (_drag == DragTarget.None)
        {
            return;
        }

        CaptureMouse();
        UpdateFromPointer();
        e.Handled = true;
    }

    protected override void OnMouseMove(object sender, MouseEventArgs e)
    {
        base.OnMouseMove(sender, e);
        if (IsMouseCaptured && _drag != DragTarget.None)
        {
            UpdateFromPointer();
        }
    }

    protected override void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(sender, e);
        if (_drag != DragTarget.None)
        {
            _drag = DragTarget.None;
            ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    // Which area (if any) is under the pointer - the press picks the drag target, and moves keep updating it.
    private DragTarget HitArea(MouseButtonEventArgs e)
    {
        if (Inside(_svArea, e))
        {
            return DragTarget.SaturationValue;
        }

        if (Inside(_hueArea, e))
        {
            return DragTarget.Hue;
        }

        if (Inside(_alphaArea, e))
        {
            return DragTarget.Alpha;
        }

        return DragTarget.None;
    }

    private static bool Inside(Border area, MouseButtonEventArgs e)
    {
        if (area == null)
        {
            return false;
        }

        var p = e.GetPosition(area);
        return p.X >= 0 && p.Y >= 0 && p.X <= area.ActualWidth && p.Y <= area.ActualHeight;
    }

    private void UpdateFromPointer()
    {
        switch (_drag)
        {
            case DragTarget.SaturationValue when _svArea != null:
            {
                var p = Mouse.GetPosition(_svArea);
                _sat = Clamp01(p.X / Math.Max(1, _svArea.ActualWidth));
                _val = Clamp01(1 - p.Y / Math.Max(1, _svArea.ActualHeight));
                break;
            }
            case DragTarget.Hue when _hueArea != null:
            {
                var p = Mouse.GetPosition(_hueArea);
                _hue = Clamp01(p.Y / Math.Max(1, _hueArea.ActualHeight)) * 360;
                break;
            }
            case DragTarget.Alpha when _alphaArea != null:
            {
                var p = Mouse.GetPosition(_alphaArea);
                _alpha = 1 - Clamp01(p.Y / Math.Max(1, _alphaArea.ActualHeight));
                break;
            }
            default:
                return;
        }

        Commit();
    }

    // ---- The colour <-> hsv <-> RGBA/hex fan-out -----------------------------------------------------------------

    private static void OnSelectedColorChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not ColorPicker picker || picker._syncing)
        {
            return;
        }

        picker.ApplyColor((Color)e.NewValue);
        picker.Commit();
    }

    private static void OnHexChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not ColorPicker picker || picker._syncing)
        {
            return;
        }

        if (TryParseHex(e.NewValue as string, out var color))
        {
            picker.ApplyColor(color);
            picker.Commit();
        }
    }

    private static void OnChannelChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not ColorPicker picker || picker._syncing)
        {
            return;
        }

        // Commit only when all four fields parse - a field mid-edit (empty / non-numeric) leaves the colour untouched
        // until it's valid again, rather than zeroing a channel per keystroke.
        if (TryByte(picker.Red, out var r) && TryByte(picker.Green, out var g)
            && TryByte(picker.Blue, out var b) && TryByte(picker.Alpha, out var alpha))
        {
            picker.ApplyColor(new Color(r, g, b, alpha));
            picker.Commit();
        }
    }

    // Adopt an externally-set colour into the HSV state. Hue is KEPT when the colour is greyscale/black (RGB can't tell the
    // hue there), so the hue thumb doesn't jump to red as the value/saturation is dragged to an edge.
    private void ApplyColor(Color color)
    {
        // If the incoming colour is exactly what our current HSV already produces, this is the round-trip of our OWN commit
        // (SelectedColor -> here), not an external set - so KEEP the HSV state. Re-deriving HSV from RGB loses information the
        // RGB can't carry and makes the thumb jump AT THE EDGES: saturation is undefined at value 0 (a black bottom row) so
        // it collapses to 0 (the SV thumb darts left/jitters), and hue 360 == hue 0 in RGB so the hue thumb snaps to the top.
        var current = HsvToColor(_hue, _sat, _val, _alpha);
        if (color.R == current.R && color.G == current.G && color.B == current.B && color.A == current.A)
        {
            return;
        }

        _alpha = color.A / 255.0;
        RgbToHsv(color, out var h, out var s, out var v);
        _val = v;
        _sat = s;
        if (s > 0)
        {
            _hue = h;
        }
    }

    // Recompute the colour from the HSV state and push it to SelectedColor + the RGBA/hex fields + the visuals, guarded so
    // none of those writes re-enters this fan-out.
    private void Commit()
    {
        var color = HsvToColor(_hue, _sat, _val, _alpha);
        _syncing = true;
        SetCurrentValue(SelectedColorProperty, color);
        SetCurrentValue(RedProperty, color.R.ToString(CultureInfo.InvariantCulture));
        SetCurrentValue(GreenProperty, color.G.ToString(CultureInfo.InvariantCulture));
        SetCurrentValue(BlueProperty, color.B.ToString(CultureInfo.InvariantCulture));
        SetCurrentValue(AlphaProperty, color.A.ToString(CultureInfo.InvariantCulture));
        SetCurrentValue(HexProperty, FormatHex(color));
        _syncing = false;

        _hueBrush.Color = HsvToColor(_hue, 1, 1, 1);
        _alphaBrush.GradientStops[0].Color = new Color(color.R, color.G, color.B, (byte)255);
        _alphaBrush.GradientStops[1].Color = new Color(color.R, color.G, color.B, (byte)0);
        _previewBrush.Color = color;
        UpdateThumbs();
    }

    // Centre each thumb on its value point using the thumb's OWN measured size (so the marker sits ON the value, whatever
    // size the theme gives it). The bars stretch their thumb horizontally, so only the vertical centre is offset there.
    private void UpdateThumbs()
    {
        if (_svThumb != null && _svArea != null)
        {
            _svThumb.Margin = new Thickness(
                _sat * _svArea.ActualWidth - _svThumb.ActualWidth / 2,
                (1 - _val) * _svArea.ActualHeight - _svThumb.ActualHeight / 2, 0, 0);
        }

        if (_hueThumb != null && _hueArea != null)
        {
            _hueThumb.Margin = new Thickness(0, _hue / 360 * _hueArea.ActualHeight - _hueThumb.ActualHeight / 2, 0, 0);
        }

        if (_alphaThumb != null && _alphaArea != null)
        {
            _alphaThumb.Margin = new Thickness(0, (1 - _alpha) * _alphaArea.ActualHeight - _alphaThumb.ActualHeight / 2, 0, 0);
        }
    }

    // ---- HSV <-> RGB + hex ---------------------------------------------------------------------------------------

    private static Color HsvToColor(double h, double s, double v, double a)
    {
        var c = v * s;
        var x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        var m = v - c;
        double r = 0, g = 0, b = 0;
        switch (((int)(h / 60) % 6 + 6) % 6)
        {
            case 0: r = c; g = x; break;
            case 1: r = x; g = c; break;
            case 2: g = c; b = x; break;
            case 3: g = x; b = c; break;
            case 4: r = x; b = c; break;
            default: r = c; b = x; break;
        }

        return new Color(ToByte(r + m), ToByte(g + m), ToByte(b + m), ToByte(a));
    }

    private static void RgbToHsv(Color color, out double h, out double s, out double v)
    {
        double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var d = max - min;
        v = max;
        s = max <= 0 ? 0 : d / max;
        if (d <= 0)
        {
            h = 0;
            return;
        }

        if (max == r)
        {
            h = 60 * (((g - b) / d % 6 + 6) % 6);
        }
        else if (max == g)
        {
            h = 60 * ((b - r) / d + 2);
        }
        else
        {
            h = 60 * ((r - g) / d + 4);
        }
    }

    private static bool TryParseHex(string text, out Color color)
    {
        color = Colors.Black;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var s = text.Trim().TrimStart('#');
        // #RGB / #RRGGBB default to opaque; #AARRGGBB carries alpha.
        if (s.Length == 6)
        {
            s = "FF" + s;
        }

        if (s.Length != 8 || !uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
        {
            return false;
        }

        color = new Color((byte)(argb >> 16), (byte)(argb >> 8), (byte)argb, (byte)(argb >> 24));
        return true;
    }

    private static bool TryByte(string text, out byte value)
    {
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
        {
            value = (byte)Math.Clamp(i, 0, 255);
            return true;
        }

        value = 0;
        return false;
    }

    private static string FormatHex(Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value * 255), 0, 255);

    private static double Clamp01(double value) => Math.Clamp(value, 0, 1);
}
