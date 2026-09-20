using Adamantium.Graphics.Fonts;
using Adamantium.ProceduralGeometry.Shapes;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Animation;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.DrawingBoard;

// HOW THE PLANE IS LOOKED AT: the camera, the stack of layers the scene is cut into, what the canvas itself draws, and
// the chrome standing over it.
public partial class InfiniteCanvas
{
    /// <summary>The world step actually being drawn - <see cref="GridSpacing"/> coarsened to whatever the camera makes
    /// readable. What a ruler or a snap has to agree with, so it is asked for rather than recomputed.</summary>
    public Double EffectiveGridSpacing => Coarsened(GridSpacing);

    /// <summary>The piece of the world the viewport is showing.</summary>
    public Rect VisibleWorld => WorldAcross(RenderSize);

    /// <summary>The piece of the world a viewport THAT SIZE would show - asked for by whoever knows the size before the
    /// canvas does, which is the arrange pass that sets <see cref="IUIComponent.RenderSize"/> in the first place.
    /// </summary>
    public Rect WorldAcross(Size viewport)
    {
        var topLeft = ScreenToWorld(Vector2.Zero);
        var bottomRight = ScreenToWorld(new Vector2(viewport.Width, viewport.Height));

        return new Rect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
    }

    /// <summary>Where a world point lands on screen. Public because no drawing tool can be written without it.</summary>
    public Vector2 WorldToScreen(Vector2 world) => world * Scale + Offset;

    /// <summary>What a screen point is in the world.</summary>
    public Vector2 ScreenToWorld(Vector2 screen) => (screen - Offset) * (1.0 / Scale);

    /// <summary>A length measured on SCREEN, said in world units. Every tolerance - what counts as a hit, how close a
    /// snap pulls - is a screen distance: at 20x nobody can hit a thin line given in world units.</summary>
    public Double ScreenToWorldLength(Double screenPixels) => screenPixels / Scale;

    /// <summary>Moves the camera by a distance measured on screen.</summary>
    public void PanBy(Vector2 screenDelta) => SetCurrentValue(OffsetProperty, Offset + screenDelta);

    /// <summary>The part of the viewport a person can actually see the plane through: everything a DOCKED pane has
    /// taken is gone from it. The honest answer to "where is the middle", which a minimap or a ruler wants and
    /// <see cref="Control.RenderSize"/> does not give.</summary>
    public Rect UsableBounds
    {
        get
        {
            var size = RenderSize;
            var taken = _chromeLayer?.Inset() ?? new Thickness(0);

            var width = Math.Max(1, size.Width - taken.Left - taken.Right);
            var height = Math.Max(1, size.Height - taken.Top - taken.Bottom);

            return new Rect(taken.Left, taken.Top, width, height);
        }
    }

    /// <summary>Puts the camera where the given piece of world fills the USABLE viewport, with room to spare: a docked
    /// panel is a wall, and fitting behind one puts half the drawing where nobody can look at it.</summary>
    public void ScaleToFit(Rect world, Double padding = 24)
    {
        var room = UsableBounds;
        if (world.Width <= 0 || world.Height <= 0 || room.Width <= 0 || room.Height <= 0) return;

        var usable = new Size(Math.Max(1, room.Width - padding * 2), Math.Max(1, room.Height - padding * 2));
        var scale = Math.Clamp(Math.Min(usable.Width / world.Width, usable.Height / world.Height), MinScale, MaxScale);

        StopZoom();
        SetCurrentValue(ScaleProperty, scale);
        CenterOn(new Vector2(world.X + world.Width / 2, world.Y + world.Height / 2));
    }

