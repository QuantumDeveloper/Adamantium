using System;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media.Animation;

namespace Adamantium.UI.Controls.Shapes;

/// <summary>A <see cref="Rectangle"/> filled with a fractal brush that pans and zooms on mouse input, so fractal
/// exploration works out of the box: drag to pan, wheel to zoom toward the cursor. Both scale their step by the current
/// zoom, so they stay precise at any depth (a fixed pixel drag is a smaller complex-plane step the deeper you are).
/// Pan/zoom drive the two-way <see cref="CenterX"/>/<see cref="CenterY"/>/<see cref="ZoomExp"/> (ZoomExp = log10 of zoom),
/// so bound sliders and read-outs stay in sync. The 1.5 span factor mirrors the fractal shader's fragment-to-complex
/// mapping (the smaller half-axis spans 1.5/zoom around the centre).</summary>
public class FractalView : Rectangle
{
    private const double Span = 1.5;      // matches BatchEffect.fx: cp = center + (local / minHalf) * (1.5 / zoom)
    private const double MinExp = -0.52;  // log10(~0.3x) .. log10(1e15x). Past ~1e5 the shader switches to the perturbation
    private const double MaxExp = 15.0;   // deep path (double reference orbit); the double centre resolves pixels to ~1e15.
    private const double ZoomStep = 0.2;      // ZoomExp added per standard wheel notch (accumulated into the target)
    private const double SmoothRate = 12.0;   // ease-to-target rate per second - higher snaps sooner, lower glides longer

    private double _lastX;
    private double _lastY;

    // Smooth wheel zoom: the wheel accumulates a TARGET exponent and a heartbeat ticker eases the live ZoomExp toward it
    // (like ZoomBox), re-anchoring the centre each step so the complex point under the cursor stays put.
    private double _targetExp;
    private bool _zoomActive;
    private bool _zoomTicker;
    private double _anchorCx;
    private double _anchorCy;
    private double _anchorOffX;
    private double _anchorOffY;

    public FractalView()
    {
        MouseWheel += OnWheel;   // no OnMouseWheel virtual on the base, so hook the routed event
    }

    public static readonly AdamantiumProperty CenterXProperty = AdamantiumProperty.Register(nameof(CenterX),
        typeof(double), typeof(FractalView), new PropertyMetadata(0.0, PropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly AdamantiumProperty CenterYProperty = AdamantiumProperty.Register(nameof(CenterY),
        typeof(double), typeof(FractalView), new PropertyMetadata(0.0, PropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly AdamantiumProperty ZoomExpProperty = AdamantiumProperty.Register(nameof(ZoomExp),
        typeof(double), typeof(FractalView), new PropertyMetadata(0.0, PropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>Complex-plane X at the view centre (two-way, so a bound slider tracks mouse panning).</summary>
    public double CenterX
    {
        get => GetValue<double>(CenterXProperty);
        set => SetValue(CenterXProperty, value);
    }

    /// <summary>Complex-plane Y at the view centre (two-way).</summary>
    public double CenterY
    {
        get => GetValue<double>(CenterYProperty);
        set => SetValue(CenterYProperty, value);
    }

    /// <summary>log10 of the zoom (two-way). Working in log-zoom keeps wheel steps multiplicative.</summary>
    public double ZoomExp
    {
        get => GetValue<double>(ZoomExpProperty);
        set => SetValue(ZoomExpProperty, value);
    }

    // Complex-plane units per pixel at the current size and zoom (the smaller half-axis spans Span/zoom).
    private double UnitsPerPixel()
    {
        var minHalf = Math.Min(ActualWidth, ActualHeight) * 0.5;
        if (minHalf < 1.0) return 0.0;
        return (Span / Math.Pow(10, ZoomExp)) / minHalf;
    }

    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(sender, e);
        var p = e.GetPosition(this);
        _lastX = p.X;
        _lastY = p.Y;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(object sender, MouseEventArgs e)
    {
        base.OnMouseMove(sender, e);
        if (!IsMouseCaptured) return;
        var p = e.GetPosition(this);
        var k = UnitsPerPixel();
        if (k > 0.0)
        {
            CenterX -= (p.X - _lastX) * k;   // grab the content: the point under the cursor stays put as you drag
            CenterY -= (p.Y - _lastY) * k;
        }
        _lastX = p.X;
        _lastY = p.Y;
    }

    protected override void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(sender, e);
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    // Wheel = zoom toward the cursor, SMOOTHLY: accumulate a target exponent and anchor the complex point currently under
    // the cursor; the ticker eases the live zoom to the target, re-deriving the centre so that point stays under the cursor.
    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        var minHalf = Math.Min(ActualWidth, ActualHeight) * 0.5;
        if (minHalf < 1.0) return;
        e.Handled = true;   // consumed as zoom, not page scroll

        var p = e.GetPosition(this);
        _anchorOffX = p.X - ActualWidth * 0.5;
        _anchorOffY = p.Y - ActualHeight * 0.5;
        var k = (Span / Math.Pow(10, ZoomExp)) / minHalf;   // live units/pixel
        _anchorCx = CenterX + _anchorOffX * k;              // the complex point under the cursor right now
        _anchorCy = CenterY + _anchorOffY * k;

        var basis = _zoomActive ? _targetExp : ZoomExp;     // accumulate into the target so quick spins add up
        _targetExp = Math.Clamp(basis + (e.Delta / 120.0) * ZoomStep, MinExp, MaxExp);

        _zoomActive = true;
        if (!_zoomTicker)
        {
            _zoomTicker = true;
            AnimationManager.AddTicker(AdvanceZoom);   // heartbeat: no dirty target, so it keeps the loop presenting while it eases
        }
    }

    // One eased zoom step: glide ZoomExp a fraction toward the target and re-anchor the centre so the point under the cursor
    // stays put. Returns true (dropping the ticker) once the target is reached. AddTicker's delegate: true = done/removed.
    private bool AdvanceZoom(double dt)
    {
        var minHalf = Math.Min(ActualWidth, ActualHeight) * 0.5;
        if (!_zoomActive || minHalf < 1.0)
        {
            _zoomTicker = false;
            return true;
        }

        var cur = ZoomExp;
        var next = cur + (_targetExp - cur) * (1.0 - Math.Exp(-SmoothRate * dt));
        if (Math.Abs(_targetExp - next) < 1e-4) next = _targetExp;

        var k = (Span / Math.Pow(10, next)) / minHalf;
        CenterX = _anchorCx - _anchorOffX * k;   // keep the anchor point under its pixel offset as the zoom eases
        CenterY = _anchorCy - _anchorOffY * k;
        ZoomExp = next;

        var done = next == _targetExp;
        if (done)
        {
            _zoomActive = false;
            _zoomTicker = false;
        }
        return done;
    }
}
