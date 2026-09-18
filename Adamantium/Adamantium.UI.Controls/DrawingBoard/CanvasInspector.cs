using System.Collections;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>THE INSPECTOR of an <see cref="InfiniteCanvas"/>, and it has two faces in one place. With nothing selected
/// it shows what you are working WITH - the tool in hand and its settings; with something selected, what you are
/// working ON. A third face lists everything on the plane, topmost first.
/// <para>ONE panel and not several: a second panel beside the first is a second panel to move, to fold and to find
/// room for - and folded it is a pair of buttons over the drawing that say nothing about what is behind them.</para>
/// <para>WHICH SECTION FITS WHAT IS SELECTED is answered here rather than by an application, because the kinds of
/// thing on a plane are the canvas's own - a stroke, a shape, a piece of text, a curve, a group, a control, a node.
/// Answered outside, every application would be taking the engine's types apart, and each would have to learn the next
/// kind somebody adds.</para></summary>
public class CanvasInspector : Control, ICanvasPart
{
    public static readonly AdamantiumProperty CanvasProperty = AdamantiumProperty.Register(nameof(Canvas),
        typeof(InfiniteCanvas), typeof(CanvasInspector), new PropertyMetadata(null, OnCanvasChanged));

    /// <summary>Which face is showing: what is selected, or what is on the plane. Two-way, because the pair of toggles
    /// that drive it are one choice.</summary>
    public static readonly AdamantiumProperty ShowsStructureProperty = AdamantiumProperty.Register(
        nameof(ShowsStructure), typeof(Boolean), typeof(CanvasInspector),
        new PropertyMetadata(false, PropertyMetadataOptions.BindsTwoWayByDefault, OnFaceChanged));

    public static readonly AdamantiumProperty ShowsPropertiesProperty = AdamantiumProperty.Register(
        nameof(ShowsProperties), typeof(Boolean), typeof(CanvasInspector),
        new PropertyMetadata(true, PropertyMetadataOptions.BindsTwoWayByDefault, OnFaceChanged));

    /// <summary>What the panel calls itself - what it is about right now, which is what makes a change of contents read
    /// as one panel following you rather than two taking turns.</summary>
    public static readonly AdamantiumProperty HeaderProperty = AdamantiumProperty.Register(nameof(Header),
        typeof(String), typeof(CanvasInspector), new PropertyMetadata(null));

    /// <summary>What is selected, as the property grid takes it.</summary>
    public static readonly AdamantiumProperty SelectionProperty = AdamantiumProperty.Register(nameof(Selection),
        typeof(IEnumerable), typeof(CanvasInspector), new PropertyMetadata(null));

    /// <summary>Everything on the plane, TOPMOST first - the same order the drawing is stacked in, so "bring to front"
    /// needs no explaining.</summary>
    public static readonly AdamantiumProperty StructureProperty = AdamantiumProperty.Register(nameof(Structure),
        typeof(IEnumerable), typeof(CanvasInspector), new PropertyMetadata(null));

    /// <summary>The row picked in that list. Picking one selects it on the plane, because there is one selection and
    /// not two.</summary>
    public static readonly AdamantiumProperty ListedProperty = AdamantiumProperty.Register(nameof(Listed),
        typeof(ICanvasItem), typeof(CanvasInspector),
        new PropertyMetadata(null, PropertyMetadataOptions.BindsTwoWayByDefault, OnListedChanged));

    private static readonly PropertyMetadata Hidden = new(Visibility.Collapsed);
    private static readonly PropertyMetadata Off = new(false);

    private static AdamantiumProperty Face(string name) =>
        AdamantiumProperty.Register(name, typeof(Visibility), typeof(CanvasInspector), Hidden);

    private static AdamantiumProperty Fact(string name) =>
        AdamantiumProperty.Register(name, typeof(Boolean), typeof(CanvasInspector), Off);

    /// <summary>Shown when nothing is selected: the tool in hand and what it will do next.</summary>
    public static readonly AdamantiumProperty ToolFaceProperty = Face(nameof(ToolFace));

    /// <summary>Shown when something selected has anything to set.</summary>
    public static readonly AdamantiumProperty SelectionFaceProperty = Face(nameof(SelectionFace));

    public static readonly AdamantiumProperty StructureFaceProperty = Face(nameof(StructureFace));

