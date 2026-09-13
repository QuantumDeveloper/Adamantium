using System;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Animation;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>
/// An unbounded plane to draw and design on: pan it and it never reaches an edge, zoom it and the step of the grid
/// coarsens instead of turning to mush.
/// <para>There is no scroll viewer here and no scrollable extent, which is what "unbounded" costs: panning moves the
/// CAMERA (<see cref="Offset"/>) and has nothing to run into. The scene lives in world coordinates - doubles, so that a
/// canvas the size of a country still has sub-micron steps - and the screen is <c>world * Scale + Offset</c>.</para>
/// <para>The grid is not objects. Only the lines that fall inside the viewport are drawn, and their coordinates come
/// out of the camera: an unbounded grid never exists as data for a moment.</para>
/// </summary>
public class InfiniteCanvas : Control
{
    private bool _zoomActive;
    private bool _zoomTickerRegistered;
    private double _targetScale = 1;
    private Vector2 _zoomAnchorScreen;
    private Vector2 _zoomAnchorWorld;
    private bool _panning;
    private Vector2 _panFrom;
    private Vector2 _panOffsetFrom;
    private bool _spaceHeld;
    private bool _cameraPlaced;

    /// <summary>Screen pixels per world unit.</summary>
    public static readonly AdamantiumProperty ScaleProperty = AdamantiumProperty.Register(nameof(Scale),
        typeof(Double), typeof(InfiniteCanvas),
        new PropertyMetadata(1.0, PropertyMetadataOptions.AffectsRender | PropertyMetadataOptions.BindsTwoWayByDefault,
            OnCameraChanged, CoerceScale));

    /// <summary>Where the world's origin sits on screen, in pixels.</summary>
    public static readonly AdamantiumProperty OffsetProperty = AdamantiumProperty.Register(nameof(Offset),
        typeof(Vector2), typeof(InfiniteCanvas),
        new PropertyMetadata(Vector2.Zero,
            PropertyMetadataOptions.AffectsRender | PropertyMetadataOptions.BindsTwoWayByDefault, OnCameraChanged));

    /// <summary>How far out and in the camera may go. A practical limit, not a technical one - raise it and nothing
    /// breaks.
    /// <para>What would break at a big zoom is precision, and the camera is built so that it does not: the world is in
    /// doubles, and what reaches the drawing is already SCREEN coordinates computed against the camera. So the numbers
    /// that matter stay small however far the origin is, and how far in the camera is zoomed costs nothing. These
    /// defaults are only what a person is likely to want - an application that needs a thousand times says so.</para>
    /// </summary>
    public static readonly AdamantiumProperty MinScaleProperty = AdamantiumProperty.Register(nameof(MinScale),
        typeof(Double), typeof(InfiniteCanvas), new PropertyMetadata(0.01));

    public static readonly AdamantiumProperty MaxScaleProperty = AdamantiumProperty.Register(nameof(MaxScale),
        typeof(Double), typeof(InfiniteCanvas), new PropertyMetadata(256.0));

    public static readonly AdamantiumProperty ZoomWithWheelProperty = AdamantiumProperty.Register(nameof(ZoomWithWheel),
        typeof(Boolean), typeof(InfiniteCanvas), new PropertyMetadata(true));

    /// <summary>The factor one notch of the wheel zooms by. Bigger than a map viewer's, because a canvas is worked at
    /// both ends of its range - a whole board and then one corner of one object - and crawling there a fifth at a time
    /// is a dozen notches each way.</summary>
    public static readonly AdamantiumProperty ZoomStepProperty = AdamantiumProperty.Register(nameof(ZoomStep),
        typeof(Double), typeof(InfiniteCanvas), new PropertyMetadata(1.4));

    /// <summary>How fast the eased zoom catches its target. Higher is snappier; the wheel sets a target and a ticker
    /// walks the live scale to it, so a quick spin adds up instead of stepping.</summary>
    public static readonly AdamantiumProperty ZoomSmoothRateProperty = AdamantiumProperty.Register(
        nameof(ZoomSmoothRate), typeof(Double), typeof(InfiniteCanvas), new PropertyMetadata(10.0));

    public static readonly AdamantiumProperty GridStyleProperty = AdamantiumProperty.Register(nameof(GridStyle),
        typeof(CanvasGridStyle), typeof(InfiniteCanvas),
        new PropertyMetadata(CanvasGridStyle.Dots, PropertyMetadataOptions.AffectsRender));