    /// <summary>Puts the camera on ONE item: centred, and zoomed so it fills the usable viewport. A flat item - a line
    /// has no height - is looked at through the square that holds it, since fitting to a box with a zero side does
    /// nothing at all.</summary>
    public void ZoomTo(ICanvasItem item, Double padding = 40)
    {
        if (item == null) return;

        var box = item.Bounds;
        if (box.Width > 0 && box.Height > 0)
        {
            ScaleToFit(box, padding);
            return;
        }

        var span = Math.Max(Math.Max(box.Width, box.Height), 1);
        ScaleToFit(new Rect(box.X + box.Width / 2 - span / 2, box.Y + box.Height / 2 - span / 2, span, span), padding);
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

    /// <summary>The world point in the middle of the usable viewport - the one thing about the camera that means the
    /// same in a window of another size, which is why it and not the origin in pixels is what gets written down.
    /// </summary>
    public Vector2 Looking
    {
        get
        {
            var room = UsableBounds;

            return ScreenToWorld(new Vector2(room.X + room.Width / 2, room.Y + room.Height / 2));
        }
    }

    private Vector2? _lookWanted;
    private Thickness _lastInset = new(-1);

    private static bool Same(Thickness one, Thickness other) =>
        Math.Abs(one.Left - other.Left) < 0.01 && Math.Abs(one.Top - other.Top) < 0.01
        && Math.Abs(one.Right - other.Right) < 0.01 && Math.Abs(one.Bottom - other.Bottom) < 0.01;

    /// <summary>Look at a world point - now, or as soon as the canvas is on screen: a saved setup arrives before the
    /// first layout, and a viewport of nothing has no middle.</summary>
    public void Look(Vector2 world)
    {
        if (RenderSize is { Width: > 0, Height: > 0 })
        {
            CenterOn(world);
            return;
        }

        _lookWanted = world;
        InvalidateArrange();
    }

    /// <summary>Puts a world point in the middle of the USABLE viewport - the middle of what is not behind a docked
    /// panel.</summary>
    public void CenterOn(Vector2 world) => CenterOn(world, RenderSize);

    // ...against a viewport SAID rather than read. Inside an arrange pass RenderSize is still what the canvas used to
    // be - this pass is what sets it - and centring then lands against a size of nothing.
    private void CenterOn(Vector2 world, Size viewport)
    {
        var taken = _chromeLayer?.Inset() ?? new Thickness(0);
        var width = Math.Max(1, viewport.Width - taken.Left - taken.Right);
        var height = Math.Max(1, viewport.Height - taken.Top - taken.Bottom);
        var middle = new Vector2(taken.Left + width / 2, taken.Top + height / 2);

        SetCurrentValue(OffsetProperty, middle - world * Scale);
    }

    /// <summary>How much of the viewport a fit leaves empty round what it is showing, as a fraction. A drawing pressed
    /// against the edges of the screen reads as one that has been cut off.</summary>
    public static readonly AdamantiumProperty FitMarginProperty = AdamantiumProperty.Register(nameof(FitMargin),
        typeof(Double), typeof(InfiniteCanvas), new PropertyMetadata(0.08));

    public Double FitMargin
    {
        get => GetValue<Double>(FitMarginProperty);
        set => SetValue(FitMarginProperty, value);
    }

    /// <summary>Points the camera at a box in the WORLD and zooms so the whole of it is in view. A box with no size is
    /// CENTRED at the zoom already in hand rather than zoomed to infinity.</summary>
    public void Fit(Rect world)
    {
        var room = UsableBounds;
        if (room.Width <= 1 || room.Height <= 1) return;

        StopZoom();

        var middle = new Vector2(world.X + world.Width / 2, world.Y + world.Height / 2);
        var margin = Math.Clamp(FitMargin, 0, 0.45);

        if (world.Width > 1e-6 && world.Height > 1e-6)
        {
            var wide = room.Width * (1 - margin * 2) / world.Width;
            var tall = room.Height * (1 - margin * 2) / world.Height;

            SetCurrentValue(ScaleProperty, Math.Clamp(Math.Min(wide, tall), MinScale, MaxScale));
        }

        CenterOn(middle);
    }

    /// <summary>Brings what is SELECTED into view. Nothing selected is not a failure - there is nothing to look at, and
    /// moving the camera would answer a question nobody asked.</summary>
    public bool FitSelection()
    {
        if (SelectionBounds is not { } bounds) return false;

        Fit(bounds);
        return true;
    }

    /// <summary>Brings the WHOLE of what is on the plane into view - of the current mode, because what the other mode
    /// holds is not being looked at.</summary>
    public bool FitAll()
    {
        var any = false;
        double left = 0, top = 0, right = 0, bottom = 0;

        foreach (var item in ItemsHere())
        {
            var box = item.Bounds;
            if (box.Width <= 0 && box.Height <= 0 && !any) continue;

            if (!any)
            {
                left = box.X;
                top = box.Y;
                right = box.X + box.Width;
                bottom = box.Y + box.Height;
                any = true;
                continue;
            }

            left = Math.Min(left, box.X);
            top = Math.Min(top, box.Y);
            right = Math.Max(right, box.X + box.Width);
            bottom = Math.Max(bottom, box.Y + box.Height);
        }

        if (!any) return false;

        Fit(new Rect(left, top, right - left, bottom - top));
        return true;
    }

    /// <summary>Back to the view that was kept - the world's origin at one to one until a bookmark says otherwise. The
    /// plane has no edges and therefore no corner to scroll back to, so getting lost on it has to have a way out.
    /// </summary>
    public void ResetCamera()
    {
        StopZoom();
        SetCurrentValue(ScaleProperty, Math.Clamp(HomeScale, MinScale, MaxScale));
        CenterOn(HomeAt);
    }

    /// <summary>Zooms about the middle of what can be seen - what a plus or a minus button means, as against the wheel,
    /// which zooms about the pointer. Writing <see cref="Scale"/> instead would zoom about the world's ORIGIN, usually
    /// not on screen at all.</summary>
    public void ZoomBy(Double factor)
    {
        var room = UsableBounds;

        ZoomAt(new Vector2(room.X + room.Width / 2, room.Y + room.Height / 2), factor);
    }

    /// <summary>Zooms about a point ON SCREEN, keeping the world under it still - what the wheel does.</summary>
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
        _holdingPoint = true;

        try
        {
            SetCurrentValue(ScaleProperty, Math.Clamp(scale, MinScale, MaxScale));
        }
        finally
        {
            _holdingPoint = false;
        }

        SetCurrentValue(OffsetProperty, screen - world * Scale);
    }

    // Whoever HOLDS a point while the scale is written says so - the wheel, the buttons, SetScaleAt. Nobody holding one
    // means the middle of the viewport is held instead.
    private bool _holdingPoint;

