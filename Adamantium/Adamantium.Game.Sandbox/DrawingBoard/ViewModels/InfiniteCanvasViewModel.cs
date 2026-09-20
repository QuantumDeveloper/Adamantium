using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Adamantium.Core.Collections;
using Adamantium.Mathematics;
using Adamantium.Navigation;
using Adamantium.MVVM;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Controls.Buttons;
using Adamantium.Game.Sandbox.ViewModels;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;

namespace Adamantium.Game.Sandbox.DrawingBoard.ViewModels;

/// <summary>Infinite canvas tab: an unbounded plane. The wheel zooms toward the cursor, the middle button - or space
/// and the left one - pans, and neither ever reaches an edge: there is no scrollable extent here at all.
/// <para>The camera's <c>Scale</c> and <c>Offset</c> are bound both ways, so the readout under the canvas is the state
/// itself rather than a copy of it.</para></summary>
[ViewModel]
public partial class InfiniteCanvasViewModel : TabPageViewModel
{
    public InfiniteCanvasViewModel() : base("Infinite canvas")
    {
        // The page counts what it holds - the canvas never tells it, because the canvas does not hold it.
        Scene.Changed += OnSceneChanged;

        // The ORDER is the order the rail shows them in, and the only thing said here about showing them: what each one
        // is called, what it looks like and which key picks it are the tool's own business.
        Tools.Add(SelectTool);
        Tools.Add(PenTool);
        Tools.Add(TextTool);
        Tools.Add(ErasePointTool);
        Tools.Add(EraseStrokeTool);
        Tools.Add(RectangleTool);
        Tools.Add(EllipseTool);
        Tools.Add(LineTool);
        Tools.Add(ArrowTool);
        Tools.Add(CurveTool);
        Tools.Add(PolygonTool);
        Tools.Add(ButtonTool);
        Tools.Add(CheckTool);
        Tools.Add(BoxTool);
        Tools.Add(TextureTool);
        Tools.Add(NodeTool);

        // A GRAPH THAT WORKS, there from the start: two colors and an amount go into a mix, the mix is dimmed, and the
        // output shows what came out. Not behind a switch, because it is the point - a graph that computes something is
        // the difference between this page and a picture of a node editor.
        Seed();

        _graph = new CanvasGraphRunner(Nodes);

        // AND THE DRAWING SIDE, the same way: things on the plane, said AS DATA. The page says what is there and where;
        // the canvas draws a shape from its description and builds a control for anything else, from the template
        // chosen for its type. Nothing here is a scene item and nothing here is a control - a view-model holding either
        // would be a view-model drawing, and what a person set on the plane would then have to be dug back out of the
        // visual tree to save it.
        {
            Put(-300, -90, 200, 48, new SampleButton("Button"));
            Put(-300, 0, 200, 40, new SampleSwitch("Check me"));
            Put(-300, 70, 200, 40, new SampleField("A field"));

            // ON THE ORIGIN, so it stays in the middle at every zoom - the one place a control shows what a zoom does
            // to it without the camera being driven anywhere.
            Put(-100, -24, 200, 48, new SampleButton("Zoom me"));

            // A ROUNDED box with a fat outline: the case where the outline has to eat inwards instead of making
            // the shape bigger.
            Put(-420, 150, 220, 130, new CanvasShapeViewModel(CanvasShape.Rectangle)
            {
                Stroke = new SolidColorBrush(Colors.MediumSpringGreen),
                Thickness = 16,
                Fill = new SolidColorBrush(Color.FromRgba(40, 90, 70, 200)),
                Corner = new Adamantium.ProceduralGeometry.CornerRadius(28, 28, 0, 28)
            });

            Put(-160, 150, 140, 140, new CanvasShapeViewModel(CanvasShape.Polygon)
            {
                Stroke = new SolidColorBrush(Colors.Orange),
                Thickness = 6,
                Fill = new SolidColorBrush(Color.FromRgba(120, 70, 20, 200)),
                Sides = 6
            });

            // NO outline at all: the fill has to keep the whole box, since there is nothing to make room for.
            Put(100, 150, 140, 100, new CanvasShapeViewModel(CanvasShape.Rectangle)
            {
                Stroke = new SolidColorBrush(Colors.Orange),
                Thickness = 0,
                Fill = new SolidColorBrush(Colors.DeepPink)
            });

            // Two arrows, one of each head, so both can be seen against the same line thickness.
            Put(-420, 330, 200, 0, new CanvasShapeViewModel(CanvasShape.Arrow)
            {
                Stroke = new SolidColorBrush(Colors.White),
                Thickness = 3
            });

            // FAT barbs, which is where the corner at the tip either closes or does not.
            Put(-160, 330, 200, 60, new CanvasShapeViewModel(CanvasShape.Arrow)
            {
                Stroke = new SolidColorBrush(Colors.MediumSpringGreen),
                Thickness = 18,
                StartHead = CanvasArrowHead.Barbs,
                EndHead = CanvasArrowHead.Barbs
            });

            // A plain LINE beside them, because a line and a straight ink stroke look alike on screen and behave
            // nothing alike: the line is reshaped by its two ends and wears no frame, the stroke is ink and keeps
            // its box. Without one here there is nothing to tell them apart against.
            Put(100, 330, 220, 70, new CanvasShapeViewModel(CanvasShape.Line)
            {
                Stroke = new SolidColorBrush(Colors.DeepSkyBlue),
                Thickness = 6
            });

        }

        // Opens on SELECT: the one tool that does not put anything down where the first press happens to land.
        Tool = SelectTool;

        // ...unless the last run left something else in hand. LAST, so that what is read back is not overwritten by
        // the line above.
        Layout = Read();
    }

