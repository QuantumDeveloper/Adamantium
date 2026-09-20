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

    /// <summary>How much is on the plane, in words - shown over the list, where what is on the plane is being looked
    /// at anyway. Said here rather than left to the application: a panel that lists everything and cannot say how much
    /// of it there is makes the count something to go and find elsewhere.</summary>
    public static readonly AdamantiumProperty CountedProperty = AdamantiumProperty.Register(nameof(Counted),
        typeof(String), typeof(CanvasInspector), new PropertyMetadata("Nothing drawn yet"));

    public String Counted
    {
        get => GetValue<String>(CountedProperty);
        private set => SetCurrentValue(CountedProperty, value);
    }

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

    // WHICH SECTIONS FIT WHAT IS SELECTED is no longer a row of flags here. There used to be one per kind of thing -
    // and one per VARIANT of a kind: has corners, has sides, has heads - so a panel had to be told about every kind the
    // canvas could hold, and the next kind meant a new flag here and an edit to three themes. The sets of lines now say
    // what they are for (see CanvasSectionSet), and what a thing IS answers them: ICanvasItem.Sort.

    /// <summary>Whether the actions that only mean something to a GRAPH are offered - lining nodes up, spreading them
    /// out, framing them, bringing them into view and the file the graph is kept in. Emptying the plane is NOT among
    /// them and is offered in both modes: it is about the canvas rather than about what is on it.</summary>
    public static readonly AdamantiumProperty GraphActionsProperty = Face(nameof(GraphActions));

    /// <summary>...and the ones that only mean something to a DRAWING: the file it is written out to and read back
    /// from. A drawing is written as SVG because other people's tools have a claim on it; a graph means something only
    /// here and is kept as JSON, which is why these two are not one pair of buttons.</summary>
    public static readonly AdamantiumProperty DrawingActionsProperty = Face(nameof(DrawingActions));


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

    public Visibility GraphActions => GetValue<Visibility>(GraphActionsProperty);
    public Visibility DrawingActions => GetValue<Visibility>(DrawingActionsProperty);



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


    // WHAT CAN BE CHOSEN is NOT kept here any more - the grids a plane can wear, the ends an arrow takes, the ways a
    // picture fills its tile. They are the canvas's own (see InfiniteCanvas.GridStyles and the rest), and a line that
    // offers one reaches it through this panel's Canvas. Kept here as well, they were a second list to hold in step
    // with the first for no gain at all.


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
    private PropertyGrid _tools;

    /// <summary>Hands the canvas the grid that shows what is selected, so that a number typed into a row becomes a step
    /// in the canvas's memory and a repaint of the plane. The canvas does the listening because it is the specialised
    /// one: a property grid is a general-purpose control and must not learn what a scene or a history is.</summary>
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_grid != null) _grid.ValueChanged -= OnRowWritten;

        _grid = GetTemplateChild("PART_Selected") as PropertyGrid;
        _tools = GetTemplateChild("PART_Tool") as PropertyGrid;

        // An EDIT can change which sections apply - a picture painted onto a surface brings a texture's lines with it -
        // and nothing else would ever say so: the selection has not changed, and the scene's own signal is about what
        // is on the plane rather than about what a panel should be showing.
        if (_grid != null) _grid.ValueChanged += OnRowWritten;

        Wire();

        // A NEW TEMPLATE MEANS A NEW THEME, and a theme can ship a different default set - so what was kept from the
        // last one is dropped rather than carried into a panel it was not written for.
        _selectionSections = null;
        _toolSections = null;

        // THE SECTIONS, now that there is somewhere to put them. What the panel shows is worked out whenever the
        // selection or the tool changes - and both of those can have happened before this control had a template at
        // all, in which case the answer was handed to nobody.
        ReadSections();
        ReadTool();
    }

    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();

        if (_grid != null) _grid.ValueChanged -= OnRowWritten;

        _grid = null;
        _tools = null;
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

    private void OnSelectionChanged(object sender, EventArgs e)
    {
        // WHAT WAS REACHED FOR LAST, remembered here and nowhere else. Picking something up is a person pointing at
        // the thing they mean; the panel shows it, even if a tool was picked up a moment earlier.
        _toolWanted = false;
        Read();
    }

    private void OnPlaneChanged(object sender, EventArgs e) => ReadStructure();

    private void OnToolChanged(object sender, EventArgs e)
    {
        // ...and picking up a TOOL is a person saying what they are about to do, so its settings come forward - with
        // whatever is selected left exactly as it is. A tool that selects what it touches is the exception: reaching
        // for it is reaching for what is on the plane.
        _toolWanted = Canvas?.Tool is not null and not SelectTool;

        ReadTool();
        ReadSections();
    }

    // Which of the two faces the person asked for last - see the note where the faces are settled.
    private bool _toolWanted;

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
        SetCurrentValue(DrawingActionsProperty, Shown(drawing));
    }

    // WHICH TOOL'S settings belong in the panel. Asked of the tool itself: the canvas holds the tools, and what a pen
    // has to say about itself is that it is a pen.
    // WHAT THE TOOL IN HAND IS SET BY. A tool used to be answered by a flag apiece - pen, eraser, text, shape - so a
    // tool an application added had nowhere at all to put its settings, however easily it got itself a button on the
    // rail. Now the sets say which tool they are for, matched against the tool's own name.
    private void ReadTool() =>
        Fill(_tools, Sections(ToolSetsKey, Canvas?.ToolSections, ref _toolSections), Names(Canvas?.Tool));

    private void ReadStructure()
    {
        if (Canvas is not { } canvas)
        {
            Structure = null;
            Counted = "Nothing drawn yet";
            return;
        }

        // TOPMOST FIRST, so the list reads the way the drawing is stacked. A fresh list every time: the scene's own is
        // edited in place and tells a binding nothing.
        var listed = new List<ICanvasItem>(canvas.ItemsHere());
        listed.Reverse();

        Structure = listed;
        Counted = listed.Count switch
        {
            0 => "Nothing drawn yet",
            1 => "1 item",
            var many => $"{many} items"
        };
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

        // WHETHER THERE IS ANYTHING TO SHOW is the answer this gives - a kind of thing the sets say nothing about has
        // an empty page, and an empty page is not worth turning to. Asked of the SETS and not of a list of classes: a
        // list of classes has to be edited for every new kind, and the one written from an SVG was not on it, so a
        // whole imported drawing could not be painted.
        var lines = Fill(_grid, Sections(SelectionSetsKey, Canvas?.InspectorSections, ref _selectionSections), Names(chosen));

        Fill(_tools, Sections(ToolSetsKey, Canvas?.ToolSections, ref _toolSections), Names(Canvas?.Tool));

        // WHICHEVER WAS REACHED FOR LAST. Picking up a tool is a person saying what they are about to do, and the
        // answer to "what is this tool set to" must not be "let go of what you are holding first": settings are chosen
        // BEFORE the stroke, and dropping the selection to see them means drawing with whatever the last settings were.
        // Touching the selection says the opposite - it is the thing that is being worked on - and the panel follows
        // back.
        var anything = many > 0;
        var tool = _toolWanted || !anything;

        SetCurrentValue(ToolFaceProperty, Shown(ShowsProperties && tool));
        SetCurrentValue(SelectionFaceProperty, Shown(ShowsProperties && !tool && anything && lines > 0));
        SetCurrentValue(StructureFaceProperty, Shown(ShowsStructure));
    }

    /// <summary>The key the default set of SELECTION lines ships under. A theme links the dictionary that holds it, and
    /// an application that wants to replace the lot declares this key of its own.</summary>
    public const string SelectionSetsKey = "CanvasSelectionSections";

    /// <summary>...and the same for the page shown while nothing is selected - what the TOOL in hand is set by.</summary>
    public const string ToolSetsKey = "CanvasToolSections";

    // WHAT THIS PANEL SHOWS, which is content and not a theme's business: the default set is a resource the themes
    // ship, and whatever the canvas was given is laid over it.
    //
    // KEPT, not asked for again. The resource is declared x:Shared="False" - it must be, since a section is a live
    // control and two canvases cannot be handed the same one - so every ask builds a fresh set. Used directly that
    // means every repaint of the panel hands the grid different objects, and everything the person had done to the
    // ones before is gone with them: a block they opened closes itself the moment anything is written.
    private CanvasInspectorSections Sections(string key, CanvasInspectorSections mine, ref CanvasInspectorSections kept)
    {
        kept ??= UIAppContext.Current?.ResourceManager?.FindResource(this, key) as CanvasInspectorSections;

        if (kept == null) return mine;

        // THE LAYING-OVER IS DONE EVERY TIME, and only the default is kept. What an application hands in can be bound,
        // and a binding arrives after the panel has been built - so a merge done once would be a merge done before the
        // application had said anything. It costs a list of references; the SECTIONS in it are the same objects either
        // way, which is what matters: they are what a person has opened and folded.
        return mine == null ? kept : kept.With(mine);
    }

    // What was built from the resource, until a new template says a new theme is in - that one can ship a different
    // default, and what was kept was not written for it.
    private CanvasInspectorSections _selectionSections;
    private CanvasInspectorSections _toolSections;

    // WHAT A THING ANSWERS TO: its class, where a family's lines live, and its own narrowest name, where one kind's do.
    //
    // WHAT THEY ALL ANSWER TO, when several are selected. A name only counts where EVERY one of them carries it: a
    // line written for a rectangle has no meaning for the ellipse beside it, and shown for both it would write corners
    // into something that has none. What is left when they agree about nothing is the empty list - which still matches
    // the sets written for everything, so a mixed selection keeps the lines every kind has (where it stands, how big
    // it is) instead of the panel going blank and looking broken.
    private static IReadOnlyList<string> Names(IReadOnlyList<ICanvasItem> chosen)
    {
        if (chosen == null || chosen.Count == 0) return null;

        var first = chosen[0];

        if (first == null) return null;

        var family = first.GetType().Name;
        var sort = first.Sort;

        for (var i = 1; i < chosen.Count; i++)
        {
            if (chosen[i] == null) return null;

            if (chosen[i].GetType().Name != family) family = null;
            if (!string.Equals(chosen[i].Sort, sort, StringComparison.Ordinal)) sort = null;
        }

        var names = new List<string>(2);

        if (family != null) names.Add(family);
        if (sort != null && sort != family) names.Add(sort);

        return names;
    }

    // A TOOL answers to its own name and its class, for the same reasons.
    // NO TOOL IS STILL A PAGE. The sets whose For is empty are about the CANVAS - the grid, the snap, how far it may
    // zoom - and they belong under every tool and under none. Answered with nothing at all, the page came up blank
    // whenever the hand was empty, and the canvas's own settings were reachable only while a tool was held.
    private static IReadOnlyList<string> Names(ICanvasTool tool) =>
        tool == null ? Array.Empty<string>() : new[] { tool.GetType().Name, tool.Name };

    // Hands a grid the sections that fit. Through SectionsSource, which is the grid's own door for an inspector that is
    // assembled rather than written out - see PropertyGrid.SectionsSource.
    private static int Fill(PropertyGrid grid, CanvasInspectorSections sections, IReadOnlyList<string> names)
    {
        if (grid == null) return 0;

        var wanted = names == null || sections == null ? null : sections.For(names);

        // ONLY WHEN IT IS ACTUALLY DIFFERENT. This is worked out again after every write - a picture painted onto a
        // surface brings a texture's lines with it - and handing over a fresh list of the SAME sections still counts as
        // a change, so the grid would rebuild its rows on every keystroke. A person dragging across a colour surface
        // writes continuously, and the row their popup belongs to was being taken out from under it mid-gesture.
        if (!Same(grid.SectionsSource, wanted)) grid.SectionsSource = wanted;

        return wanted?.Count ?? 0;
    }

    private static bool Same(IEnumerable one, IReadOnlyList<PropertySection> other)
    {
        if (one == null) return other == null;
        if (other == null) return false;

        var at = 0;

        foreach (var section in one)
        {
            if (at >= other.Count || !ReferenceEquals(section, other[at])) return false;

            at++;
        }

        return at == other.Count;
    }

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