    private void KeepTheMiddle(Double was)
    {
        if (_holdingPoint || _zoomActive || was <= 0 || !_cameraPlaced) return;

        var room = UsableBounds;
        var middle = new Vector2(room.X + room.Width / 2, room.Y + room.Height / 2);
        var world = (middle - Offset) * (1.0 / was);

        SetCurrentValue(OffsetProperty, middle - world * Scale);

        // A number typed into the panel is a finished gesture by itself.
        RememberLayout();
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

        _holdingPoint = true;

        try
        {
            SetCurrentValue(ScaleProperty, next);
        }
        finally
        {
            _holdingPoint = false;
        }

        SetCurrentValue(OffsetProperty, _zoomAnchorScreen - _zoomAnchorWorld * Scale);

        var done = Scale == _targetScale;
        if (done)
        {
            _zoomActive = false;
            _zoomTickerRegistered = false;
            RememberLayout();
        }

        return done;
    }

    private void StopZoom()
    {
        _zoomActive = false;
        _targetScale = Scale;
    }

    // The step the camera makes readable: GridSpacing multiplied or divided by whole powers of GridCoarsening until two
    // marks are at least MinGridPitch apart on screen. Powers, so what is read off the grid stays round.
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

    // WHAT IS ON THE PLANE IS NOT DRAWN HERE, and neither is the chrome over it: the canvas's own pass is one place in
    // paint order - underneath everything - and the plane has many. The items are drawn by the stack of layers cut from
    // the scene's order; the frame round what is selected goes with the layer that holds it.
    protected override void OnRender(IDrawingContext context)
    {
        base.OnRender(context);

        var size = RenderSize;
        if (size.Width <= 0 || size.Height <= 0) return;

        var session = context.ForControl(this);

        // GLASS: no ground at all, so whatever the canvas is laid over shows through. Skipped rather than painted
        // transparent - the ground is one quad over the whole viewport, and a mark-up layer should not pay for a
        // surface it does not have.
        if (GridStyle != CanvasGridStyle.Transparent) DrawGround(session, size);
    }

    // What the canvas draws is drawn in TWO places - here and in the layers - so the two are marked together. Anything
    // that changes the picture goes through this rather than InvalidateRender.
    internal void Repaint()
    {
        InvalidateRender(false);

        Drawn(layer => layer.Repaint());

        _front?.InvalidateRender(false);
    }

    /// <summary>Everything of the current mode, wherever it is - what a whole graph is, as against what can be seen of
    /// one. Saving a document is the case: a node left three screens away is still in it.</summary>
    public IEnumerable<ICanvasItem> ItemsHere() => ItemsHere(Everything);

    /// <summary>What is on the plane inside a world rectangle AND belongs to the mode the canvas is in. THE one place
    /// the mode is applied: every walk over the scene goes through here, or a mode would mean something slightly
    /// different to each of them.</summary>
    public IEnumerable<ICanvasItem> ItemsHere(Rect world)
    {
        if (Scene is not { } scene) yield break;

        var mode = Mode;
        foreach (var item in scene.ItemsIn(world))
        {
            if (item.Mode == mode) yield return item;
        }
    }

    /// <summary>Draws what the TOOL is making but has not put on the plane yet - a stroke still under the pen, a shape
    /// being dragged out, the selection band. On top of everything, which is where it is going.</summary>
    public void DrawInProgress(IDrawingSession session) => Tool?.Render(session, this);

    /// <summary>Whether the frame belongs straight AFTER this item - true for the topmost selected thing that a layer
    /// draws itself. Asked by the layers as they paint.</summary>
    public bool ChromeGoesAfter(ICanvasItem item) => _chromeAfter != null && ReferenceEquals(item, _chromeAfter);

    /// <summary>Whether the frame belongs BEFORE this item - true when what is held is a CONTROL, which no layer draws:
    /// the frame then goes at the start of the first drawn run above it.</summary>
    public bool ChromeGoesBefore(ICanvasItem item) => _chromeBefore != null && ReferenceEquals(item, _chromeBefore);

    /// <summary>Whether the frame has nowhere on the plane to be drawn and falls to the glass - what happens when what
    /// is held is at the very top, or off the screen entirely.</summary>
    public bool ChromeGoesOnGlass => _chromeOnGlass;

    public void DrawChrome(IDrawingSession session) => DrawManipulation(session);

    /// <summary>Draws what belongs to the POINTER rather than to the plane - the snap mark it has caught and the plate
    /// saying where it is. Always on the glass: a plate read through the drawing it measures is no plate at all.
    /// </summary>
    public void DrawPointer(IDrawingSession session)
    {
        if (_snap is not { } at || SnapMarkBrush == null || SnapMarkSize <= 0) return;

        var mark = WorldToScreen(at);
        var half = SnapMarkSize / 2;

        session.DrawEllipse(new Rect(mark.X - half, mark.Y - half, SnapMarkSize, SnapMarkSize),
            SnapMarkBrush, 0, 360, EllipseType.Sector);

        DrawReadout(session, at);
    }

