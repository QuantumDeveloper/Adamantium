using System;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>One panel of an <see cref="InfiniteCanvas"/>'s chrome - a tool rail, an inspector, a context bar. It sits
/// on the GLASS: it neither moves nor scales with the camera, and a press that lands on it never reaches the plane.
/// <para>Many rather than one, and that is the whole point of the type. A canvas's chrome has three jobs with three
/// different laws of placement - a rail wants to be narrow and against an edge, an inspector wide and able to slide
/// away, a context bar tiny and beside the thing it is about - and a single slot holding all three is how a tool panel
/// turns into a column that does not fit. The canvas owns WHERE a pane goes; what is in it belongs to whoever put it
/// there.</para></summary>
public class CanvasPane : ContentControl
{
    private ButtonBase _grip;
    private ButtonBase _turn;
    private ToggleButton _snap;
    private IUIComponent _headerPart;
    private IUIComponent _sizer;
    private bool _dragging;
    private bool _sizing;
    private bool _moved;
    private bool _captured;
    private double _wide;
    private Vector2 _from;
    private Vector2 _at;

    public static readonly AdamantiumProperty PlacementProperty = AdamantiumProperty.Register(nameof(Placement),
        typeof(CanvasPanePlacement), typeof(CanvasPane),
        new PropertyMetadata(CanvasPanePlacement.TopLeft,
            PropertyMetadataOptions.AffectsParentArrange | PropertyMetadataOptions.BindsTwoWayByDefault,
            OnPlacementChanged));