    public static readonly AdamantiumProperty PenSettingsProperty = Face(nameof(PenSettings));
    public static readonly AdamantiumProperty EraserSettingsProperty = Face(nameof(EraserSettings));
    public static readonly AdamantiumProperty TextSettingsProperty = Face(nameof(TextSettings));
    public static readonly AdamantiumProperty ShapeSettingsProperty = Face(nameof(ShapeSettings));

    public static readonly AdamantiumProperty StrokeSectionProperty = Face(nameof(StrokeSection));
    public static readonly AdamantiumProperty ShapeSectionProperty = Face(nameof(ShapeSection));
    public static readonly AdamantiumProperty TextSectionProperty = Face(nameof(TextSection));
    public static readonly AdamantiumProperty CurveSectionProperty = Face(nameof(CurveSection));
    public static readonly AdamantiumProperty GroupSectionProperty = Face(nameof(GroupSection));
    public static readonly AdamantiumProperty FrameSectionProperty = Face(nameof(FrameSection));
    public static readonly AdamantiumProperty ElementSectionProperty = Face(nameof(ElementSection));
    public static readonly AdamantiumProperty NodeSectionProperty = Face(nameof(NodeSection));

    /// <summary>Shown where what is selected is painted with a PICTURE: how it fits, whether it repeats, which way up,
    /// its turn and its tint. Not "is this a texture" - a texture is only a surface with a picture on it, and a button
    /// with one has exactly the same questions to answer.</summary>
    public static readonly AdamantiumProperty TextureSectionProperty = Face(nameof(TextureSection));

    /// <summary>Whether the actions that only mean something to a GRAPH are offered - lining nodes up, spreading them
    /// out, framing them, bringing them into view and the file the graph is kept in. Emptying the plane is NOT among
    /// them and is offered in both modes: it is about the canvas rather than about what is on it.</summary>
    public static readonly AdamantiumProperty GraphActionsProperty = Face(nameof(GraphActions));

    public static readonly AdamantiumProperty HasCornersProperty = Fact(nameof(HasCorners));
    public static readonly AdamantiumProperty HasSidesProperty = Fact(nameof(HasSides));
    public static readonly AdamantiumProperty HasHeadsProperty = Fact(nameof(HasHeads));
    public static readonly AdamantiumProperty IsBezierProperty = Fact(nameof(IsBezier));
    public static readonly AdamantiumProperty IsNurbsProperty = Fact(nameof(IsNurbs));
    public static readonly AdamantiumProperty HasPlainLabelProperty = Fact(nameof(HasPlainLabel));

    /// <summary>Whether the canvas is being used as a drawing - which the rows that only mean something to one are
    /// shown by.</summary>
    public static readonly AdamantiumProperty IsDrawingProperty = AdamantiumProperty.Register(nameof(IsDrawing),
        typeof(Boolean), typeof(CanvasInspector),
        new PropertyMetadata(true, PropertyMetadataOptions.BindsTwoWayByDefault, OnDrawingChosen));

    public static readonly AdamantiumProperty IsGraphProperty = AdamantiumProperty.Register(nameof(IsGraph),
        typeof(Boolean), typeof(CanvasInspector),
        new PropertyMetadata(false, PropertyMetadataOptions.BindsTwoWayByDefault, OnGraphChosen));

    /// <summary>The plane this inspects.</summary>
    public InfiniteCanvas Canvas
    {
        get => GetValue<InfiniteCanvas>(CanvasProperty);
        set => SetValue(CanvasProperty, value);
    }

    public Boolean ShowsStructure
    {
        get => GetValue<Boolean>(ShowsStructureProperty);
        set => SetValue(ShowsStructureProperty, value);
    }

    public Boolean ShowsProperties
    {
        get => GetValue<Boolean>(ShowsPropertiesProperty);
        set => SetValue(ShowsPropertiesProperty, value);
    }

    public String Header
    {
        get => GetValue<String>(HeaderProperty);
        private set => SetCurrentValue(HeaderProperty, value);
    }

    public IEnumerable Selection
    {
        get => GetValue<IEnumerable>(SelectionProperty);
        private set => SetCurrentValue(SelectionProperty, value);
    }

    public IEnumerable Structure
    {
        get => GetValue<IEnumerable>(StructureProperty);
        private set => SetCurrentValue(StructureProperty, value);
    }