    // WHERE THE FRAME GOES, worked out once whenever the stack or the selection changes rather than per item as the
    // layers paint.
    private void MarkChrome()
    {
        _chromeAfter = null;
        _chromeBefore = null;
        _chromeOnGlass = false;

        if (Held is not { } held) return;

        var found = false;

        foreach (var (hosted, items) in _runs)
        {
            if (!found)
            {
                foreach (var item in items)
                {
                    if (!ReferenceEquals(item, held)) continue;

                    found = true;
                    break;
                }

                if (!found) continue;

                // Drawn by its own layer, so the frame follows it there. A control is not drawn by anybody, and the
                // search goes on: the frame belongs at the start of the first run that IS drawn above it.
                if (!hosted)
                {
                    _chromeAfter = held;
                    return;
                }

                continue;
            }

            if (hosted || items.Count == 0) continue;

            _chromeBefore = items[0];
            return;
        }

        // Nothing is drawn over it - or it is not on the screen at all, in which case the frame is what says so.
        _chromeOnGlass = true;
    }

    // THE TOPMOST OF WHAT IS HELD. One frame is drawn round the whole selection, so it stands at the place of the one
    // furthest forward - anywhere lower and it would be cut by something the selection itself contains.
    private ICanvasItem Held
    {
        get
        {
            if (!IsDesignMode || _selection.Count == 0) return null;

            var top = _selection[0];

            for (var i = 1; i < _selection.Count; i++)
            {
                if (_selection[i].Order > top.Order) top = _selection[i];
            }

            return top;
        }
    }

    // ONE rectangle for the whole ground: the shader decides per pixel, from the world coordinate under it, whether it
    // is on a mark. A rectangle per mark was about a thousand a frame at 1:1 and could anti-alias none of them.
    private void DrawGround(IDrawingSession session, Size size)
    {
        _ground.Marks = (CanvasGridMarks)GridStyle;
        _ground.Offset = Offset;
        _ground.Scale = Scale;
        // The step ALREADY coarsened - the same number EffectiveGridSpacing reports, so a ruler and a snap agree with
        // what is actually drawn.
        _ground.Spacing = EffectiveGridSpacing;
        _ground.Coarsening = GridCoarsening;
        _ground.MinPitch = MinGridPitch;
        _ground.MarkSize = GridStyle == CanvasGridStyle.Dots ? GridDotSize : GridThickness;
        _ground.Background = ColorOf(Background, new Color(0, 0, 0, 0));
        _ground.Color = ColorOf(GridBrush, new Color(0, 0, 0, 0));
        _ground.AxisColor = ColorOf(AxisBrush, new Color(0, 0, 0, 0));

        session.DrawRectangle(_ground, new Rect(0, 0, size.Width, size.Height));
    }

    // The plate that says where the pointer is, BESIDE it: a number drawn where the mark is would cover the very
    // crossing it is about.
    private void DrawReadout(IDrawingSession session, Vector2 world)
    {
        if (!ShowsPointerReadout || !_pointerInside || ReadoutSize <= 0) return;
        if (HandleBrush is not { } plate || SelectionBrush is not SolidColorBrush ink) return;

        // Invariant, so the decimal separator is a point whatever the machine's language is: under a comma locale the
        // pair read as one number with too many digits.
        var text = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.##}; {1:0.##}",
            world.X, world.Y);
        var size = ShapeReadout(text);
        if (size.Width <= 0 || size.Height <= 0) return;

        const double padding = 5;
        const double away = 14;

        var room = RenderSize;
        var box = new Rect(_pointer.X + away, _pointer.Y + away,
            size.Width + padding * 2, size.Height + padding * 2);

        // Flipped to the other side of the pointer rather than merely clamped: a plate that slid along the edge would
        // sit under the pointer at the corner, which is the one place it must not be.
        if (box.X + box.Width > room.Width) box = new Rect(_pointer.X - away - box.Width, box.Y, box.Width, box.Height);
        if (box.Y + box.Height > room.Height) box = new Rect(box.X, _pointer.Y - away - box.Height, box.Width, box.Height);

        session.DrawRectangle(plate, box, PenOf(SelectionBrush, ref _selectionPen, ref _selectionPenBrush));

