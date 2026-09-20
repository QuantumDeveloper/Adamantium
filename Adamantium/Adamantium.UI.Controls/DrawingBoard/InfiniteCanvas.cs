using System.Collections;
using System.Collections.Specialized;
using Adamantium.Graphics.Fonts;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
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
public partial class InfiniteCanvas : Control
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

    private Panel _layers;
    private CanvasFrontLayer _front;
    private readonly List<(bool Hosted, List<ICanvasItem> Items)> _runs = new();

    // Where the frame round what is held goes - see MarkChrome. One of the three at a time.
    private ICanvasItem _chromeAfter;
    private ICanvasItem _chromeBefore;
    private bool _chromeOnGlass;
    private readonly List<ElementItem> _hosted = new();

    private CanvasChromeLayer _chromeLayer;
    private CanvasPane _palette;
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
        typeof(Double), typeof(InfiniteCanvas), new PropertyMetadata(65536.0));

    /// <summary>How the canvas was left, as text - panels, camera, tool. Two-way: bind it to wherever the application
    /// keeps such things; where that is, is the application's business. See <see cref="CanvasLayoutSerializer"/>.
    /// </summary>
    public static readonly AdamantiumProperty LayoutProperty = AdamantiumProperty.Register(nameof(Layout),
        typeof(String), typeof(InfiniteCanvas),
        new PropertyMetadata(null, PropertyMetadataOptions.BindsTwoWayByDefault, OnLayoutChanged));

    /// <summary>Whether where the PANELS were left is remembered: their place, their width, what was folded and what
    /// was switched off.</summary>
    public static readonly AdamantiumProperty RemembersPanesProperty = AdamantiumProperty.Register(
        nameof(RemembersPanes), typeof(Boolean), typeof(InfiniteCanvas),
        new PropertyMetadata(true, PropertyMetadataOptions.BindsTwoWayByDefault, OnRememberingChanged));

    /// <summary>Whether WHERE THE CAMERA WAS LOOKING is remembered.</summary>
    public static readonly AdamantiumProperty RemembersCameraProperty = AdamantiumProperty.Register(
        nameof(RemembersCamera), typeof(Boolean), typeof(InfiniteCanvas),
        new PropertyMetadata(true, PropertyMetadataOptions.BindsTwoWayByDefault, OnRememberingChanged));

    /// <summary>Whether the ZOOM is remembered - its own switch, because the two are wanted apart.</summary>
    public static readonly AdamantiumProperty RemembersZoomProperty = AdamantiumProperty.Register(
        nameof(RemembersZoom), typeof(Boolean), typeof(InfiniteCanvas),
        new PropertyMetadata(true, PropertyMetadataOptions.BindsTwoWayByDefault, OnRememberingChanged));

    /// <summary>Whether the TOOL in hand is remembered.</summary>
    public static readonly AdamantiumProperty RemembersToolProperty = AdamantiumProperty.Register(
        nameof(RemembersTool), typeof(Boolean), typeof(InfiniteCanvas),
        new PropertyMetadata(true, PropertyMetadataOptions.BindsTwoWayByDefault, OnRememberingChanged));

    /// <summary>The world point <c>Home</c> goes to, and the zoom it goes there at. The world's origin at one to one
    /// until something says otherwise - <see cref="RememberViewCommand"/> is what says otherwise.</summary>
    public static readonly AdamantiumProperty HomeAtProperty = AdamantiumProperty.Register(nameof(HomeAt),
        typeof(Vector2), typeof(InfiniteCanvas),
        new PropertyMetadata(Vector2.Zero, PropertyMetadataOptions.BindsTwoWayByDefault, OnHomeChanged));

    public static readonly AdamantiumProperty HomeScaleProperty = AdamantiumProperty.Register(nameof(HomeScale),
        typeof(Double), typeof(InfiniteCanvas),
        new PropertyMetadata(1.0, PropertyMetadataOptions.BindsTwoWayByDefault, OnHomeChanged));

    /// <summary>What one nudge of a thickness is worth, in WORLD units - a tenth of a screen pixel, whatever the
    /// camera is at. A step stated in world units is a step that means something different at every zoom: a tenth is
    /// a hair at 1:1 and a hundred and seventy pixels at seventeen thousand, where every line is one white blob.
    /// <para>Never coarser than a tenth, so zooming OUT does not turn the nudge into a jump.</para></summary>
    public static readonly AdamantiumProperty ThicknessStepProperty = AdamantiumProperty.RegisterReadOnly(
        nameof(ThicknessStep), typeof(Double), typeof(InfiniteCanvas), new PropertyMetadata(0.1));

    /// <summary>...and the thinnest a line may be set to: a hundredth of a screen pixel.</summary>
    public static readonly AdamantiumProperty ThicknessLeastProperty = AdamantiumProperty.RegisterReadOnly(
        nameof(ThicknessLeast), typeof(Double), typeof(InfiniteCanvas), new PropertyMetadata(0.01));

    public Double ThicknessStep => GetValue<Double>(ThicknessStepProperty);

    public Double ThicknessLeast => GetValue<Double>(ThicknessLeastProperty);

    private void SyncThicknessSteps()
    {
        var scale = Scale > 0 ? Scale : 1;

        SetValue(ThicknessStepProperty, Math.Min(0.1, 0.1 / scale));
        SetValue(ThicknessLeastProperty, Math.Min(0.01, 0.01 / scale));
    }

    /// <summary>Whether a view has been kept, so that giving it up can be offered only where there is something to
    /// give up.</summary>
    public static readonly AdamantiumProperty KeepsHomeFaceProperty = AdamantiumProperty.RegisterReadOnly(
        nameof(KeepsHomeFace), typeof(Visibility), typeof(InfiniteCanvas),
        new PropertyMetadata(Visibility.Collapsed));

    public Visibility KeepsHomeFace => GetValue<Visibility>(KeepsHomeFaceProperty);

    private static void OnHomeChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not InfiniteCanvas canvas) return;

        var kept = canvas.HomeAt != Vector2.Zero || Math.Abs(canvas.HomeScale - 1) > 1e-6;

        canvas.SetValue(KeepsHomeFaceProperty, kept ? Visibility.Visible : Visibility.Collapsed);
    }

    /// <summary>Puts <c>Home</c> back to the world's origin at one to one - what takes back a view that was kept.
    /// </summary>
    public CanvasCommand ForgetViewCommand => _forgetView ??= new CanvasCommand(_ => ForgetView());

    private CanvasCommand _forgetView;

    public void ForgetView()
    {
        HomeAt = Vector2.Zero;
        HomeScale = 1;
        RememberLayout();
    }

    public String Layout
    {
        get => GetValue<String>(LayoutProperty);
        set => SetValue(LayoutProperty, value);
    }

    public Boolean RemembersPanes
    {
        get => GetValue<Boolean>(RemembersPanesProperty);
        set => SetValue(RemembersPanesProperty, value);
    }

    public Boolean RemembersCamera
    {
        get => GetValue<Boolean>(RemembersCameraProperty);
        set => SetValue(RemembersCameraProperty, value);
    }

    public Boolean RemembersZoom
    {
        get => GetValue<Boolean>(RemembersZoomProperty);
        set => SetValue(RemembersZoomProperty, value);
    }

    public Boolean RemembersTool
    {
        get => GetValue<Boolean>(RemembersToolProperty);
        set => SetValue(RemembersToolProperty, value);
    }

    public Vector2 HomeAt
    {
        get => GetValue<Vector2>(HomeAtProperty);
        set => SetValue(HomeAtProperty, value);
    }

    public Double HomeScale
    {
        get => GetValue<Double>(HomeScaleProperty);
        set => SetValue(HomeScaleProperty, value);
    }

    /// <summary>The panels this canvas is wearing. Empty until it has a template.</summary>
    public IReadOnlyList<CanvasPane> Panes => _chromeLayer?.Panes ?? Array.Empty<CanvasPane>();

    // The text the canvas itself wrote: coming back as a change like any other it would be applied over the top of
    // what it already describes.
    private string _wrote;
    private bool _dressing;

    private static void OnLayoutChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not InfiniteCanvas canvas || canvas._dressing) return;
        if (e.NewValue as string == canvas._wrote) return;

        canvas._dressing = true;
        try
        {
            CanvasLayoutSerializer.Load(canvas, e.NewValue as string);
        }
        finally
        {
            canvas._dressing = false;
        }
    }

    private static void OnRememberingChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e) =>
        (component as InfiniteCanvas)?.RememberLayout();

    /// <summary>Writes how the canvas is set up now into <see cref="Layout"/>. Called whenever something settles;
    /// offered because an application closing down may want to ask rather than wait.</summary>
    public void RememberLayout()
    {
        if (_dressing || _chromeLayer == null) return;

        var said = CanvasLayoutSerializer.Save(this);
        if (said == null || said == _wrote) return;

        _wrote = said;
        SetCurrentValue(LayoutProperty, said);
    }

    /// <summary>Takes THIS view as the one <c>Home</c> goes back to.</summary>
    public CanvasCommand RememberViewCommand => _rememberView ??= new CanvasCommand(_ => RememberView());

    private CanvasCommand _rememberView;

    public void RememberView()
    {
        HomeAt = Looking;
        HomeScale = Scale;
        RememberLayout();
    }

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

    /// <summary>Whether the canvas shows its OWN view bar - undo and redo, the zoom, and the way back to the origin.
    /// <para>On by default, and a switch rather than something to assemble: steering the camera and taking back the
    /// last thing done are what a canvas IS, so it arrives wearing them. Turning one of these off takes that panel
    /// away and leaves the rest standing.</para></summary>
    public static readonly AdamantiumProperty ShowsViewBarProperty = AdamantiumProperty.Register(nameof(ShowsViewBar),
        typeof(Boolean), typeof(InfiniteCanvas), new PropertyMetadata(true));

    /// <summary>Whether the canvas shows its own tool rail - one button per entry of <see cref="Tools"/>.</summary>
    public static readonly AdamantiumProperty ShowsToolRailProperty = AdamantiumProperty.Register(nameof(ShowsToolRail),
        typeof(Boolean), typeof(InfiniteCanvas), new PropertyMetadata(true));

    /// <summary>Whether the canvas shows its own map - the whole of what is on the plane, drawn small, with a box round
    /// the part being looked at.</summary>
    public static readonly AdamantiumProperty ShowsMiniMapProperty = AdamantiumProperty.Register(nameof(ShowsMiniMap),
        typeof(Boolean), typeof(InfiniteCanvas), new PropertyMetadata(true));

    /// <summary>Whether the canvas shows its own selection bar - what can be done to what is selected, over the
    /// selection itself.</summary>
    public static readonly AdamantiumProperty ShowsSelectionBarProperty = AdamantiumProperty.Register(
        nameof(ShowsSelectionBar), typeof(Boolean), typeof(InfiniteCanvas), new PropertyMetadata(true));

    /// <summary>Whether the canvas shows its own inspector - the tool in hand and its settings with nothing selected,
    /// the properties of what is selected otherwise, and a list of everything on the plane.</summary>
    public static readonly AdamantiumProperty ShowsInspectorProperty = AdamantiumProperty.Register(
        nameof(ShowsInspector), typeof(Boolean), typeof(InfiniteCanvas), new PropertyMetadata(true));

    /// <summary>Whether the canvas shows its own list of node kinds where a wire is let go over nothing. Off, a wire
    /// dropped on the plane simply comes to nothing - see <see cref="WireDropped"/> for answering it another
    /// way.</summary>
    public static readonly AdamantiumProperty ShowsNodePaletteProperty = AdamantiumProperty.Register(
        nameof(ShowsNodePalette), typeof(Boolean), typeof(InfiniteCanvas), new PropertyMetadata(true));

    /// <summary>Whether the number rows of the inspector carry their up and down buttons. ON, because a number a hand
    /// nudges is what a panel like this is mostly used for - and because a panel that offers them on one row and not
    /// on the next is two panels in one place.
    /// <para>A switch rather than a decision taken in a theme: the buttons cost width, and how much width a panel has
    /// to spare is the application's to say - not something to be argued about again each time a row is added.</para>
    /// </summary>
    public static readonly AdamantiumProperty ShowsNumberButtonsProperty = AdamantiumProperty.Register(
        nameof(ShowsNumberButtons), typeof(Boolean), typeof(InfiniteCanvas), new PropertyMetadata(true));

    /// <summary>Which side of the number those buttons sit on. LEFT by default, against the control's own "one at each
    /// end": the RIGHT end of an inspector line is where the LINE's buttons are - the reset, and the "..." - and the
    /// reset comes and goes, since it is offered only while a value is not the default. A stepper sharing that end
    /// moves out from under the hand between one press and the next, which is felt most by the one thing in a panel
    /// people do several times without looking.
    /// <para>Outward, like the switch above, because it is a matter of taste and of how much room a panel has: both of
    /// those are the application's to say.</para></summary>
    public static readonly AdamantiumProperty NumberButtonsPlacementProperty = AdamantiumProperty.Register(
        nameof(NumberButtonsPlacement), typeof(NumericButtonsPlacement), typeof(InfiniteCanvas),
        new PropertyMetadata(NumericButtonsPlacement.Left));

    /// <summary>When the panel's lines offer the button that puts a value back. ALWAYS by default, dim until there is
    /// something to put back: a button that comes and goes takes its room with it, and a line that changes shape as it
    /// is used moves out from under the hand. An application short of width can have them only where they do something
    /// - or not at all.</summary>
    public static readonly AdamantiumProperty ResetButtonProperty = AdamantiumProperty.Register(nameof(ResetButton),
        typeof(ResetButtonState), typeof(InfiniteCanvas), new PropertyMetadata(ResetButtonState.Always));

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

    /// <summary>WHAT THE PANEL SHOWS FOR THINGS THIS APPLICATION PUTS ON THE PLANE - its own sets of inspector lines,
    /// laid OVER the ones the theme ships.
    /// <para>A set naming a kind the default already covers replaces it; a set naming a new kind is added. So a new
    /// kind of thing costs the lines it is set by and nothing else - no flag on the panel, no edit to a theme. To
    /// replace the lot, declare a resource under the default's own key instead.</para>
    /// <para>The same door for the TOOL page - see <see cref="ToolSections"/>: a new tool that brings settings of its
    /// own has somewhere to put them.</para></summary>
    public static readonly AdamantiumProperty InspectorSectionsProperty = AdamantiumProperty.Register(
        nameof(InspectorSections), typeof(CanvasInspectorSections), typeof(InfiniteCanvas),
        new PropertyMetadata(null));

    public CanvasInspectorSections InspectorSections
    {
        get => GetValue<CanvasInspectorSections>(InspectorSectionsProperty);
        set => SetValue(InspectorSectionsProperty, value);
    }

    /// <summary>The same for the page shown while nothing is selected: what a TOOL is set by. Matched against the
    /// tool's <see cref="ICanvasTool.Name"/>.</summary>
    public static readonly AdamantiumProperty ToolSectionsProperty = AdamantiumProperty.Register(
        nameof(ToolSections), typeof(CanvasInspectorSections), typeof(InfiniteCanvas),
        new PropertyMetadata(null));

    public CanvasInspectorSections ToolSections
    {
        get => GetValue<CanvasInspectorSections>(ToolSectionsProperty);
        set => SetValue(ToolSectionsProperty, value);
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

    /// <summary>The catalogue of node kinds changed - what the palette rebuilds its sections on.</summary>
    public event EventHandler NodeKindsChanged;

    private static void OnNodeKindsChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not InfiniteCanvas canvas) return;

        canvas._graph.SetKinds(e.NewValue as IEnumerable);
        canvas.NodeKindsChanged?.Invoke(canvas, EventArgs.Empty);
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

    /// <summary>The catalogue of socket kinds changed - what the line that names what a socket carries offers.</summary>
    public event EventHandler SocketKindsChanged;

    private static void OnSocketKindsChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not InfiniteCanvas canvas) return;

        canvas._graph.SetSocketKinds(e.NewValue as IEnumerable);
        canvas.SocketKindsChanged?.Invoke(canvas, EventArgs.Empty);
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

    // WHAT CAN BE CHOSEN, for the lines that offer a choice. On the CANVAS rather than on whatever panel is showing
    // them: these are the kinds of thing this plane deals in, they are the same however the panel is dressed, and a
    // set of lines held as a resource can reach them through the canvas it is pointed at. A panel that kept its own
    // copy would be a second list to keep in step with this one.
    private static readonly IReadOnlyList<CanvasGridStyle> Styles =
        [CanvasGridStyle.Dots, CanvasGridStyle.Lines, CanvasGridStyle.None, CanvasGridStyle.Transparent];

    private static readonly IReadOnlyList<CanvasArrowHead> Heads =
        [CanvasArrowHead.None, CanvasArrowHead.Barbs, CanvasArrowHead.Triangle];

    private static readonly IReadOnlyList<CanvasCurve> Curving =
        [CanvasCurve.Bezier, CanvasCurve.BSpline, CanvasCurve.Nurbs];

    // A PICTURE's own catalogues. Every one of them is a brush's property: what is offered is the whole of what the
    // engine can paint a picture with, so a plane is not a poorer place to use a texture than a window is.
    private static readonly IReadOnlyList<Stretch> Filling =
        [Stretch.Fill, Stretch.Uniform, Stretch.UniformToFill, Stretch.None];

    private static readonly IReadOnlyList<TileMode> Tiling =
        [TileMode.None, TileMode.Tile, TileMode.FlipX, TileMode.FlipY, TileMode.FlipXY];

    private static readonly IReadOnlyList<ImageBackgroundState> Grounding =
        [ImageBackgroundState.WhenEmpty, ImageBackgroundState.Always, ImageBackgroundState.Never];

    /// <summary>The grids a plane can wear, for the line that chooses one.</summary>
    public static readonly AdamantiumProperty GridStylesProperty = AdamantiumProperty.Register(nameof(GridStyles),
        typeof(IEnumerable), typeof(InfiniteCanvas), new PropertyMetadata(Styles));

    public IEnumerable GridStyles => GetValue<IEnumerable>(GridStylesProperty);

    /// <summary>The ends an arrow can wear.</summary>
    public static readonly AdamantiumProperty ArrowHeadsProperty = AdamantiumProperty.Register(nameof(ArrowHeads),
        typeof(IEnumerable), typeof(InfiniteCanvas), new PropertyMetadata(Heads));

    public IEnumerable ArrowHeads => GetValue<IEnumerable>(ArrowHeadsProperty);

    /// <summary>The kinds of curve a drawn line can be made into.</summary>
    public static readonly AdamantiumProperty CurvesProperty = AdamantiumProperty.Register(nameof(Curves),
        typeof(IEnumerable), typeof(InfiniteCanvas), new PropertyMetadata(Curving));

    public IEnumerable Curves => GetValue<IEnumerable>(CurvesProperty);

    /// <summary>The ways a picture can fill its tile.</summary>
    public static readonly AdamantiumProperty FillsProperty = AdamantiumProperty.Register(nameof(Fills),
        typeof(IEnumerable), typeof(InfiniteCanvas), new PropertyMetadata(Filling));

    public IEnumerable Fills => GetValue<IEnumerable>(FillsProperty);

    /// <summary>...and the ways that tile can repeat, mirrored or not.</summary>
    public static readonly AdamantiumProperty TilingsProperty = AdamantiumProperty.Register(nameof(Tilings),
        typeof(IEnumerable), typeof(InfiniteCanvas), new PropertyMetadata(Tiling));

    public IEnumerable Tilings => GetValue<IEnumerable>(TilingsProperty);

    /// <summary>When the ground behind a picture is painted.</summary>
    public static readonly AdamantiumProperty GroundsProperty = AdamantiumProperty.Register(nameof(Grounds),
        typeof(IEnumerable), typeof(InfiniteCanvas), new PropertyMetadata(Grounding));

    public IEnumerable Grounds => GetValue<IEnumerable>(GroundsProperty);

    /// <summary>Whether the plane is being used as a DRAWING - <see cref="Mode"/> said as a plain switch, because that
    /// is what a binding can ask. A line offered only on a drawing - snapping, which a graph does none of - says so
    /// itself: <c>IsVisible="{Binding IsDrawing}"</c>, with no help from whatever panel it is written in.</summary>
    public static readonly AdamantiumProperty IsDrawingProperty = AdamantiumProperty.Register(nameof(IsDrawing),
        typeof(Boolean), typeof(InfiniteCanvas), new PropertyMetadata(true));

    public Boolean IsDrawing => GetValue<Boolean>(IsDrawingProperty);

    /// <summary>Raised after <see cref="Mode"/> changes, so a rail can offer the tools that mode admits.</summary>
    public event EventHandler ModeChanged;

    /// <summary>AN INSPECTOR OVER THIS PLANE. Given one, a line written in it repaints the drawing and becomes a step
    /// that can be taken back; given none, nothing here changes.
    /// <para>What a canvas draws is DATA - no property store, no notification - which is exactly what lets a drawing
    /// hold tens of thousands of items. So a value written straight into one is heard by nobody: the model changes and
    /// the picture does not, until something else happens to redraw the plane. And a colour leaves no trace in a
    /// comparison of where things are, so it cannot be undone by the ordinary gesture step either.</para>
    /// <para>THIS WAY ROUND on purpose. The inspector is a general-purpose control and must not learn what a canvas,
    /// a scene or a history are - putting those on it would put them on EVERY inspector in the application. It already
    /// says everything needed, as any control should: it announces a write before and after
    /// (<see cref="PropertyGrid.ValueChanging"/>, <see cref="PropertyGrid.ValueChanged"/>) and will read a value back
    /// when asked. The canvas is the specialised one, so the canvas does the listening.</para></summary>
    public static readonly AdamantiumProperty InspectorProperty = AdamantiumProperty.Register(nameof(Inspector),
        typeof(PropertyGrid), typeof(InfiniteCanvas), new PropertyMetadata(null, OnInspectorChanged));

    public PropertyGrid Inspector
    {
        get => GetValue<PropertyGrid>(InspectorProperty);
        set => SetValue(InspectorProperty, value);
    }

    private static void OnInspectorChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not InfiniteCanvas canvas) return;

        if (e.OldValue is PropertyGrid was)
        {
            was.ValueChanging -= canvas.OnInspectorWriting;
            was.ValueChanged -= canvas.OnInspectorWritten;
        }

        if (e.NewValue is PropertyGrid now)
        {
            now.ValueChanging += canvas.OnInspectorWriting;
            now.ValueChanged += canvas.OnInspectorWritten;
        }
    }

    // What the objects held before the write happening right now. Filled as it starts and turned into a step as it
    // finishes - the previous value exists only in between.
    private readonly List<(object Target, object Was, object Is)> _edited = new();

    private void OnInspectorWriting(object sender, PropertyValuesChangedEventArgs about)
    {
        _edited.Clear();

        if (sender is not PropertyGrid grid || about?.Property == null) return;

        foreach (var target in about.Targets) _edited.Add((target, grid.ValueOf(target, about.Property), null));
    }

    private void OnInspectorWritten(object sender, PropertyValuesChangedEventArgs about)
    {
        // TOLD WHATEVER WAS EDITED, without asking whether it was an item of this plane. The inspector reaches THROUGH
        // an item to the control inside it, and through a node to one of its sockets - and a socket is not an item, so
        // asking left a recoloured socket's wire the old colour. Nothing is saved by asking: this inspector is pointed
        // at this canvas, and being told twice costs one repaint of what is visible.
        //
        // A LAYER NUMBER WRITTEN IN THE PANEL is a request to stand at that place, and this is where it is granted.
        // Asked here rather than by watching one particular row: the panel writes through a binding like any other, and
        // a canvas that had to know which line carried the number would be a canvas a theme could not restate.
        SettleOrder();

        Scene?.Touch();
        Repaint();

        if (History == null || sender is not PropertyGrid grid || about?.Property == null || _edited.Count == 0) return;

        for (var i = 0; i < _edited.Count; i++)
        {
            var (target, was, _) = _edited[i];
            _edited[i] = (target, was, grid.ValueOf(target, about.Property));
        }

        // A COPY: the list is reused by the next write, and a step holding the live one would be rewritten by it.
        History.Push(new CanvasPropertyStep(grid, about.Property, new List<(object, object, object)>(_edited)));

        _edited.Clear();
    }

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

    public Boolean ShowsViewBar
    {
        get => GetValue<Boolean>(ShowsViewBarProperty);
        set => SetValue(ShowsViewBarProperty, value);
    }

    public Boolean ShowsToolRail
    {
        get => GetValue<Boolean>(ShowsToolRailProperty);
        set => SetValue(ShowsToolRailProperty, value);
    }

    public Boolean ShowsMiniMap
    {
        get => GetValue<Boolean>(ShowsMiniMapProperty);
        set => SetValue(ShowsMiniMapProperty, value);
    }

    public Boolean ShowsSelectionBar
    {
        get => GetValue<Boolean>(ShowsSelectionBarProperty);
        set => SetValue(ShowsSelectionBarProperty, value);
    }

    public Boolean ShowsNodePalette
    {
        get => GetValue<Boolean>(ShowsNodePaletteProperty);
        set => SetValue(ShowsNodePaletteProperty, value);
    }

    public Boolean ShowsNumberButtons
    {
        get => GetValue<Boolean>(ShowsNumberButtonsProperty);
        set => SetValue(ShowsNumberButtonsProperty, value);
    }

    public NumericButtonsPlacement NumberButtonsPlacement
    {
        get => GetValue<NumericButtonsPlacement>(NumberButtonsPlacementProperty);
        set => SetValue(NumberButtonsPlacementProperty, value);
    }

    public ResetButtonState ResetButton
    {
        get => GetValue<ResetButtonState>(ResetButtonProperty);
        set => SetValue(ResetButtonProperty, value);
    }

    public Boolean ShowsInspector
    {
        get => GetValue<Boolean>(ShowsInspectorProperty);
        set => SetValue(ShowsInspectorProperty, value);
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
    private CanvasCommand _toFront;
    private CanvasCommand _toBack;
    private CanvasCommand _forward;
    private CanvasCommand _backward;
    private CanvasCommand _align;
    private CanvasCommand _spread;
    private CanvasCommand _comment;
    private CanvasCommand _fitView;
    private CanvasCommand _clear;
    private CanvasCommand _toSvg;
    private CanvasCommand _fromSvg;

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

    /// <summary>Raises what is selected to the front of paint order, and lowers it to the back. Off with nothing
    /// selected.</summary>
    public CanvasCommand BringToFrontCommand =>
        _toFront ??= new CanvasCommand(_ => Reorder(true), _ => _selection.Count > 0 && Scene != null);

    public CanvasCommand SendToBackCommand =>
        _toBack ??= new CanvasCommand(_ => Reorder(false), _ => _selection.Count > 0 && Scene != null);

    /// <summary>Moves what is selected past the next thing IN ITS WAY - the nearest one along paint order whose box
    /// meets it - forward or back.
    /// <para>The pair above only reaches the ends, and everything worth arranging is in the middle: a shape that has
    /// to sit BETWEEN two others cannot be put there by a command that can only put it on top of both.</para>
    /// <para>Past what OVERLAPS and not one place along the list, which is the difference between one press and three
    /// hundred: on a plane of five hundred things the two that cover each other may be the two hundredth and the five
    /// hundredth, and everything between them is somewhere else on the screen. How deep a thing sits is not something
    /// a drawing shows, so a count of presses is not something a person can know.</para>
    /// <para>Off when nothing on that side meets it - which says "there is nothing in front of this", not "this did
    /// not work".</para></summary>
    public CanvasCommand BringForwardCommand =>
        _forward ??= new CanvasCommand(_ => Step(true), _ => Steppable(true));

    public CanvasCommand SendBackwardCommand =>
        _backward ??= new CanvasCommand(_ => Step(false), _ => Steppable(false));

    // FROM THE END IT IS HEADING FOR, so a selection of several keeps its own order: moving the front one first would
    // walk it into the one behind it and the two would swap places instead of both moving.
    private void Step(bool forward)
    {
        if (Scene is not { } scene || _selection.Count == 0) return;

        var shown = Shown();

        for (var i = 0; i < _selection.Count; i++)
        {
            var item = forward ? _selection[_selection.Count - 1 - i] : _selection[i];

            if (Neighbour(shown, item, forward) is { } neighbour) scene.MoveNextTo(item, neighbour, forward);
        }
    }

    // Whether there is still room to go that way. ANY of the selected, because a selection where one has reached the
    // end and the rest have not is still a selection that can move.
    private bool Steppable(bool forward)
    {
        if (Scene == null || _selection.Count == 0) return false;

        var shown = Shown();

        foreach (var item in _selection)
        {
            if (Neighbour(shown, item, forward) != null) return true;
        }

        return false;
    }

    // The next thing that is ACTUALLY IN THE WAY on that side - the nearest one along the order whose box meets this
    // one's.
    //
    // NOT the next one in the list, which is the trap this exists to avoid. On a plane of five hundred things, the two
    // that overlap each other may be the two hundredth and the five hundredth, and a step defined by the list would be
    // three hundred presses to put one over the other - three hundred presses whose count nobody can know in advance,
    // since how deep a thing sits is not something the drawing shows. Every one of those presses would move it past
    // something on the other side of the screen that it never touched.
    //
    // Defined by what OVERLAPS, one press does what the person meant, whatever lies between. And when nothing on that
    // side meets it there is nothing to get past: the button greys, which is the truth - not "this did not work", but
    // "there is nothing in front of this to get above".
    private static ICanvasItem Neighbour(List<ICanvasItem> shown, ICanvasItem item, bool forward)
    {
        var at = shown.IndexOf(item);

        if (at < 0) return null;

        var box = item.Bounds;

        for (var i = at + (forward ? 1 : -1); i >= 0 && i < shown.Count; i += forward ? 1 : -1)
        {
            if (Meets(shown[i].Bounds, box)) return shown[i];
        }

        return null;
    }

    // Touching counts: two things that share an edge are one in front of the other, and a drawing where the top one
    // cannot be raised because it only just touches would be a drawing nobody can arrange.
    private static bool Meets(Rect one, Rect other) =>
        one.X <= other.X + other.Width && other.X <= one.X + one.Width &&
        one.Y <= other.Y + other.Height && other.Y <= one.Y + one.Height;

    /// <summary>Puts the selected items where their own <see cref="ICanvasItem.Order"/> says they should be.
    /// <para>The number an item carries is normally a stamp the scene writes: where it stands, written down. WRITING to
    /// it turns the stamp into a request, and this is what grants it - the disagreement between the number and the
    /// place IS the request. Called for you when the panel writes; public because an application that moves things by
    /// setting numbers rather than by calling the commands needs the same door.</para></summary>
    public void SettleOrder()
    {
        if (Scene is not { } scene || _selection.Count == 0) return;

        // EVERY NUMBER READ BEFORE ANY OF THEM IS ACTED ON. The scene restamps the whole list on every move, so the
        // first item moved rewrites the very numbers the rest of this pass was about to read - and a selection of three
        // would end up obeying two numbers it was never given.
        var asked = new List<(ICanvasItem Item, int Wanted)>(_selection.Count);

        foreach (var item in _selection) asked.Add((item, item.Order));

        foreach (var (item, wanted) in asked)
        {
            if (Placed(item) != wanted) scene.Reposition(item, wanted);
        }
    }

    // Where the item actually stands AMONG WHAT CAN BE SEEN - what the stamp would say if nobody had written over it.
    // Counted the same way the scene writes the number, or every write would look like a request to move.
    private int Placed(ICanvasItem item)
    {
        var at = 0;

        foreach (var other in ItemsHere())
        {
            if (ReferenceEquals(other, item)) return at;

            at++;
        }

        return -1;
    }

    // Everything of this mode, in the order the scene holds it.
    private List<ICanvasItem> Shown()
    {
        var shown = new List<ICanvasItem>();

        foreach (var item in ItemsHere()) shown.Add(item);

        return shown;
    }

    /// <summary>Lines the selection up on one edge - the parameter is which, a <see cref="CanvasAlignment"/> or its
    /// word. ONE command taking a word rather than four: a button says which edge it means, and four properties saying
    /// the same thing four times is a surface nobody can keep in step.</summary>
    public CanvasCommand AlignCommand => _align ??= new CanvasCommand(
        edge => { if (Edge(edge) is { } wanted) Align(wanted); },
        edge => Edge(edge) != null && _selection.Count > 1);

    /// <summary>...and this opens equal gaps between them, down a column or across a row - the parameter is a
    /// <see cref="CanvasSpread"/> or its word.</summary>
    public CanvasCommand SpreadCommand => _spread ??= new CanvasCommand(
        way => { if (Way(way) is { } wanted) Spread(wanted); },
        way => Way(way) != null && _selection.Count > 2);

    /// <summary>Draws a titled frame round what is selected - the parameter is the title, "Comment" when there is
    /// none.</summary>
    public CanvasCommand FrameCommand => _comment ??= new CanvasCommand(
        title => FrameSelection(title as String),
        _ => _selection.Count > 0 && Scene != null);

    /// <summary>Brings the WORK into view: what is selected, and the whole plane when nothing is. One button rather
    /// than two, because that is the one question a person asks of a plane with no edges.</summary>
    public CanvasCommand FitViewCommand => _fitView ??= new CanvasCommand(_ =>
    {
        if (!FitSelection()) FitAll();
    });

    /// <summary>Empties the plane - asking first when the canvas was told to ask, exactly as deleting a selection
    /// does. Off when there is nothing on it.</summary>
    public CanvasCommand ClearCommand =>
        _clear ??= new CanvasCommand(_ => RequestClear(), _ => Anything());

    /// <summary>Writes the drawing out as SVG - what is SELECTED when something is, and everything when nothing is,
    /// which is the same rule the fit button follows.
    /// <para>SVG because a drawing is the half of a canvas other people's tools have a claim on. A graph means
    /// something only here and is kept as JSON; see <see cref="CanvasSvg"/>.</para></summary>
    public CanvasCommand ExportSvgCommand => _toSvg ??= new CanvasCommand(_ => ExportSvg(), _ => Drawn());

    /// <summary>...and reads one in, adding what it holds to the plane rather than replacing it: a drawing opened on
    /// top of nothing is the same thing as opening it, and one opened onto work already there is a person bringing a
    /// piece in from elsewhere.</summary>
    public CanvasCommand ImportSvgCommand => _fromSvg ??= new CanvasCommand(_ => ImportSvg(), _ => Scene != null);

    /// <summary>Raised when a drawing was read but something in it could not be - an arc, a gradient, a foreign
    /// element. The canvas says nothing on its own: what to tell the user, and how, is the application's.</summary>
    public event EventHandler<CanvasSvgReadEventArgs> SvgRead;

    /// <summary>Puts a freshly made item on the plane - what a TOOL calls when a gesture is finished.
    /// <para>ON TOP, because the plane has ONE order and a new thing goes at the top of it. Everything already there
    /// keeps its place; what has just been drawn is over it, including over the controls, which is what drawing on a
    /// picture means. Anything can be moved afterwards - "bring to front" and "send to back" move a control and a
    /// stroke through the same order, so a picture can be raised above a drawing as easily as a drawing over a
    /// picture.</para>
    /// <para>One door rather than six calls to the scene: a tool says what it made, and where that goes is one
    /// decision in one place.</para></summary>
    public void Place(ICanvasItem item)
    {
        if (item == null || Scene == null) return;

        Scene.Add(item);
    }

    private bool Drawn()
    {
        if (Scene == null) return false;

        foreach (var item in ItemsHere())
        {
            if (CanvasSvg.Drawable(item)) return true;
        }

        return false;
    }

    private void ExportSvg()
    {
        if (!FileDialog.IsAvailable) return;

        var chosen = FileDialog.Save(new SaveFileRequest
        {
            Title = "Export the drawing",
            DefaultExtension = "svg",
            FileTypes = SvgFiles,

            // ITS OWN MEMORY, and its own window. The first means this dialog comes back the size it was left and in
            // the folder a drawing was last written to - not wherever some other dialog of this application was. The
            // second means it opens on the screen the canvas is on: a dialog belongs to a window, and one belonging to
            // nothing opens on the main screen, which on two monitors is the wrong one half the time.
            Key = "canvas.svg.export",
            Owner = GetWindow()?.Handle ?? IntPtr.Zero
        });

        if (chosen == null) return;   // cancelled is an answer

        var wanted = new List<ICanvasItem>();

        // WHAT IS SELECTED, or everything: a person who has picked three shapes out of a drawing and pressed export
        // meant those three.
        foreach (var item in _selection.Count > 0 ? _selection : (IEnumerable<ICanvasItem>)ItemsHere())
        {
            if (CanvasSvg.Drawable(item)) wanted.Add(item);
        }

        try
        {
            File.WriteAllText(chosen, CanvasSvg.Save(wanted));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // A file that cannot be written - a full disk, a folder somebody else owns - is a fact about the world.
            SvgRead?.Invoke(this, new CanvasSvgReadEventArgs(0, 0, e.Message));
        }
    }

    private void ImportSvg()
    {
        if (Scene == null || !FileDialog.IsAvailable) return;

        var chosen = FileDialog.Open(new OpenFileRequest
        {
            Title = "Open a drawing",
            FileTypes = SvgFiles,
            Key = "canvas.svg.import",
            Owner = GetWindow()?.Handle ?? IntPtr.Zero
        });

        if (chosen == null) return;

        string text;
        try
        {
            text = File.ReadAllText(chosen);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            SvgRead?.Invoke(this, new CanvasSvgReadEventArgs(0, 0, e.Message));
            return;
        }

        var made = CanvasSvg.Load(text, out var skipped);

        // ONE STEP in the canvas's memory, however many shapes arrived: opening a drawing is one thing a person did,
        // and taking it back should not mean pressing undo four hundred times.
        BeginEdit("Open a drawing");
        try
        {
            foreach (var item in made) Scene.Add(item);

            SelectMany(made, false);
        }
        finally
        {
            EndEdit();
        }

        SvgRead?.Invoke(this, new CanvasSvgReadEventArgs(made.Count, skipped, null));
    }

    private static readonly IReadOnlyList<FileType> SvgFiles =
    [
        new("Drawings", "svg"),
        new("All files", "*")
    ];

    private static CanvasAlignment? Edge(object said) => said switch
    {
        CanvasAlignment edge => edge,
        String word when Enum.TryParse<CanvasAlignment>(word, true, out var edge) => edge,
        _ => null
    };

    private static CanvasSpread? Way(object said) => said switch
    {
        CanvasSpread way => way,
        String word when Enum.TryParse<CanvasSpread>(word, true, out var way) => way,
        _ => null
    };

    // Anything AT ALL on the plane, of either mode: emptying it is not about the mode in hand, and a bin that switched
    // itself off over a plane full of the other mode's work would be saying the plane is empty when it is not.
    private bool Anything()
    {
        if (Scene is not { } scene) return false;

        foreach (var _ in scene.ItemsIn(Everything)) return true;

        return false;
    }

    // BACK TO FRONT when raising and front to back when lowering, so a selection of several keeps its own order instead
    // of being reversed by each item leapfrogging the last.
    private void Reorder(bool front)
    {
        if (Scene is not { } scene || _selection.Count == 0) return;

        for (var i = 0; i < _selection.Count; i++)
        {
            var item = front ? _selection[i] : _selection[_selection.Count - 1 - i];

            if (front) scene.BringToFront(item);
            else scene.SendToBack(item);
        }
    }

    private static Double Factor(object given, Double fallback) =>
        given switch
        {
            Double number when number > 0 => number,
            String text when Double.TryParse(text, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed > 0 => parsed,
            _ => fallback
        };

    // What a command's answer depends on has just moved, so every bound button asks again. EVERY command that answers
    // from the selection or from the plane belongs in this list: one left out is a button that was asked once, while
    // nothing was selected, and stays grey for the rest of the session however much is picked afterwards.
    private void Refresh()
    {
        _undo?.RaiseCanExecuteChanged();
        _redo?.RaiseCanExecuteChanged();
        _fitSelection?.RaiseCanExecuteChanged();
        _delete?.RaiseCanExecuteChanged();
        _group?.RaiseCanExecuteChanged();
        _ungroup?.RaiseCanExecuteChanged();
        _toFront?.RaiseCanExecuteChanged();
        _toBack?.RaiseCanExecuteChanged();
        _forward?.RaiseCanExecuteChanged();
        _backward?.RaiseCanExecuteChanged();
        _align?.RaiseCanExecuteChanged();
        _spread?.RaiseCanExecuteChanged();
        _comment?.RaiseCanExecuteChanged();
        _clear?.RaiseCanExecuteChanged();
        _toSvg?.RaiseCanExecuteChanged();
        _fromSvg?.RaiseCanExecuteChanged();
    }

    private void Selected()
    {
        // A fresh snapshot, not the live list: a binding is told a property CHANGED, and the same list object handed
        // over twice is not a change however different its contents.
        _publishing = true;
        SetCurrentValue(SelectionProperty, _selection.ToArray());
        _publishing = false;

        HasSelection = _selection.Count > 0;

        // The frame stands where the held thing stands, so a new selection is a new place for it - and the runs have
        // not changed, so nothing else would work it out.
        MarkChrome();

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

    private static Object CoerceScale(AdamantiumComponent component, Object value)
    {
        if (component is not InfiniteCanvas canvas || value is not Double scale) return value;

        return Math.Clamp(scale, canvas.MinScale, canvas.MaxScale);
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
        canvas.RememberLayout();
    }

    /// <summary>The tool in hand changed - a rail marks a different button on it.</summary>
    public event EventHandler ToolChanged;

    private static void OnOverlayChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e) =>
        (component as InfiniteCanvas)?.PlaceOverlay();

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

    // Changing what the canvas is being used AS changes everything that follows from it: what can be selected, which
    // tool is in hand, which controls the layer hosts and what is drawn.
    private static void OnModeChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not InfiniteCanvas canvas) return;

        canvas.SetCurrentValue(IsDrawingProperty, canvas.Mode == CanvasMode.Drawing);

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
        canvas.Hosts(host =>
        {
            host.InvalidateMeasure();
            host.InvalidateArrange();
        });
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
        canvas.Hosts(host =>
        {
            host.InvalidateMeasure();
            host.InvalidateArrange();
        });

        canvas.Repaint();
    }

    private void OnSceneEdited(object sender, EventArgs e)
    {
        Forget();
        SyncElements();

        // ...and place them again even when the SET has not changed. An edit here is just as often one item's rectangle
        // moving - which is exactly what dragging a control does - and a layer that only answered to items appearing and
        // disappearing left the control behind while its frame walked off.
        Hosts(host =>
        {
            host.InvalidateMeasure();
            host.InvalidateArrange();
        });

        // The same for a pane that follows the selection: an item moving moves the frame, and the bar sits on the
        // frame. This is the one place every edit passes through - a drag, a resize, an inspector writing a number -
        // so it is the one place that has to say so.
        _chromeLayer?.InvalidateArrange();

        PlaneChanged?.Invoke(this, EventArgs.Empty);

        // ...and the buttons that answer from the PLANE rather than from the selection - the bin is the one - ask
        // again. Emptying a plane that has just been filled must not be refused because it was empty when the button
        // was first bound.
        Refresh();

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

        var there = new HashSet<ICanvasItem>();

        foreach (var item in scene.ItemsIn(Everything)) Gather(item, there);

        var gone = false;

        for (var i = _selection.Count - 1; i >= 0; i--)
        {
            if (there.Contains(_selection[i])) continue;

            _selection.RemoveAt(i);
            gone = true;
        }

        if (gone) Selected();
    }

    // A GROUP'S CHILDREN ARE STILL ON THE PLANE even though the scene does not list them - making a group takes them
    // out of the scene and puts the group in their place. Asking the scene alone whether a selected thing is still
    // there therefore says no about something plainly visible, and the selection would evaporate the moment anything
    // touched the drawing: a colour written in the inspector would stop taking effect until the thing was clicked
    // again, which is exactly what it did.
    private static void Gather(ICanvasItem item, HashSet<ICanvasItem> into)
    {
        if (!into.Add(item) || item is not GroupItem group) return;

        foreach (var child in group.Children) Gather(child, into);
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

        // A SCALE WRITTEN STRAIGHT ZOOMS WHERE YOU ARE LOOKING. Scale alone leaves the world's ORIGIN where it is on
        // screen, so everything else swings round it - type 8000 into the panel and the drawing flies off sideways
        // while the empty middle of the plane fills the window. The wheel and the buttons never showed this because
        // they go through ZoomAt, which holds a point still; a number typed in had nothing holding it.
        if (e.Property == ScaleProperty)
        {
            canvas.SyncThicknessSteps();
            canvas.KeepTheMiddle(e.OldValue as Double? ?? 0);
        }

        // The controls on the plane MOVE with the camera, and moving them is an arrange: what is inside one was measured
        // for its own rectangle, which the camera does not change. A zoom changes that rectangle, so it measures again.
        canvas.SyncElements();
        canvas.Hosts(host =>
        {
            host.InvalidateArrange();
            if (e.Property == ScaleProperty) host.InvalidateMeasure();
        });

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
        Hosts(host => host.ApplyDesignMode());

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

}