    public ICanvasItem Listed
    {
        get => GetValue<ICanvasItem>(ListedProperty);
        set => SetValue(ListedProperty, value);
    }

    public Boolean IsDrawing
    {
        get => GetValue<Boolean>(IsDrawingProperty);
        set => SetValue(IsDrawingProperty, value);
    }

    public Boolean IsGraph
    {
        get => GetValue<Boolean>(IsGraphProperty);
        set => SetValue(IsGraphProperty, value);
    }

    public Visibility ToolFace => GetValue<Visibility>(ToolFaceProperty);
    public Visibility SelectionFace => GetValue<Visibility>(SelectionFaceProperty);
    public Visibility StructureFace => GetValue<Visibility>(StructureFaceProperty);

    public Visibility PenSettings => GetValue<Visibility>(PenSettingsProperty);
    public Visibility EraserSettings => GetValue<Visibility>(EraserSettingsProperty);
    public Visibility TextSettings => GetValue<Visibility>(TextSettingsProperty);
    public Visibility ShapeSettings => GetValue<Visibility>(ShapeSettingsProperty);

    public Visibility StrokeSection => GetValue<Visibility>(StrokeSectionProperty);
    public Visibility ShapeSection => GetValue<Visibility>(ShapeSectionProperty);
    public Visibility TextSection => GetValue<Visibility>(TextSectionProperty);
    public Visibility CurveSection => GetValue<Visibility>(CurveSectionProperty);
    public Visibility GroupSection => GetValue<Visibility>(GroupSectionProperty);
    public Visibility FrameSection => GetValue<Visibility>(FrameSectionProperty);
    public Visibility ElementSection => GetValue<Visibility>(ElementSectionProperty);
    public Visibility NodeSection => GetValue<Visibility>(NodeSectionProperty);
    public Visibility TextureSection => GetValue<Visibility>(TextureSectionProperty);
    public Visibility GraphActions => GetValue<Visibility>(GraphActionsProperty);

    public Boolean HasCorners => GetValue<Boolean>(HasCornersProperty);
    public Boolean HasSides => GetValue<Boolean>(HasSidesProperty);
    public Boolean HasHeads => GetValue<Boolean>(HasHeadsProperty);
    public Boolean IsBezier => GetValue<Boolean>(IsBezierProperty);
    public Boolean IsNurbs => GetValue<Boolean>(IsNurbsProperty);
    public Boolean HasPlainLabel => GetValue<Boolean>(HasPlainLabelProperty);

    // EVERY ROW THE TEMPLATE READS IS A REGISTERED PROPERTY, including the fixed lists and the two commands.
    // {TemplateBinding} resolves an AdamantiumProperty on the templated parent and nothing else: pointed at a plain
    // CLR property it finds none, and applying the template THROWS - which a theme swallows, so the control simply
    // comes up with no template at all. Measured exactly that: an inspector 340 wide and 0 tall, in all three themes.
    private static readonly IReadOnlyList<CanvasGridStyle> Grids =
        [CanvasGridStyle.Dots, CanvasGridStyle.Lines, CanvasGridStyle.None, CanvasGridStyle.Transparent];

    private static readonly IReadOnlyList<CanvasArrowHead> Heads =
        [CanvasArrowHead.None, CanvasArrowHead.Barbs, CanvasArrowHead.Triangle];

    private static readonly IReadOnlyList<CanvasCurve> Kinds =
        [CanvasCurve.Bezier, CanvasCurve.BSpline, CanvasCurve.Nurbs];

    // A TEXTURE's own catalogues. Every one of them is a brush's property and not the canvas's: what is offered here is
    // the whole of what the engine can paint a picture with, so a plane is not a poorer place to use a texture than a
    // control in a window is.
    private static readonly IReadOnlyList<Stretch> Filling =
        [Stretch.Fill, Stretch.Uniform, Stretch.UniformToFill, Stretch.None];

    private static readonly IReadOnlyList<TileMode> Tiling =
        [TileMode.None, TileMode.Tile, TileMode.FlipX, TileMode.FlipY, TileMode.FlipXY];

    private static readonly IReadOnlyList<ImageBackgroundState> Grounding =
        [ImageBackgroundState.WhenEmpty, ImageBackgroundState.Always, ImageBackgroundState.Never];

