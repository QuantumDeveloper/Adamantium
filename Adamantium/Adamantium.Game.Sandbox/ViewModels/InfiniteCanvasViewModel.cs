using System.Collections.Generic;
using System.Threading.Tasks;
using Adamantium.Mathematics;
using Adamantium.Navigation;
using Adamantium.MVVM;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;

namespace Adamantium.Game.Sandbox.ViewModels;

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
        Tools.Add(NodeTool);

        Tool = SelectTool;

        // TEMP instrument (ADAM_CANVAS_PICK=1): selects the first seeded item, so the inspector's SECOND face can be
        // seen without a mouse. Goes with the seed above.

        // TEMP instrument (ADAM_CANVAS_SEED=1): puts things on the plane WITHOUT a mouse, so what they look like against
        // the ground can be measured. ADAM_CANVAS_SCALE sets the camera, so "controls grow when zoomed out" can be seen
        // without driving the wheel. Both off unless asked for.
        if (System.Environment.GetEnvironmentVariable("ADAM_CANVAS_SEED") == "1")
        {
            // ADAM_CANVAS_NOCONTROLS=1: strokes only, so what the INK costs can be told apart from what the controls do.
            if (System.Environment.GetEnvironmentVariable("ADAM_CANVAS_NOCONTROLS") != "1")
            {
                Scene.Add(new ElementItem(new Button { Content = "Button" }, new Rect(-300, -90, 200, 48)));
                Scene.Add(new ElementItem(new CheckBox { Content = "Check me" }, new Rect(-300, 0, 200, 40)));
                Scene.Add(new ElementItem(new TextBox { Text = "A field" }, new Rect(-300, 70, 200, 40)));

                // ON THE ORIGIN, so it stays in the middle at every zoom - which is the only way to SEE what a control
                // does under one without driving the camera by hand.
                Scene.Add(new ElementItem(new Button { Content = "Zoom me" }, new Rect(-100, -24, 200, 48)));

                // A ROUNDED box with a fat outline: the case where the outline has to eat inwards instead of making
                // the shape bigger, and the only seeded shape there is.
                Scene.Add(new ShapeItem(CanvasShape.Rectangle, new Rect(-420, 150, 220, 130),
                    new SolidColorBrush(Colors.MediumSpringGreen), 16,
                    new SolidColorBrush(Color.FromRgba(40, 90, 70, 200)))
                { Corner = new Adamantium.ProceduralGeometry.CornerRadius(28, 28, 0, 28) });

                Scene.Add(new ShapeItem(CanvasShape.Polygon, new Rect(-160, 150, 140, 140),
                    new SolidColorBrush(Colors.Orange), 6,
                    new SolidColorBrush(Color.FromRgba(120, 70, 20, 200))) { Sides = 6 });

                // NO outline at all: the fill has to keep the whole box, since there is nothing to make room for.
                Scene.Add(new ShapeItem(CanvasShape.Rectangle, new Rect(100, 150, 140, 100),
                    new SolidColorBrush(Colors.Orange), 0, new SolidColorBrush(Colors.DeepPink)));

                // A NODE of a graph, to see the control against the theme it is in. Its sockets take the theme's accent,
                // which is what a node says with when nothing has said otherwise.
                Scene.Add(new ElementItem(
                    new CanvasNode { Title = "Multiply", Inputs = 2, Outputs = 1 },
                    new Rect(-60, 180, 190, 120)));

                // And the other case: sockets coloured PER PIN, the way a graph editor says what may be joined to what,
                // with two of them docked so hollow and solid can be told apart side by side.
                var clamp = new CanvasNode { Title = "Clamp", Inputs = 3, Outputs = 2 };
                clamp.InputPins[0].Color = new SolidColorBrush(Colors.MediumSpringGreen);
                clamp.InputPins[1].Color = new SolidColorBrush(Colors.Orange);
                clamp.InputPins[2].Color = new SolidColorBrush(Colors.Orange);
                clamp.OutputPins[0].Color = new SolidColorBrush(Colors.MediumSpringGreen);
                clamp.InputPins[0].IsConnected = true;
                clamp.OutputPins[0].IsConnected = true;
                Scene.Add(new ElementItem(clamp, new Rect(200, 180, 190, 160)));

                // Two arrows, one of each head, so both can be seen against the same line thickness.
                Scene.Add(new ShapeItem(CanvasShape.Arrow, new Rect(-420, 330, 200, 0),
                    new SolidColorBrush(Colors.White), 3));

                // FAT barbs, which is where the corner at the tip either closes or does not.
                Scene.Add(new ShapeItem(CanvasShape.Arrow, new Rect(-160, 330, 200, 60),
                    new SolidColorBrush(Colors.MediumSpringGreen), 18)
                { StartHead = CanvasArrowHead.Barbs, EndHead = CanvasArrowHead.Barbs });

                // A plain LINE beside them, because a line and a straight ink stroke look alike on screen and behave
                // nothing alike: the line is reshaped by its two ends and wears no frame, the stroke is ink and keeps
                // its box. Without one here there is nothing to check that against.
                Scene.Add(new ShapeItem(CanvasShape.Line, new Rect(100, 330, 220, 70),
                    new SolidColorBrush(Colors.DeepSkyBlue), 6));
            }

            // A stroke laid ACROSS the controls, to see which of them ends up on top, plus three of decreasing alpha, to
            // see whether a highlighter is possible at all.
            var across = new StrokeItem(new Vector2(-340, -70), Brushes.White, 6);
            for (var i = 0; i < 24; i++) across.Add(new Vector2(-340 + i * 20, -70 + i * 8));
            Scene.Add(across);

            // ADAM_CANVAS_LONG=n: one stroke of n points across a big box - the stress case for a pass that asks every
            // covered pixel about every point of the stroke it is in.
            if (System.Environment.GetEnvironmentVariable("ADAM_CANVAS_LONG") is { Length: > 0 } longPoints)
            {
                var many = int.Parse(longPoints);
                var big = new StrokeItem(new Vector2(-500, 150), Brushes.MediumSpringGreen, 4);
                for (var i = 0; i < many; i++)
                {
                    big.Add(new Vector2(-500 + i * (1000.0 / many), 150 + System.Math.Sin(i * 0.12) * 120));
                }

                Scene.Add(big);
            }

            for (var band = 0; band < 3; band++)
            {
                var alpha = (byte)(255 - band * 90);
                var fade = new StrokeItem(new Vector2(60, -60 + band * 50),
                    new SolidColorBrush(Color.FromRgba(255, 210, 60, alpha)), 26);

                for (var i = 0; i < 12; i++) fade.Add(new Vector2(60 + i * 26, -60 + band * 50));
                Scene.Add(fade);
            }

            if (System.Environment.GetEnvironmentVariable("ADAM_CANVAS_SCALE") is { Length: > 0 } zoom)
            {
                _scale = double.Parse(zoom, System.Globalization.CultureInfo.InvariantCulture);
            }

            // TEMP instrument (ADAM_CANVAS_PICK=<index>): selects one seeded item, so the inspector's SECOND face can
            // be seen without a mouse - and a chosen index, because which KIND is selected is the whole question the
            // inspector answers. AFTER the seed, or there is nothing to select yet.
            if (System.Environment.GetEnvironmentVariable("ADAM_CANVAS_PICK") is { Length: > 0 } pick
                && int.TryParse(pick, out var at) && at >= 0 && at < Scene.Items.Count)
            {
                Selection = new[] { Scene.Items[at] };
            }

            // GLASS: no ground at all. Tells "covered by the ground" apart from "never drawn", which look identical.
            if (System.Environment.GetEnvironmentVariable("ADAM_CANVAS_GLASS") == "1")
            {
                _gridStyle = CanvasGridStyle.Transparent;
            }
        }

        // Opens EMPTY and on SELECT. Nothing is seeded: a canvas that starts with somebody else's marks on it is one
        // whose first action is clearing them, and select is the tool that does not put anything down where the first
        // press happens to land.
        Tool = SelectTool;
    }

    private void OnSceneChanged(object sender, System.EventArgs e)
    {
        RaisePropertyChanged(nameof(Drawn));
        RefreshStructure();
    }

    /// <summary>What is on the plane, TOPMOST FIRST. Reversed from paint order on purpose: the list reads top to
    /// bottom the way the drawing is stacked front to back, which is what every editor's layer list does and what
    /// "bring to front" then means without explanation.
    /// <para>A fresh array every time, not the scene's own list: the same instance handed over twice is not a change,
    /// and a list bound to it would never notice anything.</para></summary>
    public IReadOnlyList<ICanvasItem> Structure { get; private set; } = System.Array.Empty<ICanvasItem>();

    private void RefreshStructure()
    {
        var items = Scene.Items;
        var listed = new ICanvasItem[items.Count];

        for (var i = 0; i < items.Count; i++) listed[i] = items[items.Count - 1 - i];

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

            Selection = new[] { value };
        }
    }

    public IReadOnlyList<CanvasGridStyle> GridStyles { get; } =
        [CanvasGridStyle.Dots, CanvasGridStyle.Lines, CanvasGridStyle.None, CanvasGridStyle.Transparent];

    public IReadOnlyList<CanvasArrowHead> ArrowHeads { get; } =
        [CanvasArrowHead.None, CanvasArrowHead.Barbs, CanvasArrowHead.Triangle];

    /// <summary>What was done to this drawing. The PAGE's, like the scene - undo belongs to whoever owns the drawing,
    /// and the canvas only says where one step ends and the next begins.</summary>
    public CanvasHistory History { get; } = new();

    /// <summary>The whiteboard switch: the canvas becomes glass over whatever is behind it. It writes the SAME property
    /// the grid drop-down does, because it is the same setting said two ways - and putting the mode back where it was
    /// when the switch goes off is why the old one is remembered rather than assumed.</summary>
    [Bindable] private bool _whiteboard;

    private CanvasGridStyle _gridBeforeWhiteboard = CanvasGridStyle.Dots;

    partial void OnWhiteboardChanged(bool value)
    {
        if (value)
        {
            _gridBeforeWhiteboard = GridStyle;
            GridStyle = CanvasGridStyle.Transparent;
        }
        else if (GridStyle == CanvasGridStyle.Transparent)
        {
            GridStyle = _gridBeforeWhiteboard;
        }
    }

    /// <summary>Whether the canvas carries its own tool panel. Off, everything is steered from out here - which is the
    /// case the whiteboard mode is for.</summary>
    [Bindable] private bool _showPanel = true;

    /// <summary>What is drawn on the plane. The PAGE holds it, not the canvas - undo, saving and everything else a
    /// drawing is for belong to whoever owns the drawing, which is why the control only ever asks what is visible.
    /// </summary>
    public CanvasScene Scene { get; } = new();

    /// <summary>The tools, made once and kept. A tool holds the gesture it is halfway through, so a fresh one on every
    /// click of a tool button would be a tool that forgets what it was doing.</summary>
    public ICanvasTool SelectTool { get; } = new SelectTool();

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

    /// <summary>A NODE of a graph. The same tool as the three above and not a mechanism of its own: a node IS a
    /// control, which was the point of building it as one - so putting it on the plane needs nothing the canvas did not
    /// already have.</summary>
    public ICanvasTool NodeTool { get; } =
        new ElementTool(() => new CanvasNode { Title = "Node", Inputs = 2, Outputs = 1 }, new Size(190, 110))
        { Name = "Node", Icon = "ToolNodeIcon", Description = "drag out a graph node" };

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
    public string Face => _selection is { Count: > 1 } many ? $"{many.Count} objects selected" : null;

    /// <summary>Whether a press on a control on the plane MOVES it or presses it.</summary>
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
    [Bindable] private double _maxZoom = 1024;

    [Bindable] private Color _snapColor = Colors.DodgerBlue;

    public Brush SnapMark { get; private set; } = new SolidColorBrush(Colors.DodgerBlue);

    partial void OnSnapColorChanged(Color value)
    {
        SnapMark = new SolidColorBrush(value);
        RaisePropertyChanged(nameof(SnapMark));
    }

    [Bindable] private double _snapMarkSize = 9;

    /// <summary>How much is on the canvas, and what that costs to look at. The point of the scene living out here is
    /// that this is the application's business to answer.</summary>
    public string Drawn => Scene.Items.Count switch
    {
        0 => "Nothing drawn yet - pick a tool and drag",
        1 => "1 item",
        var many => $"{many} items"
    };

    /// <summary>WHAT is selected, by name.
    /// <para>Two things that look alike on screen can be different kinds and behave differently - a straight ink stroke
    /// and a line are the same picture, and one is reshaped by a box while the other is reshaped by its ends. Without
    /// this the only way to tell them apart is to try a gesture and see, which is how an afternoon goes to arguing
    /// about which of them is in front of you.</para></summary>
    public string Chosen
    {
        get
        {
            if (_selection is not { Count: > 0 } chosen) return string.Empty;

            return chosen.Count > 1 ? $"{chosen.Count} selected" : "Selected: " + chosen[0].Title;
        }
    }

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
        RaisePropertyChanged(nameof(Drawn));
    }

    [Bindable] private CanvasGridStyle _gridStyle = CanvasGridStyle.Dots;

    // Picking the mode straight off the drop-down keeps the switch honest: choose Transparent there and the whiteboard
    // box is ticked, choose anything else and it is not. One state said two ways, and neither way lies about it.
    partial void OnGridStyleChanged(CanvasGridStyle value) =>
        Whiteboard = value == CanvasGridStyle.Transparent;

    [Bindable] private double _scale = 1;

    [Bindable] private Vector2 _offset;

    /// <summary>What the camera is, in words. An edgeless plane has no position to read off it, so the numbers are the
    /// only way to see that panning a long way out costs nothing and loses nothing.</summary>
    public string Camera => string.Format(System.Globalization.CultureInfo.InvariantCulture,
        "scale {0:0.###}x     world origin at ({1:0}, {2:0}) px on screen", _scale, _offset.X, _offset.Y);

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
        RaisePropertyChanged(nameof(NodeSection));
        RaisePropertyChanged(nameof(HasPlainLabel));
        RaisePropertyChanged(nameof(Chosen));
        RaisePropertyChanged(nameof(IsNurbs));
        RaisePropertyChanged(nameof(HasCorners));
        RaisePropertyChanged(nameof(HasSides));
        RaisePropertyChanged(nameof(HasHeads));
        RaisePropertyChanged(nameof(Listed));
        RaisePropertyChanged(nameof(Face));
    }

    // The inspector has TWO faces and one place. With nothing selected it shows what you are working WITH - the tool
    // and its settings; with something selected, what you are working ON. One panel, never empty, and the settings
    // that belong to a tool stop having to live somewhere else.
    public Visibility ToolFace => Anything ? Visibility.Collapsed : Visibility.Visible;

    public Visibility SelectionFace => Anything ? Visibility.Visible : Visibility.Collapsed;

    // A section per KIND, shown when the selection holds one. Several kinds at once show several sections, which is
    // what a mixed selection honestly is.
    public Visibility StrokeSection => Shown<StrokeItem>();

    public Visibility ShapeSection => Shown<ShapeItem>();

    public Visibility TextSection => Shown<TextItem>();

    public Visibility ElementSection => Shown<ElementItem>();

    /// <summary>The node's OWN lines, under the ones every control has. A node is an ElementItem like a button is, so
    /// the section above already gives it a place and a size; what this adds is the handful of things that make it a
    /// node - what it is called, how many sockets down each side, and the two colours it is read by.</summary>
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

    public IReadOnlyList<CanvasCurve> Curves { get; } =
        [CanvasCurve.Bezier, CanvasCurve.BSpline, CanvasCurve.Nurbs];

    /// <summary>Degree and evenness belong to a NURBS and to nothing else - the other two curves would show two lines
    /// that do nothing.</summary>
    public bool IsNurbs
    {
        get
        {
            if (_selection == null) return false;

            foreach (var item in _selection)
            {
                if (item is CurveItem { Kind: CanvasCurve.Nurbs }) return true;
            }

            return false;
        }
    }

    // Within the one Shape section, the lines that belong to ONE shape. Rounding is a rectangle's and sides are a
    // polygon's; an ellipse has neither. A line that means nothing to what is selected is worse than no line, because
    // it invites a number that will quietly do nothing.
    public bool HasCorners => HasShape(CanvasShape.Rectangle);

    public bool HasSides => HasShape(CanvasShape.Polygon);

    public bool HasHeads => HasShape(CanvasShape.Arrow);

    /// <summary>Takes one socket off the node holding it. The row's button hands over the socket and nothing else, so
    /// which node it belongs to is asked of the selection - the only nodes on screen whose sockets are being shown.
    /// <para>ASKS FIRST, under the same switch as deleting anything else on the plane. Dropping a socket takes its name,
    /// its colour and whatever is wired to it with it, there is no undo reaching in here yet, and the button sits one
    /// row away from the one that renames it.</para></summary>
    [Command]
    private async Task RemoveSocket(object which)
    {
        if (which is not CanvasNodePin pin || _selection == null) return;

        CanvasNode holder = null;
        foreach (var item in _selection)
        {
            if (item is not ElementItem { Element: CanvasNode node } || !node.Holds(pin)) continue;

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
                    .Add("message", $"Remove \"{pin.Name}\" from {holder.Title}?"));

                if (result.Result != DialogButtonResult.Ok) return;
            }
        }

        holder.Remove(pin);
        Scene.Touch();
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

    partial void OnScaleChanged(double value)
    {
        RaisePropertyChanged(nameof(Camera));
        RaisePropertyChanged(nameof(ZoomText));
    }

    partial void OnOffsetChanged(Vector2 value) => RaisePropertyChanged(nameof(Camera));
}
