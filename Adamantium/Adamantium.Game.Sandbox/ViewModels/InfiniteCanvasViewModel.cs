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

    public ICanvasTool TextTool { get; } = new TextTool();

    /// <summary>Two erasers and not one with a switch: which of them you want is the same kind of choice as which tool
    /// you want, so it is made in the same place and in the same way.</summary>
    public ICanvasTool ErasePointTool { get; } = new EraseTool(CanvasEraseMode.Point);

    public ICanvasTool EraseStrokeTool { get; } = new EraseTool(CanvasEraseMode.Stroke);

    /// <summary>The toolbox: each entry is the same tool with a different factory, because what a control IS belongs to
    /// the application and not to the canvas. A NEW control every time it is used - one instance handed out twice would
    /// be one control that cannot be in two places.</summary>
    public ICanvasTool ButtonTool { get; } = new ElementTool(() => new Button { Content = "Button" });

    public ICanvasTool CheckTool { get; } =
        new ElementTool(() => new CheckBox { Content = "Check me" }, new Size(130, 28));

    public ICanvasTool BoxTool { get; } =
        new ElementTool(() => new TextBox { Text = "Editable" }, new Size(160, 30));

    [Bindable] private ICanvasTool _tool;

    partial void OnToolChanged(ICanvasTool value)
    {
        RaisePropertyChanged(nameof(IsSelecting));
        RaisePropertyChanged(nameof(IsDrawing));
        RaisePropertyChanged(nameof(IsRectangle));
        RaisePropertyChanged(nameof(IsEllipse));
        RaisePropertyChanged(nameof(IsLine));
        RaisePropertyChanged(nameof(IsTyping));
        RaisePropertyChanged(nameof(IsErasingPoints));
        RaisePropertyChanged(nameof(IsErasingStrokes));
        RaisePropertyChanged(nameof(IsButton));
        RaisePropertyChanged(nameof(IsCheck));
        RaisePropertyChanged(nameof(IsBox));
    }

    // One property per button rather than a converter: a toggle button asks a yes/no question, and the answer to "is
    // this the current tool" is exactly that.
    public bool IsSelecting => ReferenceEquals(_tool, SelectTool);
    public bool IsDrawing => ReferenceEquals(_tool, PenTool);
    public bool IsRectangle => ReferenceEquals(_tool, RectangleTool);
    public bool IsEllipse => ReferenceEquals(_tool, EllipseTool);
    public bool IsLine => ReferenceEquals(_tool, LineTool);
    public bool IsTyping => ReferenceEquals(_tool, TextTool);
    public bool IsErasingPoints => ReferenceEquals(_tool, ErasePointTool);
    public bool IsErasingStrokes => ReferenceEquals(_tool, EraseStrokeTool);
    public bool IsButton => ReferenceEquals(_tool, ButtonTool);
    public bool IsCheck => ReferenceEquals(_tool, CheckTool);
    public bool IsBox => ReferenceEquals(_tool, BoxTool);

    [Command] private void UseSelect() => Tool = SelectTool;
    [Command] private void UsePen() => Tool = PenTool;
    [Command] private void UseRectangle() => Tool = RectangleTool;
    [Command] private void UseEllipse() => Tool = EllipseTool;
    [Command] private void UseLine() => Tool = LineTool;
    [Command] private void UseText() => Tool = TextTool;

    [Command] private void UseErasePoint() => Tool = ErasePointTool;
    [Command] private void UseEraseStroke() => Tool = EraseStrokeTool;
    [Command] private void UseButton() => Tool = ButtonTool;
    [Command] private void UseCheck() => Tool = CheckTool;
    [Command] private void UseBox() => Tool = BoxTool;

    /// <summary>Whether a press on a control on the plane MOVES it or presses it.</summary>
    [Bindable] private bool _designMode = true;

    [Bindable] private Color _inkColour = Colors.White;

    /// <summary>What the pen paints with. A NEW brush on every change, never the same one repainted: a finished stroke
    /// keeps the brush it was drawn with, so repainting it would go back and recolour everything already on the plane.
    /// </summary>
    public Brush Ink { get; private set; } = new SolidColorBrush(Colors.White);

    partial void OnInkColourChanged(Color value)
    {
        Ink = new SolidColorBrush(value);
        RaisePropertyChanged(nameof(Ink));
    }

    [Bindable] private Color _fillColour = Colors.SteelBlue;

    public Brush ShapeFill { get; private set; } = new SolidColorBrush(Colors.SteelBlue);

    partial void OnFillColourChanged(Color value)
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

    [Bindable] private Color _snapColour = Colors.DodgerBlue;

    public Brush SnapMark { get; private set; } = new SolidColorBrush(Colors.DodgerBlue);

    partial void OnSnapColourChanged(Color value)
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
    public string Camera =>
        $"scale {_scale:0.###}x     world origin at ({_offset.X:0}, {_offset.Y:0}) px on screen";

    partial void OnScaleChanged(double value) => RaisePropertyChanged(nameof(Camera));

    partial void OnOffsetChanged(Vector2 value) => RaisePropertyChanged(nameof(Camera));
}