    /// <summary>The grids a plane can wear, for the row that chooses one.</summary>
    public static readonly AdamantiumProperty GridStylesProperty = AdamantiumProperty.Register(nameof(GridStyles),
        typeof(IEnumerable), typeof(CanvasInspector), new PropertyMetadata(Grids));

    public static readonly AdamantiumProperty ArrowHeadsProperty = AdamantiumProperty.Register(nameof(ArrowHeads),
        typeof(IEnumerable), typeof(CanvasInspector), new PropertyMetadata(Heads));

    public static readonly AdamantiumProperty CurvesProperty = AdamantiumProperty.Register(nameof(Curves),
        typeof(IEnumerable), typeof(CanvasInspector), new PropertyMetadata(Kinds));

    /// <summary>The ways a picture can fill its tile, for the row that chooses one.</summary>
    public static readonly AdamantiumProperty FillsProperty = AdamantiumProperty.Register(nameof(Fills),
        typeof(IEnumerable), typeof(CanvasInspector), new PropertyMetadata(Filling));

    /// <summary>...and the ways that tile can repeat, mirrored or not.</summary>
    public static readonly AdamantiumProperty TilingsProperty = AdamantiumProperty.Register(nameof(Tilings),
        typeof(IEnumerable), typeof(CanvasInspector), new PropertyMetadata(Tiling));

    /// <summary>When the ground behind a picture is painted, for the row that chooses one.</summary>
    public static readonly AdamantiumProperty GroundsProperty = AdamantiumProperty.Register(nameof(Grounds),
        typeof(IEnumerable), typeof(CanvasInspector), new PropertyMetadata(Grounding));

    /// <summary>The catalogues, straight off the canvas - so the row that names a node's kind offers exactly what the
    /// palette offers and the two cannot drift apart.</summary>
    public static readonly AdamantiumProperty NodeKindsProperty = AdamantiumProperty.Register(nameof(NodeKinds),
        typeof(IEnumerable), typeof(CanvasInspector), new PropertyMetadata(null));

    public static readonly AdamantiumProperty SocketKindsProperty = AdamantiumProperty.Register(nameof(SocketKinds),
        typeof(IEnumerable), typeof(CanvasInspector), new PropertyMetadata(null));

    /// <summary>Puts one more socket on the selected node, down the side the plus was pressed on.</summary>
    public static readonly AdamantiumProperty AddSocketCommandProperty = AdamantiumProperty.Register(
        nameof(AddSocketCommand), typeof(ICommand), typeof(CanvasInspector), new PropertyMetadata(null));

    /// <summary>...and takes THAT one away - the bin beside a socket's own name, because asked through a count
    /// "drop the second of three" takes the third.</summary>
    public static readonly AdamantiumProperty RemoveSocketCommandProperty = AdamantiumProperty.Register(
        nameof(RemoveSocketCommand), typeof(ICommand), typeof(CanvasInspector), new PropertyMetadata(null));

    /// <summary>Sizes what is selected to the PICTURE painted on it - its own pixels, so its proportions are the
    /// photographer's and not whatever rectangle the hand happened to drag out. The one thing about a texture a person
    /// cannot do by typing: nobody knows a file is 1024 by 768 until the picture is in front of them.</summary>
    public static readonly AdamantiumProperty ActualSizeCommandProperty = AdamantiumProperty.Register(
        nameof(ActualSizeCommand), typeof(ICommand), typeof(CanvasInspector), new PropertyMetadata(null));

    public IEnumerable GridStyles => GetValue<IEnumerable>(GridStylesProperty);

    public IEnumerable ArrowHeads => GetValue<IEnumerable>(ArrowHeadsProperty);

    public IEnumerable Curves => GetValue<IEnumerable>(CurvesProperty);

    public IEnumerable Fills => GetValue<IEnumerable>(FillsProperty);

    public IEnumerable Tilings => GetValue<IEnumerable>(TilingsProperty);

    public IEnumerable Grounds => GetValue<IEnumerable>(GroundsProperty);

    public IEnumerable NodeKinds => GetValue<IEnumerable>(NodeKindsProperty);

    public IEnumerable SocketKinds => GetValue<IEnumerable>(SocketKindsProperty);

    public ICommand AddSocketCommand => GetValue<ICommand>(AddSocketCommandProperty);

    public ICommand RemoveSocketCommand => GetValue<ICommand>(RemoveSocketCommandProperty);

    public ICommand ActualSizeCommand => GetValue<ICommand>(ActualSizeCommandProperty);