    /// <summary>The grid's step in WORLD units. What is drawn is this step multiplied by a power of
    /// <see cref="GridCoarsening"/>, chosen so the lines stay readable however far out the camera is.</summary>
    public static readonly AdamantiumProperty GridSpacingProperty = AdamantiumProperty.Register(nameof(GridSpacing),
        typeof(Double), typeof(InfiniteCanvas), new PropertyMetadata(20.0, PropertyMetadataOptions.AffectsRender));

    /// <summary>What the step is multiplied (or divided) by when the drawn grid would be too dense or too sparse.
    /// Ten by default, so the steps a user reads off it stay round.</summary>
    public static readonly AdamantiumProperty GridCoarseningProperty = AdamantiumProperty.Register(
        nameof(GridCoarsening), typeof(Double), typeof(InfiniteCanvas),
        new PropertyMetadata(10.0, PropertyMetadataOptions.AffectsRender));

    /// <summary>How close two grid marks may come on SCREEN before the step is coarsened. Screen pixels, not world
    /// units: how dense a grid reads has nothing to do with how far out the camera is.</summary>
    public static readonly AdamantiumProperty MinGridPitchProperty = AdamantiumProperty.Register(nameof(MinGridPitch),
        typeof(Double), typeof(InfiniteCanvas), new PropertyMetadata(12.0, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty GridBrushProperty = AdamantiumProperty.Register(nameof(GridBrush),
        typeof(Brush), typeof(InfiniteCanvas), new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

    /// <summary>The mark on the world's own axes, drawn where they cross the viewport. Without it there is nothing on
    /// an unbounded plane to say where the origin went.</summary>
    public static readonly AdamantiumProperty AxisBrushProperty = AdamantiumProperty.Register(nameof(AxisBrush),
        typeof(Brush), typeof(InfiniteCanvas), new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty GridThicknessProperty = AdamantiumProperty.Register(nameof(GridThickness),
        typeof(Double), typeof(InfiniteCanvas), new PropertyMetadata(1.0, PropertyMetadataOptions.AffectsRender));

    /// <summary>The side of a dot, in SCREEN pixels - the grid is a ruler, not part of the drawing, so it keeps its
    /// size however far the camera is zoomed.</summary>
    public static readonly AdamantiumProperty GridDotSizeProperty = AdamantiumProperty.Register(nameof(GridDotSize),
        typeof(Double), typeof(InfiniteCanvas), new PropertyMetadata(2.0, PropertyMetadataOptions.AffectsRender));

    public InfiniteCanvas()
    {
        Focusable = true;
        ClipToBounds = true;

        // Its OWN events, so nothing is unsubscribed: the canvas outlives none of these, and there is no template here
        // whose parts could come and go.
        MouseWheel += OnWheel;
        MouseDoubleClick += OnPointerDoubleClick;
        MouseDown += OnPointerDown;
        MouseMove += OnPointerMove;
        MouseUp += OnPointerUp;
        KeyDown += OnKeyPressed;
        KeyUp += OnKeyReleased;
    }

    public Double Scale
    {
        get => GetValue<Double>(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    public Vector2 Offset
    {
        get => GetValue<Vector2>(OffsetProperty);
        set => SetValue(OffsetProperty, value);
    }

    public Double MinScale
    {
        get => GetValue<Double>(MinScaleProperty);
        set => SetValue(MinScaleProperty, value);
    }

    public Double MaxScale
    {
        get => GetValue<Double>(MaxScaleProperty);
        set => SetValue(MaxScaleProperty, value);
    }

    public Boolean ZoomWithWheel
    {
        get => GetValue<Boolean>(ZoomWithWheelProperty);
        set => SetValue(ZoomWithWheelProperty, value);
    }

    public Double ZoomStep
    {
        get => GetValue<Double>(ZoomStepProperty);
        set => SetValue(ZoomStepProperty, value);
    }

    public Double ZoomSmoothRate
    {
        get => GetValue<Double>(ZoomSmoothRateProperty);
        set => SetValue(ZoomSmoothRateProperty, value);
    }

    public CanvasGridStyle GridStyle
    {
        get => GetValue<CanvasGridStyle>(GridStyleProperty);
        set => SetValue(GridStyleProperty, value);
    }

    public Double GridSpacing
    {
        get => GetValue<Double>(GridSpacingProperty);
        set => SetValue(GridSpacingProperty, value);
    }

    public Double GridCoarsening
    {
        get => GetValue<Double>(GridCoarseningProperty);
        set => SetValue(GridCoarseningProperty, value);
    }

    public Double MinGridPitch
    {
        get => GetValue<Double>(MinGridPitchProperty);
        set => SetValue(MinGridPitchProperty, value);
    }

    public Brush GridBrush
    {
        get => GetValue<Brush>(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    public Brush AxisBrush
    {
        get => GetValue<Brush>(AxisBrushProperty);
        set => SetValue(AxisBrushProperty, value);
    }

    public Double GridThickness
    {
        get => GetValue<Double>(GridThicknessProperty);
        set => SetValue(GridThicknessProperty, value);
    }

    public Double GridDotSize
    {
        get => GetValue<Double>(GridDotSizeProperty);
        set => SetValue(GridDotSizeProperty, value);
    }

    /// <summary>The world step actually being drawn - <see cref="GridSpacing"/> coarsened to whatever the camera makes
    /// readable. What a ruler or a snap has to agree with, so it is asked for rather than recomputed.</summary>
    public Double EffectiveGridSpacing => Coarsened(GridSpacing);

    /// <summary>The piece of the world the viewport is showing.</summary>
    public Rect VisibleWorld
    {
        get
        {
            var size = RenderSize;
            var topLeft = ScreenToWorld(Vector2.Zero);
            var bottomRight = ScreenToWorld(new Vector2(size.Width, size.Height));

            return new Rect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
        }
    }

    /// <summary>Where a world point lands on screen. Public because no drawing tool can be written without it.</summary>
    public Vector2 WorldToScreen(Vector2 world) => world * Scale + Offset;

    /// <summary>What a screen point is in the world.</summary>
    public Vector2 ScreenToWorld(Vector2 screen) => (screen - Offset) * (1.0 / Scale);

    /// <summary>A length measured on SCREEN, said in world units. Every tolerance - what counts as a hit, how close a
    /// snap pulls - is a screen distance: at 20x nobody can hit a thin line given in world units, and at 0.1x everything
    /// is a hit.</summary>
    public Double ScreenToWorldLength(Double screenPixels) => screenPixels / Scale;

    /// <summary>Moves the camera by a distance measured on screen.</summary>
    public void PanBy(Vector2 screenDelta) => SetCurrentValue(OffsetProperty, Offset + screenDelta);

    /// <summary>Puts the camera where the given piece of world fills the viewport, with room to spare around it.</summary>
    public void ScaleToFit(Rect world, Double padding = 24)
    {
        var size = RenderSize;
        if (world.Width <= 0 || world.Height <= 0 || size.Width <= 0 || size.Height <= 0) return;

        var usable = new Size(Math.Max(1, size.Width - padding * 2), Math.Max(1, size.Height - padding * 2));
        var scale = Math.Clamp(Math.Min(usable.Width / world.Width, usable.Height / world.Height), MinScale, MaxScale);

        StopZoom();
        SetCurrentValue(ScaleProperty, scale);
        CentreOn(new Vector2(world.X + world.Width / 2, world.Y + world.Height / 2));
    }

    /// <summary>Moves the camera - and only the camera - so a piece of world is on screen. Does not zoom: something
    /// already the right size does not need resizing to be looked at.</summary>
    public void BringIntoView(Rect world)
    {
        var size = RenderSize;
        if (size.Width <= 0 || size.Height <= 0) return;

        var topLeft = WorldToScreen(new Vector2(world.X, world.Y));
        var bottomRight = WorldToScreen(new Vector2(world.X + world.Width, world.Y + world.Height));

        var dx = Shift(topLeft.X, bottomRight.X, size.Width);
        var dy = Shift(topLeft.Y, bottomRight.Y, size.Height);

        if (dx != 0 || dy != 0) PanBy(new Vector2(dx, dy));
    }

    /// <summary>Puts a world point in the middle of the viewport.</summary>
    public void CentreOn(Vector2 world)
    {
        var size = RenderSize;
        SetCurrentValue(OffsetProperty, new Vector2(size.Width / 2, size.Height / 2) - world * Scale);
    }

    /// <summary>Back to where everything starts: the world's origin in the middle of the viewport, at one to one.
    /// <para>The plane has no edges and therefore no corner to scroll back to, so getting lost on it has to have a way
    /// out. <c>Home</c> does this.</para></summary>
    public void ResetCamera()
    {
        StopZoom();
        SetCurrentValue(ScaleProperty, Math.Clamp(1.0, MinScale, MaxScale));
        CentreOn(Vector2.Zero);
    }

    /// <summary>Zooms about a point ON SCREEN, keeping the world under it still - what the wheel does, offered for a
    /// button or a test.</summary>
    public void ZoomAt(Vector2 screen, Double factor)
    {
        var basis = _zoomActive ? _targetScale : Scale;
        var wanted = Math.Clamp(basis * factor, MinScale, MaxScale);

        _zoomAnchorScreen = screen;
        _zoomAnchorWorld = ScreenToWorld(screen);
        _targetScale = wanted;

        _zoomActive = true;
        if (_zoomTickerRegistered) return;

        _zoomTickerRegistered = true;
        AnimationManager.AddTicker(AdvanceZoom);
    }

    /// <summary>Sets the scale straight, with no easing, keeping a screen point still.</summary>
    public void SetScaleAt(Vector2 screen, Double scale)
    {
        var world = ScreenToWorld(screen);

        StopZoom();
        SetCurrentValue(ScaleProperty, Math.Clamp(scale, MinScale, MaxScale));
        SetCurrentValue(OffsetProperty, screen - world * Scale);
    }

    protected override void OnRender(IDrawingContext context)
    {
        base.OnRender(context);

        var size = RenderSize;
        if (size.Width <= 0 || size.Height <= 0) return;

        var session = context.ForControl(this);
        var area = new Rect(0, 0, size.Width, size.Height);

        // The ground is drawn here rather than left to a template: the canvas has no template, and a transparent ground
        // would also mean no hit test - nothing to pan or draw on.
        session.DrawRectangle(Background ?? Brushes.Transparent, area);

        DrawGrid(session, size);
        DrawAxes(session, size);
    }

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        if (!ZoomWithWheel || e.Handled) return;

        // ZoomStep is one standard notch (120), raised to how far the wheel actually turned - so a hi-res wheel firing
        // many fractional events zooms the same total as a standard one.
        ZoomAt(e.GetPosition(this), Math.Pow(ZoomStep, e.Delta / 120.0));
        e.Handled = true;
    }

    // The middle button pans, so double-clicking it is the gesture for "put it back" - the same button, and the hand
    // is already on it. Marked handled, and the device raises MouseDown from the SAME args right after this, so the
    // press does not also start a pan.
    private void OnPointerDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled || e.ChangedButton != MouseButtons.Middle) return;

        Focus();
        ResetCamera();
        e.Handled = true;
    }

    // Middle button, or space and the left one. Both, because both are what people already do - and the left button on
    // its own is left alone, because that is what the tools will want.
    private void OnPointerDown(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled) return;

        // Any press takes the focus, not only one that pans: the keys the canvas answers - Home, and space to pan with -
        // are useless until it has the focus, and nobody drags a canvas to be allowed to press Home.
        Focus();

        var pans = e.ChangedButton == MouseButtons.Middle || (e.ChangedButton == MouseButtons.Left && _spaceHeld);
        if (!pans) return;

        _panning = true;
        _panFrom = e.GetPosition(this);
        _panOffsetFrom = Offset;

        CaptureMouse();
        e.Handled = true;
    }

    private void OnPointerMove(object sender, MouseEventArgs e)
    {
        if (!_panning) return;

        // From where the drag STARTED, not from the last move: accumulating deltas drifts, and the camera has nothing
        // to clamp against that would hide it.
        SetCurrentValue(OffsetProperty, _panOffsetFrom + (e.GetPosition(this) - _panFrom));
        e.Handled = true;
    }

    private void OnPointerUp(object sender, MouseButtonEventArgs e)
    {
        if (!_panning) return;

        _panning = false;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    private void OnKeyPressed(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            _spaceHeld = true;
            return;
        }

        if (e.Key != Key.Home) return;

        ResetCamera();
        e.Handled = true;
    }

    private void OnKeyReleased(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space) _spaceHeld = false;
    }

    private static Double Shift(Double from, Double to, Double viewport)
    {
        if (from < 0 && to <= viewport) return -from;
        if (to > viewport && from >= 0) return viewport - to;

        return 0;
    }

    private static Object CoerceScale(AdamantiumComponent component, Object value)
    {
        if (component is not InfiniteCanvas canvas || value is not Double scale) return value;

        return Math.Clamp(scale, canvas.MinScale, canvas.MaxScale);
    }

    private static void OnCameraChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not InfiniteCanvas canvas) return;

        // Whoever moved the camera - a binding, the wheel, the application - has placed it, and the canvas must not
        // then move it somewhere of its own on the first layout.
        if (e.Property == OffsetProperty) canvas._cameraPlaced = true;

        canvas.InvalidateRender(false);
    }

    // The world's origin starts in the MIDDLE of the viewport, not in its top-left corner. Zero offset would put it in
    // the corner, and a plane whose origin sits off in a corner reads as one that has been scrolled away from - on
    // something with no edges that is the one impression to avoid.
    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);

