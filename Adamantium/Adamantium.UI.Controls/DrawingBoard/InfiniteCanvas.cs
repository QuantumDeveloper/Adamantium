using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Adamantium.Graphics.Fonts;
using Adamantium.ProceduralGeometry.Shapes;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Animation;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls.DrawingBoard;

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

    private int _draggingPoint = -1;
    private int _editDepth;
    private string _editReason;
    private List<ICanvasItem> _editBefore;
    private Dictionary<ICanvasItem, Rect> _editWasAt;

    private CanvasElementLayer _elements;
    private CanvasFrontLayer _front;
    private readonly List<ElementItem> _visibleElements = new();

    private CanvasChromeLayer _chromeLayer;
    private CanvasPanes _chrome;
    private CanvasTools _tools;
    private readonly CanvasFrameGesture _frame = new();
    private bool _publishing;

    private TextLayout _readout;
    private FontFamily _readoutFont;
    private string _readoutText;
    private Size _readoutSize;
    private Vector2 _pointer;
    private bool _pointerInside;

    private ContentPresenter _overlay;
    private readonly CanvasGraphHost _graph;
    private readonly CanvasDrawingHost _drawing;
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

    /// <summary>The tools this canvas offers, in the order a rail should show them. Putting a tool here is what makes
    /// it pickable by a button and by its own shortcut; <see cref="Tool"/> is which of them is in hand.
    /// <para>A list on the canvas rather than buttons in markup, because everything a button needs - the name, the
    /// picture, the key - is a fact about the tool, and mirroring those into commands and flags is work that goes out
    /// of step the first time somebody adds a tool.</para></summary>
    public static readonly AdamantiumProperty ToolsProperty = AdamantiumProperty.Register(nameof(Tools),
        typeof(CanvasTools), typeof(InfiniteCanvas), new PropertyMetadata(null, OnToolsChanged));

    /// <summary>Whether a tool's own <see cref="ICanvasTool.Shortcut"/> picks it. On by default; an application that
    /// spends those keys on something else turns it off, and one that only disagrees about WHICH key sets a different
    /// shortcut on the tool instead.</summary>
    public static readonly AdamantiumProperty AreToolShortcutsEnabledProperty = AdamantiumProperty.Register(
        nameof(AreToolShortcutsEnabled), typeof(Boolean), typeof(InfiniteCanvas), new PropertyMetadata(true));

    /// <summary>The panels shown over the plane - a tool rail, an inspector, a context bar. The canvas owns WHERE each
    /// one goes (<see cref="CanvasPane.Placement"/>) and the application owns what is in it.
    /// <para>A collection and not one slot, because a canvas's chrome has three jobs with three different laws of
    /// placement, and putting all three in one panel is how a tool panel turns into a column that does not fit. See
    /// <see cref="CanvasPane"/>.</para>
    /// <para><see cref="Overlay"/> still works and is the same thing said for one panel: keep it for a single floating
    /// panel, use this when there is more than one.</para></summary>
    public static readonly AdamantiumProperty ChromeProperty = AdamantiumProperty.Register(nameof(Chrome),
        typeof(CanvasPanes), typeof(InfiniteCanvas), new PropertyMetadata(null, OnChromeChanged));

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

    /// <summary>The GRAPH: the application's own node objects, one container made for each. Written from both sides -
    /// a node made on the plane appears here, one removed from here leaves the plane - so this collection is the only
    /// account of what the graph is, and the canvas keeps no second copy of it.
    /// <para>Nothing about it is a control: see <see cref="ICanvasNode"/> for the whole of what a node has to say.
    /// </para></summary>
    public static readonly AdamantiumProperty NodesProperty = AdamantiumProperty.Register(nameof(Nodes),
        typeof(IEnumerable), typeof(InfiniteCanvas), new PropertyMetadata(null, OnNodesChanged));

    public IEnumerable Nodes
    {
        get => GetValue<IEnumerable>(NodesProperty);
        set => SetValue(NodesProperty, value);
    }

    private static void OnNodesChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is InfiniteCanvas canvas) canvas._graph.SetNodes(e.NewValue as IEnumerable);
    }

    /// <summary>WHAT IS ON THE PLANE besides the graph - the application's own objects for the drawing, the way
    /// <see cref="Nodes"/> is for the graph.
    /// <para>The canvas makes what draws them: a shape described becomes the canvas's own drawing of it, anything else
    /// becomes a control built here from <see cref="ObjectTemplateSelector"/>. An application never constructs a scene
    /// item and never a control of its own to put on a plane.</para></summary>
    public static readonly AdamantiumProperty ObjectsProperty = AdamantiumProperty.Register(nameof(Objects),
        typeof(IEnumerable), typeof(InfiniteCanvas), new PropertyMetadata(null, OnObjectsChanged));

    public IEnumerable Objects
    {
        get => GetValue<IEnumerable>(ObjectsProperty);
        set => SetValue(ObjectsProperty, value);
    }

    private static void OnObjectsChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is InfiniteCanvas canvas) canvas._drawing.SetObjects(e.NewValue as IEnumerable);
    }

    /// <summary>What draws the objects that are not shapes - picked for the type of what each one carries, the same way
    /// a node's body is.</summary>
    public static readonly AdamantiumProperty ObjectTemplateSelectorProperty = AdamantiumProperty.Register(
        nameof(ObjectTemplateSelector), typeof(DataTemplateSelector), typeof(InfiniteCanvas),
        new PropertyMetadata(null, OnObjectTemplateSelectorChanged));

    public DataTemplateSelector ObjectTemplateSelector
    {
        get => GetValue<DataTemplateSelector>(ObjectTemplateSelectorProperty);
        set => SetValue(ObjectTemplateSelectorProperty, value);
    }

    private static void OnObjectTemplateSelectorChanged(AdamantiumComponent component,
        AdamantiumPropertyChangedEventArgs e)
    {
        if (component is InfiniteCanvas canvas) canvas._drawing.SetTemplateSelector(e.NewValue as DataTemplateSelector);
    }

    /// <summary>The KINDS of node this application has, said by the application itself - a collection of
    /// <see cref="ICanvasNodeKind"/>, each of which knows its word and can make the inside of a node of that sort.
    /// <para>Given to the canvas rather than gathered from what is already on the plane: a list collected that way is
    /// never complete - it holds what somebody happened to make and nothing else - and the palette, the inspector and
    /// the loader would each be offering a different one. This is the one list, and all three read it.</para>
    /// <para>It is also what a factory handed to the control would have been: the shell of a node is the engine's own
    /// and the canvas makes it, and what that node IS comes from the kind picked out of here. So there is no delegate
    /// on this surface - the kinds are data, which is what a drop-down and a palette need them to be.</para></summary>
    public static readonly AdamantiumProperty NodeKindsProperty = AdamantiumProperty.Register(nameof(NodeKinds),
        typeof(IEnumerable), typeof(InfiniteCanvas), new PropertyMetadata(null, OnNodeKindsChanged));

    public IEnumerable NodeKinds
    {
        get => GetValue<IEnumerable>(NodeKindsProperty);
        set => SetValue(NodeKindsProperty, value);
    }

    private static void OnNodeKindsChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is InfiniteCanvas canvas) canvas._graph.SetKinds(e.NewValue as IEnumerable);
    }

    /// <summary>The kinds a SOCKET may carry, said the same way. Two sockets join when their kinds agree, so a name
    /// typed by hand is a wire that quietly will not go on.</summary>
    public static readonly AdamantiumProperty SocketKindsProperty = AdamantiumProperty.Register(nameof(SocketKinds),
        typeof(IEnumerable), typeof(InfiniteCanvas), new PropertyMetadata(null, OnSocketKindsChanged));

    public IEnumerable SocketKinds
    {
        get => GetValue<IEnumerable>(SocketKindsProperty);
        set => SetValue(SocketKindsProperty, value);
    }

    private static void OnSocketKindsChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is InfiniteCanvas canvas) canvas._graph.SetSocketKinds(e.NewValue as IEnumerable);
    }


    /// <summary>Puts a node of that kind on the plane, at that point in the world - by adding it to
    /// <see cref="Nodes"/>, which is the only way anything gets onto the graph.
    /// <para>Returns what was added, or null when there is no collection to add to: a canvas showing a graph it was
    /// never given cannot grow one.</para></summary>
    public ICanvasNode AddNode(String kind, Vector2 at) => _graph.Add(kind, at);

    /// <summary>What the canvas is being used AS - a drawing, or a graph of nodes. See <see cref="CanvasMode"/> for why
    /// this is a mode and not a filter the user sets.
    /// <para>What is not of the current mode is not drawn, not picked, not selected and not listed. It is not deleted
    /// either: a scene may hold both, and switching back brings the other one out again untouched.</para></summary>
    public static readonly AdamantiumProperty ModeProperty = AdamantiumProperty.Register(nameof(Mode),
        typeof(CanvasMode), typeof(InfiniteCanvas),
        new PropertyMetadata(CanvasMode.Drawing,
            PropertyMetadataOptions.AffectsRender | PropertyMetadataOptions.BindsTwoWayByDefault, OnModeChanged));

    public CanvasMode Mode
    {
        get => GetValue<CanvasMode>(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    /// <summary>Raised after <see cref="Mode"/> changes, so a rail can offer the tools that mode admits.</summary>
    public event EventHandler ModeChanged;

    /// <summary>Where what was done is remembered. The canvas makes its OWN and undo works out of the box; bind another
    /// to share one memory across a whole editor, or set null for a canvas that is to remember nothing at all.
    /// <para>Not something an application must bring. It used to be, and that made a canvas that could not take back so
    /// much as a stroke until somebody wired one up - a feature the control had and refused to use.</para></summary>
    public static readonly AdamantiumProperty HistoryProperty = AdamantiumProperty.Register(nameof(History),
        typeof(CanvasHistory), typeof(InfiniteCanvas), new PropertyMetadata(null, OnHistoryChanged));

    // WHAT THERE IS TO UNDO decides whether the undo button is pressable, so the canvas listens to the memory it was
    // given and tells its commands. Swapping one history for another moves that ear with it.
    private static void OnHistoryChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not InfiniteCanvas canvas) return;

        if (e.OldValue is CanvasHistory was) was.Changed -= canvas.OnHistoryMoved;
        if (e.NewValue is CanvasHistory now) now.Changed += canvas.OnHistoryMoved;

        canvas.Refresh();
    }

    // ...and the plane is DRAWN AGAIN. Something was done, undone or put back, and the only thing every one of those
    // has in common is that the memory moved: an inspector writing a colour leaves no trace in where anything is, so
    // this is what makes the wire hanging off a recoloured socket follow it without anybody wiring the two together.
    private void OnHistoryMoved(object sender, EventArgs e)
    {
        Refresh();
        Scene?.Touch();
        Repaint();
    }

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

        _graph = new CanvasGraphHost(this);
        _drawing = new CanvasDrawingHost();

        // An empty one to start with, so that code can add to these without making the collection first - and written
        // at DEFAULT priority, which is the whole point. Local(1) outranks Binding(2) permanently here, so a collection
        // made the ordinary way - in the constructor, or lazily from the getter - masks {Binding} on that property for
        // good. Measured exactly that: the view model held eleven tools and the canvas a different, empty list, and no
        // binding could ever reach it again.
        SetValue(ToolsProperty, new CanvasTools(), ValuePriority.Default);
        SetValue(ChromeProperty, new CanvasPanes(), ValuePriority.Default);

        // ITS OWN MEMORY, and not something an application has to bring. A canvas is an editor: taking the last thing
        // back is part of what it IS, and a control that could only do it once somebody handed it a history was a
        // control that did nothing out of the box. Written at DEFAULT priority for the same reason as the collections
        // above - so an application that wants ONE history for a whole editor binds its own here and this one gives way.
        SetValue(HistoryProperty, new CanvasHistory(), ValuePriority.Default);

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

    public CanvasPanes Chrome
    {
        get => _chrome;
        set => SetValue(ChromeProperty, value);
    }

    public CanvasTools Tools
    {
        get => _tools;
        set => SetValue(ToolsProperty, value);
    }

    public Boolean AreToolShortcutsEnabled
    {
        get => GetValue<Boolean>(AreToolShortcutsEnabledProperty);
        set => SetValue(AreToolShortcutsEnabledProperty, value);
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

    /// <summary>Where what was done is remembered. Null means nothing is.</summary>
    public CanvasHistory History
    {
        get => GetValue<CanvasHistory>(HistoryProperty);
        set => SetValue(HistoryProperty, value);
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

        // NOT IN A GRAPH. Snapping is about PLACING something on the plane, and in a graph nothing is placed on a
        // crossing: a node is dragged where it belongs and a wire joins two sockets. The mark that shows where the
        // pointer would be pulled to then stands under the cursor promising a press that would draw, which is the one
        // thing a graph does not do. Lining a graph up is what Align and Spread are for.
        if (Mode == CanvasMode.Nodes) return false;

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
    /// here and because a tool being swapped must not take the selection with it.
    /// <para>A PROPERTY and not a plain list, so an inspector can follow it: a binding has to be told the selection
    /// changed, and a list quietly edited in place tells nobody. Each change publishes a fresh snapshot - selection is
    /// a thing a person does a few times a minute, so the copy costs nothing worth counting.</para></summary>
    public static readonly AdamantiumProperty SelectionProperty = AdamantiumProperty.Register(nameof(Selection),
        typeof(IReadOnlyList<ICanvasItem>), typeof(InfiniteCanvas),
        new PropertyMetadata(null, PropertyMetadataOptions.BindsTwoWayByDefault, OnSelectionSet));

    /// <summary>Whether a small plate follows the pointer showing where it is on the plane, while the grid is pulling.
    /// <para>An OPTION and not a rule: a readout is what you want while you are placing something to a coordinate and
    /// clutter the rest of the time, and only the application knows which of those its user is doing. Shown only under
    /// <see cref="SnapToGrid"/>, because that is when the numbers are worth reading - free-hand they change with every
    /// pixel and say nothing.</para></summary>
    public static readonly AdamantiumProperty ShowsPointerReadoutProperty = AdamantiumProperty.Register(
        nameof(ShowsPointerReadout), typeof(Boolean), typeof(InfiniteCanvas),
        new PropertyMetadata(true, PropertyMetadataOptions.AffectsRender));

    /// <summary>How big the readout's digits are, in screen pixels.</summary>
    public static readonly AdamantiumProperty ReadoutSizeProperty = AdamantiumProperty.Register(nameof(ReadoutSize),
        typeof(Double), typeof(InfiniteCanvas), new PropertyMetadata(12.0, PropertyMetadataOptions.AffectsRender));

    public Boolean ShowsPointerReadout
    {
        get => GetValue<Boolean>(ShowsPointerReadoutProperty);
        set => SetValue(ShowsPointerReadoutProperty, value);
    }

    public Double ReadoutSize
    {
        get => GetValue<Double>(ReadoutSizeProperty);
        set => SetValue(ReadoutSizeProperty, value);
    }

    /// <summary>Whether anything is selected - the one question an inspector asks to know which of its two faces to
    /// show, and a property so it can be asked by a binding.</summary>
    public static readonly AdamantiumProperty HasSelectionProperty = AdamantiumProperty.Register(nameof(HasSelection),
        typeof(Boolean), typeof(InfiniteCanvas),
        new PropertyMetadata(false, PropertyMetadataOptions.BindsTwoWayByDefault));

    public IReadOnlyList<ICanvasItem> Selection => _selection;

    public Boolean HasSelection
    {
        get => GetValue<Boolean>(HasSelectionProperty);
        private set => SetValue(HasSelectionProperty, value);
    }

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

            // ROUND WHAT HAS A PLACE. A band takes the wires along with the nodes, and a wire's box is its whole bend
            // from one node to the other - a frame that included those stood far outside everything a person can see
            // they picked. Nothing is left out that way: a wire is always between two of the things in the box.
            if (Placed() is { Count: > 0 } placed) return Box(placed);

            return Box(new List<ICanvasItem>(_selection));
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

    /// <summary>Opens an EDIT: everything that happens until it is closed is one step of undo.
    /// <para>The canvas opens one around every gesture on its own - one drag of ten things is one step, not ten
    /// thousand - and this is here so an application can put its own changes in a step too: a clear, a paste, a line
    /// written in a panel. Opened inside an open one, it joins it rather than starting a second.</para>
    /// <para>Costs nothing at all with no <see cref="History"/>: there is nobody to tell, so nothing is snapshotted.
    /// </para></summary>
    public void BeginEdit(String reason)
    {
        if (History == null || Scene == null) return;

        if (_editDepth++ > 0) return;

        _editReason = reason;
        CanvasStep.Snapshot(Scene, out _editBefore, out _editWasAt);
    }

    /// <summary>Closes the edit and records it, if anything actually changed.</summary>
    public void EndEdit()
    {
        if (History == null || Scene == null || _editDepth == 0) return;
        if (--_editDepth > 0) return;

        CanvasStep.Snapshot(Scene, out var after, out var isAt);
        History.Push(new CanvasStep(_editReason, _editBefore, after, _editWasAt, isAt));

        _editBefore = null;
        _editWasAt = null;
    }

    /// <summary>Puts the last step back. What Ctrl+Z is wired to, and what a button calls.</summary>
    public bool Undo()
    {
        if (History?.Undo(Scene) != true) return false;

        // What was selected may have left the drawing - a frame drawn round something the scene no longer holds is a
        // frame round nothing.
        Reselect();
        Retie();
        return true;
    }

    public bool Redo()
    {
        if (History?.Redo(Scene) != true) return false;

        Reselect();
        Retie();
        return true;
    }

    // A step puts WIRES back and takes them away, and whether a socket is drawn as taken is a fact about the wires -
    // so after a step every socket is asked again. The history remembers what the scene holds and where, which is the
    // whole of a drawing; that a socket has something on it is not a separate thing to remember, and remembering it
    // would be a second answer to keep in step.
    private void Retie()
    {
        if (Scene is not { } scene) return;

        foreach (var item in scene.ItemsIn(Everything))
        {
            if (item is not ElementItem { Element: CanvasNode node }) continue;

            foreach (var pin in node.InputPins) ConnectionItem.Retie(scene, pin);
            foreach (var pin in node.OutputPins) ConnectionItem.Retie(scene, pin);
        }
    }

    // Everything selected that the scene still holds. Undo and redo both add and remove things, and the selection must
    // not point at what is gone.
    private void Reselect()
    {
        var alive = new HashSet<ICanvasItem>(Scene.ItemsIn(Everything));
        var kept = new List<ICanvasItem>();

        foreach (var item in _selection)
        {
            if (alive.Contains(item)) kept.Add(item);
        }

        if (kept.Count != _selection.Count) SelectMany(kept, false);

        Repaint();
    }

    /// <summary>Makes ONE thing out of what is selected, and selects it. Null when there is nothing to group.
    /// <para>The children LEAVE the scene: what is drawn stays one flat list the renderer can walk without asking
    /// anything about groups, and the group takes the place of the topmost of them in paint order, so a group does not
    /// jump to the front merely by being made.</para></summary>
    public GroupItem GroupSelection()
    {
        if (_selection.Count < 2 || Scene == null) return null;

        BeginEdit("Group");
        try
        {
            return Gather();
        }
        finally
        {
            EndEdit();
        }
    }

    private GroupItem Gather()
    {
        // In PAINT order, taken from the scene rather than from the selection: what was selected first is not what is
        // drawn first, and a group that reordered its own contents would change the drawing by being made.
        var ordered = new List<ICanvasItem>();
        foreach (var item in Scene.ItemsIn(Everything))
        {
            if (_selection.Contains(item)) ordered.Add(item);
        }

        if (ordered.Count < 2) return null;

        var group = new GroupItem(ordered);
        var topmost = ordered[^1];

        if (!Scene.Replace(topmost, new ICanvasItem[] { group })) return null;

        foreach (var child in ordered)
        {
            if (!ReferenceEquals(child, topmost)) Scene.Remove(child);
        }

        Select(group, false);
        return group;
    }

    /// <summary>Breaks the selected groups open, putting their children back exactly where the group was, and selects
    /// what came out. False when nothing selected was a group.</summary>
    public bool UngroupSelection()
    {
        if (_selection.Count == 0 || Scene == null) return false;

        BeginEdit("Ungroup");
        try
        {
            return Scatter();
        }
        finally
        {
            EndEdit();
        }
    }

    private bool Scatter()
    {
        var freed = new List<ICanvasItem>();
        var kept = new List<ICanvasItem>();

        foreach (var item in _selection.ToArray())
        {
            if (item is GroupItem group && Scene.Replace(group, group.Children)) freed.AddRange(group.Children);
            else kept.Add(item);
        }

        if (freed.Count == 0) return false;

        kept.AddRange(freed);
        SelectMany(kept, false);
        return true;
    }

    // Everything there is. The scene answers by VISIBLE region, and grouping is about what is selected wherever it
    // happens to be - including the part of it that is off screen.
    private static Rect Everything =>
        new(Double.MinValue / 4, Double.MinValue / 4, Double.MaxValue / 2, Double.MaxValue / 2);

    /// <summary>Raised before anything is taken out, so the application can ask first. See
    /// <see cref="CanvasDeleteRequestedEventArgs"/> for what answering means.</summary>
    public event EventHandler<CanvasDeleteRequestedEventArgs> DeleteRequested;

    /// <summary>WHETHER A DELETION IS WORTH A QUESTION. Off by default - with undo in place, being asked about every
    /// object is noise - and on, the canvas asks in its own chrome and deletes when it is answered.
    /// <para>A switch and not a rule, and the asking is the CANVAS's: an application says whether it wants the question,
    /// not how to ask it. Handling <see cref="DeleteRequested"/> still takes the job over entirely, for an application
    /// that wants its own dialog.</para></summary>
    public static readonly AdamantiumProperty ConfirmsDeleteProperty = AdamantiumProperty.Register(
        nameof(ConfirmsDelete), typeof(Boolean), typeof(InfiniteCanvas), new PropertyMetadata(false));

    public Boolean ConfirmsDelete
    {
        get => GetValue<Boolean>(ConfirmsDeleteProperty);
        set => SetValue(ConfirmsDeleteProperty, value);
    }

    /// <summary>What the question says, or null for the canvas's own words. <c>{0}</c> is how many things are
    /// going.</summary>
    public static readonly AdamantiumProperty DeleteQuestionProperty = AdamantiumProperty.Register(
        nameof(DeleteQuestion), typeof(String), typeof(InfiniteCanvas), new PropertyMetadata(null));

    public String DeleteQuestion
    {
        get => GetValue<String>(DeleteQuestionProperty);
        set => SetValue(DeleteQuestionProperty, value);
    }

    // The question while it stands. One at a time: a second Delete while it is up is the same question.
    private CanvasPane _asking;

    /// <summary>Asks to take everything selected out - what `Delete` and a delete button both go through. A handler of
    /// <see cref="DeleteRequested"/> may take the job over; otherwise the canvas asks when
    /// <see cref="ConfirmsDelete"/> says to, and deletes straight away when it does not.</summary>
    public bool RequestDeleteSelection()
    {
        if (_selection.Count == 0 || Scene == null) return false;

        var asked = new CanvasDeleteRequestedEventArgs(_selection.ToArray());
        DeleteRequested?.Invoke(this, asked);
        if (asked.Handled) return false;

        if (ConfirmsDelete)
        {
            Ask(asked.Items.Count);
            return false;
        }

        DeleteSelection();
        return true;
    }

    // THE QUESTION, IN THE CANVAS'S OWN CHROME. Built here rather than left to an application: a control that has a
    // feature must carry it, and "ask before deleting" is not something every application should write again - nor can
    // it be a dialog of the engine's, because what a dialog IS belongs to the application above this one.
    private void Ask(int many)
    {
        if (_asking != null) return;

        var words = new TextBlock
        {
            Text = string.Format(System.Globalization.CultureInfo.CurrentCulture,
                DeleteQuestion ?? (many > 1 ? "Remove {0} objects?" : "Remove it?"), many),
            VerticalAlignment = VerticalAlignment.Center
        };

        var yes = new Button { Content = "Delete", MinWidth = 76, MarginLeft = 12 };
        var no = new Button { Content = "Cancel", MinWidth = 76, MarginLeft = 6 };

        yes.Click += (_, _) => Answered(true);
        no.Click += (_, _) => Answered(false);

        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(words);
        row.Children.Add(yes);
        row.Children.Add(no);

        _asking = new CanvasPane
        {
            Kind = CanvasPaneKind.Bar,
            Placement = CanvasPanePlacement.TopCenter,
            IsDocked = true,
            Content = row
        };

        Chrome?.Add(_asking);
    }

    private void Answered(bool yes)
    {
        if (_asking == null) return;

        Chrome?.Remove(_asking);
        _asking = null;

        if (yes) DeleteSelection();
    }

    /// <summary>Takes everything selected out of the scene, asking nobody. What a handler of
    /// <see cref="DeleteRequested"/> calls once it has its answer.</summary>
    /// <summary>How far a pasted or duplicated copy is put from the original, in WORLD units. Far enough to see that
    /// there are two, near enough that the copy is where you were looking.</summary>
    public static readonly AdamantiumProperty CopyOffsetProperty = AdamantiumProperty.Register(nameof(CopyOffset),
        typeof(Double), typeof(InfiniteCanvas), new PropertyMetadata(24.0));

    public Double CopyOffset
    {
        get => GetValue<Double>(CopyOffsetProperty);
        set => SetValue(CopyOffsetProperty, value);
    }

    // The canvas's OWN clipboard and not the system's: what is copied here is a piece of a drawing or a piece of a
    // graph, and there is no agreed way to put one of those on a clipboard that another application could read. Copies
    // and not the items themselves, so editing the original afterwards does not edit what will be pasted.
    private readonly List<ICanvasItem> _clipboard = new();
    private readonly List<(int From, int FromPin, int To, int ToPin)> _clipboardWires = new();

    /// <summary>Whether anything can be pasted.</summary>
    public Boolean CanPaste => _clipboard.Count > 0;

    /// <summary>Takes a copy of what is selected, with the wires that run BETWEEN the selected nodes.
    /// <para>What cannot be copied is left behind rather than refusing the whole gesture - see
    /// <see cref="ICanvasItem.Copy"/>.</para></summary>
    public void Copy()
    {
        _clipboard.Clear();
        _clipboardWires.Clear();

        if (_selection.Count == 0) return;

        var taken = new List<ICanvasItem>();
        foreach (var item in _selection)
        {
            if (item.Copy() is { } made)
            {
                _clipboard.Add(made);
                taken.Add(item);
            }
        }

        GatherWires(taken);
    }

    /// <summary>Puts what was copied on the plane, a little to one side, and selects it - so the thing you are looking
    /// at after a paste is the thing you just made.</summary>
    public void Paste() => Put(_clipboard, _clipboardWires);

    /// <summary>Copies what is selected and puts it down at once, leaving the clipboard alone: duplicating is its own
    /// gesture and must not spend what was copied an hour ago.</summary>
    public void Duplicate()
    {
        if (_selection.Count == 0) return;

        var copies = new List<ICanvasItem>();
        var taken = new List<ICanvasItem>();

        foreach (var item in _selection)
        {
            if (item.Copy() is not { } made) continue;

            copies.Add(made);
            taken.Add(item);
        }

        var wires = new List<(int, int, int, int)>();
        GatherWires(taken, wires);

        Put(copies, wires);
    }

    // The wires that run BETWEEN the things being copied, said by POSITION in that list: a wire whose far end is not
    // being copied is not part of what is being copied, and one recorded by object would point at the originals.
    private void GatherWires(List<ICanvasItem> taken, List<(int, int, int, int)> into = null)
    {
        into ??= _clipboardWires;
        if (Scene == null) return;

        foreach (var item in Scene.ItemsIn(Everything))
        {
            if (item is not ConnectionItem wire) continue;

            var from = taken.IndexOf(wire.FromItem);
            var to = taken.IndexOf(wire.ToItem);
            if (from < 0 || to < 0) continue;

            var fromPin = Index(wire.FromItem, wire.FromPin);
            var toPin = Index(wire.ToItem, wire.ToPin);
            if (fromPin < 0 || toPin < 0) continue;

            into.Add((from, fromPin, to, toPin));
        }
    }

    // Puts a set down, offset, wired the way it was, and selected.
    private void Put(List<ICanvasItem> copies, List<(int From, int FromPin, int To, int ToPin)> wires)
    {
        if (copies.Count == 0 || Scene == null) return;

        BeginEdit("Paste");
        try
        {
            var step = new Vector2(CopyOffset, CopyOffset);
            var made = new List<ICanvasItem>(copies.Count);

            foreach (var item in copies)
            {
                // A copy OF THE COPY: the clipboard holds one set and may be pasted many times, and pasting the same
                // objects twice would put one thing on the plane in two places.
                if (item.Copy() is not { } fresh) continue;

                fresh.Move(step);
                Scene.Add(fresh);
                made.Add(fresh);
            }

            foreach (var (from, fromPin, to, toPin) in wires)
            {
                if (from >= made.Count || to >= made.Count) continue;
                if (made[from] is not ElementItem source || made[to] is not ElementItem target) continue;
                if (source.Element is not CanvasNode left || target.Element is not CanvasNode right) continue;
                if (fromPin >= left.OutputPins.Count || toPin >= right.InputPins.Count) continue;

                var wire = new ConnectionItem(source, left.OutputPins[fromPin], target, right.InputPins[toPin]);
                Scene.Add(wire);

                left.OutputPins[fromPin].IsConnected = true;
                right.InputPins[toPin].IsConnected = true;
            }

            SelectMany(made, false);
            Scene.Touch();
        }
        finally
        {
            EndEdit();
        }
    }

    private static int Index(ElementItem item, CanvasNodePin pin)
    {
        if (item?.Element is not CanvasNode node || pin == null) return -1;

        var list = pin.IsInput ? node.InputPins : node.OutputPins;
        for (var i = 0; i < list.Count; i++)
        {
            if (ReferenceEquals(list[i], pin)) return i;
        }

        return -1;
    }

    /// <summary>How much room a new frame leaves round what it is drawn about, in WORLD units - and the strip at the
    /// top is that much again, so the title has somewhere to sit that is not over a node.</summary>
    public static readonly AdamantiumProperty FrameMarginProperty = AdamantiumProperty.Register(nameof(FrameMargin),
        typeof(Double), typeof(InfiniteCanvas), new PropertyMetadata(20.0));

    public Double FrameMargin
    {
        get => GetValue<Double>(FrameMarginProperty);
        set => SetValue(FrameMarginProperty, value);
    }

    /// <summary>Draws a titled frame round what is selected and selects IT - the comment every graph editor has, made
    /// the way every graph editor makes one: pick the nodes it is about, and say so.
    /// <para>Null when there is nothing selected. The frame holds nothing: what is inside it goes on being moved,
    /// wired and deleted without ever consulting it - see <see cref="CanvasFrameItem"/>.</para></summary>
    public CanvasFrameItem FrameSelection(string title = null)
    {
        if (Scene is not { } scene || SelectionBounds is not { } bounds) return null;

        var room = Math.Max(0, FrameMargin);
        var strip = room * 1.4;

        var frame = new CanvasFrameItem(
            new Rect(bounds.X - room, bounds.Y - room - strip, bounds.Width + room * 2, bounds.Height + room * 2 + strip),
            title ?? "Comment",
            SelectionBrush) { TitleHeight = strip };

        BeginEdit("Frame");
        try
        {
            // UNDER what it is about, and the scene draws in order - so it goes to the BACK. The band already keeps it
            // behind the nodes; this keeps it behind the other frames it may be nested in.
            scene.Add(frame);
            scene.SendToBack(frame);

            Select(frame, false);
            scene.Touch();
        }
        finally
        {
            EndEdit();
        }

        return frame;
    }

    /// <summary>The least room left between two things that lining up would otherwise have put on top of each other,
    /// in WORLD units.</summary>
    public static readonly AdamantiumProperty AlignGapProperty = AdamantiumProperty.Register(nameof(AlignGap),
        typeof(Double), typeof(InfiniteCanvas), new PropertyMetadata(16.0));

    public Double AlignGap
    {
        get => GetValue<Double>(AlignGapProperty);
        set => SetValue(AlignGapProperty, value);
    }

    /// <summary>Lines the selection up on one edge - or through one middle - of the box round all of it, and OPENS THE
    /// LINE OUT so that nothing ends up on top of anything else.
    /// <para>Against the WHOLE selection's box and not against one chosen member: "align left" has one obvious meaning,
    /// which is that nothing ends up further left than it was, and picking a member to align to is a second question
    /// nobody asked. Fewer than two things is nothing to line up.</para>
    /// <para>The second half is what makes it usable on a GRAPH. Lining a row of nodes up on their left edges puts
    /// every one of them at the same place, and a pile of nodes says less than the row did - so the other axis keeps
    /// the order it had and is opened just enough that nothing covers anything. What was already clear of its
    /// neighbour is not moved at all, so a layout that was tidy stays where it was put.</para></summary>
    public bool Align(CanvasAlignment edge)
    {
        // ONLY WHAT HAS A PLACE. A band takes the wires along with the nodes, and a wire is wherever its two sockets
        // are - it cannot be lined up, and the room it covers is not room anything has to be moved out of.
        var placed = Placed();

        if (placed.Count < 2 || Box(placed) is not { } frame) return false;

        // Which way the line RUNS: lining up on a left or right edge makes a column, so the things in it are told apart
        // down the page; lining up on a top or bottom edge makes a row, and they are told apart across it.
        var column = edge is CanvasAlignment.Left or CanvasAlignment.Right or CanvasAlignment.HorizontalCenter;

        // Ordered BEFORE anything moves. Afterwards every item shares one coordinate, and an order taken then would be
        // whatever the sort happened to do with the tie - a row lined up into a column would come out shuffled.
        var order = placed;
        order.Sort((a, b) =>
        {
            var first = column
                ? a.Bounds.Y.CompareTo(b.Bounds.Y)
                : a.Bounds.X.CompareTo(b.Bounds.X);

            return first != 0
                ? first
                : column ? a.Bounds.X.CompareTo(b.Bounds.X) : a.Bounds.Y.CompareTo(b.Bounds.Y);
        });

        BeginEdit("Align");
        try
        {
            foreach (var item in order)
            {
                var box = item.Bounds;
                var to = edge switch
                {
                    CanvasAlignment.Left => new Vector2(frame.X - box.X, 0),
                    CanvasAlignment.Right => new Vector2(frame.X + frame.Width - (box.X + box.Width), 0),
                    CanvasAlignment.HorizontalCenter =>
                        new Vector2(frame.X + frame.Width / 2 - (box.X + box.Width / 2), 0),
                    CanvasAlignment.Top => new Vector2(0, frame.Y - box.Y),
                    CanvasAlignment.Bottom => new Vector2(0, frame.Y + frame.Height - (box.Y + box.Height)),
                    _ => new Vector2(0, frame.Y + frame.Height / 2 - (box.Y + box.Height / 2))
                };

                if (to.X != 0 || to.Y != 0) item.Move(to);
            }

            Unstack(order, column);

            Scene?.Touch();
            Selected();
        }
        finally
        {
            EndEdit();
        }

        return true;
    }

    // What in the selection has a place of its own - which is everything that arranging the plane is ABOUT. See
    // ICanvasItem.IsPlaced for what the other kind is.
    private List<ICanvasItem> Placed()
    {
        var placed = new List<ICanvasItem>(_selection.Count);

        foreach (var item in _selection)
        {
            if (item.IsPlaced) placed.Add(item);
        }

        return placed;
    }

    // The box round a set of items, in world units.
    private static Rect? Box(List<ICanvasItem> items)
    {
        if (items.Count == 0) return null;

        var box = items[0].Bounds;

        for (var i = 1; i < items.Count; i++)
        {
            var next = items[i].Bounds;
            var left = Math.Min(box.X, next.X);
            var top = Math.Min(box.Y, next.Y);

            box = new Rect(left, top,
                Math.Max(box.X + box.Width, next.X + next.Width) - left,
                Math.Max(box.Y + box.Height, next.Y + next.Height) - top);
        }

        return box;
    }

    // Opens the line out ALONG the axis it was not lined up on, in the order given, so that no two of them overlap.
    // Only what is too close moves, and only far enough: a line that was already readable is left exactly as it was.
    private void Unstack(List<ICanvasItem> order, bool column)
    {
        var gap = Math.Max(0, AlignGap);
        var free = Double.NegativeInfinity;

        foreach (var item in order)
        {
            var box = item.Bounds;
            var from = column ? box.Y : box.X;
            var size = column ? box.Height : box.Width;

            if (from < free)
            {
                item.Move(column ? new Vector2(0, free - from) : new Vector2(free - from, 0));
                from = free;
            }

            free = from + size + gap;
        }
    }

    /// <summary>Puts EQUAL GAPS between the selection, along one axis.
    /// <para>The two on the outside stay where they are and everything between them is spread out: those two are what
    /// the person has already placed, and moving them would be the tool deciding where the row goes.</para>
    /// <para>Equal GAPS and not equal centres: things of different sizes look evenly spaced when the space between them
    /// is equal, which is what the eye actually reads.</para></summary>
    public bool Spread(CanvasSpread way)
    {
        var order = Placed();

        if (order.Count < 3) return false;
        order.Sort((a, b) => way == CanvasSpread.Horizontal
            ? a.Bounds.X.CompareTo(b.Bounds.X)
            : a.Bounds.Y.CompareTo(b.Bounds.Y));

        // The room the middle ones have to share: from the end of the first to the start of the last, less what they
        // themselves take up.
        var first = order[0].Bounds;
        var last = order[^1].Bounds;

        var from = way == CanvasSpread.Horizontal ? first.X + first.Width : first.Y + first.Height;
        var to = way == CanvasSpread.Horizontal ? last.X : last.Y;

        double taken = 0;
        for (var i = 1; i < order.Count - 1; i++)
        {
            var box = order[i].Bounds;
            taken += way == CanvasSpread.Horizontal ? box.Width : box.Height;
        }

        var gap = (to - from - taken) / (order.Count - 1);

        BeginEdit("Spread");
        try
        {
            var at = from + gap;
            for (var i = 1; i < order.Count - 1; i++)
            {
                var box = order[i].Bounds;
                var step = way == CanvasSpread.Horizontal
                    ? new Vector2(at - box.X, 0)
                    : new Vector2(0, at - box.Y);

                if (step.X != 0 || step.Y != 0) order[i].Move(step);

                at += (way == CanvasSpread.Horizontal ? box.Width : box.Height) + gap;
            }

            Scene?.Touch();
            Selected();
        }
        finally
        {
            EndEdit();
        }

        return true;
    }

    public void DeleteSelection()
    {
        if (_selection.Count == 0 || Scene == null) return;

        BeginEdit("Delete");
        try
        {
            Remove();
        }
        finally
        {
            EndEdit();
        }
    }

    private void Remove()
    {
        foreach (var item in _selection)
        {
            // A NODE TAKES ITS WIRES WITH IT. A wire is held by two sockets, and a socket whose node is gone is not
            // somewhere a wire can end - left behind, it would be drawn from a node that is not there to a node that
            // is, and no gesture could ever reach it to take it away.
            if (item is ElementItem { Element: CanvasNode }) Unwire(item as ElementItem);

            // What the scene refuses is inside a GROUP - a group's children leave the scene when it is made. Without
            // this, deleting something reached by entering a group would quietly do nothing at all.
            if (!Scene.Remove(item)) RemoveFromGroups(item);
        }

        _selection.Clear();
        Selected();
        Scene.Touch();
    }

    // Every wire fastened to a node, taken out with it. Gathered into a LIST first: removing from the scene changes
    // what a walk over the scene is walking.
    private void Unwire(ElementItem node)
    {
        if (node == null || Scene == null) return;

        var wires = new List<ConnectionItem>();
        foreach (var item in Scene.ItemsIn(Everything))
        {
            if (item is ConnectionItem wire && wire.Holds(node)) wires.Add(wire);
        }

        foreach (var wire in wires) ConnectionItem.Cut(Scene, wire);
    }

    private void RemoveFromGroups(ICanvasItem item)
    {
        // Taken as a LIST first: the walk is over the scene's own store, and emptying a group removes it from that
        // store - changing what is being walked while it is walked.
        var tops = new List<ICanvasItem>(Scene.ItemsIn(Everything));

        foreach (var top in tops)
        {
            if (top is not GroupItem group || !group.Remove(item)) continue;

            // A group with nothing left is not a group; leaving it would put an invisible thing in the paint order that
            // can still be selected by its own empty box.
            if (group.Children.Count == 0) Scene.Remove(group);
            return;
        }
    }

    /// <summary>Which grip of the manipulation frame a SCREEN point is on, or <see cref="CanvasHandle.None"/>.
    /// <para>Asked in screen pixels and not in the world on purpose: a grip is something the hand aims at, so how close
    /// counts as on it is a distance on the glass at any zoom.</para></summary>
    /// <summary>The turn a single selected item wears, or nothing. A frame round SEVERAL things is never turned: their
    /// turns are not one turn, and a box that pretended otherwise would lie about every one of them.</summary>
    private CanvasTransform? SelectionTurn =>
        _selection.Count == 1 && _selection[0] is ICanvasTransformed { Transform.IsSomething: true } turned
            ? turned.Transform
            : null;

    // The middle of what is selected, in WORLD units - what a turn turns about.
    private Vector2 SelectionMiddle
    {
        get
        {
            var box = SelectionBounds ?? default;
            return new Vector2(box.X + box.Width / 2, box.Y + box.Height / 2);
        }
    }

    // A pointer position in the SHAPE's OWN frame. A grip drag is arithmetic on a box that stands square, so a turned
    // shape is resized by un-turning the pointer rather than by teaching the gesture about angles - which is also what
    // makes a drag of the top edge of a turned box move its top edge, not the world's.
    private Vector2 InShapeSpace(Vector2 world) =>
        SelectionTurn is { } turn ? turn.Undo(world, SelectionMiddle) : world;

    /// <summary>Which of a single selected item's POINTS a screen position is on, or -1. Only one item at a time offers
    /// them: the points of two things at once are not one shape, and a frame is what a several-thing selection is.
    /// </summary>
    public int PointHandleAt(Vector2 screen)
    {
        if (_selection.Count != 1 || _selection[0] is not ICanvasPoints shaped) return -1;

        var reach = Math.Max(4, HandleSize) / 2 + 2;
        var points = shaped.Points;

        for (var i = 0; i < points.Count; i++)
        {
            var at = WorldToScreen(points[i]);
            if (Math.Abs(screen.X - at.X) <= reach && Math.Abs(screen.Y - at.Y) <= reach) return i;
        }

        return -1;
    }

    /// <summary>Which grips what is selected offers - the AND of what every selected item offers, because a frame round
    /// several things can only do what all of them can.</summary>
    public CanvasHandles OfferedHandles
    {
        get
        {
            if (_selection.Count == 0) return CanvasHandles.None;

            // What offers NOTHING does not get a vote. A wire has no handles - it is the two sockets it joins and
            // there is nothing on it to drag - and counted in, one of them took the frame and the body drag away from
            // every node it was selected with: a band round two wired nodes left the bar standing over no frame, and
            // the nodes could not be moved together.
            var offered = CanvasHandles.All;
            var said = false;

            foreach (var item in _selection)
            {
                if (item.Handles == CanvasHandles.None) continue;

                offered &= item.Handles;
                said = true;
            }

            return said ? offered : CanvasHandles.None;
        }
    }

    public CanvasHandle HandleAt(Vector2 screen)
    {
        if (SelectionBounds is not { } bounds) return CanvasHandle.None;

        var offered = OfferedHandles;
        if (offered == CanvasHandles.None) return CanvasHandle.None;

        // Asked in the SHAPE's own frame: the grips are drawn on the turned box, so the pointer has to be brought back
        // to where the box stands square before it is compared with anything.
        if (SelectionTurn != null) screen = WorldToScreen(InShapeSpace(ScreenToWorld(screen)));

        // THE SAME frame the grips are drawn on, held off the selection by the same half grip - aimed at where they
        // are rather than at where the box is, or every one of them would be half a grip out.
        var frame = FrameOf(bounds);
        var topLeft = new Vector2(frame.X, frame.Y);
        var bottomRight = new Vector2(frame.X + frame.Width, frame.Y + frame.Height);
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
        if (offered.HasFlag(CanvasHandles.Corners))
        {
            if (left && top) return CanvasHandle.TopLeft;
            if (right && top) return CanvasHandle.TopRight;
            if (left && bottom) return CanvasHandle.BottomLeft;
            if (right && bottom) return CanvasHandle.BottomRight;
        }

        if (!offered.HasFlag(CanvasHandles.Sides)) return CanvasHandle.None;

        var midX = Math.Abs(screen.X - (topLeft.X + bottomRight.X) / 2) <= reach;
        var midY = Math.Abs(screen.Y - (topLeft.Y + bottomRight.Y) / 2) <= reach;

        if (left && midY) return CanvasHandle.Left;
        if (right && midY) return CanvasHandle.Right;
        if (top && midX) return CanvasHandle.Top;
        if (bottom && midX) return CanvasHandle.Bottom;

        return CanvasHandle.None;
    }

    // Set from OUTSIDE - by an application restoring what was selected last time, or by a list beside the canvas. A
    // two-way property that only ever published would be lying about being two-way, so what arrives is adopted; what
    // the canvas published itself is recognised and ignored, or the two would push each other round in circles.
    private static void OnSelectionSet(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not InfiniteCanvas canvas || canvas._publishing) return;

        var wanted = e.NewValue as IReadOnlyList<ICanvasItem>;
        canvas._selection.Clear();

        if (wanted != null)
        {
            foreach (var item in wanted)
            {
                if (item != null && !canvas._selection.Contains(item)) canvas._selection.Add(item);
            }
        }

        canvas.HasSelection = canvas._selection.Count > 0;
        canvas._chromeLayer?.SyncSelection();
        canvas.SelectionChanged?.Invoke(canvas, EventArgs.Empty);
        canvas.Repaint();
    }

    // ------------------------------------------------------------------ what it can do, as commands
    //
    // ON THE CANVAS because the canvas is what does them. A button in an application binds straight to one of these and
    // needs nothing else: no click handler, no behaviour, no view-model method that forwards. And each says when it can
    // be pressed, so a bound button switches itself off with nothing left to undo - which is the difference between a
    // control and a kit of parts.
    private CanvasCommand _undo;
    private CanvasCommand _redo;
    private CanvasCommand _zoomIn;
    private CanvasCommand _zoomOut;
    private CanvasCommand _home;
    private CanvasCommand _fitAll;
    private CanvasCommand _fitSelection;
    private CanvasCommand _zoomTo;
    private CanvasCommand _delete;
    private CanvasCommand _group;
    private CanvasCommand _ungroup;

    /// <summary>Takes the last step back. Off when there is nothing behind it.</summary>
    public CanvasCommand UndoCommand => _undo ??= new CanvasCommand(_ => Undo(), _ => History is { CanUndo: true });

    /// <summary>...and forward again. Off when nothing has been taken back.</summary>
    public CanvasCommand RedoCommand => _redo ??= new CanvasCommand(_ => Redo(), _ => History is { CanRedo: true });

    /// <summary>Closer in. The parameter is the factor, and one step of <see cref="ZoomStep"/> when nothing is
    /// given.</summary>
    public CanvasCommand ZoomInCommand => _zoomIn ??= new CanvasCommand(by => ZoomBy(Factor(by, ZoomStep)));

    /// <summary>...and further out.</summary>
    public CanvasCommand ZoomOutCommand => _zoomOut ??= new CanvasCommand(by => ZoomBy(1 / Factor(by, ZoomStep)));

    /// <summary>Back to the origin at one to one - the one place a plane with no edges can always be found.</summary>
    public CanvasCommand HomeCommand => _home ??= new CanvasCommand(_ => ResetCamera());

    /// <summary>Puts everything on the plane in view.</summary>
    public CanvasCommand FitAllCommand => _fitAll ??= new CanvasCommand(_ => FitAll());

    /// <summary>...and this puts what is selected in view. Off with nothing selected.</summary>
    public CanvasCommand FitSelectionCommand =>
        _fitSelection ??= new CanvasCommand(_ => FitSelection(), _ => _selection.Count > 0);

    /// <summary>Puts ONE thing in view - the parameter, which is the item to show. What a list of everything on the
    /// plane answers a double click with.</summary>
    public CanvasCommand ZoomToCommand => _zoomTo ??= new CanvasCommand(
        item => ZoomTo(item as ICanvasItem),
        item => item is ICanvasItem);

    /// <summary>Takes the selection out - asking first when the canvas was told to ask. Off with nothing
    /// selected.</summary>
    public CanvasCommand DeleteCommand =>
        _delete ??= new CanvasCommand(_ => RequestDeleteSelection(), _ => _selection.Count > 0);

    /// <summary>Makes one thing of what is selected, and breaks one open again.</summary>
    public CanvasCommand GroupCommand =>
        _group ??= new CanvasCommand(_ => GroupSelection(), _ => _selection.Count > 1);

    public CanvasCommand UngroupCommand =>
        _ungroup ??= new CanvasCommand(_ => UngroupSelection(), _ => _selection.Count > 0);

    private static Double Factor(object given, Double fallback) =>
        given switch
        {
            Double number when number > 0 => number,
            String text when Double.TryParse(text, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed > 0 => parsed,
            _ => fallback
        };

    // What a command's answer depends on has just moved, so every bound button asks again. Cheap and exact: these are
    // the only two things any of them look at.
    private void Refresh()
    {
        _undo?.RaiseCanExecuteChanged();
        _redo?.RaiseCanExecuteChanged();
        _fitSelection?.RaiseCanExecuteChanged();
        _delete?.RaiseCanExecuteChanged();
        _group?.RaiseCanExecuteChanged();
        _ungroup?.RaiseCanExecuteChanged();
    }

    private void Selected()
    {
        // A fresh snapshot, not the live list: a binding is told a property CHANGED, and the same list object handed
        // over twice is not a change however different its contents.
        _publishing = true;
        SetCurrentValue(SelectionProperty, _selection.ToArray());
        _publishing = false;

        HasSelection = _selection.Count > 0;

        // A pane that FOLLOWS the selection is shown by there being one, so the moment the selection changes is the
        // moment to say so - and not the arrange pass, where writing a layout input would invalidate the pass running.
        _chromeLayer?.SyncSelection();
        _chromeLayer?.InvalidateArrange();

        SelectionChanged?.Invoke(this, EventArgs.Empty);
        Refresh();
        Repaint();
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
    public Rect VisibleWorld => WorldAcross(RenderSize);

    /// <summary>The piece of the world a viewport THAT SIZE would show. Asked for by whoever knows the size before the
    /// canvas does - the arrange pass, which is what sets <see cref="IUIComponent.RenderSize"/> in the first place and
    /// would otherwise be answering from the size it had last time.</summary>
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
    /// snap pulls - is a screen distance: at 20x nobody can hit a thin line given in world units, and at 0.1x everything
    /// is a hit.</summary>
    public Double ScreenToWorldLength(Double screenPixels) => screenPixels / Scale;

    /// <summary>Moves the camera by a distance measured on screen.</summary>
    public void PanBy(Vector2 screenDelta) => SetCurrentValue(OffsetProperty, Offset + screenDelta);

    /// <summary>The part of the viewport a person can actually see the plane through: everything a DOCKED pane has
    /// taken is gone from it. In screen pixels, from the canvas's top-left.
    /// <para>Public because it is the honest answer to "where is the middle" - a minimap, a ruler and anything else
    /// drawn against the viewport wants this and not <see cref="Control.RenderSize"/>.</para></summary>
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

    /// <summary>Puts the camera where the given piece of world fills the viewport, with room to spare around it.
    /// <para>Fills the USABLE viewport: a docked panel is a wall, and fitting behind one puts half the drawing
    /// somewhere nobody can look at it.</para></summary>
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

    /// <summary>Puts the camera on ONE item: centred, and zoomed so it fills the usable viewport.
    /// <para>Not simply <see cref="ScaleToFit"/> of its bounds, because a line has no height and a point has neither,
    /// and fitting to a box with a zero side does nothing at all. A flat item is looked at through the square that
    /// holds it.</para></summary>
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

    /// <summary>Puts a world point in the middle of the USABLE viewport - the middle of what is not behind a docked
    /// panel. With nothing docked that is the middle of the control, which is what it was before there were panes.
    /// </summary>
    public void CenterOn(Vector2 world)
    {
        var room = UsableBounds;
        var middle = new Vector2(room.X + room.Width / 2, room.Y + room.Height / 2);

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

    /// <summary>Points the camera at a box in the WORLD and zooms so the whole of it is in view.
    /// <para>The plane has no edges, so "where is my work" is a question it has to be able to answer. On a big graph it
    /// is asked constantly, which is why every editor has it on a key.</para>
    /// <para>A box with no size - one thing, a single point - is CENTRED at the zoom already in hand rather than zoomed
    /// to infinity.</para></summary>
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

    /// <summary>Brings what is SELECTED into view. Nothing selected is not a failure - there is simply nothing to look
    /// at, and moving the camera would be an answer to a question nobody asked.</summary>
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

    /// <summary>Back to where everything starts: the world's origin in the middle of the viewport, at one to one.
    /// <para>The plane has no edges and therefore no corner to scroll back to, so getting lost on it has to have a way
    /// out. <c>Home</c> does this.</para></summary>
    public void ResetCamera()
    {
        StopZoom();
        SetCurrentValue(ScaleProperty, Math.Clamp(1.0, MinScale, MaxScale));
        CenterOn(Vector2.Zero);
    }

    /// <summary>Zooms about a point ON SCREEN, keeping the world under it still - what the wheel does, offered for a
    /// button or a test.</summary>
    /// <summary>Zooms about the middle of what can be seen - what a plus or a minus button means, as against the wheel,
    /// which zooms about the pointer. Writing <see cref="Scale"/> instead would zoom about the world's ORIGIN, which is
    /// wherever it happens to be and usually not on screen at all.</summary>
    public void ZoomBy(Double factor)
    {
        var room = UsableBounds;

        ZoomAt(new Vector2(room.X + room.Width / 2, room.Y + room.Height / 2), factor);
    }

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

        DrawBand(session, CanvasBand.Under);

        // What the TOOL is making but has not put in the scene yet - a stroke still under the pen, a shape being dragged
        // out, the selection band. It goes in when the gesture ends, so a half-made thing cannot be hit-tested, saved or
        // undone halfway through.
        Tool?.Render(session, this);

        // THE OVERLAY: what is drawn on the glass rather than on the plane. It comes last, it is stated in SCREEN
        // pixels, and it never scales - a mark that grew with the zoom would be part of the drawing, which is exactly
        // what it is not.
        DrawOverlay(session);
    }

    // What the canvas draws is drawn in TWO places - here, and in the layer that stands in front of the controls - so
    // the two are marked together. Anything that changes the picture goes through this rather than InvalidateRender,
    // or the band in front stays as it was and the drawing comes apart into two ages of itself.
    internal void Repaint()
    {
        InvalidateRender(false);
        _front?.InvalidateRender(false);
    }

    /// <summary>What is on the plane inside a world rectangle AND belongs to the mode the canvas is in.
    /// <para>THE one place the mode is applied. Every walk over the scene - drawing it, picking in it, erasing, hosting
    /// its controls - goes through here rather than asking the scene directly, or a mode would mean something slightly
    /// different to each of them and the one that forgot would hand back a node to a pen.</para>
    /// <para>A scene may hold both a drawing and a graph; what is not of the current mode is simply not offered, and
    /// switching back offers it again untouched.</para></summary>
    /// <summary>Everything of the current mode, wherever it is - what a whole graph is, as against what can be seen of
    /// one. Saving a document is the case: a node left three screens away is still in it.</summary>
    public IEnumerable<ICanvasItem> ItemsHere() => ItemsHere(Everything);

    public IEnumerable<ICanvasItem> ItemsHere(Rect world)
    {
        if (Scene is not { } scene) yield break;

        var mode = Mode;
        foreach (var item in scene.ItemsIn(world))
        {
            if (item.Mode == mode) yield return item;
        }
    }

    /// <summary>Draws the items of one BAND - what is behind the hosted controls, or what is in front of them.
    /// <para>Only what can be SEEN. The cost of a frame follows the viewport, not the drawing: a scene of a hundred
    /// thousand strokes on a plane the size of a town draws the dozen under the camera.</para>
    /// <para>Public because the front band is drawn by a layer of the template and not by the canvas: a layer is one
    /// place in paint order, so the only way for an item to be in front of a control is for something in front of that
    /// control to draw it. See <see cref="CanvasFrontLayer"/>.</para></summary>
    public void DrawBand(IDrawingSession session, CanvasBand band)
    {
        if (session == null) return;

        foreach (var item in ItemsHere(VisibleWorld))
        {
            if (item.Band == band) item.Render(session, this);
        }
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
        _ground.Background = ColorOf(Background, new Color(0, 0, 0, 0));
        _ground.Color = ColorOf(GridBrush, new Color(0, 0, 0, 0));
        _ground.AxisColor = ColorOf(AxisBrush, new Color(0, 0, 0, 0));

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

        DrawReadout(session, at);
    }

    // The plate that says where the pointer is, beside the pointer. Beside and not under it: a number drawn where the
    // mark is would be covering the very crossing it is about.
    private void DrawReadout(IDrawingSession session, Vector2 world)
    {
        if (!ShowsPointerReadout || !_pointerInside || ReadoutSize <= 0) return;
        if (HandleBrush is not { } plate || SelectionBrush is not SolidColorBrush ink) return;

        // INVARIANT, so the decimal separator is a point whatever the machine's language is. Under a locale that uses a
        // comma the two numbers were "82,4, 59,8" - four commas doing two different jobs, and the pair read as one
        // number with too many digits.
        var text = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.##}; {1:0.##}",
            world.X, world.Y);
        var size = ShapeReadout(text);
        if (size.Width <= 0 || size.Height <= 0) return;

        const double padding = 5;
        const double away = 14;

        var room = RenderSize;
        var box = new Rect(_pointer.X + away, _pointer.Y + away,
            size.Width + padding * 2, size.Height + padding * 2);

        // Kept inside the canvas, and flipped to the other side of the pointer rather than merely clamped: a plate that
        // slid along the edge would sit under the pointer at the corner, which is the one place it must not be.
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

    // Shaped only when the digits actually change - the pointer moves far more often than the numbers it is over do,
    // and re-shaping the same string every frame is the whole cost of a readout done badly.
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
    // never scaled - a grip that grew with the zoom would be part of the drawing, which is exactly what it is not, and a
    // frame round a group is the thing that makes a group one thing to drag.
    private void DrawManipulation(IDrawingSession session)
    {
        // Not while the controls are being USED: the frame and its grips are how a thing is edited, and nothing on the
        // plane can be edited in that mode - a frame drawn there is a handle that does not work.
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

        // Only the grips that would ANSWER. A grip drawn where nothing can be dragged is worse than no grip: it is an
        // invitation to a gesture that does nothing.
        var offered = OfferedHandles;
        var corners = offered.HasFlag(CanvasHandles.Corners);
        var sides = offered.HasFlag(CanvasHandles.Sides);

        // ...and a frame that offers NO grip is the same mistake one size larger. A frame is a thing to grab; round a
        // line it is not even where the line is - the box of a diagonal is a huge rectangle, most of which is nowhere
        // near the shape, and it reads as though the line were everywhere inside it. What says a line or a curve is
        // selected is its own ends, which are drawn below.
        if (corners || sides) session.DrawRectangle(null, frame, pen);

        if (HandleSize <= 0) return;

        // BEFORE the frame's own grips, and outside the test below: an item reshaped by its points usually offers no
        // frame grips at all, and drawing its points only when the frame has some would hide them on exactly the items
        // that have nothing else.
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

    // The POINTS of a single selected item that has them. In screen pixels like every other grip - a point handle is
    // something the hand aims at, so it is the same size however far out the camera is.
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

    /// <summary>The manipulation frame of a selection, in screen pixels: its box held OFF by half a grip so the grips
    /// stand against what is selected rather than on it.
    /// <para>Drawn on the box itself every grip is half inside the thing it belongs to, and over anything with a filled
    /// edge of its own - a node's title strip, a solid shape - that half simply disappears into it. Held off, the same
    /// grips read as a frame round the thing. Nothing about WHAT is selected moves: this is where the frame is drawn
    /// and where its grips are aimed at, both from here so the two cannot drift apart.</para></summary>
    public Rect FrameOf(Rect bounds)
    {
        var clear = Math.Max(0, HandleSize) / 2;
        var frame = ToScreen(bounds);

        return new Rect(frame.X - clear, frame.Y - clear, frame.Width + clear * 2, frame.Height + clear * 2);
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
    // so anything that is not a plain color falls back to nothing rather than being approximated into something the
    // theme did not ask for.
    private static Color ColorOf(Brush brush, Color fallback) =>
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
    // WHAT A PRESS COSTS, when somebody is asking. Written to the file named by ADAM_CANVAS_PRESSLOG and not shown on
    // screen: a plate over the drawing is in the way of the very thing being measured, and a number that scrolls past
    // settles nothing anyway. Off unless the variable is set, so an ordinary run pays a null check.
    private static readonly string PressLog = Environment.GetEnvironmentVariable("ADAM_CANVAS_PRESSLOG");

    private readonly System.Diagnostics.Stopwatch _sinceLastPress = new();
    private long _measuresAtLastPress;
    private long _templatesAtLastPress;
    private long _madeAtLastPress;
    private long _rebuildsAtLastPress;
    private long _refreshesAtLastPress;

    // Counting is what names the culprit, and it costs an interlocked increment per invalidation - nothing next to what
    // it is being used to find, and too much to leave on for a run nobody is measuring.
    static InfiniteCanvas()
    {
        if (PressLog == null) return;

        Core.Diagnostics.LayoutTrace.Counting = true;

        // ...and WHO CALLED, when asked for. A count names the type being re-measured; only the stack names the thing
        // doing it, and a type appears in a dozen places at once - the same Button is in the tool rail and on the plane.
        //
        // It walks the stack PER EVENT, and this application raises thousands a second: switched on, it turned a
        // running stand into one that took fifty-five seconds between two clicks. It answers the question and it cannot
        // be left on, so it says both here.
        Core.Diagnostics.LayoutTrace.CountCallers =
            Environment.GetEnvironmentVariable("ADAM_CANVAS_PRESSCALLERS") == "1";
    }

    private void OnPointerDown(object sender, MouseButtonEventArgs e)
    {
        if (PressLog == null)
        {
            PointerDown(sender, e);
            return;
        }

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var wasMeasures = MeasurableUIComponent.TotalMeasureCores;

        // What happened BETWEEN this press and the last one, which is where the cost of a press actually lands: the
        // handler returns at once and leaves a layout pass and a re-record behind it. Measured this way round because
        // there is nowhere inside a press to stand and watch the frame that follows it.
        var since = wasMeasures - _measuresAtLastPress;
        var gap = _sinceLastPress.IsRunning ? _sinceLastPress.Elapsed.TotalMilliseconds : 0;

        PointerDown(sender, e);

        clock.Stop();

        _measuresAtLastPress = MeasurableUIComponent.TotalMeasureCores;
        _sinceLastPress.Restart();

        try
        {
            // ...and WHO. A count of measures says the application is busy; it does not say what it is busy with, and
            // the difference between those two is the whole job. The counters are keyed by the type and the property
            // that caused the invalidation, so the busiest line here names the thing to fix.
            var busiest = Core.Diagnostics.LayoutTrace.DumpCounts();
            Core.Diagnostics.LayoutTrace.ResetCounts();

            // The AGGREGATES first - "this type was invalidated N times" - and then the same list with them taken out.
            // A type says where the work landed; only the PROPERTY says what caused it, and the aggregates are by
            // definition the biggest numbers, so a plain top-of-the-list shows nothing else at all.
            var lines = busiest.Split('\n');
            var top = new System.Text.StringBuilder();
            var named = new System.Text.StringBuilder();
            var shown = 0;
            var namedShown = 0;

            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length == 0) continue;

                var aggregate = lines[i].Contains('*');

                if (aggregate && shown < 6)
                {
                    top.Append("    ").Append(lines[i]).Append("\r\n");
                    shown++;
                }
                else if (!aggregate && namedShown < 10)
                {
                    named.Append("      by ").Append(lines[i].Trim()).Append("\r\n");
                    namedShown++;
                }
            }

            top.Append(named);

            // TEMPLATES BUILT and CONTROLS MADE, which is the difference between "something was written to" and
            // "everything was built again". The properties in the list above - Child, Content, CornerRadius, Width -
            // are what a template's assembly writes, so this says whether that is what is happening.
            var builtNow = Base.TemplatedUIComponent.TemplatesBuilt;
            var madeNow = Base.TemplatedUIComponent.TemplatedControlsMade;

            System.IO.File.AppendAllText(PressLog,
                $"press {clock.Elapsed.TotalMilliseconds:F3} ms, measures in it {MeasurableUIComponent.TotalMeasureCores - wasMeasures}" +
                $" | since the last press: {since} measures over {gap:F0} ms" +
                $", templates built {builtNow - _templatesAtLastPress}, controls made {madeNow - _madeAtLastPress}" +
                $", inspector rebuilds {PropertyGrid.Rebuilds - _rebuildsAtLastPress}" +
                $", refreshes {PropertyGrid.Refreshes - _refreshesAtLastPress}\r\n" + top);

            _templatesAtLastPress = builtNow;
            _madeAtLastPress = madeNow;
            _rebuildsAtLastPress = PropertyGrid.Rebuilds;
            _refreshesAtLastPress = PropertyGrid.Refreshes;
        }
        catch
        {
            // An instrument that throws is worse than one that says nothing.
        }
    }

    private void PointerDown(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled) return;

        // ONE STEP per gesture. A press is where a change to the drawing begins and the release is where it ends, so
        // that is what a step is: dragging ten things across the plane is one thing to undo, not one per mouse move.
        // Opened before anything is asked of the tool, so whatever the tool does is inside it.
        BeginEdit("Gesture");

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

            // ...and a double click on EMPTY PLANE offers the same list a dropped wire does - the other way into it,
            // for a node that starts nothing and joins nothing. Only where there is nothing: over a thing, a double
            // click is about that thing.
            if (e.ChangedButton == MouseButtons.Left && Mode == CanvasMode.Nodes && !_pressIsPlane)
            {
                var spot = ScreenToWorld(e.GetPosition(this));
                var reach = ScreenToWorldLength(4);
                var box = new Rect(spot.X - reach, spot.Y - reach, reach * 2, reach * 2);

                var empty = true;
                foreach (var item in ItemsHere(box))
                {
                    if (!item.HitTest(spot, reach)) continue;

                    empty = false;
                    break;
                }

                if (empty && OfferWire(null, null, spot))
                {
                    e.Handled = true;
                    return;
                }
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
        //
        // UNLESS THE LAYER SAID OTHERWISE. A node is dragged by its title strip, and that press does start inside a
        // control - the layer is what tells the two apart, and having decided, it hands the press here through
        // PressFromElement. Asking again from the original source would undo that decision.
        if (!_pressIsPlane && FromGlass(e.OriginalSource)) return;

        var at = e.GetPosition(this);

        // A GRIP of the manipulation frame, before any tool sees the press. The frame is the canvas's - it draws it -
        // so dragging one is the canvas's answer to give, and it is the same answer whatever tool is in hand. That is
        // the point: having just dragged out a rectangle you can pull its corner straight away, instead of putting the
        // shape tool down first to be allowed to. Only the GRIPS: a press inside the frame still means what the tool
        // says it means, or a shape could never be drawn over something already selected.
        if (e.ChangedButton == MouseButtons.Left && _selection.Count > 0)
        {
            // POINT handles first. They sit inside the frame, so asking about the box first would answer "the body" and
            // start a move - and a curve would be impossible to reshape without moving it.
            var point = PointHandleAt(at);
            if (point >= 0)
            {
                BeginEdit("Reshape");
                _draggingPoint = point;
                CaptureMouse();

                e.Handled = true;
                return;
            }

            var handle = HandleAt(at);
            if (handle is not (CanvasHandle.None or CanvasHandle.Body))
            {
                _frame.Begin(this, handle, InShapeSpace(ScreenToWorld(at)));
                CaptureMouse();

                e.Handled = true;
                return;
            }
        }

        // Everything else is the TOOL's. The canvas keeps the wheel, the middle button, space and Home - how you look at
        // a drawing - and knows nothing about what the plain left button means.
        var args = Describe(at, e.ChangedButton, e.ClickCount, e.Modifiers);
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
        _pointer = pointer;

        // OVER A PANEL the pointer is the panel's, not the plane's: a crosshair over an inspector says a press there
        // would draw, and it would not - the press path has ignored the glass all along, and the pointer has to say the
        // same thing the press will do. The mark and the readout go with it: a readout drawn under the panel is about a
        // point nobody is aiming at. A drag keeps the capture, so its moves report the canvas and are unaffected.
        if (FromGlass(e.OriginalSource))
        {
            var had = _snap != null || _pointerInside;

            _snap = null;
            _pointerInside = false;
            Cursor = Cursors.Arrow;

            if (had) Repaint();
            return;
        }

        _pointerInside = true;

        // Where the pointer would be PULLED to, worked out whatever the tool is doing - the mark has to appear before the
        // press, or nobody can aim at it.
        var pulled = TrySnap(ScreenToWorld(pointer), out var target) ? target : (Vector2?)null;
        if (pulled != _snap)
        {
            _snap = pulled;
            Repaint();
        }

        // A POINT taken before any tool saw the press is dragged before any tool sees the move. Pulled to the grid like
        // everything else the hand places: a point of a curve is a place on the plane, not a place on the glass.
        if (_draggingPoint >= 0 && _selection.Count == 1 && _selection[0] is ICanvasPoints shaped)
        {
            shaped.MovePoint(_draggingPoint, pulled ?? ScreenToWorld(pointer));
            Scene?.Touch();

            e.Handled = true;
            return;
        }

        // A grip taken before any tool saw the press is dragged before any tool sees the move.
        if (_frame.IsActive)
        {
            _frame.MoveTo(this, InShapeSpace(ScreenToWorld(pointer)));
            e.Handled = true;
            return;
        }

        ShowPointer(pointer);

        var args = Describe(pointer, MouseButtons.None, 0, e.Modifiers);
        Tool?.OnMoved(this, args);
        if (args.Handled) e.Handled = true;
    }

    // What the pointer is ABOUT to do. A grip is eight pixels of glass and looks the same whichever corner it is, so
    // the cursor is the only thing that says which way it will pull; anywhere else the pointer wears the TOOL, which is
    // the one place a person is already looking to find out what is in hand. A tool put down by the right button
    // announces itself nowhere else.
    private void ShowPointer(Vector2 screen)
    {
        var overGrip = _selection.Count > 0 ? CanvasFrameGesture.CursorFor(HandleAt(screen)) : null;

        Cursor = overGrip ?? OverSelection(screen) ?? Tool?.Cursor ?? Cursors.Arrow;
    }

    // What is under the pointer WITHIN the selection: one of its points, or the thing itself.
    // <para>The only thing that says so on a line. A box says where it can be grabbed by being drawn; a line has no box
    // - and now no frame either - so without this there is nothing at all to tell a hand that it has crossed into the
    // few pixels where a drag would do something, and finding that edge means pressing and seeing.</para>
    private Cursor OverSelection(Vector2 screen)
    {
        // A SOCKET first, and whether anything is selected or not: a wire is pulled out of one with the select tool in
        // hand, so what says the pointer has reached one has to be the same whatever else is going on.
        if (Tool is SelectTool select && select.Wires.Under(this, ScreenToWorld(screen)) != null) return Cursors.Crosshair;

        if (_selection.Count == 0) return null;

        if (PointHandleAt(screen) >= 0) return Cursors.Crosshair;

        // Only what MOVES with a press here. A press inside the frame of something that offers no body drag does
        // nothing, and a cursor promising otherwise is the same lie a grip drawn where nothing drags would be.
        if (!OfferedHandles.HasFlag(CanvasHandles.Body)) return null;

        var world = ScreenToWorld(screen);
        var reach = ScreenToWorldLength(Math.Max(4, HandleSize) / 2 + 2);

        foreach (var item in _selection)
        {
            if (item.HitTest(InShapeSpace(world), reach)) return Cursors.SizeAll;
        }

        return null;
    }

    private void OnPointerLeft(object sender, MouseEventArgs e)
    {
        var had = _snap != null || _pointerInside;

        _snap = null;
        _pointerInside = false;

        if (had) Repaint();
    }

    private void OnPointerUp(object sender, MouseButtonEventArgs e)
    {
        // The step the press opened is closed HERE whatever the release turns out to mean - a pan, a grip, a tool. A
        // gesture that changed nothing records nothing, so closing one that was only a click costs a comparison.
        try
        {
            if (_draggingPoint >= 0)
            {
                _draggingPoint = -1;
                ReleaseMouseCapture();
                e.Handled = true;
                return;
            }

            if (_panning)
            {
                _panning = false;
                ReleaseMouseCapture();
                e.Handled = true;
                return;
            }

            if (_frame.IsActive)
            {
                _frame.End();
                ReleaseMouseCapture();

                e.Handled = true;
                return;
            }

            var args = Describe(e.GetPosition(this), e.ChangedButton, e.ClickCount, e.Modifiers);
            Tool?.OnReleased(this, args);
            if (args.Handled) e.Handled = true;
        }
        finally
        {
            EndEdit();
        }
    }

    // Whether an event came from something the canvas CARRIES rather than from the plane: the tool panel on the glass,
    // or a control standing on the drawing. Their own events bubble through this canvas, and the ones that matter are
    // not marked handled by whoever answered them - a control answers MouseLeftButtonDown, which is raised from a
    // SEPARATE args object, and a scroller deliberately leaves the wheel alone once it has reached its end - so where
    // the event came from is the only thing there is to go on.
    private bool FromGlass(object source)
    {
        if (_overlayRoot == null && _elements == null && _chromeLayer == null) return false;

        for (var at = source as IUIComponent; at != null; at = at.VisualParent)
        {
            // A PANE is glass; the layer holding them is not. A panel in this engine catches the mouse across its whole
            // bounds whether or not it has a background, and the chrome layer covers the entire canvas - so counting
            // the layer itself as glass threw away every press that landed anywhere but on a panel, which is to say
            // every press meant for the drawing. The pane is met first walking up, so this order is the whole fix.
            if (at is CanvasPane) return true;
            if (ReferenceEquals(at, _chromeLayer)) return false;

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
        if (!Ours(e)) return;

        // The TOOL first. The canvas spends Delete on the selection, Escape on letting go and space on panning, and all
        // three mean something else to a tool with a caret in a word - so the tool is asked before any of them.
        Tool?.OnKey(this, e);
        if (e.Handled) return;

        // A TOOL IN THE MIDDLE OF SOMETHING OWNS THE KEYBOARD. A caret in a word answers only the few keys that mean
        // something to it - the letters themselves arrive as TEXT, not as keys - so everything it left unanswered would
        // have gone on to the canvas: typing "vet" picked three tools and threw the word away. Nothing below is worth
        // more than the thing being typed, and the way out of a gesture is Escape, which the tool answers itself.
        if (Tool is { IsBusy: true }) return;

        if (e.Key == Key.Space)
        {
            _spaceHeld = true;
            return;
        }

        // GROUPING and UNDO, before the tool shortcuts: Ctrl+G and Ctrl+Z are keys a shape tool would otherwise take
        // for itself, and a tool letter with Ctrl held has never meant the tool anywhere.
        var control = (e.Modifiers & (InputModifiers.LeftControl | InputModifiers.RightControl)) != 0;
        var shift = (e.Modifiers & (InputModifiers.LeftShift | InputModifiers.RightShift)) != 0;

        if (control && e.Key == Key.G)
        {
            if (shift) UngroupSelection();
            else GroupSelection();

            e.Handled = true;
            return;
        }

        // Ctrl+Z, and BOTH of the two things the world calls redo: Ctrl+Y and Ctrl+Shift+Z. Which one a person reaches
        // for depends on what they used last, and there is nothing to be gained by insisting on one.
        if (control && (e.Key == Key.Z || e.Key == Key.Y))
        {
            if (e.Key == Key.Y || shift) Redo();
            else Undo();

            e.Handled = true;
            return;
        }

        // Copy, paste and duplicate. Before the tool shortcuts for the same reason undo is: they are held with Ctrl, and
        // a tool whose key is C must not be picked because somebody copied.
        if (control && e.Key is Key.C or Key.V or Key.D)
        {
            if (e.Key == Key.C) Copy();
            else if (e.Key == Key.V) Paste();
            else Duplicate();

            e.Handled = true;
            return;
        }

        // AFTER the tool and BEFORE the canvas's own keys. After, because a tool with a caret in a word owns every
        // letter while it is typing and a shortcut must not steal one. Before, because picking a tool is the commonest
        // thing a person does here and the canvas's own keys are Delete, Escape and Home, which no tool wants.
        if (PickByShortcut(e.Key))
        {
            e.Handled = true;
            return;
        }

        // WHERE IS MY WORK, on one key - what is selected, or the whole of it when nothing is. The plane has no edges
        // and no corner to scroll back to, so this is the way out of being lost, and every editor puts it here.
        if (e.Key == Key.F && !control)
        {
            if (!FitSelection()) FitAll();

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete)
        {
            if (_selection.Count == 0) return;

            // Through the REQUEST, so a key press meets the same question a button does.
            RequestDeleteSelection();
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

    // The FIRST tool claiming the key wins, and a second one claiming the same key is simply never reached. Not an
    // error to report: which keys a tool answers to is the application's to arrange, and a control that threw over it
    // would be a control that cannot be experimented with.
    private bool PickByShortcut(Key key)
    {
        if (!AreToolShortcutsEnabled || key == Key.None || _tools == null) return false;

        foreach (var tool in _tools)
        {
            if (tool == null || tool.Shortcut != key) continue;

            // ...and it must WORK HERE. A tool the rail does not offer in this mode is one a letter must not hand over
            // either, or a graph ends up with a pen in its hand - which is the one thing a mode exists to prevent.
            if (!tool.WorksIn(Mode)) continue;

            if (ReferenceEquals(tool, Tool)) return true;

            SetCurrentValue(ToolProperty, tool);
            return true;
        }

        return false;
    }

    private void OnKeyReleased(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space) _spaceHeld = false;
    }

    // Characters go straight to the tool and nowhere else: the canvas has nothing to type into, and what a key means on
    // the user's own keyboard is a question only the layout can answer - which is what this event already did.
    private void OnTextTyped(object sender, TextInputEventArgs e)
    {
        if (!Ours(e)) return;

        Tool?.OnText(this, e);
    }

    /// <summary>Whether a keyboard event is the CANVAS'S to answer, rather than one belonging to something standing on
    /// it or on the glass over it.
    /// <para>Keyboard events bubble, so every letter typed into a field inside a node or into a panel beside the plane
    /// arrives here as well - and the canvas answers letters: a node being renamed in the inspector put a text tool in
    /// the hand halfway through its new name, and the rest of the name went onto the drawing. Whoever has the keyboard
    /// is the one the event was raised on, and that is the whole of the question.</para></summary>
    private bool Ours(RoutedEventArgs e) => ReferenceEquals(e.OriginalSource, this);

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

        _front = GetTemplateChild("PART_Front") as CanvasFrontLayer;
        if (_front != null) _front.Owner = this;

        _chromeLayer = GetTemplateChild("PART_Chrome") as CanvasChromeLayer;
        if (_chromeLayer != null) _chromeLayer.Owner = this;
        SyncChrome();

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

        if (_chromeLayer != null)
        {
            // The panes are the APPLICATION's - they are handed back rather than dropped, or a template swap would take
            // the user's own panels with it.
            _chromeLayer.Sync(null);
            _chromeLayer.Owner = null;
        }

        _elements = null;
        _chromeLayer = null;
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
        //
        // Gone as well when there is NOTHING in it. The grip exists to fold the panel away and bring it back, so with
        // no panel behind it it is a handle that opens nothing - which is exactly what it looked like beside the panes
        // once an application had moved its content into Chrome: two grips, one of them a lie.
        var wanted = IsOverlayVisible && Overlay != null;
        _overlayRoot.Visibility = wanted ? Visibility.Visible : Visibility.Collapsed;

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

        // The pointer wears the tool, so it changes WITH the tool and not at the next twitch of the mouse. Put down by
        // the right button, the old tool would otherwise go on pointing at the drawing until something moved - which is
        // exactly the moment a person needs telling that it is gone.
        if (canvas._pointerInside) canvas.ShowPointer(canvas._pointer);

        canvas.ToolChanged?.Invoke(canvas, EventArgs.Empty);
        canvas.Repaint();
    }

    /// <summary>The tool in hand changed - a rail marks a different button on it.</summary>
    public event EventHandler ToolChanged;

    private static void OnOverlayChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e) =>
        (component as InfiniteCanvas)?.PlaceOverlay();

    private static void OnChromeChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not InfiniteCanvas canvas) return;

        if (canvas._chrome != null) canvas._chrome.CollectionChanged -= canvas.OnChromeEdited;

        canvas._chrome = e.NewValue as CanvasPanes;

        if (canvas._chrome != null) canvas._chrome.CollectionChanged += canvas.OnChromeEdited;

        canvas.SyncChrome();
    }

    private void OnChromeEdited(object sender, NotifyCollectionChangedEventArgs e) => SyncChrome();

    private static void OnToolsChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not InfiniteCanvas canvas) return;

        if (canvas._tools != null) canvas._tools.CollectionChanged -= canvas.OnToolsEdited;

        canvas._tools = e.NewValue as CanvasTools;

        if (canvas._tools != null) canvas._tools.CollectionChanged += canvas.OnToolsEdited;

        canvas.ToolsChanged?.Invoke(canvas, EventArgs.Empty);
    }

    private void OnToolsEdited(object sender, NotifyCollectionChangedEventArgs e) =>
        ToolsChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>The set of tools changed - a rail rebuilds its buttons on it.</summary>
    public event EventHandler ToolsChanged;

    private void SyncChrome()
    {
        if (_chromeLayer == null) return;

        _chromeLayer.Sync(_chrome);
        _chromeLayer.InvalidateMeasure();
        _chromeLayer.InvalidateArrange();
    }

    // Changing what the canvas is being used AS changes everything that follows from it: what can be selected, which
    // tool is in hand, which controls the layer hosts and what is drawn.
    private static void OnModeChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not InfiniteCanvas canvas) return;

        // LET GO FIRST. What was selected belongs to the mode that has just been left, and a frame round something the
        // canvas no longer draws is a frame round nothing that still answers Delete.
        canvas.ClearSelection();

        // ...and put down a tool that has nothing to do here. A pen in a graph would draw ink into a scene the graph
        // never shows again.
        if (canvas.Tool is { } tool && !tool.WorksIn(canvas.Mode))
        {
            tool.Cancel(canvas);
            canvas.SetCurrentValue(ToolProperty, canvas.FirstToolFor(canvas.Mode));
        }

        canvas.SyncElements();
        canvas._elements?.InvalidateMeasure();
        canvas._elements?.InvalidateArrange();
        canvas.Repaint();

        canvas.ModeChanged?.Invoke(canvas, EventArgs.Empty);
    }

    // The tool to fall back on when the one in hand does not belong: the default if it fits, else the first offered
    // tool that does, else none at all - a canvas with no tool still pans, zooms and shows what is on it.
    private ICanvasTool FirstToolFor(CanvasMode mode)
    {
        if (DefaultTool is { } fallback && fallback.WorksIn(mode)) return fallback;

        if (Tools is { } tools)
        {
            foreach (var tool in tools)
            {
                if (tool != null && tool.WorksIn(mode)) return tool;
            }
        }

        return null;
    }

    private static void OnSceneChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not InfiniteCanvas canvas) return;

        if (e.OldValue is ICanvasScene previous) previous.Changed -= canvas.OnSceneEdited;
        if (e.NewValue is ICanvasScene current) current.Changed += canvas.OnSceneEdited;

        canvas._graph.SetScene(e.NewValue as ICanvasScene);
        canvas._drawing.SetScene(e.NewValue as ICanvasScene);

        // A whole scene arriving is as much a change as an item being put in one, and the controls in it have to reach
        // the layer the same way.
        canvas.SyncElements();
        canvas._elements?.InvalidateMeasure();
        canvas._elements?.InvalidateArrange();

        canvas.Repaint();
    }

    private void OnSceneEdited(object sender, EventArgs e)
    {
        Forget();
        SyncElements();

        // ...and place them again even when the SET has not changed. An edit here is just as often one item's rectangle
        // moving - which is exactly what dragging a control does - and a layer that only answered to items appearing and
        // disappearing left the control behind while its frame walked off.
        _elements?.InvalidateMeasure();
        _elements?.InvalidateArrange();

        // The same for a pane that follows the selection: an item moving moves the frame, and the bar sits on the
        // frame. This is the one place every edit passes through - a drag, a resize, an inspector writing a number -
        // so it is the one place that has to say so.
        _chromeLayer?.InvalidateArrange();

        PlaneChanged?.Invoke(this, EventArgs.Empty);

        Repaint();
    }

    // WHAT IS SELECTED BUT NO LONGER THERE, let go of. Anything can take an item off the plane - a clear, an undo, an
    // application writing its own collection - and none of them knows what is selected. Left holding it, the frame is
    // drawn round something that is not drawn, its grips resize a thing nobody can see, and Delete works on it.
    //
    // Only when something IS selected, which is the case that costs anything: an empty selection is the common one and
    // it answers in a branch.
    private void Forget()
    {
        if (_selection.Count == 0 || Scene is not { } scene) return;

        var there = new HashSet<ICanvasItem>(scene.ItemsIn(Everything));
        var gone = false;

        for (var i = _selection.Count - 1; i >= 0; i--)
        {
            if (there.Contains(_selection[i])) continue;

            _selection.RemoveAt(i);
            gone = true;
        }

        if (gone) Selected();
    }

    /// <summary>WHAT IS UNDER A POINT, topmost first - or null where the plane is empty.
    /// <para>The one question every tool asks before deciding what a press means, so it is asked in one place: a tool
    /// that answered it for itself would pick with a different reach than the selection does, and the two would
    /// disagree about what was clicked.</para></summary>
    public ICanvasItem ItemAt(Vector2 world)
    {
        if (Scene == null) return null;

        var reach = ScreenToWorldLength(4);
        var probe = new Rect(world.X - reach, world.Y - reach, reach * 2, reach * 2);

        ICanvasItem found = null;
        foreach (var item in ItemsHere(probe))
        {
            if (item.HitTest(world, reach)) found = item;
        }

        return found;
    }

    /// <summary>Raised after something on the plane changes - put there, taken away, moved, reshaped. For anything that
    /// draws a picture OF the plane rather than on it: a map repaints itself for the camera, and a node added while the
    /// camera stood still is exactly the change it would otherwise never hear about.</summary>
    public event EventHandler PlaneChanged;

    /// <summary>Raised after the camera moves or zooms - for anything that draws a picture of WHERE the camera is, such
    /// as a map of the plane. The canvas repaints itself for this; something standing beside it has to be told.</summary>
    public event EventHandler CameraChanged;

    private static void OnCameraChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not InfiniteCanvas canvas) return;

        canvas.CameraChanged?.Invoke(canvas, EventArgs.Empty);

        // Whoever moved the camera - a binding, the wheel, the application - has placed it, and the canvas must not
        // then move it somewhere of its own on the first layout.
        if (e.Property == OffsetProperty)
            canvas._cameraPlaced = true;

        // The controls on the plane MOVE with the camera, and moving them is an arrange: what is inside one was measured
        // for its own rectangle, which the camera does not change. A zoom changes that rectangle, so it measures again.
        canvas.SyncElements();
        canvas._elements?.InvalidateArrange();
        if (e.Property == ScaleProperty) canvas._elements?.InvalidateMeasure();

        // A pane that FOLLOWS THE SELECTION is placed from where the frame is on screen, and the camera is half of that
        // sum - so moving the camera has to re-place it just as it re-places the controls. Without this the bar kept
        // the pixels it had when the selection was made and slid off the frame at the first turn of the wheel: the
        // frame is drawn every render, the panes only when something arranges them.
        canvas._chromeLayer?.InvalidateArrange();

        canvas.Repaint();
    }

    private static void OnDesignModeChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is InfiniteCanvas canvas) canvas.ApplyDesignMode();
    }

    // Editing, a control on the plane is INVISIBLE TO THE POINTER, so a press on it lands on the plane and the select
    // tool picks it up. Using, the press is the control's. For a button or a field there is no third answer: a press
    // cannot both operate one and drag it.
    //
    // A NODE is the exception, and the layer knows it. What is in a node is a field, a switch, a list - a node whose
    // contents cannot be clicked is a picture of a node - so it stays live and is dragged by its title strip instead.
    private void ApplyDesignMode()
    {
        _elements?.ApplyDesignMode();

        Repaint();
    }

    /// <summary>A press that began on a hosted control but MEANS the plane - a node taken by its title strip. The layer
    /// hands it here, and from here on it is an ordinary press: the tool picks, the gesture begins, one undo step opens.
    /// </summary>
    internal void PressFromElement(MouseButtonEventArgs e)
    {
        _pressIsPlane = true;
        try
        {
            OnPointerDown(this, e);
        }
        finally
        {
            _pressIsPlane = false;
        }
    }

    // Whether the press being read right now was handed over by the element layer as one that MEANS the plane. See the
    // guard in PointerDown.
    private bool _pressIsPlane;

    /// <summary>Raised when a wire is let go over empty plane. See <see cref="CanvasWireDroppedEventArgs"/> for why the
    /// canvas offers this instead of deciding it.</summary>
    public event EventHandler<CanvasWireDroppedEventArgs> WireDropped;

    /// <summary>Offers a dropped wire to whoever is listening. Called by the gesture; true when somebody took it.
    /// </summary>
    internal bool OfferWire(ElementItem item, CanvasNodePin pin, Vector2 world)
    {
        if (WireDropped != null)
        {
            var args = new CanvasWireDroppedEventArgs
            {
                FromItem = item,
                FromPin = pin,
                World = world,
                Screen = WorldToScreen(world)
            };

            WireDropped(this, args);
            if (args.Handled) return true;
        }

        // Nobody took it, so the canvas offers what it can: the list of kinds it was given. Without one there is
        // nothing to offer and the wire is simply abandoned, which is what letting go over nothing usually means.
        return OpenPalette(item, pin, world);
    }

    /// <summary>Whether the list of node kinds is showing. Two-way: the application's own list closes itself by writing
    /// false here.</summary>
    public static readonly AdamantiumProperty IsPaletteOpenProperty = AdamantiumProperty.Register(
        nameof(IsPaletteOpen), typeof(Boolean), typeof(InfiniteCanvas),
        new PropertyMetadata(false, PropertyMetadataOptions.BindsTwoWayByDefault, OnPaletteOpenChanged));

    /// <summary>The same state as something markup can bind straight to a panel. Two names for one thing, because a
    /// question in code and a visibility in markup are not the same shape - and keeping them in step here is a line
    /// rather than a converter in three themes.</summary>
    public static readonly AdamantiumProperty PaletteFaceProperty = AdamantiumProperty.RegisterReadOnly(
        nameof(PaletteFace), typeof(Visibility), typeof(InfiniteCanvas),
        new PropertyMetadata(Visibility.Collapsed));

    /// <summary>Where the list opens, in SCREEN pixels from the canvas's corner - under the pointer, which is where the
    /// hand already is.</summary>
    public static readonly AdamantiumProperty PaletteAtProperty = AdamantiumProperty.Register(nameof(PaletteAt),
        typeof(Vector2), typeof(InfiniteCanvas), new PropertyMetadata(Vector2.Zero));

    /// <summary>What was picked out of the list - an entry of <see cref="NodeKinds"/>, not a word: the list shows the
    /// kinds themselves, so what comes back is one of them. PICKING IS THE ANSWER: a list opened by a gesture is
    /// answered by choosing from it, so writing here puts that node on the plane and closes the list.</summary>
    public static readonly AdamantiumProperty PickedKindProperty = AdamantiumProperty.Register(nameof(PickedKind),
        typeof(ICanvasNodeKind), typeof(InfiniteCanvas),
        new PropertyMetadata(null, PropertyMetadataOptions.BindsTwoWayByDefault, OnPickedKindChanged));

    public Boolean IsPaletteOpen
    {
        get => GetValue<Boolean>(IsPaletteOpenProperty);
        set => SetValue(IsPaletteOpenProperty, value);
    }

    public Visibility PaletteFace => GetValue<Visibility>(PaletteFaceProperty);

    public Vector2 PaletteAt
    {
        get => GetValue<Vector2>(PaletteAtProperty);
        set => SetValue(PaletteAtProperty, value);
    }

    public ICanvasNodeKind PickedKind
    {
        get => GetValue<ICanvasNodeKind>(PickedKindProperty);
        set => SetValue(PickedKindProperty, value);
    }

    private ElementItem _askedFrom;
    private CanvasNodePin _askedPin;
    private Vector2 _askedWorld;

    private static void OnPaletteOpenChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not InfiniteCanvas canvas) return;

        var open = e.NewValue is true;
        canvas.SetValue(PaletteFaceProperty, open ? Visibility.Visible : Visibility.Collapsed);

        if (open) return;

        canvas._askedFrom = null;
        canvas._askedPin = null;
    }

    /// <summary>Opens the list of kinds at that point in the world, for whoever wants a node there and has no wire to
    /// join - the tool that places one, or a double click on empty plane.</summary>
    public Boolean AskForNode(Vector2 world) => OpenPalette(null, null, world);

    private bool OpenPalette(ElementItem item, CanvasNodePin pin, Vector2 world)
    {
        if (NodeKinds == null || Nodes == null) return false;

        _askedFrom = item;
        _askedPin = pin;
        _askedWorld = world;

        PaletteAt = WorldToScreen(world);
        IsPaletteOpen = true;

        return true;
    }

    // A KIND was picked: the node goes where the list was opened, and the wire that opened it lands on it.
    private static void OnPickedKindChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not InfiniteCanvas canvas || e.NewValue is not ICanvasNodeKind picked) return;

        var kind = picked.Kind;

        var from = canvas._askedFrom;
        var pin = canvas._askedPin;

        canvas.IsPaletteOpen = false;

        canvas.BeginEdit("Add node");
        try
        {
            var made = canvas.AddNode(kind, canvas._askedWorld);
            if (made == null) return;

            // The wire arrives at the FIRST socket of the other side - the one an editor picks when a wire is dropped
            // on a node rather than on one of its sockets, because it is the only one it can pick without asking.
            if (pin != null && from != null && canvas.ItemOf(made) is { } item)
            {
                var node = item.Element as CanvasNode;
                var wanted = pin.IsInput ? node?.OutputPins : node?.InputPins;

                if (wanted is { Count: > 0 }) canvas.Join(from, pin, item, wanted[0]);
            }
        }
        finally
        {
            canvas.EndEdit();
            canvas.SetCurrentValue(PickedKindProperty, null);
        }
    }

    /// <summary>The thing on the plane standing for that node, or null while it has not been made yet.</summary>
    public ElementItem ItemOf(ICanvasNode node) => _graph.ItemOf(node);

    /// <summary>WHAT DRAWS what a node carries, chosen for the type of the thing it is carrying.
    /// <para>Given to the canvas because the canvas is what makes the containers: it hands each new node its template
    /// at birth. The templates themselves live in the markup, where pictures belong, and the application's node objects
    /// stay free of them.</para></summary>
    public static readonly AdamantiumProperty NodeContentSelectorProperty = AdamantiumProperty.Register(
        nameof(NodeContentSelector), typeof(DataTemplateSelector), typeof(InfiniteCanvas),
        new PropertyMetadata(null, OnNodeContentSelectorChanged));

    public DataTemplateSelector NodeContentSelector
    {
        get => GetValue<DataTemplateSelector>(NodeContentSelectorProperty);
        set => SetValue(NodeContentSelectorProperty, value);
    }

    private static void OnNodeContentSelectorChanged(AdamantiumComponent component,
        AdamantiumPropertyChangedEventArgs e)
    {
        if (component is InfiniteCanvas canvas) canvas._graph.SetContentSelector(e.NewValue as DataTemplateSelector);
    }

    /// <summary>Joins two nodes, the way the gesture does - for whoever answered <see cref="WireDropped"/> and has just
    /// made the node the wire was reaching for.
    /// <para>Here and not in the application: the rules about what may be joined to what, and how many wires a socket
    /// takes, belong to the graph and not to whoever is adding to it.</para></summary>
    public ConnectionItem Join(ElementItem fromItem, CanvasNodePin from, ElementItem toItem, CanvasNodePin to)
    {
        if (Scene is not { } scene || !ConnectGesture.Joinable(scene, fromItem, from, toItem, to)) return null;

        ConnectionItem.MakeRoom(scene, from.IsInput ? from : to);

        var wire = from.IsInput
            ? new ConnectionItem(toItem, to, fromItem, from)
            : new ConnectionItem(fromItem, from, toItem, to);

        scene.Add(wire);
        from.IsConnected = true;
        to.IsConnected = true;

        return wire;
    }

    // The controls that are ON SCREEN, handed to the layer. Only the visible ones, like everything else here: the cost
    // of a frame follows the viewport and not the drawing. A control scrolled off the plane leaves the visual tree and
    // comes back when it returns - which is the same bargain a virtualized list makes.
    private void SyncElements() => SyncElements(RenderSize);

    // ...and across a viewport SAID OUT LOUD, because the one pass that must not read RenderSize is the pass that sets
    // it: arranging with a new size asked "what can be seen" of the size before it, so everything that had just come
    // into view was left off the plane - and nothing asked again until something else re-synced (a zoom, a pan, a node
    // added). Which is exactly what it looked like: a window resized, and half the graph gone until the first zoom.
    private void SyncElements(Size viewport)
    {
        if (_elements == null) return;

        _visibleElements.Clear();
        foreach (var item in ItemsHere(WorldAcross(viewport)))
        {
            if (item is ElementItem element) _visibleElements.Add(element);
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
        //
        // FROM finalSize, for the same reason the camera above is: RenderSize is what this pass sets, so reading it
        // here answers with the size the canvas USED to be.
        SyncElements(finalSize);

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