    public CanvasInspector()
    {
        // At DEFAULT priority, which is the whole point: written the ordinary way these would sit in the Local slot,
        // and Local outranks Binding for good - an application that wanted to answer the plus itself could never get
        // a {Binding} through.
        SetValue(AddSocketCommandProperty, new CanvasCommand(side => Socketed(side, true)), ValuePriority.Default);
        SetValue(RemoveSocketCommandProperty, new CanvasCommand(socket => Socketed(socket, false)),
            ValuePriority.Default);
        SetValue(ActualSizeCommandProperty, new CanvasCommand(_ => Actual()), ValuePriority.Default);
    }

    // EVERY selected picture at once, and each at its OWN size: "the picture's own size" is a different number for
    // each of them, which is the one thing a selection cannot be given as one value.
    private void Actual()
    {
        if (Canvas is not { } canvas || canvas.Selection.Count == 0) return;

        canvas.BeginEdit("Actual size");
        try
        {
            foreach (var item in canvas.Selection)
            {
                if (item is not ElementItem { Tiled.Source: { } picture } element) continue;
                if (picture.Width <= 0 || picture.Height <= 0) continue;

                element.Width = picture.Width;
                element.Height = picture.Height;
            }

            canvas.Scene?.Touch();
        }
        finally
        {
            canvas.EndEdit();
        }
    }

    // ONE node, not all of them: the panel shows the sockets of what is selected, and adding "one more input" to
    // twenty nodes at once is not what the plus beside one list of them says.
    private void Socketed(object which, bool adding)
    {
        if (Canvas is not { } canvas) return;

        foreach (var item in canvas.Selection)
        {
            if (item is not ElementItem { Model: ICanvasNode node }) continue;

            if (adding)
            {
                var out_ = String.Equals(which as string, "Out", StringComparison.OrdinalIgnoreCase);
                var sockets = out_ ? node.Outputs : node.Inputs;
                var made = node.NewSocket($"{(out_ ? "Out" : "In")} {sockets.Count + 1}");

                if (made != null) sockets.Add(made);
            }
            else if (which is ICanvasSocket socket)
            {
                if (!node.Inputs.Remove(socket)) node.Outputs.Remove(socket);
            }

            canvas.Scene?.Touch();
            return;
        }
    }

    private PropertyGrid _grid;