        // From finalSize, not RenderSize: this IS the pass that sets the latter.
        if (!_cameraPlaced && finalSize.Width > 0 && finalSize.Height > 0)
        {
            _cameraPlaced = true;
            SetCurrentValue(OffsetProperty, new Vector2(finalSize.Width / 2, finalSize.Height / 2));
        }

        return size;
    }

    // One eased step toward the wheel's target, re-anchoring the offset so the world under the cursor stays put.
    private bool AdvanceZoom(double dt)
    {
        if (!_zoomActive)
        {
            _zoomTickerRegistered = false;
            return true;
        }

        var current = Scale;
        var next = current + (_targetScale - current) * (1.0 - Math.Exp(-ZoomSmoothRate * dt));
        if (Math.Abs(_targetScale - next) < _targetScale * 1e-3) next = _targetScale;

        SetCurrentValue(ScaleProperty, next);
        SetCurrentValue(OffsetProperty, _zoomAnchorScreen - _zoomAnchorWorld * Scale);

        var done = Scale == _targetScale;
        if (done)
        {
            _zoomActive = false;
            _zoomTickerRegistered = false;
        }

        return done;
    }

    private void StopZoom()
    {
        _zoomActive = false;
        _targetScale = Scale;
    }

    // The step the camera makes readable: GridSpacing multiplied or divided by whole powers of GridCoarsening until two
    // marks are at least MinGridPitch apart on screen. Powers, not a free scale, so what the user reads off the grid
    // stays round - 10, 100, 1000, and not 37.
    private Double Coarsened(Double spacing)
    {
        if (spacing <= 0) return 0;

        var coarsening = Math.Max(2, GridCoarsening);
        var pitch = Math.Max(1, MinGridPitch);
        var step = spacing;

        while (step * Scale < pitch) step *= coarsening;
        while (step * Scale >= pitch * coarsening) step /= coarsening;

        return step;
    }

    private void DrawGrid(IDrawingSession session, Size size)
    {
        var style = GridStyle;
        if (style == CanvasGridStyle.None || GridBrush == null) return;

        var step = Coarsened(GridSpacing);
        if (step <= 0) return;

        var world = VisibleWorld;
        var firstX = Math.Floor(world.X / step) * step;
        var firstY = Math.Floor(world.Y / step) * step;

        if (style == CanvasGridStyle.Lines)
        {
            var pen = new Pen(GridBrush, GridThickness);

            for (var x = firstX; x <= world.X + world.Width; x += step)
            {
                var at = WorldToScreen(new Vector2(x, 0)).X;
                session.DrawLine(new Vector2(at, 0), new Vector2(at, size.Height), pen);
            }

            for (var y = firstY; y <= world.Y + world.Height; y += step)
            {
                var at = WorldToScreen(new Vector2(0, y)).Y;
                session.DrawLine(new Vector2(0, at), new Vector2(size.Width, at), pen);
            }

            return;
        }

        // Dots keep their SCREEN size: the grid is a ruler laid over the drawing, not part of it.
        var dot = Math.Max(1, GridDotSize);
        var half = dot / 2;

        for (var x = firstX; x <= world.X + world.Width; x += step)
        {
            for (var y = firstY; y <= world.Y + world.Height; y += step)
            {
                var at = WorldToScreen(new Vector2(x, y));
                session.DrawRectangle(GridBrush, new Rect(at.X - half, at.Y - half, dot, dot));
            }
        }
    }

    // The world's own axes, where they cross the viewport. On a plane with no edges this is the only thing that says
    // where the origin is.
    private void DrawAxes(IDrawingSession session, Size size)
    {
        if (AxisBrush == null) return;

        var origin = WorldToScreen(Vector2.Zero);
        var pen = new Pen(AxisBrush, GridThickness);

        if (origin.X >= 0 && origin.X <= size.Width)
        {
            session.DrawLine(new Vector2(origin.X, 0), new Vector2(origin.X, size.Height), pen);
        }

        if (origin.Y >= 0 && origin.Y <= size.Height)
        {
            session.DrawLine(new Vector2(0, origin.Y), new Vector2(size.Width, origin.Y), pen);
        }
    }
}
