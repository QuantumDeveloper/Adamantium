using System;
using System.Collections.Generic;
using Adamantium.ProceduralGeometry.Shapes;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Primitives;
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

    private readonly List<ICanvasItem> _selection = new();
    private Pen _selectionPen;
    private Brush _selectionPenBrush;

    private CanvasElementLayer _elements;
    private readonly List<ElementItem> _visibleElements = new();

    private ContentPresenter _overlay;
    private InputUIComponent _overlayRoot;
    private ButtonBase _overlayGrip;
    private Thickness _inset;
    private bool _draggingOverlay;
    private bool _overlayCaptured;
    private bool _overlayMoved;
    private Vector2 _overlayFrom;
    private Vector2 _overlayAt;

    // Where the pointer is being PULLED, or nothing. Kept rather than recomputed while drawing, because the mark has to
    // be on screen before the press - nobody can aim at a point that only appears once it has been hit.
    private Vector2? _snap;


    // ONE brush, kept and re-pointed rather than made each frame: it is the parameter block the grid shader reads, and
    // a new one every frame would be a new paint identity every frame.
    private readonly CanvasGridBrush _ground = new();

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
        typeof(Double), typeof(InfiniteCanvas), new PropertyMetadata(1024.0));

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

    /// <summary>What stands ON THE GLASS: a floating tool panel, a legend, a minimap. Anything put here is laid over the
    /// plane and does NOT move or scale with the camera - that is what makes it the overlay rather than content.
    /// <para>The canvas carries the layer so nobody has to build one around it, and the CONTENT comes from outside so
    /// nobody is stuck with ours. Same division the table makes with its header: the control owns the place, the
    /// application owns what is in it.</para></summary>
    public static readonly AdamantiumProperty OverlayProperty = AdamantiumProperty.Register(nameof(Overlay),
        typeof(object), typeof(InfiniteCanvas),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure, OnOverlayChanged));

    /// <summary>Where the overlay sits in the viewport. Its own property rather than the content's alignment, because a
    /// floating panel is placed against the CANVAS and not against whatever it happens to contain.</summary>
    public static readonly AdamantiumProperty OverlayPlacementProperty = AdamantiumProperty.Register(
        nameof(OverlayPlacement), typeof(CanvasOverlayPlacement), typeof(InfiniteCanvas),
        new PropertyMetadata(CanvasOverlayPlacement.TopLeft, PropertyMetadataOptions.AffectsArrange, OnOverlayChanged));

    /// <summary>Where the overlay sits when <see cref="OverlayPlacement"/> is
    /// <see cref="CanvasOverlayPlacement.Free"/>: screen pixels from the canvas's top-left. Dragging the panel writes
    /// it.</summary>
    public static readonly AdamantiumProperty OverlayOffsetProperty = AdamantiumProperty.Register(nameof(OverlayOffset),
        typeof(Vector2), typeof(InfiniteCanvas),
        new PropertyMetadata(Vector2.Zero, PropertyMetadataOptions.AffectsArrange | PropertyMetadataOptions.BindsTwoWayByDefault,
            OnOverlayChanged));

    /// <summary>Whether the overlay can be dragged around the canvas by its own background.</summary>
    public static readonly AdamantiumProperty IsOverlayDraggableProperty = AdamantiumProperty.Register(
        nameof(IsOverlayDraggable), typeof(Boolean), typeof(InfiniteCanvas), new PropertyMetadata(true));

    /// <summary>What is ON the canvas. Held by the APPLICATION - the same arrangement the table has with its rows, so
    /// undo, saving and everything else a drawing is for stay where the drawing does.</summary>
    public static readonly AdamantiumProperty SceneProperty = AdamantiumProperty.Register(nameof(Scene),
        typeof(ICanvasScene), typeof(InfiniteCanvas),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender, OnSceneChanged));

    /// <summary>Whether what the pointer is doing snaps to the grid - to its crossings, which are the points a drawing
    /// is measured against.
    /// <para>This also changes what drawing IS. Snapped, the left button places vertices and the line runs straight from
    /// one to the next, following the pointer between clicks until a double click ends it; nothing is smoothed, because
    /// every vertex is somewhere that was aimed at. Free, the button draws a freehand stroke that is smoothed when it is
    /// lifted.</para></summary>
    public static readonly AdamantiumProperty SnapToGridProperty = AdamantiumProperty.Register(nameof(SnapToGrid),
        typeof(Boolean), typeof(InfiniteCanvas),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender, OnSnapToGridChanged));

    /// <summary>How close a crossing must be to pull, in SCREEN pixels. A screen distance for the same reason every
    /// other tolerance here is one: a snap stated in world units would reach halfway across the viewport when zoomed
    /// out and be impossible to trigger when zoomed in.</summary>
    public static readonly AdamantiumProperty SnapDistanceProperty = AdamantiumProperty.Register(nameof(SnapDistance),
        typeof(Double), typeof(InfiniteCanvas), new PropertyMetadata(12.0));

    /// <summary>What the snap point is marked with. Null draws no mark - the snap still pulls.</summary>
    public static readonly AdamantiumProperty SnapMarkBrushProperty = AdamantiumProperty.Register(nameof(SnapMarkBrush),
        typeof(Brush), typeof(InfiniteCanvas), new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

    /// <summary>How wide the mark is, in SCREEN pixels - it is a mark on the glass, not a thing on the plane, so it
    /// keeps its size at every zoom.</summary>
    public static readonly AdamantiumProperty SnapMarkSizeProperty = AdamantiumProperty.Register(nameof(SnapMarkSize),
        typeof(Double), typeof(InfiniteCanvas), new PropertyMetadata(9.0, PropertyMetadataOptions.AffectsRender));

    /// <summary>What the pen leaves behind. Null means the canvas draws nothing on a press, which is what a canvas
    /// being used to LOOK at something wants.</summary>
    public static readonly AdamantiumProperty InkProperty = AdamantiumProperty.Register(nameof(Ink),
        typeof(Brush), typeof(InfiniteCanvas), new PropertyMetadata(null));

    /// <summary>How wide the pen draws, in SCREEN pixels - the nib is a size on the glass, like every other tolerance
    /// this control states.
    /// <para>The MARK it leaves belongs to the plane: the width is turned into world units the moment a stroke starts,
    /// and from then on that stroke grows and shrinks with the zoom the way ink on paper does. Which is the same
    /// division a stylus makes - the nib is a physical size, the line it leaves is part of the drawing. Stated in world
    /// units the pen would be right at exactly one zoom and useless at the others: at 256x the thinnest setting drew a
    /// band half the viewport wide.</para></summary>
    public static readonly AdamantiumProperty InkThicknessProperty = AdamantiumProperty.Register(nameof(InkThickness),
        typeof(Double), typeof(InfiniteCanvas), new PropertyMetadata(2.0));

    /// <summary>What the RIGHT button puts the canvas back to - the tool a gesture ends in. Nothing by default, which
    /// leaves the right button alone; set it and one click anywhere drops whatever was picked up.
    /// <para>The right button and not a key, because the hand is already on the mouse and because it is the one press
    /// no tool here wants for itself. And a property rather than a hard-wired "select", because which tool is the
    /// resting one is the application's to say.</para></summary>
    public static readonly AdamantiumProperty DefaultToolProperty = AdamantiumProperty.Register(nameof(DefaultTool),
        typeof(ICanvasTool), typeof(InfiniteCanvas), new PropertyMetadata(null));

    /// <summary>What the plain left button DOES - the pen, the selection frame, a shape. A swappable object, so that
    /// one more gesture is one more class rather than one more branch in here.
    /// <para>Nothing by default: a canvas with no tool is a canvas you can look around but not change, which is what an
    /// application that only displays a drawing wants.</para></summary>
    public static readonly AdamantiumProperty ToolProperty = AdamantiumProperty.Register(nameof(Tool),
        typeof(ICanvasTool), typeof(InfiniteCanvas),
        // Two-way by default, because the CANVAS changes it too: the right button puts the resting tool back, and a
        // panel bound one way would go on showing whatever it last chose.
        new PropertyMetadata(null, PropertyMetadataOptions.BindsTwoWayByDefault, OnToolChanged));

    /// <summary>How big new text is, in SCREEN pixels - the same division the pen makes: the size is what you see while
    /// you type, and the words it leaves belong to the plane and grow with it.</summary>
    public static readonly AdamantiumProperty TextSizeProperty = AdamantiumProperty.Register(nameof(TextSize),
        typeof(Double), typeof(InfiniteCanvas), new PropertyMetadata(16.0));

    /// <summary>How wide the eraser is, in SCREEN pixels - the ring the hand aims with, so it stays the same size
    /// however far the camera is zoomed. What it takes out of the drawing is that ring turned into world units, which
    /// is why a zoomed-in eraser rubs a finer hole.</summary>
    public static readonly AdamantiumProperty EraserSizeProperty = AdamantiumProperty.Register(nameof(EraserSize),
        typeof(Double), typeof(InfiniteCanvas), new PropertyMetadata(18.0, PropertyMetadataOptions.AffectsRender));

    /// <summary>What a newly drawn shape is filled with, or nothing for an outline only.</summary>
    public static readonly AdamantiumProperty ShapeFillProperty = AdamantiumProperty.Register(nameof(ShapeFill),
        typeof(Brush), typeof(InfiniteCanvas), new PropertyMetadata(null));

    /// <summary>The frame drawn round what is selected, and the outline of its grips.</summary>
    public static readonly AdamantiumProperty SelectionBrushProperty = AdamantiumProperty.Register(
        nameof(SelectionBrush), typeof(Brush), typeof(InfiniteCanvas),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

    /// <summary>What the grips are filled with.</summary>
    public static readonly AdamantiumProperty HandleBrushProperty = AdamantiumProperty.Register(nameof(HandleBrush),
        typeof(Brush), typeof(InfiniteCanvas), new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

    /// <summary>What the band dragged round several things is filled with. Translucent, or it would hide what it is
    /// being dragged round.</summary>
    public static readonly AdamantiumProperty RubberBandBrushProperty = AdamantiumProperty.Register(
        nameof(RubberBandBrush), typeof(Brush), typeof(InfiniteCanvas),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

    /// <summary>The side of a grip, in SCREEN pixels. A grip is something the hand aims at, so it stays the same size
    /// however far the camera is zoomed - the whole reason the manipulation frame is drawn on the glass and not on the
    /// plane.</summary>
    public static readonly AdamantiumProperty HandleSizeProperty = AdamantiumProperty.Register(nameof(HandleSize),
        typeof(Double), typeof(InfiniteCanvas), new PropertyMetadata(9.0, PropertyMetadataOptions.AffectsRender));

    /// <summary>Whether the controls on the plane are being EDITED rather than used. Editing, a press on one selects and
    /// drags it like anything else here; using, it goes to the control and the button is pressed.
    /// <para>Both are needed and neither can be guessed: a board being laid out and the same board being worked with are
    /// the same objects, and the only difference is which of the two a press means. Design is the default, because a
    /// canvas that holds controls is one somebody is arranging.</para></summary>
    public static readonly AdamantiumProperty IsDesignModeProperty = AdamantiumProperty.Register(nameof(IsDesignMode),
        typeof(Boolean), typeof(InfiniteCanvas),
        new PropertyMetadata(true, PropertyMetadataOptions.BindsTwoWayByDefault, OnDesignModeChanged));

    /// <summary>Whether the overlay is there AT ALL - grip included. Off, the canvas carries nothing of its own and is
    /// driven entirely from outside: a binding, a panel in another window, a view model.
    /// <para>Different from <see cref="IsOverlayOpen"/>, and the difference matters: folding leaves the grip, because a
    /// panel you cannot get back is a panel you have lost. Hiding takes the grip too, and that is only safe when
    /// something else is steering - which is exactly the case this exists for, a mark-up layer over somebody else's
    /// screen.</para></summary>
    public static readonly AdamantiumProperty IsOverlayVisibleProperty = AdamantiumProperty.Register(
        nameof(IsOverlayVisible), typeof(Boolean), typeof(InfiniteCanvas),
        new PropertyMetadata(true, PropertyMetadataOptions.BindsTwoWayByDefault, OnOverlayChanged));

    /// <summary>Whether the overlay's content is showing. Closed, only its grip is left - a tool panel that cannot be
    /// got out of the way is a tool panel sitting on the part of the drawing you need.</summary>
    public static readonly AdamantiumProperty IsOverlayOpenProperty = AdamantiumProperty.Register(nameof(IsOverlayOpen),
        typeof(Boolean), typeof(InfiniteCanvas),
        new PropertyMetadata(true, PropertyMetadataOptions.BindsTwoWayByDefault, OnOverlayChanged));

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
        MouseDown += OnPointerDown;
        MouseMove += OnPointerMove;
        MouseUp += OnPointerUp;
        MouseLeave += OnPointerLeft;
        KeyDown += OnKeyPressed;
        KeyUp += OnKeyReleased;
        TextInput += OnTextTyped;
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

    public object Overlay
    {
        get => GetValue(OverlayProperty);
        set => SetValue(OverlayProperty, value);
    }

    public CanvasOverlayPlacement OverlayPlacement
    {
        get => GetValue<CanvasOverlayPlacement>(OverlayPlacementProperty);
        set => SetValue(OverlayPlacementProperty, value);
    }

    public Vector2 OverlayOffset
    {
        get => GetValue<Vector2>(OverlayOffsetProperty);
        set => SetValue(OverlayOffsetProperty, value);
    }

    public Boolean IsOverlayDraggable
    {
        get => GetValue<Boolean>(IsOverlayDraggableProperty);
        set => SetValue(IsOverlayDraggableProperty, value);
    }

    public ICanvasScene Scene
    {
        get => GetValue<ICanvasScene>(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    public Boolean SnapToGrid
    {
        get => GetValue<Boolean>(SnapToGridProperty);
        set => SetValue(SnapToGridProperty, value);
    }

    public Double SnapDistance
    {
        get => GetValue<Double>(SnapDistanceProperty);
        set => SetValue(SnapDistanceProperty, value);
    }

    public Brush SnapMarkBrush
    {
        get => GetValue<Brush>(SnapMarkBrushProperty);
        set => SetValue(SnapMarkBrushProperty, value);
    }

    public Double SnapMarkSize
    {
        get => GetValue<Double>(SnapMarkSizeProperty);
        set => SetValue(SnapMarkSizeProperty, value);
    }

    /// <summary>Where a world point would be pulled to, and whether anything pulled it. The crossings of the grid AS
    /// DRAWN - the coarsened step, not the declared one - so what snaps is what can be seen to snap.</summary>
    public bool TrySnap(Vector2 world, out Vector2 snapped)
    {
        snapped = world;

        var step = EffectiveGridSpacing;
        if (!SnapToGrid || step <= 0 || GridStyle == CanvasGridStyle.None) return false;

        var nearest = new Vector2(Math.Round(world.X / step) * step, Math.Round(world.Y / step) * step);
        if ((nearest - world).Length() > ScreenToWorldLength(SnapDistance)) return false;

        snapped = nearest;
        return true;
    }

    public Brush Ink
    {
        get => GetValue<Brush>(InkProperty);
        set => SetValue(InkProperty, value);
    }

    public Double InkThickness
    {
        get => GetValue<Double>(InkThicknessProperty);
        set => SetValue(InkThicknessProperty, value);
    }

    public ICanvasTool Tool
    {
        get => GetValue<ICanvasTool>(ToolProperty);
        set => SetValue(ToolProperty, value);
    }

    public ICanvasTool DefaultTool
    {
        get => GetValue<ICanvasTool>(DefaultToolProperty);
        set => SetValue(DefaultToolProperty, value);
    }

    public Double TextSize
    {
        get => GetValue<Double>(TextSizeProperty);
        set => SetValue(TextSizeProperty, value);
    }

    public Double EraserSize
    {
        get => GetValue<Double>(EraserSizeProperty);
        set => SetValue(EraserSizeProperty, value);
    }

    public Brush ShapeFill
    {
        get => GetValue<Brush>(ShapeFillProperty);
        set => SetValue(ShapeFillProperty, value);
    }

    public Brush SelectionBrush
    {
        get => GetValue<Brush>(SelectionBrushProperty);
        set => SetValue(SelectionBrushProperty, value);
    }

    public Brush HandleBrush
    {
        get => GetValue<Brush>(HandleBrushProperty);
        set => SetValue(HandleBrushProperty, value);
    }

    public Brush RubberBandBrush
    {
        get => GetValue<Brush>(RubberBandBrushProperty);
        set => SetValue(RubberBandBrushProperty, value);
    }

    public Double HandleSize
    {
        get => GetValue<Double>(HandleSizeProperty);
        set => SetValue(HandleSizeProperty, value);
    }

    public Boolean IsOverlayVisible
    {
        get => GetValue<Boolean>(IsOverlayVisibleProperty);
        set => SetValue(IsOverlayVisibleProperty, value);
    }

    public Boolean IsOverlayOpen
    {
        get => GetValue<Boolean>(IsOverlayOpenProperty);
        set => SetValue(IsOverlayOpenProperty, value);
    }

    public Boolean IsDesignMode
    {
        get => GetValue<Boolean>(IsDesignModeProperty);
        set => SetValue(IsDesignModeProperty, value);
    }

    /// <summary>What is selected. The canvas holds it rather than the tool, because the frame that shows it is drawn
    /// here and because a tool being swapped must not take the selection with it.</summary>
    public IReadOnlyList<ICanvasItem> Selection => _selection;

    /// <summary>Raised when what is selected changes - so an application can show what is selected, or enable what only
    /// works on a selection.</summary>
    public event EventHandler SelectionChanged;

    /// <summary>The box round everything selected, in WORLD units - what the manipulation frame is drawn on and what a
    /// resize scales. Nothing when nothing is selected.</summary>
    public Rect? SelectionBounds
    {
        get
        {
            if (_selection.Count == 0) return null;

            var box = _selection[0].Bounds;
            for (var i = 1; i < _selection.Count; i++)
            {
                var next = _selection[i].Bounds;
                var left = Math.Min(box.X, next.X);
                var top = Math.Min(box.Y, next.Y);

                box = new Rect(left, top,
                    Math.Max(box.X + box.Width, next.X + next.Width) - left,
                    Math.Max(box.Y + box.Height, next.Y + next.Height) - top);
            }

            return box;
        }
    }

    public bool IsSelected(ICanvasItem item) => item != null && _selection.Contains(item);

    /// <summary>Selects one thing, replacing what was selected unless <paramref name="extend"/> says to add to it.
    /// </summary>
    public void Select(ICanvasItem item, bool extend)
    {
        if (item == null) return;

        if (!extend) _selection.Clear();
        if (!_selection.Contains(item)) _selection.Add(item);

        Selected();
    }

    public void SelectMany(IEnumerable<ICanvasItem> items, bool extend)
    {
        if (!extend) _selection.Clear();

        foreach (var item in items)
        {
            if (item != null && !_selection.Contains(item)) _selection.Add(item);
        }

        Selected();
    }

    public void Deselect(ICanvasItem item)
    {
        if (item == null || !_selection.Remove(item)) return;

        Selected();
    }

    public void ClearSelection()
    {
        if (_selection.Count == 0) return;

        _selection.Clear();
        Selected();
    }

    /// <summary>Takes everything selected out of the scene.</summary>
    public void DeleteSelection()
    {
        if (_selection.Count == 0 || Scene == null) return;

        foreach (var item in _selection) Scene.Remove(item);

        _selection.Clear();
        Selected();
    }

    /// <summary>Which grip of the manipulation frame a SCREEN point is on, or <see cref="CanvasHandle.None"/>.
    /// <para>Asked in screen pixels and not in the world on purpose: a grip is something the hand aims at, so how close
    /// counts as on it is a distance on the glass at any zoom.</para></summary>
    public CanvasHandle HandleAt(Vector2 screen)
    {
        if (SelectionBounds is not { } bounds) return CanvasHandle.None;

        var topLeft = WorldToScreen(new Vector2(bounds.X, bounds.Y));
        var bottomRight = WorldToScreen(new Vector2(bounds.X + bounds.Width, bounds.Y + bounds.Height));
        var reach = Math.Max(4, HandleSize) / 2 + 2;

        var left = Math.Abs(screen.X - topLeft.X) <= reach;
        var right = Math.Abs(screen.X - bottomRight.X) <= reach;
        var top = Math.Abs(screen.Y - topLeft.Y) <= reach;
        var bottom = Math.Abs(screen.Y - bottomRight.Y) <= reach;

        var withinX = screen.X >= topLeft.X - reach && screen.X <= bottomRight.X + reach;
        var withinY = screen.Y >= topLeft.Y - reach && screen.Y <= bottomRight.Y + reach;

        if (!withinX || !withinY) return CanvasHandle.None;

        // Corners before edges: at a corner both an edge grip and a corner grip are under the pointer, and the corner is
        // the one that was aimed at - it is the only one that resizes both ways.
        if (left && top) return CanvasHandle.TopLeft;
        if (right && top) return CanvasHandle.TopRight;
        if (left && bottom) return CanvasHandle.BottomLeft;
        if (right && bottom) return CanvasHandle.BottomRight;

        var midX = Math.Abs(screen.X - (topLeft.X + bottomRight.X) / 2) <= reach;
        var midY = Math.Abs(screen.Y - (topLeft.Y + bottomRight.Y) / 2) <= reach;

        if (left && midY) return CanvasHandle.Left;
        if (right && midY) return CanvasHandle.Right;
        if (top && midX) return CanvasHandle.Top;
        if (bottom && midX) return CanvasHandle.Bottom;

        return CanvasHandle.None;
    }

    private void Selected()
    {
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        InvalidateRender(false);
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

        // GLASS: no ground at all, so whatever the canvas is laid over shows through it. Skipped rather than painted
        // transparent - the ground is one quad over the WHOLE viewport with a shader on it, and at a large size that
        // quad is the most expensive thing on an otherwise empty canvas. A mark-up layer should not pay for a surface
        // it does not have.
        if (GridStyle != CanvasGridStyle.Transparent) DrawGround(session, size);

        // Only what can be SEEN. The cost of a frame follows the viewport, not the drawing: a scene of a hundred
        // thousand strokes on a plane the size of a town draws the dozen under the camera.
        if (Scene is { } scene)
        {
            var visible = VisibleWorld;
            foreach (var item in scene.ItemsIn(visible)) item.Render(session, this);
        }

        // What the TOOL is making but has not put in the scene yet - a stroke still under the pen, a shape being dragged
        // out, the selection band. It goes in when the gesture ends, so a half-made thing cannot be hit-tested, saved or
        // undone halfway through.
        Tool?.Render(session, this);

        // THE OVERLAY: what is drawn on the glass rather than on the plane. It comes last, it is stated in SCREEN
        // pixels, and it never scales - a mark that grew with the zoom would be part of the drawing, which is exactly
        // what it is not.
        DrawOverlay(session);
    }

    // ONE rectangle for the whole ground: the shader decides per pixel, from the world coordinate under it, whether it
    // is on a mark. What stood here emitted a rectangle per mark - about a thousand a frame at 1:1, growing as viewport
    // area over pitch squared - and could not anti-alias a mark or fade one step of the grid into the next.
    private void DrawGround(IDrawingSession session, Size size)
    {
        _ground.Marks = (CanvasGridMarks)GridStyle;
        _ground.Offset = Offset;
        _ground.Scale = Scale;
        // The step ALREADY coarsened - the same number EffectiveGridSpacing reports, so a ruler and a snap agree with
        // what is actually drawn instead of the shader working it out a second time.
        _ground.Spacing = EffectiveGridSpacing;
        _ground.Coarsening = GridCoarsening;
        _ground.MinPitch = MinGridPitch;
        _ground.MarkSize = GridStyle == CanvasGridStyle.Dots ? GridDotSize : GridThickness;
        _ground.Background = ColourOf(Background, new Color(0, 0, 0, 0));
        _ground.Color = ColourOf(GridBrush, new Color(0, 0, 0, 0));
        _ground.AxisColor = ColourOf(AxisBrush, new Color(0, 0, 0, 0));

        session.DrawRectangle(_ground, new Rect(0, 0, size.Width, size.Height));
    }

    private void DrawOverlay(IDrawingSession session)
    {
        DrawManipulation(session);

        if (_snap is not { } at || SnapMarkBrush == null || SnapMarkSize <= 0) return;

        var mark = WorldToScreen(at);
        var half = SnapMarkSize / 2;

        session.DrawEllipse(new Rect(mark.X - half, mark.Y - half, SnapMarkSize, SnapMarkSize),
            SnapMarkBrush, 0, 360, EllipseType.Sector);
    }

    // The manipulation frame: one box round everything selected, with eight grips on it. Drawn in SCREEN pixels and
    // never scaled - a grip that grew with the zoom would be part of the drawing, which is exactly what it is not, and a
    // frame round a group is the thing that makes a group one thing to drag.
    private void DrawManipulation(IDrawingSession session)
    {
        // Not while the controls are being USED: the frame and its grips are how a thing is edited, and nothing on the
        // plane can be edited in that mode - a frame drawn there is a handle that does not work.
        if (!IsDesignMode) return;

        if (SelectionBounds is not { } bounds || SelectionBrush == null) return;

        var pen = PenOf(SelectionBrush, ref _selectionPen, ref _selectionPenBrush);
        var frame = ToScreen(bounds);

        session.DrawRectangle(null, frame, pen);

        if (HandleSize <= 0) return;

        foreach (var grip in Grips(frame)) session.DrawRectangle(HandleBrush ?? SelectionBrush, grip, pen);
    }

    private IEnumerable<Rect> Grips(Rect frame)
    {
        var side = HandleSize;
        var half = side / 2;

        var left = frame.X;
        var middle = frame.X + frame.Width / 2;
        var right = frame.X + frame.Width;
        var top = frame.Y;
        var centre = frame.Y + frame.Height / 2;
        var bottom = frame.Y + frame.Height;

        yield return new Rect(left - half, top - half, side, side);
        yield return new Rect(middle - half, top - half, side, side);
        yield return new Rect(right - half, top - half, side, side);
        yield return new Rect(right - half, centre - half, side, side);
        yield return new Rect(right - half, bottom - half, side, side);
        yield return new Rect(middle - half, bottom - half, side, side);
        yield return new Rect(left - half, bottom - half, side, side);
        yield return new Rect(left - half, centre - half, side, side);
    }

    /// <summary>A world rectangle as it lands on screen.</summary>
    public Rect ToScreen(Rect world)
    {
        var topLeft = WorldToScreen(new Vector2(world.X, world.Y));
        var bottomRight = WorldToScreen(new Vector2(world.X + world.Width, world.Y + world.Height));

        return new Rect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
    }

    // A pen per brush, kept: a pen is immutable-per-change, so building one every frame would allocate for every framed
    // thing on screen on every frame - and these are drawn on every frame there is a selection.
    private static Pen PenOf(Brush brush, ref Pen pen, ref Brush was)
    {
        if (brush == null) return null;
        if (pen != null && ReferenceEquals(was, brush)) return pen;

        was = brush;
        pen = new Pen(brush);

        return pen;
    }

    // The grid is a shader, and a shader wants COLOURS. A gradient or a picture would say nothing about where a mark is,
    // so anything that is not a plain colour falls back to nothing rather than being approximated into something the
    // theme did not ask for.
    private static Color ColourOf(Brush brush, Color fallback) =>
        brush is SolidColorBrush solid ? solid.Color : fallback;

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        if (!ZoomWithWheel || e.Handled) return;

        // A wheel turned over the tool panel, or over a control standing on the drawing, belongs to THAT and to nothing
        // else - even when it did nothing with it. A scroller that has reached its end deliberately leaves the wheel
        // unhandled so the page underneath carries on scrolling, which is right for a page and wrong for this: here the
        // thing underneath is the camera, and a panel scrolled to its last row would zoom the whole drawing.
        if (FromGlass(e.OriginalSource)) return;

        // ZoomStep is one standard notch (120), raised to how far the wheel actually turned - so a hi-res wheel firing
        // many fractional events zooms the same total as a standard one.
        ZoomAt(e.GetPosition(this), Math.Pow(ZoomStep, e.Delta / 120.0));
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

        // A double click is the CLICK COUNT on the press, which is how everything else in the engine reads one. What
        // stood here listened to Mouse.MouseDoubleClickEvent - and nothing anywhere raises it: a CS_DBLCLKS window's
        // WM_*BUTTONDBLCLK is deliberately surfaced as a plain button DOWN so the count keeps working, so the event is
        // dead. That is why this gesture never fired once, and why fixing the button mapping under it changed nothing.
        if (e.ClickCount >= 2)
        {
            // The middle button pans, so double-clicking it is the gesture for "put it back": same button, hand already
            // on it.
            if (e.ChangedButton == MouseButtons.Middle)
            {
                ResetCamera();
                e.Handled = true;
                return;
            }

        }

        // The RIGHT button puts the tool back. Whatever was half done is given up first - a line still being placed
        // belongs to the pen, and the tool taking over has no way to end it.
        if (e.ChangedButton == MouseButtons.Right && DefaultTool is { } resting)
        {
            Tool?.Cancel(this);
            SetCurrentValue(ToolProperty, resting);

            e.Handled = true;
            return;
        }

        var pans = e.ChangedButton == MouseButtons.Middle || (e.ChangedButton == MouseButtons.Left && _spaceHeld);
        if (pans)
        {
            _panning = true;
            _panFrom = e.GetPosition(this);
            _panOffsetFrom = Offset;

            CaptureMouse();
            e.Handled = true;
            return;
        }

        // A press that started INSIDE a control on the plane is that control's, and the tool must not read it as a press
        // on the plane as well - it bubbles up here having done its work, and a tool acting on it put a second control
        // wherever the first one was clicked. Panning is deliberately left alone above: looking at a drawing works
        // everywhere, including over the things on it.
        if (FromGlass(e.OriginalSource)) return;

        // Everything else is the TOOL's. The canvas keeps the wheel, the middle button, space and Home - how you look at
        // a drawing - and knows nothing about what the plain left button means.
        var args = Describe(e.GetPosition(this), e.ChangedButton, e.ClickCount, e.Modifiers);
        Tool?.OnPressed(this, args);
        if (args.Handled) e.Handled = true;
    }

    private void OnPointerMove(object sender, MouseEventArgs e)
    {
        if (_panning)
        {
            // From where the drag STARTED, not from the last move: accumulating deltas drifts, and the camera has
            // nothing to clamp against that would hide it.
            SetCurrentValue(OffsetProperty, _panOffsetFrom + (e.GetPosition(this) - _panFrom));
            e.Handled = true;
            return;
        }

        var pointer = e.GetPosition(this);

        // Where the pointer would be PULLED to, worked out whatever the tool is doing - the mark has to appear before the
        // press, or nobody can aim at it.
        var pulled = TrySnap(ScreenToWorld(pointer), out var target) ? target : (Vector2?)null;
        if (pulled != _snap)
        {
            _snap = pulled;
            InvalidateRender(false);
        }

        var args = Describe(pointer, MouseButtons.None, 0, e.Modifiers);
        Tool?.OnMoved(this, args);
        if (args.Handled) e.Handled = true;
    }

    private void OnPointerLeft(object sender, MouseEventArgs e)
    {
        if (_snap == null) return;

        _snap = null;
        InvalidateRender(false);
    }

    private void OnPointerUp(object sender, MouseButtonEventArgs e)
    {
        if (_panning)
        {
            _panning = false;
            ReleaseMouseCapture();
            e.Handled = true;
            return;
        }

        var args = Describe(e.GetPosition(this), e.ChangedButton, e.ClickCount, e.Modifiers);
        Tool?.OnReleased(this, args);
        if (args.Handled) e.Handled = true;
    }

    // Whether an event came from something the canvas CARRIES rather than from the plane: the tool panel on the glass,
    // or a control standing on the drawing. Their own events bubble through this canvas, and the ones that matter are
    // not marked handled by whoever answered them - a control answers MouseLeftButtonDown, which is raised from a
    // SEPARATE args object, and a scroller deliberately leaves the wheel alone once it has reached its end - so where
    // the event came from is the only thing there is to go on.
    private bool FromGlass(object source)
    {
        if (_overlayRoot == null && _elements == null) return false;

        for (var at = source as IUIComponent; at != null; at = at.VisualParent)
        {
            if (ReferenceEquals(at, _overlayRoot) || ReferenceEquals(at, _elements)) return true;
        }

        return false;
    }

    // The pointer as a TOOL sees it: in the world, already pulled to the grid when the grid pulls. Snapping is decided
    // here and once, so that no tool has to know the setting exists and every tool obeys it the same way.
    private CanvasPointerEventArgs Describe(Vector2 screen, MouseButtons button, int clicks, InputModifiers modifiers)
    {
        var world = ScreenToWorld(screen);
        var snapped = TrySnap(world, out var pulled);

        return new CanvasPointerEventArgs
        {
            Screen = screen,
            World = snapped ? pulled : world,
            Pointer = world,
            IsSnapped = snapped,
            Button = button,
            ClickCount = clicks,
            Modifiers = modifiers
        };
    }

    private void OnKeyPressed(object sender, KeyEventArgs e)
    {
        // The TOOL first. The canvas spends Delete on the selection, Escape on letting go and space on panning, and all
        // three mean something else to a tool with a caret in a word - so the tool is asked before any of them.
        Tool?.OnKey(this, e);
        if (e.Handled) return;

        if (e.Key == Key.Space)
        {
            _spaceHeld = true;
            return;
        }

        if (e.Key == Key.Delete)
        {
            if (_selection.Count == 0) return;

            DeleteSelection();
            e.Handled = true;
            return;
        }

        // Escape gives up whatever is half done - a line still being placed, a band still open - and then lets go of the
        // selection. Two presses rather than one, so that abandoning a gesture does not also lose what was selected.
        if (e.Key == Key.Escape)
        {
            if (Tool is { IsBusy: true } busy)
            {
                busy.Cancel(this);
                e.Handled = true;
                return;
            }

            if (_selection.Count == 0) return;

            ClearSelection();
            e.Handled = true;
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

    // Characters go straight to the tool and nowhere else: the canvas has nothing to type into, and what a key means on
    // the user's own keyboard is a question only the layout can answer - which is what this event already did.
    private void OnTextTyped(object sender, TextInputEventArgs e) => Tool?.OnText(this, e);

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

    // The scene is the application's, so the canvas listens rather than owns - and lets go of the old one, which is the
    // whole reason this is not done with a lambda.
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _elements = GetTemplateChild("PART_Elements") as CanvasElementLayer;
        if (_elements != null) _elements.Owner = this;

        ApplyDesignMode();
        SyncElements();

        _overlay = GetTemplateChild("PART_Overlay") as ContentPresenter;

        // The BLOCK that is placed and dragged: the grip and the panel travel together, because a grip that stayed put
        // while its panel moved would not be that panel's grip any more.
        _overlayRoot = GetTemplateChild("PART_OverlayRoot") as InputUIComponent ?? _overlay;
        _overlayGrip = GetTemplateChild("PART_OverlayGrip") as ButtonBase;

        // The inset the THEME asked for, kept before anything overwrites it: placing the block rewrites the margin, and
        // a panel pinned flush against the edge is not what any of the three themes said.
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

        if (_elements != null) _elements.Owner = null;

        _elements = null;
        _overlay = null;
        _overlayRoot = null;
        _overlayGrip = null;
    }

    // The grip both opens the panel and carries it, and one press cannot be both - so the press starts a drag and the
    // CLICK toggles, and a press that turned into a drag is not a click. Which is how every draggable handle that also
    // does something on click has to work.
    private void OnGripClicked(object sender, RoutedEventArgs e)
    {
        if (_overlayMoved) return;

        SetCurrentValue(IsOverlayOpenProperty, !IsOverlayOpen);
    }

    // Every press that reaches the panel is the PANEL's, whatever the panel then does with it, and the canvas must not
    // read it as a press on the plane as well. It was: the stroke's CaptureMouse() took the capture that a button inside
    // the panel had just taken for itself, so that button never saw its own release and nothing in the panel answered.
    // MouseDown is not marked handled by the controls that answer it - they answer MouseLeftButtonDown, which is raised
    // from a SEPARATE args object - so where the press came from is the only thing there is to go on.
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

        // Taken only when NOBODY below took it. The grip captures the press for itself, and stealing that would leave it
        // never seeing its own release - so no click, so the panel could never be folded, which is exactly what it did.
        // Nothing is lost by not taking it: the grip is inside this block, so its moves and its release bubble here
        // anyway.
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

    // What else the panel may be PICKED UP by: its own chrome - the edge, the padding, the background. Not the button or
    // the slider under the pointer: a panel dragged out from under a control takes away the capture that control just
    // took, which is the whole fault this handler exists for. The panel's own ROOT is exempt, or a panel that is itself
    // a control would have nowhere to be picked up by.
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

        // A press that has actually MOVED is a drag and no longer a click, so the grip does not also toggle when it is
        // let go. Measured in screen pixels, because "did not move" is a fact about the hand.
        if ((e.GetPosition(this) - _overlayFrom).Length() > 3) _overlayMoved = true;

        // Kept INSIDE the canvas: a panel dragged off the edge is a panel nobody can get back, and there is no edge on
        // the plane itself to find it by.
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

    // The placement is the CANVAS's, so the control puts it on the part - a template cannot map one to the other, and
    // saying it in the theme would mean three themes having to agree about it.
    private void PlaceOverlay()
    {
        if (_overlay != null)
        {
            _overlay.Content = Overlay;

            // Closed, the content is COLLAPSED and not merely hidden: hidden it would still take its place, and the grip
            // would sit exactly where it sat when the panel was open, having got nothing out of the way.
            _overlay.Visibility = IsOverlayOpen ? Visibility.Visible : Visibility.Collapsed;
        }

        if (_overlayRoot == null) return;

        // Gone ENTIRELY - grip and all - and collapsed rather than hidden, so it takes no place either. This is the
        // canvas as a bare sheet: whatever is steering it is somewhere else.
        _overlayRoot.Visibility = IsOverlayVisible ? Visibility.Visible : Visibility.Collapsed;

        // Put somewhere by hand: it is held by its own margin from the top-left, which is the only placement that can
        // say "here" rather than "in that corner".
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
            CanvasOverlayPlacement.TopCentre or CanvasOverlayPlacement.BottomCentre => HorizontalAlignment.Center,
            _ => HorizontalAlignment.Left
        };

        _overlayRoot.VerticalAlignment = OverlayPlacement switch
        {
            CanvasOverlayPlacement.BottomLeft or CanvasOverlayPlacement.BottomRight
                or CanvasOverlayPlacement.BottomCentre => VerticalAlignment.Bottom,
            _ => VerticalAlignment.Top
        };
    }

    // Turning the snap off in the middle of a line ends it where it had got to. The alternative is a half-drawn line
    // that can no longer be finished, because the gesture that would have finished it is gone with the mode.
    private static void OnSnapToGridChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is InfiniteCanvas canvas && e.NewValue is false && canvas.Tool is { IsBusy: true } tool)
        {
            tool.Cancel(canvas);
        }
    }

    // A tool being put down finishes what it had started: a half-drawn line belongs to the pen, and the tool taking over
    // has no way to end it.
    private static void OnToolChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not InfiniteCanvas canvas) return;

        (e.OldValue as ICanvasTool)?.Cancel(canvas);
        canvas.InvalidateRender(false);
    }

    private static void OnOverlayChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e) =>
        (component as InfiniteCanvas)?.PlaceOverlay();

    private static void OnSceneChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not InfiniteCanvas canvas) return;

        if (e.OldValue is ICanvasScene previous) previous.Changed -= canvas.OnSceneEdited;
        if (e.NewValue is ICanvasScene current) current.Changed += canvas.OnSceneEdited;

        // A whole scene arriving is as much a change as an item being put in one, and the controls in it have to reach
        // the layer the same way.
        canvas.SyncElements();
        canvas._elements?.InvalidateMeasure();
        canvas._elements?.InvalidateArrange();

        canvas.InvalidateRender(false);
    }

    private void OnSceneEdited(object sender, EventArgs e)
    {
        SyncElements();

        // ...and place them again even when the SET has not changed. An edit here is just as often one item's rectangle
        // moving - which is exactly what dragging a control does - and a layer that only answered to items appearing and
        // disappearing left the control behind while its frame walked off.
        _elements?.InvalidateMeasure();
        _elements?.InvalidateArrange();

        InvalidateRender(false);
    }

    private static void OnCameraChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not InfiniteCanvas canvas) return;

        // Whoever moved the camera - a binding, the wheel, the application - has placed it, and the canvas must not
        // then move it somewhere of its own on the first layout.
        if (e.Property == OffsetProperty)
            canvas._cameraPlaced = true;

        // The controls on the plane MOVE with the camera, and moving them is an arrange: what is inside one was measured
        // for its own rectangle, which the camera does not change. A zoom changes that rectangle, so it measures again.
        canvas.SyncElements();
        canvas._elements?.InvalidateArrange();
        if (e.Property == ScaleProperty) canvas._elements?.InvalidateMeasure();

        canvas.InvalidateRender(false);
    }

    private static void OnDesignModeChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is InfiniteCanvas canvas) canvas.ApplyDesignMode();
    }

    // Editing, the layer is INVISIBLE TO THE POINTER, so a press on a control lands on the plane and the select tool
    // picks the control up. Using, the press is the control's. There is no third answer: a press cannot both operate a
    // button and drag it.
    private void ApplyDesignMode()
    {
        if (_elements != null) _elements.IsHitTestVisible = !IsDesignMode;

        InvalidateRender(false);
    }

    // The controls that are ON SCREEN, handed to the layer. Only the visible ones, like everything else here: the cost
    // of a frame follows the viewport and not the drawing. A control scrolled off the plane leaves the visual tree and
    // comes back when it returns - which is the same bargain a virtualized list makes.
    private void SyncElements()
    {
        if (_elements == null) return;

        _visibleElements.Clear();
        if (Scene is { } scene)
        {
            var visible = VisibleWorld;
            foreach (var item in scene.ItemsIn(visible))
            {
                if (item is ElementItem element) _visibleElements.Add(element);
            }
        }

        _elements.Sync(_visibleElements);
    }

    // The world's origin starts in the MIDDLE of the viewport, not in its top-left corner. Zero offset would put it in
    // the corner, and a plane whose origin sits off in a corner reads as one that has been scrolled away from - on
    // something with no edges that is the one impression to avoid.
    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);

        // From finalSize, not RenderSize: this IS the pass that sets the latter.
        if (!_cameraPlaced && finalSize is { Width: > 0, Height: > 0 })
        {
            _cameraPlaced = true;
            SetCurrentValue(OffsetProperty, new Vector2(finalSize.Width / 2, finalSize.Height / 2));
        }

        // The controls on the plane are asked for again HERE, because "which of them can be seen" is answered from the
        // viewport - and until this pass there is no viewport to answer from. Every earlier sync ran against a size of
        // zero, found nothing, and nothing ever asked again: a scene that arrived with controls already in it showed
        // none of them, for good, while one built by hand afterwards worked, because putting an item in re-asks.
        SyncElements();

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
}
