using System;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>
/// A circular HSV picker (the payoff of the conic gradient): a hue wheel (angle = hue) whose radius is saturation
/// (desaturated centre -> full-hue rim), dragged to set hue + saturation at once; <see cref="Value"/> (brightness) rides a
/// separate slider and dims the wheel. The visual is authored from a <c>ConicGradientBrush</c> (hue) under a radial
/// white-&gt;transparent overlay (saturation) - no bespoke rendering. HSV is the internal source of truth (hue is KEPT at
/// grey/black, where RGB can't tell it), and <see cref="SelectedColor"/> is derived from it and, when set, decomposed back.
/// </summary>
public class ColorWheel : Control
{
    // HSV is the source of truth: hue 0..360, sat/val 0..1.
    private double _hue;
    private double _sat = 1;
    private double _val = 1;
    private double _alpha = 1;   // the wheel doesn't EDIT alpha, but it PRESERVES it - so binding to an alpha-carrying colour (an alpha bar on the same colour) isn't clobbered opaque
    private bool _syncing;   // guards the colour <-> hsv <-> Value fan-out from re-entering itself
    private bool _dragging;

    private Border _wheel;
    private Border _valueOverlay;
    private MeasurableUIComponent _thumb;

    public static readonly AdamantiumProperty SelectedColorProperty = AdamantiumProperty.Register(nameof(SelectedColor),
        typeof(Color), typeof(ColorWheel),
        new PropertyMetadata(Colors.Red, PropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedColorChanged));

    public static readonly AdamantiumProperty ValueProperty = AdamantiumProperty.Register(nameof(Value),
        typeof(double), typeof(ColorWheel),
        new PropertyMetadata(1.0, PropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    /// <summary>The picked colour. Two-way; also driven by the wheel drag and <see cref="Value"/>. The clean binding target.</summary>
    public Color SelectedColor { get => GetValue<Color>(SelectedColorProperty); set => SetValue(SelectedColorProperty, value); }

    /// <summary>Brightness 0..1 (the wheel only picks hue + saturation). Two-way - bind a slider to it. Dims the wheel.</summary>
    public double Value { get => GetValue<double>(ValueProperty); set => SetValue(ValueProperty, value); }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (_wheel != null)
        {
            _wheel.SizeChanged -= OnWheelSizeChanged;
        }

        _wheel = GetTemplateChild("PART_Wheel") as Border;
        _valueOverlay = GetTemplateChild("PART_ValueOverlay") as Border;
        _thumb = GetTemplateChild("PART_Thumb") as MeasurableUIComponent;

        if (_wheel != null)
        {
            _wheel.SizeChanged += OnWheelSizeChanged;
        }

        ApplyColor(SelectedColor);
        Commit();
    }

    /// <summary>Let the template's parts go when the template does - see ScrollBar.OnRemoveTemplate.</summary>
    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();
        if (_wheel != null) _wheel.SizeChanged -= OnWheelSizeChanged;
        _wheel = null;
    }

    private void OnWheelSizeChanged(object sender, SizeChangedEventArgs e) => UpdateThumb();

    // ---- Pointer drag over the wheel -----------------------------------------------------------------------------

    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(sender, e);
        if (_wheel == null || !Inside(_wheel, e))
        {
            return;
        }

        _dragging = true;
        CaptureMouse();
        UpdateFromPointer();
        e.Handled = true;
    }

    protected override void OnMouseMove(object sender, MouseEventArgs e)
    {
        base.OnMouseMove(sender, e);
        if (IsMouseCaptured && _dragging)
        {
            UpdateFromPointer();
        }
    }

    protected override void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(sender, e);
        if (_dragging)
        {
            _dragging = false;
            ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private static bool Inside(Border area, MouseButtonEventArgs e)
    {
        var p = e.GetPosition(area);
        return p.X >= 0 && p.Y >= 0 && p.X <= area.ActualWidth && p.Y <= area.ActualHeight;
    }

    // Pointer -> (hue, saturation): angle from the centre (0 at top, clockwise, matching the conic gradient) is hue;
    // distance / radius is saturation, clamped so a drag past the rim pins saturation at 1.
    private void UpdateFromPointer()
    {
        if (_wheel == null)
        {
            return;
        }

        var p = Mouse.GetPosition(_wheel);
        var cx = _wheel.ActualWidth / 2;
        var cy = _wheel.ActualHeight / 2;
        var maxR = Math.Max(1, Math.Min(cx, cy));
        var dx = p.X - cx;
        var dy = p.Y - cy;

        var angle = Math.Atan2(dx, -dy);   // 0 at top, +clockwise (screen y is down) - matches the conic hue layout
        _hue = ((angle * 180 / Math.PI) % 360 + 360) % 360;
        _sat = Clamp01(Math.Sqrt(dx * dx + dy * dy) / maxR);
        Commit();
    }

    // ---- The colour <-> hsv <-> Value fan-out --------------------------------------------------------------------

    private static void OnSelectedColorChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not ColorWheel wheel || wheel._syncing)
        {
            return;
        }

        wheel.ApplyColor((Color)e.NewValue);
        wheel.Commit();
    }

    private static void OnValueChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not ColorWheel wheel || wheel._syncing)
        {
            return;
        }

        wheel._val = Clamp01((double)e.NewValue);
        wheel.Commit();
    }

    // Adopt an externally-set colour into the HSV state. Hue is KEPT at grey/black (RGB can't tell it there) so the thumb
    // doesn't snap as saturation is dragged to the centre.
    private void ApplyColor(Color color)
    {
        // If the incoming colour is exactly what our current HSV already produces, this is the round-trip of our OWN commit
        // (SelectedColor -> here), not an external set - KEEP the HSV state. Re-deriving from RGB loses info the RGB can't
        // carry (saturation is undefined at value 0 -> collapses to 0), which snapped the thumb from the rim to the centre.
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

    // Recompute the colour from HSV and push it to SelectedColor + Value + the visuals, guarded so none re-enters the fan-out.
    private void Commit()
    {
        var color = HsvToColor(_hue, _sat, _val, _alpha);
        _syncing = true;
        SetCurrentValue(SelectedColorProperty, color);
        SetCurrentValue(ValueProperty, _val);
        _syncing = false;

        if (_valueOverlay != null)
        {
            _valueOverlay.Opacity = (1 - _val) * 0.7;   // dim toward (but never fully to) black as value drops, so the hue
                                                         // wheel stays visible/usable even when the picked colour is black
        }

        UpdateThumb();
    }

    // Centre the thumb on the (hue, saturation) point: hue is the angle from the top (clockwise), saturation the fraction of
    // the radius. Uses the thumb's own measured size so the marker sits ON the point whatever size the theme gives it.
    private void UpdateThumb()
    {
        if (_thumb == null || _wheel == null)
        {
            return;
        }

        var cx = _wheel.ActualWidth / 2;
        var cy = _wheel.ActualHeight / 2;
        var maxR = Math.Min(cx, cy);
        var angle = _hue * Math.PI / 180;
        var r = _sat * maxR;
        var px = cx + r * Math.Sin(angle);
        var py = cy - r * Math.Cos(angle);
        _thumb.Margin = new Thickness(px - _thumb.ActualWidth / 2, py - _thumb.ActualHeight / 2, 0, 0);
    }

    // ---- HSV <-> RGB ---------------------------------------------------------------------------------------------

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

    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value * 255), 0, 255);

    private static double Clamp01(double value) => Math.Clamp(value, 0, 1);
}