    /// <summary>Hands the canvas the grid that shows what is selected, so that a number typed into a row becomes a step
    /// in the canvas's memory and a repaint of the plane. The canvas does the listening because it is the specialised
    /// one: a property grid is a general-purpose control and must not learn what a scene or a history is.</summary>
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_grid != null) _grid.ValueChanged -= OnRowWritten;

        _grid = GetTemplateChild("PART_Selected") as PropertyGrid;

        // An EDIT can change which sections apply - a picture painted onto a surface brings a texture's lines with it -
        // and nothing else would ever say so: the selection has not changed, and the scene's own signal is about what
        // is on the plane rather than about what a panel should be showing.
        if (_grid != null) _grid.ValueChanged += OnRowWritten;

        Wire();
    }

    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();

        if (_grid != null) _grid.ValueChanged -= OnRowWritten;

        _grid = null;
    }

    private void OnRowWritten(object sender, PropertyValuesChangedEventArgs e) => ReadSections();

    private void Wire()
    {
        if (Canvas != null) Canvas.Inspector = _grid;
    }

    private static void OnCanvasChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not CanvasInspector inspector) return;

        if (e.OldValue is InfiniteCanvas old && ReferenceEquals(old.Inspector, inspector._grid)) old.Inspector = null;

        if (e.OldValue is InfiniteCanvas was)
        {
            was.SelectionChanged -= inspector.OnSelectionChanged;
            was.ModeChanged -= inspector.OnModeChanged;
            was.ToolChanged -= inspector.OnToolChanged;
            was.PlaneChanged -= inspector.OnPlaneChanged;
            was.NodeKindsChanged -= inspector.OnKindsChanged;
            was.SocketKindsChanged -= inspector.OnKindsChanged;
        }

        if (e.NewValue is InfiniteCanvas now)
        {
            now.SelectionChanged += inspector.OnSelectionChanged;
            now.ModeChanged += inspector.OnModeChanged;
            now.ToolChanged += inspector.OnToolChanged;
            now.PlaneChanged += inspector.OnPlaneChanged;
            now.NodeKindsChanged += inspector.OnKindsChanged;
            now.SocketKindsChanged += inspector.OnKindsChanged;
        }

        inspector.Wire();
        inspector.ReadAll();
    }

    private void OnSelectionChanged(object sender, EventArgs e) => Read();

    private void OnPlaneChanged(object sender, EventArgs e) => ReadStructure();

    private void OnToolChanged(object sender, EventArgs e) => ReadTool();

    private void OnModeChanged(object sender, EventArgs e) => ReadMode();

    // FOLLOWED rather than taken once. A page states its catalogues with bindings, and a binding is pushed after the
    // canvas has been templated and its panes have handed it round - so a panel that read them when it was given the
    // canvas read null, and the lines that name a kind stood blank for ever beside a node that plainly has one.
    private void OnKindsChanged(object sender, EventArgs e) => ReadKinds();

    private void ReadKinds()
    {
        SetCurrentValue(NodeKindsProperty, Canvas?.NodeKinds);
        SetCurrentValue(SocketKindsProperty, Canvas?.SocketKinds);
    }

    private void ReadAll()
    {
        ReadKinds();

        Read();
        ReadTool();
        ReadMode();
        ReadStructure();
    }

    // ONE CHOICE said as two switches, so a pair of radios can drive it: the one that was just turned on wins and the
    // other goes off, which is what a radio group does and what a single enum could not be bound to.
    private static void OnFaceChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not CanvasInspector inspector || inspector._settling) return;

        inspector._settling = true;
        try
        {
            if (e.Property == ShowsStructureProperty)
                inspector.SetCurrentValue(ShowsPropertiesProperty, !inspector.ShowsStructure);
            else
                inspector.SetCurrentValue(ShowsStructureProperty, !inspector.ShowsProperties);
        }
        finally
        {
            inspector._settling = false;
        }

        inspector.Read();
    }

    private bool _settling;

    private static void OnDrawingChosen(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is CanvasInspector { _settling: false, Canvas: { } canvas } inspector && e.NewValue is true)
        {
            canvas.Mode = CanvasMode.Drawing;
            inspector.ReadMode();
        }
    }

    private static void OnGraphChosen(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is CanvasInspector { _settling: false, Canvas: { } canvas } inspector && e.NewValue is true)
        {
            canvas.Mode = CanvasMode.Nodes;
            inspector.ReadMode();
        }
    }

    // A CURRENT value and not a Local one: these two are what the mode radios bind to, and a control that wrote its own
    // answer into the Local slot would mask that binding - and every trigger a theme might put on them - for good.
    private void ReadMode()
    {
        var drawing = Canvas is not { } canvas || canvas.Mode == CanvasMode.Drawing;

        _settling = true;
        try
        {
            SetCurrentValue(IsDrawingProperty, drawing);
            SetCurrentValue(IsGraphProperty, !drawing);
        }
        finally
        {
            _settling = false;
        }

        SetCurrentValue(GraphActionsProperty, Shown(!drawing));
    }

    // WHICH TOOL'S settings belong in the panel. Asked of the tool itself: the canvas holds the tools, and what a pen
    // has to say about itself is that it is a pen.
    private void ReadTool()
    {
        var tool = Canvas?.Tool;

        SetCurrentValue(PenSettingsProperty, Shown(tool is PenTool));
        SetCurrentValue(EraserSettingsProperty, Shown(tool is EraseTool));
        SetCurrentValue(TextSettingsProperty, Shown(tool is TextTool));
        SetCurrentValue(ShapeSettingsProperty, Shown(tool is ShapeTool or CurveTool));
    }

    private void ReadStructure()
    {
        if (Canvas is not { } canvas)
        {
            Structure = null;
            return;
        }

        // TOPMOST FIRST, so the list reads the way the drawing is stacked. A fresh list every time: the scene's own is
        // edited in place and tells a binding nothing.
        var listed = new List<ICanvasItem>(canvas.ItemsHere());
        listed.Reverse();

        Structure = listed;
    }

    private static void OnListedChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not CanvasInspector { Canvas: { } canvas } inspector || inspector._settling) return;
        if (e.NewValue is not ICanvasItem item) return;

        canvas.SelectMany([item], false);
    }

    private void Read()
    {
        var chosen = Canvas?.Selection;
        var many = chosen?.Count ?? 0;

        // A FRESH LIST every time, exactly as the structure face makes one: the canvas's selection is edited in place,
        // so handing the same instance over again is the same value as far as a property says - and the rows go on
        // showing what was selected when they were last built. Which is a panel full of blank lines beside a node that
        // plainly has a kind and a title.
        Selection = chosen == null ? null : new List<ICanvasItem>(chosen);
        Header = ShowsProperties && many > 1 ? $"{many} objects selected" : null;

        ReadSections();
    }

    // WHICH SECTIONS APPLY, and nothing else. Split out because an EDIT can change the answer without changing what is
    // selected: painting a picture onto a surface is what makes a texture's lines mean something, and a panel that
    // worked this out once when the thing was picked up would go on offering nothing about the picture just put on it.
    //
    // The selection itself is deliberately NOT republished here. It is handed over as a fresh list - that is what makes
    // the rows re-read - and doing that on every keystroke would rebuild the rows under the hand typing into them.
    private void ReadSections()
    {
        var chosen = Canvas?.Selection;
        var many = chosen?.Count ?? 0;

        SetCurrentValue(StrokeSectionProperty, Shown<StrokeItem>(chosen));
        SetCurrentValue(ShapeSectionProperty, Shown<ShapeItem>(chosen));
        SetCurrentValue(TextSectionProperty, Shown<TextItem>(chosen));
        SetCurrentValue(CurveSectionProperty, Shown<CurveItem>(chosen));
        SetCurrentValue(GroupSectionProperty, Shown<GroupItem>(chosen));
        SetCurrentValue(FrameSectionProperty, Shown<CanvasFrameItem>(chosen));
        SetCurrentValue(ElementSectionProperty, Shown<ElementItem>(chosen));
        SetCurrentValue(NodeSectionProperty, Shown(Any(chosen, item => item is ElementItem { Model: ICanvasNode })));
        SetCurrentValue(TextureSectionProperty, Shown(Any(chosen, item => item is ElementItem { Tiled: not null })));
        SetCurrentValue(HasCornersProperty, Any(chosen, item => item is ShapeItem { Shape: CanvasShape.Rectangle }));
        SetCurrentValue(HasSidesProperty, Any(chosen, item => item is ShapeItem { Shape: CanvasShape.Polygon }));
        SetCurrentValue(HasHeadsProperty, Any(chosen, item => item is ShapeItem { Shape: CanvasShape.Arrow }));
        SetCurrentValue(IsBezierProperty, Any(chosen, item => item is CurveItem { Kind: CanvasCurve.Bezier }));
        SetCurrentValue(IsNurbsProperty, Any(chosen, item => item is CurveItem { Kind: CanvasCurve.Nurbs }));

        // A NODE says its name on its own Title row, so the generic one would be the same name written twice - and two
        // rows writing one thing disagree the moment one of them is used.
        SetCurrentValue(HasPlainLabelProperty,
            Any(chosen, item => item is ElementItem and not ElementItem { Model: ICanvasNode }));

        var anything = many > 0;

        SetCurrentValue(ToolFaceProperty, Shown(ShowsProperties && !anything));
        SetCurrentValue(SelectionFaceProperty, Shown(ShowsProperties && anything && Describable(chosen)));
        SetCurrentValue(StructureFaceProperty, Shown(ShowsStructure));
    }

    // Whether the panel has a section for what is selected. A WIRE has none and wants none - it is the two sockets it
    // joins and nothing else - and without this the panel stood there as a border round no rows, which reads as a panel
    // that has lost its contents rather than as "nothing to set here".
    private static bool Describable(IReadOnlyList<ICanvasItem> chosen) =>
        Any(chosen, item => item is StrokeItem or ShapeItem or TextItem or CurveItem or GroupItem or ElementItem
            or CanvasFrameItem);

    private static Visibility Shown<T>(IReadOnlyList<ICanvasItem> chosen) where T : ICanvasItem =>
        Shown(Any(chosen, item => item is T));

    private static Visibility Shown(bool yes) => yes ? Visibility.Visible : Visibility.Collapsed;

    private static bool Any(IReadOnlyList<ICanvasItem> chosen, Func<ICanvasItem, bool> is_)
    {
        if (chosen == null) return false;

        foreach (var item in chosen)
        {
            if (is_(item)) return true;
        }

        return false;
    }
}