    /// <summary>HOW THE CANVAS WAS LEFT - panels, camera, tool - as the canvas itself writes it down. The application
    /// decides where that text lives; this one keeps it beside the user's own settings.</summary>
    [Bindable] private string _layout;

    private static string Kept => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Adamantium", "canvas-layout.json");

    private static string Read()
    {
        try
        {
            return System.IO.File.Exists(Kept) ? System.IO.File.ReadAllText(Kept) : null;
        }
        catch (System.IO.IOException)
        {
            return null;
        }
    }

    partial void OnLayoutChanged(string value)
    {
        if (string.IsNullOrEmpty(value)) return;

        // A setting nobody asked for must never be what stops the application: a profile on a drive that has gone
        // away is a panel in the wrong place, and nothing worse than that.
        try
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Kept));
            System.IO.File.WriteAllText(Kept, value);
        }
        catch (System.IO.IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void OnSceneChanged(object sender, System.EventArgs e)
    {
        RefreshStructure();
    }

    /// <summary>Which face of the inspector is showing: the list of what is on the plane, or the properties. The two
    /// are one choice, so the toggles that drive them only answer to being turned ON - a radio never unchecks itself,
    /// and pressing the face that is already showing leaves it showing.</summary>
    [Bindable] private bool _showsStructure;

    public bool ShowsProperties
    {
        get => !ShowsStructure;
        set { if (value) ShowsStructure = false; }
    }

    partial void OnShowsStructureChanged(bool value)
    {
        RaisePropertyChanged(nameof(ShowsProperties));
        RaisePropertyChanged(nameof(ToolFace));
        RaisePropertyChanged(nameof(SelectionFace));
        RaisePropertyChanged(nameof(StructureFace));
        RaisePropertyChanged(nameof(Face));
    }

    /// <summary>What is on the plane, TOPMOST FIRST. Reversed from paint order on purpose: the list reads top to
    /// bottom the way the drawing is stacked front to back, which is what every editor's layer list does and what
    /// "bring to front" then means without explanation.
    /// <para>A fresh array every time, not the scene's own list: the same instance handed over twice is not a change,
    /// and a list bound to it would never notice anything.</para></summary>
    public IReadOnlyList<ICanvasItem> Structure { get; private set; } = System.Array.Empty<ICanvasItem>();

    private void RefreshStructure()
    {
        // Of the MODE in hand: the list says what is on the plane, and what the plane is not showing is not on it as
        // far as anyone looking is concerned.
        var items = Scene.Items;
        var listed = new List<ICanvasItem>(items.Count);

        for (var i = items.Count - 1; i >= 0; i--)
        {
            if (items[i].Mode == Mode) listed.Add(items[i]);
        }

        Structure = listed;
        RaisePropertyChanged(nameof(Structure));

        // The row that stands for what is selected follows the selection rather than the other way round here: the
        // list was rebuilt, and a stale row would point at an object that has left the drawing.
        RaisePropertyChanged(nameof(Listed));
    }

    /// <summary>The row the list has picked out. Writing it selects on the plane; reading it follows what the canvas
    /// selected, however that happened - clicking the drawing and clicking the list are one selection, not two.</summary>
    public ICanvasItem Listed
    {
        get => _selection is { Count: 1 } one ? one[0] : null;
        set
        {
            if (value == null || (_selection is { Count: 1 } already && ReferenceEquals(already[0], value))) return;

            Selection = [value];
        }
    }

    public IReadOnlyList<CanvasGridStyle> GridStyles { get; } =
        [CanvasGridStyle.Dots, CanvasGridStyle.Lines, CanvasGridStyle.None, CanvasGridStyle.Transparent];

    public IReadOnlyList<CanvasArrowHead> ArrowHeads { get; } =
        [CanvasArrowHead.None, CanvasArrowHead.Barbs, CanvasArrowHead.Triangle];

    /// <summary>What was done to this drawing. The PAGE's, like the scene - undo belongs to whoever owns the drawing,
    /// and the canvas only says where one step ends and the next begins.</summary>
    public CanvasHistory History { get; } = new();

    // The WHITEBOARD switch and the one that hid the tool panel stood here and are gone. The whiteboard was the grid
    // set to Transparent said a second way, and the inspector's own Grid row says it; the other could not move into
    // the panel it hides, and the panel already folds by its grip. See the note at the top of the view.

    /// <summary>What is drawn on the plane. The PAGE holds it, not the canvas - undo, saving and everything else a
    /// drawing is for belong to whoever owns the drawing, which is why the control only ever asks what is visible.
    /// </summary>
    public CanvasScene Scene { get; } = new();

    /// <summary>The tools, made once and kept. A tool holds the gesture it is halfway through, so a fresh one on every
    /// click of a tool button would be a tool that forgets what it was doing.</summary>
    // The wire refuses in RED while it is being pulled, so "this socket will not take it" is answered before the button
    // is let go rather than by the wire quietly not appearing.
    public ICanvasTool SelectTool { get; } =
        new SelectTool { Wires = { RefusedStroke = new SolidColorBrush(Colors.OrangeRed) } };

    public ICanvasTool PenTool { get; } = new PenTool();

    public ICanvasTool RectangleTool { get; } = new ShapeTool(CanvasShape.Rectangle);

    public ICanvasTool EllipseTool { get; } = new ShapeTool(CanvasShape.Ellipse);

    public ICanvasTool LineTool { get; } = new ShapeTool(CanvasShape.Line);

    /// <summary>One tool for every regular polygon - how many sides is a setting of the tool, not a tool of its
    /// own.</summary>
    public ICanvasTool PolygonTool { get; } = new ShapeTool(CanvasShape.Polygon);

    /// <summary>An arrow. Which head each end wears is a setting, so an application wanting a double-headed one beside
    /// this builds a second of the same class.</summary>
    public ICanvasTool ArrowTool { get; } = new ShapeTool(CanvasShape.Arrow);

    /// <summary>A curve. ONE button for all three kinds - which one suits a line is decided by looking at it, so the
    /// kind is changed in the panel afterwards rather than chosen in advance.</summary>
    public ICanvasTool CurveTool { get; } = new CurveTool { Name = "Curve" };

    public ICanvasTool TextTool { get; } = new TextTool();


    /// <summary>Two erasers and not one with a switch: which of them you want is the same kind of choice as which tool
    /// you want, so it is made in the same place and in the same way.</summary>
    public ICanvasTool ErasePointTool { get; } = new EraseTool(CanvasEraseMode.Point);

    public ICanvasTool EraseStrokeTool { get; } = new EraseTool(CanvasEraseMode.Stroke);

    /// <summary>The toolbox: each entry is the same tool with a different factory, because what a control IS belongs to
    /// the application and not to the canvas. A NEW control every time it is used - one instance handed out twice would
    /// be one control that cannot be in two places.</summary>
    // NAMED and PICTURED here and nowhere else. A control tool has no look of its own - what control it puts down is
    // this application's choice - so the application is what says which of the theme's pictures fits.
    public ICanvasTool ButtonTool { get; } =
        new ElementTool(() => new Button { Content = "Button" })
        { Name = "Button", Icon = "ToolButtonIcon", Description = "drag out a button" };

    public ICanvasTool CheckTool { get; } =
        new ElementTool(() => new CheckBox { Content = "Check me" }, new Size(130, 28))
        { Name = "Check box", Icon = "ToolCheckBoxIcon", Description = "drag out a checkbox" };

    public ICanvasTool BoxTool { get; } =
        new ElementTool(() => new TextBox { Text = "Editable" }, new Size(160, 30))
        { Name = "Text box", Icon = "ToolTextBoxIcon", Description = "drag out a text box" };

    /// <summary>A TEXTURE - a surface to paint a picture on. The engine's own tool, not one of this page's: what it
    /// puts down is a plain fill and the file is chosen afterwards in the panel, so there is nothing here to
    /// decide.</summary>
    public ICanvasTool TextureTool { get; } = new TextureTool();

    /// <summary>A NODE of a graph. The same tool as the three above and not a mechanism of its own: a node IS a
    /// control, which was the point of building it as one - so putting it on the plane needs nothing the canvas did not
    /// already have.</summary>
    /// <summary>The one tool of the GRAPH, and the only one of these that says so: a rail offers what its canvas's mode
    /// admits, and a node has no business in a drawing any more than a pen has in a graph.
    /// <para>It does not MAKE anything: a press asks which kind, out of the list this page gave the canvas, and the
    /// pick puts the node into this page's collection. Which kind is the one thing a tool cannot know.</para></summary>
    public ICanvasTool NodeTool { get; } = new NodeTool { Description = "put a node on the plane" };

    /// <summary>The tools the canvas offers, in the order its rail shows them. THE list - there is no second copy of
    /// it in the markup, because everything a button needs (the name, the picture, the key) is a fact about the tool.
    /// <para>What this replaced: eleven commands, eleven mirror flags and a hand-written grid of toggle buttons, all
    /// kept in step by hand. Adding a tool was six edits, five of them about showing it; it is now one line here.
    /// </para></summary>
    public CanvasTools Tools { get; } = new();

    [Bindable] private ICanvasTool _tool;

    partial void OnToolChanged(ICanvasTool value)
    {
        RaisePropertyChanged(nameof(PenSettings));
        RaisePropertyChanged(nameof(EraserSettings));
        RaisePropertyChanged(nameof(TextSettings));
        RaisePropertyChanged(nameof(ShapeSettings));
        RaisePropertyChanged(nameof(Face));
    }

    // Which settings the FIRST face shows - the ones belonging to the tool in hand. The same shape of answer the
    // selection sections give, so the two faces of the inspector are one panel changing its contents and not two
    // panels taking turns.
    public Visibility PenSettings => _tool is PenTool ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EraserSettings => _tool is EraseTool ? Visibility.Visible : Visibility.Collapsed;

    public Visibility TextSettings => _tool is TextTool ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ShapeSettings => _tool is ShapeTool ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>A caption over the rows, and NULL whenever it would only repeat them. The sections already name what
    /// they hold - "Pen", "Control" - so a caption saying the same word above them is noise; several objects at once
    /// is the one case the sections cannot state, because then there are several of them.</summary>
    public string Face =>
        ShowsProperties && _selection is { Count: > 1 } many ? $"{many.Count} objects selected" : null;

    /// <summary>Whether a press on a control on the plane MOVES it or presses it.</summary>
    /// <summary>What the canvas is being used AS. Two-way to the canvas, and the pair of toggles that drive it are one
    /// choice - the same shape as the inspector's faces.</summary>
    [Bindable] private CanvasMode _mode = CanvasMode.Drawing;

    public bool IsDrawing
    {
        get => Mode == CanvasMode.Drawing;
        set { if (value) Mode = CanvasMode.Drawing; }
    }

    public bool IsGraph
    {
        get => Mode == CanvasMode.Nodes;
        set { if (value) Mode = CanvasMode.Nodes; }
    }

    partial void OnModeChanged(CanvasMode value)
    {
        RaisePropertyChanged(nameof(IsDrawing));
        RaisePropertyChanged(nameof(IsGraph));
        RaisePropertyChanged(nameof(GraphFace));
        RaisePropertyChanged(nameof(DrawingFace));
        RefreshStructure();
    }

    /// <summary>The commands that are about a GRAPH and nothing else - saving it, loading it - shown only while one is
    /// what the canvas is holding.</summary>
    public Visibility GraphFace => Mode == CanvasMode.Nodes ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>What belongs to a DRAWING and to nothing else - the color of a thing, its place in paint order, and
    /// grouping. In a graph all three mean nothing: a node's color is its accent and lives in the inspector, nodes are
    /// all in one band so their order is not something anybody reasons about, and the thing that groups a graph is a
    /// comment frame.</summary>
    public Visibility DrawingFace => Mode == CanvasMode.Drawing ? Visibility.Visible : Visibility.Collapsed;

    [Bindable] private bool _designMode = true;

    [Bindable] private Color _inkColor = Colors.White;

    /// <summary>What the pen paints with. A NEW brush on every change, never the same one repainted: a finished stroke
    /// keeps the brush it was drawn with, so repainting it would go back and recolor everything already on the plane.
    /// </summary>
    public Brush Ink { get; private set; } = new SolidColorBrush(Colors.White);

    partial void OnInkColorChanged(Color value)
    {
        Ink = new SolidColorBrush(value);
        RaisePropertyChanged(nameof(Ink));
    }

    [Bindable] private Color _fillColor = Colors.SteelBlue;

    public Brush ShapeFill { get; private set; } = new SolidColorBrush(Colors.SteelBlue);

    partial void OnFillColorChanged(Color value)
    {
        ShapeFill = new SolidColorBrush(value);
        RaisePropertyChanged(nameof(ShapeFill));
    }

    /// <summary>Whether a new shape is filled at all. Off, a rectangle is a frame - and a frame is what you can press
    /// through, which is the difference a hit test makes.</summary>
    [Bindable] private bool _filled = true;

    partial void OnFilledChanged(bool value) => RaisePropertyChanged(nameof(ShapeFillOrNothing));

    public Brush ShapeFillOrNothing => _filled ? ShapeFill : null;

    [Bindable] private double _inkThickness = 2;

    [Bindable] private double _textSize = 16;

    [Bindable] private double _eraserSize = 18;

    [Bindable] private bool _snapToGrid;

    /// <summary>Whether the plate with the coordinates rides with the pointer while the grid is pulling.</summary>
    [Bindable] private bool _showReadout = true;

    /// <summary>How far in the camera may go. A practical limit and not a technical one - see the canvas's own note on
    /// MaxScale - so it belongs in the panel where the rest of the surface is set.</summary>
    [Bindable] private double _maxZoom = 65536;

    [Bindable] private Color _snapColor = Colors.DodgerBlue;

    public Brush SnapMark { get; private set; } = new SolidColorBrush(Colors.DodgerBlue);

    partial void OnSnapColorChanged(Color value)
    {
        SnapMark = new SolidColorBrush(value);
        RaisePropertyChanged(nameof(SnapMark));
    }

    [Bindable] private double _snapMarkSize = 9;

    /// <summary>Whether ONE object is worth a question before it goes. A switch, because with undo in place being asked
    /// every time is noise - and which of the two a drawing wants is not knowable in advance. Clearing the WHOLE scene
    /// always asks: there is no gesture that does it by accident, so being asked is never a surprise, and there is a lot
    /// on the other side of the answer.</summary>
    [Bindable] private bool _asksBeforeDelete = true;

    [Command]
    private async Task Erase()
    {
        if (Scene.Items.Count == 0) return;

        // Resolved here rather than taken in the constructor: this page is built with `new` by the gallery, so there is
        // nothing to inject it through, and threading a service through the gallery to reach one dialog is worse than
        // asking the application that owns both.
        var dialogs = Adamantium.UI.UIApplication.Current?.Container?.Resolve<IDialogService>();

        if (dialogs != null)
        {
            var result = await dialogs.ShowDialogAsync<ConfirmDialogViewModel>(new NavigationParameters()
                .Add("title", "Clear the canvas")
                .Add("message", $"Remove all {Scene.Items.Count} objects?"));

            if (result.Result != DialogButtonResult.Ok) return;
        }

        // Through the HISTORY: clearing a drawing is the one action most worth being able to take back.
        History.Record(Scene, "Clear", Scene.Clear);
        Selection = System.Array.Empty<ICanvasItem>();
    }

    [Bindable] private CanvasGridStyle _gridStyle = CanvasGridStyle.Dots;

    [Bindable] private double _scale = 1;

    [Bindable] private Vector2 _offset;

    /// <summary>The zoom for the view bar - a MULTIPLIER from one, not a percentage.
    /// <para>Everything else about the camera is stated that way: the limits are 0.01 and 1024, the readout under the
    /// canvas says "0.4x", and the panel's own row is called Max zoom. A percentage beside them is a second unit for
    /// one quantity, and at the far end it stops reading at all - 0.01 shown as "1%" looks like a hundredth of a
    /// percent rather than a hundredth of full size.</para></summary>
    public string ZoomText => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.###}x", _scale);

    /// <summary>What the canvas has selected, taken straight off it. The inspector reads THIS and not a copy: the
    /// canvas publishes a fresh snapshot on every change, which is what lets a binding notice.</summary>
    [Bindable] private IReadOnlyList<ICanvasItem> _selection;

    partial void OnSelectionChanged(IReadOnlyList<ICanvasItem> value)
    {
        RaisePropertyChanged(nameof(ToolFace));
        RaisePropertyChanged(nameof(SelectionFace));
        RaisePropertyChanged(nameof(StrokeSection));
        RaisePropertyChanged(nameof(ShapeSection));
        RaisePropertyChanged(nameof(TextSection));
        RaisePropertyChanged(nameof(ElementSection));
        RaisePropertyChanged(nameof(GroupSection));
        RaisePropertyChanged(nameof(CurveSection));
        RaisePropertyChanged(nameof(FrameSection));
        RaisePropertyChanged(nameof(NodeSection));
        RaisePropertyChanged(nameof(HasPlainLabel));
        RaisePropertyChanged(nameof(IsNurbs));
        RaisePropertyChanged(nameof(IsBezier));
        RaisePropertyChanged(nameof(HasCorners));
        RaisePropertyChanged(nameof(HasSides));
        RaisePropertyChanged(nameof(HasHeads));
        RaisePropertyChanged(nameof(Listed));
        RaisePropertyChanged(nameof(Face));
    }

    // The inspector has ONE place and several faces. Which one is on is partly the user's - properties or the list of
    // what is on the plane - and partly the drawing's: on properties, with nothing selected it shows what you are
    // working WITH, and with something selected what you are working ON.
    //
    // ONE panel and not several, because a second panel beside the first is a second panel to move, to fold and to
    // find room for - and folded it is a pair of buttons over the drawing that say nothing about what is behind them.
    public Visibility ToolFace => ShowsProperties && !Anything ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SelectionFace =>
        ShowsProperties && Anything && Describable ? Visibility.Visible : Visibility.Collapsed;

    public Visibility StructureFace => ShowsStructure ? Visibility.Visible : Visibility.Collapsed;

    // A section per KIND, shown when the selection holds one. Several kinds at once show several sections, which is
    // what a mixed selection honestly is.
    public Visibility StrokeSection => Shown<StrokeItem>();

    public Visibility ShapeSection => Shown<ShapeItem>();

    public Visibility TextSection => Shown<TextItem>();

    public Visibility ElementSection => Shown<ElementItem>();

    /// <summary>The node's OWN lines, under the ones every control has. A node is an ElementItem like a button is, so
    /// the section above already gives it a place and a size; what this adds is the handful of things that make it a
    /// node - what it is called, how many sockets down each side, and the two colors it is read by.</summary>
    public Visibility NodeSection => ShownElement<CanvasNode>();

    /// <summary>Whether the generic "what the control says" line belongs in the panel at all. A node says it in its own
    /// Title line, and the same name written on two lines is a panel that contradicts itself the moment one of them is
    /// edited - which it did, showing the old name above the new one.</summary>
    public bool HasPlainLabel
    {
        get
        {
            if (_selection == null) return false;

            foreach (var item in _selection)
            {
                if (item is ElementItem and not ElementItem { Element: CanvasNode }) return true;
            }

            return false;
        }
    }

    public Visibility GroupSection => Shown<GroupItem>();

    public Visibility CurveSection => Shown<CurveItem>();

    public Visibility FrameSection => Shown<CanvasFrameItem>();

    public IReadOnlyList<CanvasCurve> Curves { get; } =
        [CanvasCurve.Bezier, CanvasCurve.BSpline, CanvasCurve.Nurbs];

    /// <summary>Evenness belongs to a NURBS and to nothing else - the other two curves would show a line that does
    /// nothing.</summary>
    public bool IsNurbs => AnyCurve(CanvasCurve.Nurbs);

    /// <summary>Whether the order is worth showing: a Bezier has one that follows from its points, and the other two
    /// kinds say what they are some other way.</summary>
    public bool IsBezier => AnyCurve(CanvasCurve.Bezier);

    private bool AnyCurve(CanvasCurve kind)
    {
        if (_selection == null) return false;

        foreach (var item in _selection)
        {
            if (item is CurveItem curve && curve.Kind == kind) return true;
        }

        return false;
    }

    // Within the one Shape section, the lines that belong to ONE shape. Rounding is a rectangle's and sides are a
    // polygon's; an ellipse has neither. A line that means nothing to what is selected is worse than no line, because
    // it invites a number that will quietly do nothing.
    public bool HasCorners => HasShape(CanvasShape.Rectangle);

    public bool HasSides => HasShape(CanvasShape.Polygon);

    public bool HasHeads => HasShape(CanvasShape.Arrow);

    /// <summary>Puts one more socket on the selected node, down the side whose plus was pressed. It carries no kind:
    /// what flows through it is the next thing to say, and the row below says it.</summary>
    [Command]
    private void AddSocket(object side)
    {
        if (_selection == null) return;

        foreach (var item in _selection)
        {
            if (item is not ElementItem { Model: ICanvasNode node }) continue;

            var input = (side as string) != Models.GraphWords.Sides.Out;
            var sockets = input ? node.Inputs : node.Outputs;
            var prefix = input ? Models.GraphWords.Sides.In : Models.GraphWords.Sides.Out;

            sockets.Add(new CanvasSocketViewModel { Name = $"{prefix} {sockets.Count + 1}" });
            return;
        }
    }

    /// <summary>Takes one socket off the node holding it. The row's button hands over the socket and nothing else, so
    /// which node it belongs to is asked of the selection - the only nodes on screen whose sockets are being shown.
    /// <para>ASKS FIRST, under the same switch as deleting anything else on the plane. Dropping a socket takes its name,
    /// its color and whatever is wired to it with it, there is no undo reaching in here yet, and the button sits one
    /// row away from the one that renames it.</para></summary>
    [Command]
    private async Task RemoveSocket(object which)
    {
        if (which is not ICanvasSocket socket || _selection == null) return;

        ICanvasNode holder = null;
        foreach (var item in _selection)
        {
            if (item is not ElementItem { Model: ICanvasNode node }) continue;
            if (!node.Inputs.Contains(socket) && !node.Outputs.Contains(socket)) continue;

            holder = node;
            break;
        }

        if (holder == null) return;

        if (AsksBeforeDelete)
        {
            var dialogs = Adamantium.UI.UIApplication.Current?.Container?.Resolve<IDialogService>();
            if (dialogs != null)
            {
                var result = await dialogs.ShowDialogAsync<ConfirmDialogViewModel>(new NavigationParameters()
                    .Add("title", "Remove socket")
                    .Add("message", $"Remove \"{socket.Name}\" from {holder.Title}?"));

                if (result.Result != DialogButtonResult.Ok) return;
            }
        }

        if (!holder.Inputs.Remove(socket)) holder.Outputs.Remove(socket);
    }

    /// <summary>THE DRAWING: this page's own things on the plane - where each is and what it is. The canvas draws a
    /// shape from its description and builds a control for anything else; nothing here is a scene item and nothing here
    /// is a control.
    /// <para>The same bargain as <see cref="Nodes"/>, on the other half of the canvas.</para></summary>
    public TrackingCollection<ICanvasObject> Objects { get; } = new();

    // One thing on the plane: where it goes and what it is.
    private void Put(double left, double top, double width, double height, object what) =>
        Objects.Add(new CanvasObjectViewModel
        {
            Left = left,
            Top = top,
            Width = width,
            Height = height,
            Content = what
        });

    /// <summary>THE GRAPH: this page's own objects. The canvas shows them and writes into them - a node made on the
    /// plane arrives here, one deleted leaves - so this is the whole account of the graph and there is no second copy
    /// of it anywhere.</summary>
    public TrackingCollection<ICanvasNode> Nodes { get; } = new();

    /// <summary>The kinds this page offers. Given to the canvas, so the palette, the inspector's drop-down and the
    /// loader all read the one list - and each entry knows how to make the inside of a node of its sort, which is why
    /// the canvas needs no factory handed to it.</summary>
    public IReadOnlyList<ICanvasNodeKind> NodeKinds => NodeSet.Kinds;

    /// <summary>WHICH CATALOGUE this page is holding. A page doing two jobs holds two of these and binds the one it
    /// wants - the canvas is handed a list of kinds and knows nothing about sets; a saved graph records the name.
    /// </summary>
    public Models.GraphNodeSet NodeSet { get; } = Models.GraphNodeKind.Set;

    private ICanvasNode Node(string kind, double left, double top)
    {
        var entry = NodeSet.Named(kind);

        var made = new CanvasNodeViewModel
        {
            Kind = kind,
            Title = entry?.Title ?? kind,
            Left = left,
            Top = top,
            Accent = entry?.Accent,
            Specialization = entry?.Create()
        };

        Nodes.Add(made);

        return made;
    }

    // WHAT KEEPS THE GRAPH WORKED OUT. One line, because none of it is this page's business: which node is owed a
    // value, in what order they go, when a pass is worth starting and when the one running is already answering a
    // question nobody is asking any more. All this page says is what a node computes - see NodeSpecialization.
    private readonly CanvasGraphRunner _graph;
    // THE GRAPH THE PAGE OPENS WITH: two colors mixed by an amount, the mix dimmed, and the result shown. Small enough
    // to take in at a glance and complete enough to be worth changing - which is what a demonstration has to be.
    private void Seed()
    {
        var warm = Node(Models.GraphWords.Kinds.Color, -520, 40);
        var cool = Node(Models.GraphWords.Kinds.Color, -520, 170);
        var amount = Node(Models.GraphWords.Kinds.Number, -520, 300);
        var mix = Node(Models.GraphWords.Kinds.Mix, -260, 120);
        var dim = Node(Models.GraphWords.Kinds.Number, -260, 320);
        var scale = Node(Models.GraphWords.Kinds.Scale, -20, 160);
        var output = Node(Models.GraphWords.Kinds.Output, 240, 160);

        if (warm.Specialization is ColorSpecialization first)
        {
            first.Color = Colors.OrangeRed;
        }

        if (cool.Specialization is ColorSpecialization second)
        {
            second.Color = Colors.DeepSkyBlue;
        }

        if (dim.Specialization is NumberSpecialization brightness)
        {
            brightness.Value = 1;
        }

        _ = new CanvasConnection(warm.Outputs[0], mix.Inputs[0]);
        _ = new CanvasConnection(cool.Outputs[0], mix.Inputs[1]);
        _ = new CanvasConnection(amount.Outputs[0], mix.Inputs[2]);
        _ = new CanvasConnection(mix.Outputs[0], scale.Inputs[0]);
        _ = new CanvasConnection(dim.Outputs[0], scale.Inputs[1]);
        _ = new CanvasConnection(scale.Outputs[0], output.Inputs[0]);
    }

    /// <summary>What a SOCKET may carry. Offered as a list for the same reason the node's kind is: two sockets are
    /// joined when their kinds agree, so a mistyped one is a wire that silently refuses to go on. The empty entry is
    /// first and means "anything", which is what a socket that has not been told says.</summary>
    public IReadOnlyList<ICanvasSocketKind> SocketKinds { get; } = Models.GraphSocketKind.All;

    /// <summary>What has been typed into the list of kinds. The list itself, when it is open and where, is the
    /// canvas's - see its PaletteFace and PickedKind; this is only the narrowing.</summary>
    [Bindable] private string _nodeSearch = string.Empty;

    partial void OnNodeSearchChanged(string value)
    {
        RaisePropertyChanged(nameof(FoundKinds));
        RaisePropertyChanged(nameof(FoundGroups));
        RaisePropertyChanged(nameof(SearchFace));
    }

    /// <summary>The palette's sections, each holding its kinds - what the list actually shows. Built from the kinds the
    /// search leaves, so a section with nothing left in it simply is not there.</summary>
    public IReadOnlyList<Models.GraphKindGroup> FoundGroups
    {
        get
        {
            var groups = new List<Models.GraphKindGroup>();
            var byName = new Dictionary<string, List<ICanvasNodeKind>>();

            foreach (var kind in FoundKinds)
            {
                var name = string.IsNullOrEmpty(kind.Group) ? "Other" : kind.Group;

                if (byName.TryGetValue(name, out var family))
                {
                    family.Add(kind);
                    continue;
                }

                byName[name] = [kind];
                groups.Add(new Models.GraphKindGroup(name, byName[name], true));
            }

            return groups;
        }
    }

    /// <summary>The cross that empties the search, shown only while there is something to empty: a cross on an empty
    /// field offers to undo nothing.</summary>
    public Visibility SearchFace =>
        string.IsNullOrEmpty(NodeSearch) ? Visibility.Collapsed : Visibility.Visible;

    [Command]
    private void ClearNodeSearch() => NodeSearch = string.Empty;

    /// <summary>The kinds the search leaves. Everything when nothing has been typed, which is what a list opened by a
    /// gesture should show first - the search is for narrowing, not for making the list appear.</summary>
    public IReadOnlyList<ICanvasNodeKind> FoundKinds
    {
        get
        {
            var found = new List<ICanvasNodeKind>();
            foreach (var kind in NodeKinds)
            {
                if (string.IsNullOrWhiteSpace(NodeSearch)
                    || kind.Title.Contains(NodeSearch, System.StringComparison.OrdinalIgnoreCase))
                {
                    found.Add(kind);
                }
            }

            // BY SECTION, then by name inside it. The list is read top to bottom by somebody looking for a sort of node,
            // and sorts of node are what the sections are.
            found.Sort((left, right) =>
            {
                var section = string.CompareOrdinal(left.Group, right.Group);

                return section != 0 ? section : string.CompareOrdinal(left.Title, right.Title);
            });

            return found;
        }
    }

    /// <summary>Shuts the list without choosing. Closing is the canvas's state, the narrowing is the page's, so this
    /// clears both.</summary>
    [Command]
    private void CloseNodes(object which)
    {
        if (which is InfiniteCanvas canvas) canvas.IsPaletteOpen = false;

        NodeSearch = string.Empty;
    }

    /// <summary>Where the graph is kept between runs.</summary>
    public string GraphPath { get; set; } =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "adamantium-graph.json");

    /// <summary>What the last save or load did, in words, so the stand says something rather than appearing to do
    /// nothing.</summary>
    [Bindable] private string _graphStatus = "not saved yet";

    /// <summary>Draws a comment frame round what is selected. The canvas makes it; what it is CALLED is the page's, and
    /// this one starts with a word a person will replace at once - which is better than an empty strip that looks
    /// broken.</summary>
    [Command]
    private void FrameSelection(object which)
    {
        if (which is InfiniteCanvas canvas) canvas.FrameSelection("Comment");
    }

    // LINING UP and SPREADING OUT, one command each and the canvas handed in as the parameter - the same shape as
    // saving and loading above. One command taking a word would need a second parameter to say WHICH canvas, and a
    // command takes one.
    [Command]
    private void AlignLeft(object which) => Do(which, c => c.Align(CanvasAlignment.Left));

    [Command]
    private void AlignMiddle(object which) => Do(which, c => c.Align(CanvasAlignment.HorizontalCenter));

    [Command]
    private void AlignRight(object which) => Do(which, c => c.Align(CanvasAlignment.Right));

    [Command]
    private void AlignTop(object which) => Do(which, c => c.Align(CanvasAlignment.Top));

    [Command]
    private void SpreadColumns(object which) => Do(which, c => c.Spread(CanvasSpread.Horizontal));

    [Command]
    private void SpreadRows(object which) => Do(which, c => c.Spread(CanvasSpread.Vertical));

    /// <summary>Brings the work into view: what is selected, or the whole graph when nothing is.</summary>
    [Command]
    private void FitView(object which) => Do(which, c => { if (!c.FitSelection()) c.FitAll(); });

    private static void Do(object which, System.Action<InfiniteCanvas> what)
    {
        if (which is InfiniteCanvas canvas) what(canvas);
    }

    // The CANVAS is handed in by the view. The page does not hold one - a view-model that reached for a control would
    // be a view-model that has to be given one before it can answer anything - and the button that saves is standing
    // next to it anyway.
    [Command]
    private void SaveGraph(object which)
    {
        if (which is not InfiniteCanvas canvas) return;

        var path = Asked(FileDialog.Save(new SaveFileRequest
        {
            Title = "Save the graph",
            FileName = System.IO.Path.GetFileName(GraphPath),
            DefaultExtension = "json",
            FileTypes = GraphFiles,
            Key = "sandbox.graph.save",
            Owner = canvas.GetWindow()?.Handle ?? IntPtr.Zero
        }));

        if (path == null) return;

        try
        {
            System.IO.File.WriteAllText(path, Models.GraphFile.Write(Nodes, NodeSet));
            GraphPath = path;
            GraphStatus = $"saved to {path}";
        }
        catch (System.IO.IOException e)
        {
            GraphStatus = $"could not save: {e.Message}";
        }
    }

    [Command]
    private void LoadGraph(object which)
    {
        if (which is not InfiniteCanvas canvas) return;

        var path = Asked(FileDialog.Open(new OpenFileRequest
        {
            Title = "Open a graph",
            FileTypes = GraphFiles,
            Key = "sandbox.graph.open",
            Owner = canvas.GetWindow()?.Handle ?? IntPtr.Zero
        }));

        if (path == null) return;

        try
        {
            if (!System.IO.File.Exists(path))
            {
                GraphStatus = "there is no such file";
                return;
            }

            GraphPath = path;
            GraphStatus = Models.GraphFile.Read(System.IO.File.ReadAllText(path), Nodes, NodeSet)
                          ?? $"loaded from {path}";

            canvas.FitAll();
        }
        catch (System.IO.IOException e)
        {
            GraphStatus = $"could not load: {e.Message}";
        }
    }

    private static readonly IReadOnlyList<FileType> GraphFiles = [new FileType("Graph", "json")];

    // A dialog that cannot be shown is not a cancelled one, and the difference matters: cancelling is the user's
    // answer and needs no report, while a platform with no dialog would leave the button doing nothing at all. Then
    // the path that was remembered stands in for the question.
    private string Asked(string chosen)
    {
        if (chosen != null) return chosen;
        if (FileDialog.IsAvailable) return null;

        GraphStatus = "there is nowhere to ask, so the remembered file is used";
        return GraphPath;
    }

    // The CONTEXT BAR's actions. Actions and not properties: what a thing looks like is the inspector's business and
    // saying it twice would be two places to keep in step - what belongs beside the selection is what you do to it.
    [Command] private void BringToFront() => Reorder(front: true);

    [Command] private void SendToBack() => Reorder(front: false);

    // Deleting the selection is NOT here any more: it goes through the canvas, which asks first, so that a button and
    // the Delete key meet the same question. See CanvasViewBehavior with Does="Delete".

    // Back to front when raising and front to back when lowering, so a group keeps its own order instead of being
    // reversed by each item leapfrogging the last.
    private void Reorder(bool front)
    {
        if (_selection is not { Count: > 0 } chosen) return;

        for (var i = 0; i < chosen.Count; i++)
        {
            var item = front ? chosen[i] : chosen[chosen.Count - 1 - i];

            if (front) Scene.BringToFront(item);
            else Scene.SendToBack(item);
        }
    }

    /// <summary>The color of what is selected, for the bar beside it: the first one's, and writing it paints them all.
    /// <para>Every kind of item keeps its color under its own name - a stroke and a shape do not agree - so the answer
    /// is given here rather than asked of a common property none of them has.</para></summary>
    public Color SelectionColor
    {
        get => _selection is { Count: > 0 } chosen && ColorOf(chosen[0]) is { } color ? color : _inkColor;
        set
        {
            if (_selection is not { Count: > 0 } chosen) return;

            var brush = new SolidColorBrush(value);
            foreach (var item in chosen) Paint(item, brush);

            Scene.Touch();
            RaisePropertyChanged(nameof(SelectionColor));
        }
    }

    private static Color? ColorOf(ICanvasItem item) => item switch
    {
        StrokeItem stroke => (stroke.Brush as SolidColorBrush)?.Color,
        ShapeItem shape => (shape.Stroke as SolidColorBrush)?.Color,
        TextItem text => (text.Brush as SolidColorBrush)?.Color,
        _ => null
    };

    private static void Paint(ICanvasItem item, Brush brush)
    {
        switch (item)
        {
            case StrokeItem stroke:
                stroke.Brush = brush;
                break;

            case ShapeItem shape:
                shape.Stroke = brush;
                break;

            case TextItem text:
                text.Brush = brush;
                break;
        }
    }

    private bool Anything => _selection is { Count: > 0 };

    // Whether the panel has a section for what is selected. A WIRE has none and wants none: it is the two sockets it
    // joins and nothing else, so there is nothing to set. Without this the panel stood there as an empty frame with a
    // border round no rows, which reads as a panel that has lost its contents.
    private bool Describable
    {
        get
        {
            if (_selection == null) return false;

            foreach (var item in _selection)
            {
                if (item is StrokeItem or ShapeItem or TextItem or CurveItem or GroupItem or CanvasFrameItem
                    or ElementItem)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private bool HasShape(CanvasShape shape)
    {
        if (_selection == null) return false;

        foreach (var item in _selection)
        {
            if (item is ShapeItem found && found.Shape == shape) return true;
        }

        return false;
    }

    private Visibility Shown<T>() where T : ICanvasItem
    {
        if (_selection == null) return Visibility.Collapsed;

        foreach (var item in _selection)
        {
            if (item is T) return Visibility.Visible;
        }

        return Visibility.Collapsed;
    }

    // The same question one level down: not what KIND of item is selected, but what kind of control one of them is
    // carrying. Every control on the plane is the same item, so its own lines can only be found this way.
    private Visibility ShownElement<T>()
    {
        if (_selection == null) return Visibility.Collapsed;

        foreach (var item in _selection)
        {
            if (item is ElementItem { Element: T }) return Visibility.Visible;
        }

        return Visibility.Collapsed;
    }

    partial void OnScaleChanged(double value) => RaisePropertyChanged(nameof(ZoomText));
}