        session.DrawText(
            new TextRenderingParameters
            {
                HorizontalTextAlignment = HorizontalTextAlignment.Left,
                VerticalTextAlignment = VerticalTextAlignment.Top,
                TextTrimming = TextTrimming.None,
                TextWrapping = TextWrapping.NoWrap,
                Color = ink.Color,
                TextArea = new Rectangle(
                    new Vector2F((float)(box.X + padding), (float)(box.Y + padding)), size)
            },
            size, _readout, SelectionBrush, Brushes.Transparent, Brushes.Transparent);
    }

    // Shaped only when the digits actually change - the pointer moves far more often than the numbers it is over do.
    private Size ShapeReadout(string text)
    {
        var font = UIComponent.DefaultFontFamily;
        if (font == null) return default;

        if (_readout == null || !ReferenceEquals(_readoutFont, font))
        {
            _readout = new TextLayout(font.Typeface, font.Fonts[0]);
            _readoutFont = font;
            _readoutText = null;
        }

        if (_readoutText == text) return _readoutSize;

        _readoutSize = _readout.ProcessText(text, ReadoutSize, new Size(double.NaN, double.NaN), TextWrapping.NoWrap,
            TextTrimming.None, HorizontalTextAlignment.Left, VerticalTextAlignment.Top, false);
        _readoutText = text;

        return _readoutSize;
    }

    // The manipulation frame: one box round everything selected, with eight grips on it. Drawn in SCREEN pixels and
    // never scaled - a grip that grew with the zoom would be part of the drawing, which is exactly what it is not.
    private void DrawManipulation(IDrawingSession session)
    {
        // Not while the controls are being USED: nothing on the plane can be edited in that mode, and a frame drawn
        // there is a handle that does not work.
        if (!IsDesignMode) return;

        if (SelectionBounds is not { } bounds || SelectionBrush == null) return;

        var pen = PenOf(SelectionBrush, ref _selectionPen, ref _selectionPenBrush);
        var frame = FrameOf(bounds);

        // TURNED, the frame is four lines and not a rectangle: a rectangle stands square by definition, and a square
        // frame round a turned shape says the shape is somewhere it is not.
        if (SelectionTurn is { } turn)
        {
            var about = WorldToScreen(SelectionMiddle);
            var corner = new[]
            {
                turn.Apply(new Vector2(frame.X, frame.Y), about),
                turn.Apply(new Vector2(frame.X + frame.Width, frame.Y), about),
                turn.Apply(new Vector2(frame.X + frame.Width, frame.Y + frame.Height), about),
                turn.Apply(new Vector2(frame.X, frame.Y + frame.Height), about)
            };

            for (var i = 0; i < 4; i++) session.DrawLine(corner[i], corner[(i + 1) % 4], pen);

            if (HandleSize > 0 && OfferedHandles.HasFlag(CanvasHandles.Corners))
            {
                var side = HandleSize;
                var half = side / 2;

                foreach (var grip in corner)
                {
                    session.DrawRectangle(HandleBrush ?? SelectionBrush,
                        new Rect(grip.X - half, grip.Y - half, side, side), pen);
                }
            }

            return;
        }

        // Only the grips that would ANSWER: one drawn where nothing can be dragged is an invitation to a gesture that
        // does nothing.
        var offered = OfferedHandles;
        var corners = offered.HasFlag(CanvasHandles.Corners);
        var sides = offered.HasFlag(CanvasHandles.Sides);

        // A frame round ONE thing whose own shape says where it is - a line, a curve - is the same mistake one size
        // larger: the box of a diagonal is mostly nowhere near the shape. SEVERAL things at once are the exception:
        // a band round five says nothing by itself, and its box is the only mark that says what was caught.
        if (corners || sides || _selection.Count > 1) session.DrawRectangle(null, frame, pen);

        if (HandleSize <= 0) return;

        // BEFORE the frame's own grips, and outside the test below: an item reshaped by its points usually offers no
        // frame grips at all, and its points would then be hidden on exactly the items that have nothing else.
        DrawPointHandles(session, pen);

        if (!corners && !sides) return;

        var at = 0;
        foreach (var grip in Grips(frame))
        {
            // Grips come round the frame, so they ALTERNATE: corner, middle, corner, middle.
            var wanted = at++ % 2 == 0 ? corners : sides;
            if (wanted) session.DrawRectangle(HandleBrush ?? SelectionBrush, grip, pen);
        }
    }

    // The POINTS of a single selected item that has them, in screen pixels like every other grip.
    private void DrawPointHandles(IDrawingSession session, Pen pen)
    {
        if (_selection.Count != 1 || _selection[0] is not ICanvasPoints shaped) return;

        var side = HandleSize;
        var half = side / 2;
        var brush = HandleBrush ?? SelectionBrush;

        foreach (var point in shaped.Points)
        {
            var at = WorldToScreen(point);
            session.DrawEllipse(new Rect(at.X - half, at.Y - half, side, side), brush, 0, 360, EllipseType.Sector, pen);
        }
    }

    private IEnumerable<Rect> Grips(Rect frame)
    {
        var side = HandleSize;
        var half = side / 2;

        var left = frame.X;
        var middle = frame.X + frame.Width / 2;
        var right = frame.X + frame.Width;
        var top = frame.Y;
        var center = frame.Y + frame.Height / 2;
        var bottom = frame.Y + frame.Height;

        yield return new Rect(left - half, top - half, side, side);
        yield return new Rect(middle - half, top - half, side, side);
        yield return new Rect(right - half, top - half, side, side);
        yield return new Rect(right - half, center - half, side, side);
        yield return new Rect(right - half, bottom - half, side, side);
        yield return new Rect(middle - half, bottom - half, side, side);
        yield return new Rect(left - half, bottom - half, side, side);
        yield return new Rect(left - half, center - half, side, side);
    }

    /// <summary>A world rectangle as it lands on screen.</summary>
    public Rect ToScreen(Rect world)
    {
        var topLeft = WorldToScreen(new Vector2(world.X, world.Y));
        var bottomRight = WorldToScreen(new Vector2(world.X + world.Width, world.Y + world.Height));

        return new Rect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
    }

    /// <summary>The manipulation frame of a selection, in screen pixels: its box held OFF by half a grip, so the grips
    /// stand against what is selected rather than half-buried in it.</summary>
    public Rect FrameOf(Rect bounds)
    {
        var clear = Math.Max(0, HandleSize) / 2;
        var frame = ToScreen(bounds);

        return new Rect(frame.X - clear, frame.Y - clear, frame.Width + clear * 2, frame.Height + clear * 2);
    }

    // A pen per brush, kept: these are drawn on every frame there is a selection, and a pen built per frame allocates
    // for every framed thing on screen.
    private static Pen PenOf(Brush brush, ref Pen pen, ref Brush was)
    {
        if (brush == null) return null;
        if (pen != null && ReferenceEquals(was, brush)) return pen;

        was = brush;
        pen = new Pen(brush);

        return pen;
    }

    // The grid is a shader, and a shader wants COLOURS: anything that is not a plain colour falls back to nothing
    // rather than being approximated into something the theme did not ask for.
    private static Color ColorOf(Brush brush, Color fallback) =>
        brush is SolidColorBrush solid ? solid.Color : fallback;

    // The stack the canvas fills itself: how many layers there are and what sort each is depends on the order of the
    // scene, so a template cannot state them - it states the place they go.
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _layers = GetTemplateChild("PART_Layers") as Panel;

        _front = GetTemplateChild("PART_Front") as CanvasFrontLayer;
        if (_front != null) _front.Owner = this;

        _chromeLayer = GetTemplateChild("PART_Chrome") as CanvasChromeLayer;
        if (_chromeLayer != null) _chromeLayer.Owner = this;

        _palette = GetTemplateChild("PART_NodePalette") as CanvasPane;

        SyncChrome();

        ApplyDesignMode();
        SyncElements();

        _overlay = GetTemplateChild("PART_Overlay") as ContentPresenter;

        // The BLOCK that is placed and dragged: the grip and the panel travel together, because a grip that stayed put
        // while its panel moved would not be that panel's grip any more.
        _overlayRoot = GetTemplateChild("PART_OverlayRoot") as InputUIComponent ?? _overlay;
        _overlayGrip = GetTemplateChild("PART_OverlayGrip") as ButtonBase;

        // The inset the THEME asked for, kept before placing overwrites the margin.
        _inset = _overlayRoot?.Margin ?? new Thickness(0);

        if (_overlayRoot != null)
        {
            _overlayRoot.MouseDown += OnOverlayPressed;
            _overlayRoot.MouseMove += OnOverlayMoved;
            _overlayRoot.MouseUp += OnOverlayReleased;
        }

        if (_overlayGrip != null) _overlayGrip.Click += OnGripClicked;

        PlaceOverlay();
    }

    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();

        if (_overlayRoot != null)
        {
            _overlayRoot.MouseDown -= OnOverlayPressed;
            _overlayRoot.MouseMove -= OnOverlayMoved;
            _overlayRoot.MouseUp -= OnOverlayReleased;
        }

        if (_overlayGrip != null) _overlayGrip.Click -= OnGripClicked;

        Hosts(host => host.Owner = null);
        Drawn(drawn => drawn.Owner = null);

        if (_chromeLayer != null)
        {
            _chromeLayer.Release();
            _chromeLayer.Owner = null;
        }

        _layers = null;
        _chromeLayer = null;
        _overlay = null;
        _overlayRoot = null;
        _overlayGrip = null;
    }

    // The grip both opens the panel and carries it, and one press cannot be both: the press starts a drag and the
    // CLICK toggles, and a press that turned into a drag is not a click.
    private void OnGripClicked(object sender, RoutedEventArgs e)
    {
        if (_overlayMoved) return;

        SetCurrentValue(IsOverlayOpenProperty, !IsOverlayOpen);
    }

    // Every press that reaches the panel is the PANEL's, and the canvas must not read it as a press on the plane too:
    // a stroke's CaptureMouse() took the capture a button inside the panel had just taken, and that button never saw
    // its own release. MouseDown is not marked handled by controls that answer it - they answer MouseLeftButtonDown,
    // raised from a separate args object - so where the press came from is the only thing to go on.
    private void OnOverlayPressed(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        _overlayMoved = false;

        if (!IsOverlayDraggable || e.ChangedButton != MouseButtons.Left || _overlayRoot == null) return;

        var onGrip = OnGrip(e.OriginalSource);
        if (!onGrip && !IsChrome(e.OriginalSource)) return;

        _overlayFrom = e.GetPosition(this);
        _overlayAt = OverlayPlacement == CanvasOverlayPlacement.Free
            ? OverlayOffset
            : new Vector2(_overlayRoot.Bounds.X, _overlayRoot.Bounds.Y);

        _draggingOverlay = true;

        // Taken only when NOBODY below took it: the grip captures the press for itself, and stealing that would leave
        // it never seeing its own release.
        _overlayCaptured = !onGrip;
        if (_overlayCaptured) _overlayRoot.CaptureMouse();
    }

    private bool OnGrip(object source)
    {
        if (_overlayGrip == null) return false;

        for (var at = source as IUIComponent; at != null; at = at.VisualParent)
        {
            if (ReferenceEquals(at, _overlayGrip)) return true;
            if (ReferenceEquals(at, _overlayRoot)) return false;
        }

        return false;
    }

    // What else the panel may be PICKED UP by: its own chrome - the edge, the padding, the background. Not the button
    // under the pointer: a panel dragged out from under a control takes away the capture that control just took.
    private bool IsChrome(object source)
    {
        var content = _overlay?.Content;

        for (var at = source as IUIComponent; at != null && !ReferenceEquals(at, _overlayRoot); at = at.VisualParent)
        {
            if (at is Control && !ReferenceEquals(at, content)) return false;
        }

        return true;
    }

    private void OnOverlayMoved(object sender, MouseEventArgs e)
    {
        if (!_draggingOverlay || _overlayRoot == null) return;

        var wanted = _overlayAt + (e.GetPosition(this) - _overlayFrom);
        var room = RenderSize;
        var size = _overlayRoot.RenderSize;

        // A press that has actually MOVED is a drag and no longer a click.
        if ((e.GetPosition(this) - _overlayFrom).Length() > 3) _overlayMoved = true;

        // Kept INSIDE the canvas: a panel dragged off the edge is a panel nobody can get back.
        SetCurrentValue(OverlayPlacementProperty, CanvasOverlayPlacement.Free);
        SetCurrentValue(OverlayOffsetProperty, new Vector2(
            Math.Clamp(wanted.X, 0, Math.Max(0, room.Width - size.Width)),
            Math.Clamp(wanted.Y, 0, Math.Max(0, room.Height - size.Height))));

        e.Handled = true;
    }

    private void OnOverlayReleased(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (!_draggingOverlay) return;

        _draggingOverlay = false;
        if (_overlayCaptured) _overlayRoot?.ReleaseMouseCapture();
        _overlayCaptured = false;
    }

    // The placement is the CANVAS's, so the control puts it on the part: a template cannot map one to the other, and
    // saying it in the theme would mean three themes having to agree about it.
    private void PlaceOverlay()
    {
        if (_overlay != null)
        {
            _overlay.Content = Overlay;

            // Closed, the content is COLLAPSED and not merely hidden: hidden it would still take its place, and the
            // grip would sit where it sat with the panel open, having got nothing out of the way.
            _overlay.Visibility = IsOverlayOpen ? Visibility.Visible : Visibility.Collapsed;
        }

        if (_overlayRoot == null) return;

        // Gone ENTIRELY - grip and all - when nobody wants it or there is nothing in it: a grip with no panel behind
        // it is a handle that opens nothing.
        var wanted = IsOverlayVisible && Overlay != null;
        _overlayRoot.Visibility = wanted ? Visibility.Visible : Visibility.Collapsed;

        // Put somewhere by hand: held by its own margin from the top-left, the only placement that can say "here".
        if (OverlayPlacement == CanvasOverlayPlacement.Free)
        {
            _overlayRoot.HorizontalAlignment = HorizontalAlignment.Left;
            _overlayRoot.VerticalAlignment = VerticalAlignment.Top;
            _overlayRoot.Margin = new Thickness(OverlayOffset.X, OverlayOffset.Y, 0, 0);
            return;
        }

        _overlayRoot.Margin = _inset;
        _overlayRoot.HorizontalAlignment = OverlayPlacement switch
        {
            CanvasOverlayPlacement.TopRight or CanvasOverlayPlacement.BottomRight => HorizontalAlignment.Right,
            CanvasOverlayPlacement.TopCenter or CanvasOverlayPlacement.BottomCenter => HorizontalAlignment.Center,
            _ => HorizontalAlignment.Left
        };

        _overlayRoot.VerticalAlignment = OverlayPlacement switch
        {
            CanvasOverlayPlacement.BottomLeft or CanvasOverlayPlacement.BottomRight
                or CanvasOverlayPlacement.BottomCenter => VerticalAlignment.Bottom,
            _ => VerticalAlignment.Top
        };
    }

    // THE CANVAS'S OWN PANELS, which the template brought - plus any the canvas puts up while it is running.
    private void SyncChrome()
    {
        if (_chromeLayer == null) return;

        _chromeLayer.Sync();
        _chromeLayer.InvalidateMeasure();
        _chromeLayer.InvalidateArrange();
    }

    private void SyncElements() => SyncElements(RenderSize);

    // Every GROUP OPENED OUT, in place. A group holds things - it is moved, resized and selected as one - but it is
    // not a way of DRAWING them: left folded, a picture gathered into a group simply vanished. The group is still one
    // item to the scene, to picking and to whoever moves it; this is only about which layer each thing is laid in.
    private static IEnumerable<ICanvasItem> Laid(IEnumerable<ICanvasItem> items)
    {
        foreach (var item in items)
        {
            if (item is not GroupItem group)
            {
                yield return item;
                continue;
            }

            foreach (var child in Laid(group.Children)) yield return child;
        }
    }

    // ONE ORDER FOR EVERYTHING, and the layers are cut from it: a run of items of the same sort - drawn things, or
    // controls - becomes one layer, and the next sort starts the next. That is what makes a picture, a stroke over it
    // and another picture over that three places in paint order rather than two buckets.
    //
    // ...across a viewport SAID OUT LOUD, because the one pass that must not read RenderSize is the pass that sets it:
    // arranging with a new size asked "what can be seen" of the size before it, and everything that had just come into
    // view was left off the plane until something else re-synced.
    private void SyncElements(Size viewport)
    {
        if (_layers == null) return;

        _runs.Clear();

        List<ICanvasItem> run = null;
        var hosted = false;

        foreach (var item in Laid(ItemsHere(WorldAcross(viewport))))
        {
            var control = item is ElementItem;

            if (run == null || control != hosted)
            {
                run = new List<ICanvasItem>();
                hosted = control;
                _runs.Add((control, run));
            }

            run.Add(item);
        }

        // The frame's place is a place in THESE runs, so it is settled here rather than looked for as the layers paint.
        MarkChrome();

        Restack(viewport);
    }

    // MATCHED BY SORT, and only then rebuilt: a layer that is already the right kind keeps the controls it is holding.
    // Emptying the stack and filling it again would take every control off the plane and put it back - re-templated,
    // re-recorded, and with whatever it was in the middle of gone.
    private void Restack(Size viewport)
    {
        var changed = false;

        for (var i = 0; i < _runs.Count; i++)
        {
            var (hosted, items) = _runs[i];
            var standing = i < _layers.Children.Count ? _layers.Children[i] : null;

            if (hosted && standing is CanvasElementLayer host)
            {
                _hosted.Clear();
                foreach (var item in items) _hosted.Add((ElementItem)item);
                host.Sync(_hosted);
                continue;
            }

            if (!hosted && standing is CanvasDrawLayer drawn)
            {
                drawn.Sync(items);
                continue;
            }

            var made = Layer(hosted, items);

            if (standing != null) _layers.Children.RemoveAt(i);

            _layers.Children.Insert(i, made);

            // Placed NOW, at the size the canvas has this moment: the stack is filled from inside the canvas's own
            // arrange, so a layer left for the next pass would stand at no size at all, and a thing of no size draws
            // nothing.
            if (viewport is { Width: > 0, Height: > 0 })
            {
                made.Measure(viewport);
                made.Arrange(new Rect(0, 0, viewport.Width, viewport.Height));
            }

            changed = true;
        }

        while (_layers.Children.Count > _runs.Count)
        {
            var last = _layers.Children.Count - 1;

            if (_layers.Children[last] is CanvasElementLayer host) host.Owner = null;
            if (_layers.Children[last] is CanvasDrawLayer drawn) drawn.Owner = null;

            _layers.Children.RemoveAt(last);
            changed = true;
        }

        if (!changed) return;

        _layers.InvalidateMeasure();
        _layers.InvalidateArrange();

        // ...AND THE WHOLE STACK RECORDED AGAIN, not patched: a layer appearing or going means controls have changed
        // parents, and what is kept of a frame is kept per element - patched, such an element simply stops being
        // drawn. Only when the stack itself changed, which is rare.
        InvalidateRender(true);
    }

    // EVERY layer of a sort, because there is no longer one of each.
    private void Hosts(Action<CanvasElementLayer> what)
    {
        if (_layers == null) return;

        foreach (var child in _layers.Children)
        {
            if (child is CanvasElementLayer host) what(host);
        }
    }

    private void Drawn(Action<CanvasDrawLayer> what)
    {
        if (_layers == null) return;

        foreach (var child in _layers.Children)
        {
            if (child is CanvasDrawLayer drawn) what(drawn);
        }
    }

    private IMeasurableComponent Layer(bool hosted, List<ICanvasItem> items)
    {
        if (!hosted)
        {
            var drawn = new CanvasDrawLayer { Owner = this };

            drawn.Sync(items);
            return drawn;
        }

        var host = new CanvasElementLayer { Owner = this };

        _hosted.Clear();
        foreach (var item in items) _hosted.Add((ElementItem)item);
        host.Sync(_hosted);

        return host;
    }

    // The world's origin starts in the MIDDLE of the viewport: a plane whose origin sits off in a corner reads as one
    // that has been scrolled away from, which on something with no edges is the one impression to avoid.
    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);

        // From finalSize, not RenderSize: this IS the pass that sets the latter.
        if (!_cameraPlaced && finalSize is { Width: > 0, Height: > 0 })
        {
            _cameraPlaced = true;
            SetCurrentValue(OffsetProperty, new Vector2(finalSize.Width / 2, finalSize.Height / 2));
        }

        // A view asked for before there was a viewport - and only once the panels have stopped moving: the middle is
        // measured against the room the docked ones leave, and one folded away by the same saved setup is still at its
        // old size on this pass.
        if (_lookWanted is { } wanted && finalSize is { Width: > 0, Height: > 0 })
        {
            var inset = _chromeLayer?.Inset() ?? new Thickness(0);

            if (Same(inset, _lastInset))
            {
                _lookWanted = null;
                CenterOn(wanted, finalSize);
            }
            else
            {
                _lastInset = inset;
                InvalidateArrange();
            }
        }

        // The controls on the plane are asked for again HERE: "which of them can be seen" is answered from the
        // viewport, and until this pass there is none to answer from.
        SyncElements(finalSize);

        return size;
    }
}