    public static readonly AdamantiumProperty KindProperty = AdamantiumProperty.Register(nameof(Kind),
        typeof(CanvasPaneKind), typeof(CanvasPane),
        new PropertyMetadata(CanvasPaneKind.Sheet, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty OffsetProperty = AdamantiumProperty.Register(nameof(Offset),
        typeof(Vector2), typeof(CanvasPane),
        new PropertyMetadata(Vector2.Zero,
            PropertyMetadataOptions.AffectsParentArrange | PropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly AdamantiumProperty IsOpenProperty = AdamantiumProperty.Register(nameof(IsOpen),
        typeof(Boolean), typeof(CanvasPane),
        new PropertyMetadata(true,
            PropertyMetadataOptions.AffectsParentArrange | PropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly AdamantiumProperty IsDockedProperty = AdamantiumProperty.Register(nameof(IsDocked),
        typeof(Boolean), typeof(CanvasPane),
        new PropertyMetadata(false,
            PropertyMetadataOptions.AffectsParentArrange | PropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly AdamantiumProperty CanDragProperty = AdamantiumProperty.Register(nameof(CanDrag),
        typeof(Boolean), typeof(CanvasPane), new PropertyMetadata(true));

    public static readonly AdamantiumProperty SnapDistanceProperty = AdamantiumProperty.Register(nameof(SnapDistance),
        typeof(Double), typeof(CanvasPane), new PropertyMetadata(28.0));

    /// <summary>Whether the pane's inner edge can be dragged to widen it. OFF by default - a rail and a bar are as wide
    /// as what is in them, and a grip on either is a strip of nothing to catch the mouse on.</summary>
    public static readonly AdamantiumProperty CanResizeProperty = AdamantiumProperty.Register(nameof(CanResize),
        typeof(Boolean), typeof(CanvasPane), new PropertyMetadata(false, OnCanResizeChanged));

    /// <summary>The narrowest the pane may be dragged, in screen pixels. A panel pulled to nothing is a panel with no
    /// edge left to pull back out by.</summary>
    public static readonly AdamantiumProperty MinResizeWidthProperty = AdamantiumProperty.Register(
        nameof(MinResizeWidth), typeof(Double), typeof(CanvasPane), new PropertyMetadata(160.0));

    /// <summary>Whether letting a pane go near an edge sticks it to that edge. OFF by default, and deliberately.
    /// <para>A panel on a canvas is moved often, and a panel that jumps to an edge whenever it passes near one is a
    /// panel that will not stay where it was put. Sticking is useful when it is what you meant, so it is asked for -
    /// the pane's own toggle, or the property - rather than done to you.</para></summary>
    public static readonly AdamantiumProperty SnapsToEdgesProperty = AdamantiumProperty.Register(nameof(SnapsToEdges),
        typeof(Boolean), typeof(CanvasPane),
        new PropertyMetadata(false, PropertyMetadataOptions.BindsTwoWayByDefault, OnSnapsToEdgesChanged));

    public Boolean SnapsToEdges
    {
        get => GetValue<Boolean>(SnapsToEdgesProperty);
        set => SetValue(SnapsToEdgesProperty, value);
    }

    public static readonly AdamantiumProperty HeaderProperty = AdamantiumProperty.Register(nameof(Header),
        typeof(Object), typeof(CanvasPane),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure, OnHeaderChanged));

    /// <summary>Which way the pane's own content runs - a rail's buttons in a column or in a row.
    /// <para>SAID, never inferred. It was derived from <see cref="Placement"/> at first, so that a rail dragged to the
    /// bottom turned itself into a row; that reads as the panel changing shape under the hand every time it is moved,
    /// which is exactly what a person moving it does not want. Which way a rail runs and where it sits are two
    /// separate choices, and the pane's own button is where the first one is made.</para></summary>
    public static readonly AdamantiumProperty OrientationProperty = AdamantiumProperty.Register(nameof(Orientation),
        typeof(Orientation), typeof(CanvasPane),
        new PropertyMetadata(Orientation.Vertical,
            PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsParentArrange |
            PropertyMetadataOptions.BindsTwoWayByDefault));

    public Orientation Orientation
    {
        get => GetValue<Orientation>(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>The canvas this pane belongs to. Written by the layer that took the pane, so that a template can reach
    /// past the pane to the canvas - a rail needs its tools, an inspector its selection - without the markup having to
    /// name the canvas and without the pane having to know what the template does with it.</summary>
    public static readonly AdamantiumProperty CanvasProperty = AdamantiumProperty.Register(nameof(Canvas),
        typeof(InfiniteCanvas), typeof(CanvasPane), new PropertyMetadata(null));

    public InfiniteCanvas Canvas
    {
        get => GetValue<InfiniteCanvas>(CanvasProperty);
        internal set => SetValue(CanvasProperty, value);
    }

    /// <summary>Where this pane sits. Dragging it sets <see cref="CanvasPanePlacement.Free"/>, and letting it go near
    /// an edge sets that edge - so the property is the record of where the user left it, not only what the markup
    /// asked for.</summary>
    public CanvasPanePlacement Placement
    {
        get => GetValue<CanvasPanePlacement>(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    /// <summary>What shape of panel this is, which is what the theme templates it as.</summary>
    public CanvasPaneKind Kind
    {
        get => GetValue<CanvasPaneKind>(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>Where it sits under <see cref="CanvasPanePlacement.Free"/>, in screen pixels from the canvas's
    /// top-left.</summary>
    public Vector2 Offset
    {
        get => GetValue<Vector2>(OffsetProperty);
        set => SetValue(OffsetProperty, value);
    }

    /// <summary>Whether the body is shown. Closed, the pane is its grip and nothing else - which is how a panel gets
    /// out of the way without being lost.</summary>
    public Boolean IsOpen
    {
        get => GetValue<Boolean>(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>Whether this pane RESERVES the room it occupies. Docked, the canvas stops counting that strip as
    /// usable, so "fit to view" and "home" center on what is actually visible rather than on what is behind the panel.
    /// <para>Only edges reserve, and along the edge the placement names: left placements take width from the left,
    /// right ones from the right, <see cref="CanvasPanePlacement.TopCenter"/> and
    /// <see cref="CanvasPanePlacement.BottomCenter"/> take height. Free and Selection reserve nothing - a pane that
    /// follows something cannot also be a wall.</para></summary>
    public Boolean IsDocked
    {
        get => GetValue<Boolean>(IsDockedProperty);
        set => SetValue(IsDockedProperty, value);
    }

    /// <summary>Whether the grip carries the pane as well as folding it.</summary>
    public Boolean CanDrag
    {
        get => GetValue<Boolean>(CanDragProperty);
        set => SetValue(CanDragProperty, value);
    }

    /// <summary>How near an edge a dropped pane has to be to stick to it, in screen pixels.</summary>
    public Double SnapDistance
    {
        get => GetValue<Double>(SnapDistanceProperty);
        set => SetValue(SnapDistanceProperty, value);
    }

    /// <summary>Whether the pane's inner edge widens it when dragged.</summary>
    public Boolean CanResize
    {
        get => GetValue<Boolean>(CanResizeProperty);
        set => SetValue(CanResizeProperty, value);
    }

    /// <summary>The narrowest the pane may be dragged, in screen pixels.</summary>
    public Double MinResizeWidth
    {
        get => GetValue<Double>(MinResizeWidthProperty);
        set => SetValue(MinResizeWidthProperty, value);
    }

    /// <summary>What the header shows. Nothing by default, which is what a bar and a rail want.</summary>
    public Object Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>The layer this pane is in. Set by <see cref="CanvasChromeLayer"/> when it takes the pane, and the only
    /// thing the pane needs from outside: the viewport it has to stay inside, and the canvas behind it.</summary>
    public CanvasChromeLayer Layer { get; internal set; }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _grip = GetTemplateChild("PART_Grip") as ButtonBase;
        if (_grip != null) _grip.Click += OnGripClicked;

        _turn = GetTemplateChild("PART_Turn") as ButtonBase;
        if (_turn != null) _turn.Click += OnTurnClicked;

        _snap = GetTemplateChild("PART_Snap") as ToggleButton;
        if (_snap != null)
        {
            _snap.IsChecked = SnapsToEdges;
            _snap.Click += OnSnapClicked;
        }


        _headerPart = GetTemplateChild("PART_Header") as IUIComponent;
        SyncHeader();

        _sizer = GetTemplateChild("PART_Sizer") as IUIComponent;
        SyncSizer();

        MouseDown += OnPressed;
        MouseMove += OnMoved;
        MouseUp += OnReleased;
    }

    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();

        if (_grip != null) _grip.Click -= OnGripClicked;
        if (_turn != null) _turn.Click -= OnTurnClicked;
        if (_snap != null) _snap.Click -= OnSnapClicked;

        MouseDown -= OnPressed;
        MouseMove -= OnMoved;
        MouseUp -= OnReleased;

        _grip = null;
        _turn = null;
        _snap = null;
        _headerPart = null;
        _sizer = null;
    }

    // An EMPTY presenter is not free: it draws nothing but keeps its margin, so a pane with no header stood its content
    // a few pixels low and came out taller than what is in it. A bar looked like its text had slipped upwards.
    private void SyncHeader()
    {
        if (_headerPart == null) return;

        var wanted = Header == null ? Visibility.Collapsed : Visibility.Visible;
        if (_headerPart.Visibility != wanted) _headerPart.Visibility = wanted;
    }

    private static void OnHeaderChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e) =>
        (component as CanvasPane)?.SyncHeader();

    // The grip goes on the side that faces INTO the canvas, which is the side there is room to pull towards: a panel
    // against the right edge widens leftwards, one against the left edge rightwards. Put on the outer side it would
    // either hang off the viewport or make the pane walk across the screen as it grew.
    private void SyncSizer()
    {
        if (_sizer == null) return;

        var wanted = CanResize ? Visibility.Visible : Visibility.Collapsed;
        if (_sizer.Visibility != wanted) _sizer.Visibility = wanted;

        DockPanel.SetDock(_sizer, Inner() < 0 ? Dock.Left : Dock.Right);
    }

    // How wide the pane becomes when its edge has been dragged `reach` pixels along X from a width of `from`. The
    // ceiling is the viewport: a panel wider than the canvas leaves nothing behind it to look at.
    internal double Widened(double from, double reach, double room) =>
        Math.Clamp(from + reach * Inner(), MinResizeWidth, Math.Max(MinResizeWidth, room));

    // -1 when the pane's own inner edge is its LEFT one, +1 when it is its right one. Also the sign a drag along X has
    // to be multiplied by to become a change in width.
    private int Inner() => Placement switch
    {
        CanvasPanePlacement.TopRight or CanvasPanePlacement.Right or CanvasPanePlacement.BottomRight => -1,
        _ => 1
    };

    private static void OnCanResizeChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e) =>
        (component as CanvasPane)?.SyncSizer();

    private static void OnPlacementChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e) =>
        (component as CanvasPane)?.SyncSizer();

    /// <summary>What this pane takes out of the canvas's usable area - all zero unless it is docked.</summary>
    internal Thickness Reserved()
    {
        if (!IsDocked) return new Thickness(0);

        var size = RenderSize;

        return Placement switch
        {
            CanvasPanePlacement.TopLeft or CanvasPanePlacement.Left or CanvasPanePlacement.BottomLeft
                => new Thickness(size.Width, 0, 0, 0),
            CanvasPanePlacement.TopRight or CanvasPanePlacement.Right or CanvasPanePlacement.BottomRight
                => new Thickness(0, 0, size.Width, 0),
            CanvasPanePlacement.TopCenter => new Thickness(0, size.Height, 0, 0),
            CanvasPanePlacement.BottomCenter => new Thickness(0, 0, 0, size.Height),
            _ => new Thickness(0)
        };
    }

    // The grip both opens the pane and carries it, and one press cannot be both - so the press starts a drag and the
    // CLICK toggles, and a press that turned into a drag is not a click.
    private void OnGripClicked(object sender, RoutedEventArgs e)
    {
        if (_moved) return;

        SetCurrentValue(IsOpenProperty, !IsOpen);
    }

    private void OnSnapClicked(object sender, RoutedEventArgs e)
    {
        if (_snap != null) SetCurrentValue(SnapsToEdgesProperty, _snap.IsChecked == true);
    }

    // The button is a VIEW of the property and never its owner: set from markup or a binding, it has to move too.
    private static void OnSnapsToEdgesChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is CanvasPane pane && pane._snap != null) pane._snap.IsChecked = pane.SnapsToEdges;
    }

    private void OnTurnClicked(object sender, RoutedEventArgs e)
    {
        if (_moved) return;

        SetCurrentValue(OrientationProperty,
            Orientation == Orientation.Vertical ? Orientation.Horizontal : Orientation.Vertical);
    }

    // Every press that reaches a pane is the PANE's, whatever the pane then does with it, and the canvas must not read
    // it as a press on the plane as well. It was: a stroke's CaptureMouse() took the capture a button inside the panel
    // had just taken for itself, so that button never saw its own release and nothing in the panel answered. MouseDown
    // is not marked handled by the controls that answer it - they answer MouseLeftButtonDown, raised from a SEPARATE
    // args object - so where the press came from is the only thing there is to go on.
    private void OnPressed(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        _moved = false;

        if (e.ChangedButton != MouseButtons.Left || Layer == null) return;

        // The edge is asked about FIRST and answers for itself. It is inside the pane, so a press on it is also a press
        // on the pane, and whichever of the two gestures is read first is the only one the hand can ever get.
        if (CanResize && Within(e.OriginalSource, _sizer))
        {
            _from = e.GetPosition(Layer);
            _wide = ActualWidth;
            _sizing = true;
            _captured = true;
            CaptureMouse();
            return;
        }

        if (!CanDrag) return;

        var onGrip = OnGrip(e.OriginalSource);
        if (!onGrip && !IsChrome(e.OriginalSource)) return;

        _from = e.GetPosition(Layer);
        _at = Placement == CanvasPanePlacement.Free ? Offset : new Vector2(Bounds.X, Bounds.Y);
        _dragging = true;

        // Taken only when NOBODY below took it. The grip captures the press for itself, and stealing that would leave
        // it never seeing its own release - so no click, so the pane could never be folded. Nothing is lost by not
        // taking it: the grip is inside this pane, so its moves and its release bubble here anyway.
        _captured = !onGrip;
        if (_captured) CaptureMouse();
    }

    private void OnMoved(object sender, MouseEventArgs e)
    {
        if (_sizing && Layer != null)
        {
            Width = Widened(_wide, (e.GetPosition(Layer) - _from).X, Layer.RenderSize.Width);
            e.Handled = true;
            return;
        }

        if (!_dragging || Layer == null) return;

        var now = e.GetPosition(Layer);
        var wanted = _at + (now - _from);
        var room = Layer.RenderSize;
        var size = RenderSize;

        // A press that has actually MOVED is a drag and no longer a click, so the grip does not also toggle when it is
        // let go. Measured in screen pixels, because "did not move" is a fact about the hand.
        if ((now - _from).Length() > 3) _moved = true;

        // Kept INSIDE the canvas: a pane dragged off the edge is a pane nobody can get back, and there is no edge on
        // the plane itself to find it by.
        SetCurrentValue(PlacementProperty, CanvasPanePlacement.Free);
        SetCurrentValue(OffsetProperty, new Vector2(
            Math.Clamp(wanted.X, 0, Math.Max(0, room.Width - size.Width)),
            Math.Clamp(wanted.Y, 0, Math.Max(0, room.Height - size.Height))));

        e.Handled = true;
    }

    private void OnReleased(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        if (_sizing)
        {
            _sizing = false;
            if (_captured) ReleaseMouseCapture();
            _captured = false;
            return;
        }

        if (!_dragging) return;

        _dragging = false;
        if (_captured) ReleaseMouseCapture();
        _captured = false;

        if (_moved) StickToEdge();
    }

    // Dropped near an edge, the pane takes that edge. Which SLOT of the edge is decided by where along it the pane
    // came to rest - thirds, so that aiming at a corner is aiming at a corner and not at a pixel.
    private void StickToEdge()
    {
        if (!SnapsToEdges || Layer == null) return;

        var room = Layer.RenderSize;
        var size = RenderSize;
        if (room.Width <= 0 || room.Height <= 0) return;

        var left = Offset.X;
        var top = Offset.Y;
        var right = room.Width - (Offset.X + size.Width);
        var bottom = room.Height - (Offset.Y + size.Height);

        var nearest = Math.Min(Math.Min(left, right), Math.Min(top, bottom));
        if (nearest > SnapDistance) return;

        var centerX = Offset.X + size.Width / 2;
        var centerY = Offset.Y + size.Height / 2;

        CanvasPanePlacement wanted;

        if (nearest == left || nearest == right)
        {
            var band = Band(centerY, room.Height);
            wanted = nearest == left
                ? band switch
                {
                    < 0 => CanvasPanePlacement.TopLeft,
                    0 => CanvasPanePlacement.Left,
                    _ => CanvasPanePlacement.BottomLeft
                }
                : band switch
                {
                    < 0 => CanvasPanePlacement.TopRight,
                    0 => CanvasPanePlacement.Right,
                    _ => CanvasPanePlacement.BottomRight
                };
        }
        else
        {
            var band = Band(centerX, room.Width);
            wanted = nearest == top
                ? band switch
                {
                    < 0 => CanvasPanePlacement.TopLeft,
                    0 => CanvasPanePlacement.TopCenter,
                    _ => CanvasPanePlacement.TopRight
                }
                : band switch
                {
                    < 0 => CanvasPanePlacement.BottomLeft,
                    0 => CanvasPanePlacement.BottomCenter,
                    _ => CanvasPanePlacement.BottomRight
                };
        }

        SetCurrentValue(PlacementProperty, wanted);
    }

    private static int Band(double at, double of) => at < of / 3 ? -1 : at > of * 2 / 3 ? 1 : 0;

    private bool OnGrip(object source) => Within(source, _grip);

    // Whether a press landed on one named part of the template, or on anything that part is made of. Stopping at the
    // pane itself, so the walk cannot wander out into whatever the pane happens to be sitting in.
    private bool Within(object source, object part)
    {
        if (part == null) return false;

        for (var at = source as IUIComponent; at != null; at = at.VisualParent)
        {
            if (ReferenceEquals(at, part)) return true;
            if (ReferenceEquals(at, this)) return false;
        }

        return false;
    }

    // What else the pane may be PICKED UP by: its own chrome - the edge, the padding, the background. Not the button or
    // the slider under the pointer: a panel dragged out from under a control takes away the capture that control just
    // took, which is the whole fault this handler exists for. The pane's own content root is exempt, or a pane holding
    // a single control would have nowhere to be picked up by.
    private bool IsChrome(object source)
    {
        var content = Content;

        for (var at = source as IUIComponent; at != null && !ReferenceEquals(at, this); at = at.VisualParent)
        {
            if (at is Control && !ReferenceEquals(at, content)) return false;
        }

        return true;
    }
}
