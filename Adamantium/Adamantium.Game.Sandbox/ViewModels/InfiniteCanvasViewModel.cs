using System.Collections.Generic;
using Adamantium.Mathematics;
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
        Tools.Add(PolygonTool);
        Tools.Add(ButtonTool);
        Tools.Add(CheckTool);
        Tools.Add(BoxTool);

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

    private void OnSceneChanged(object sender, System.EventArgs e) => RaisePropertyChanged(nameof(Drawn));

    public IReadOnlyList<CanvasGridStyle> GridStyles { get; } =
        [CanvasGridStyle.Dots, CanvasGridStyle.Lines, CanvasGridStyle.None, CanvasGridStyle.Transparent];

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

    [Command]
    private void Erase()
    {
        Scene.Clear();
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
        RaisePropertyChanged(nameof(HasCorners));
        RaisePropertyChanged(nameof(HasSides));
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

    // Within the one Shape section, the lines that belong to ONE shape. Rounding is a rectangle's and sides are a
    // polygon's; an ellipse has neither. A line that means nothing to what is selected is worse than no line, because
    // it invites a number that will quietly do nothing.
    public bool HasCorners => HasShape(CanvasShape.Rectangle);

    public bool HasSides => HasShape(CanvasShape.Polygon);

    // The CONTEXT BAR's actions. Actions and not properties: what a thing looks like is the inspector's business and
    // saying it twice would be two places to keep in step - what belongs beside the selection is what you do to it.
    [Command] private void BringToFront() => Reorder(front: true);

    [Command] private void SendToBack() => Reorder(front: false);

    [Command] private void DeleteSelected()
    {
        if (_selection is not { Count: > 0 } chosen) return;

        foreach (var item in chosen) Scene.Remove(item);

        // Cleared THROUGH the property, because that is the one the canvas is bound to - clearing a local copy would
        // leave a frame drawn round things that are no longer in the scene.
        Selection = System.Array.Empty<ICanvasItem>();
    }

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

    partial void OnScaleChanged(double value)
    {
        RaisePropertyChanged(nameof(Camera));
        RaisePropertyChanged(nameof(ZoomText));
    }

    partial void OnOffsetChanged(Vector2 value) => RaisePropertyChanged(nameof(Camera));
}
