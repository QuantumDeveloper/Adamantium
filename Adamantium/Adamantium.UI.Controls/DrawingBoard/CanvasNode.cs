using System.Collections.Generic;
using System.Collections.ObjectModel;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Animation;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>A NODE of a graph: a titled block with sockets down its sides - the thing a blueprint editor is made of.
/// <para>A real control and not something drawn on the plane, which is the decision this whole editor rests on: what a
/// node is FOR is the editable things inside it - fields, switches, lists - and a control has those already. Put on an
/// <see cref="InfiniteCanvas"/> through an <see cref="ElementItem"/>, it moves, scales and is selected like anything
/// else there, and nothing about the canvas has to learn what a node is.</para>
/// <para>How many sockets it has is a NUMBER here, because that is what an application usually knows: three inputs and
/// one output. Anything more - what they are called, what colour each is - is said on the pins themselves, which stay
/// put when the count does not change.</para>
/// <para>A CONTENT CONTROL, so what is inside a node is whatever the application puts there - a number field, a colour
/// swatch, a picture, a list. That is what a node is for, and it is why this is a control at all; the sockets and the
/// strip are the frame around it. The content sits BETWEEN the two socket columns, where every editor puts it.</para>
/// </summary>
public class CanvasNode : ContentControl
{
    private readonly ObservableCollection<CanvasNodePin> _inputs = new();
    private readonly ObservableCollection<CanvasNodePin> _outputs = new();
    private readonly Dictionary<CanvasNodePin, Vector2> _places = new();

    private IUIComponent _header;
    private IUIComponent _content;
    private IUIComponent _fold;
    private CanvasNodeSocket _foldedIn;
    private CanvasNodeSocket _foldedOut;

    private bool _placed;
    private double _reach;

    public static readonly AdamantiumProperty TitleProperty = AdamantiumProperty.Register(nameof(Title),
        typeof(Object), typeof(CanvasNode),
        new PropertyMetadata("Node", PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty InputsProperty = AdamantiumProperty.Register(nameof(Inputs),
        typeof(Int32), typeof(CanvasNode),
        new PropertyMetadata(1, PropertyMetadataOptions.AffectsMeasure, OnPinCountChanged));

    public static readonly AdamantiumProperty OutputsProperty = AdamantiumProperty.Register(nameof(Outputs),
        typeof(Int32), typeof(CanvasNode),
        new PropertyMetadata(1, PropertyMetadataOptions.AffectsMeasure, OnPinCountChanged));

    /// <summary>WHAT SORT of node this is, in the application's own words - "Multiply", "Texture". Empty for a node
    /// nobody claimed.
    /// <para>Not the same thing as <see cref="Title"/>, and the difference is the whole point: a title is a LABEL, which
    /// a person edits and renames, and a graph that recognised its nodes by their titles would lose one the first time
    /// somebody renamed it. This is never shown and never edited - it is what the application looks up to know what the
    /// node DOES.</para>
    /// <para>On the node rather than in a table beside it, so it travels with the node through copying, undo and the
    /// file without anybody having to remember to carry it.</para></summary>
    public static readonly AdamantiumProperty KindProperty = AdamantiumProperty.Register(nameof(Kind),
        typeof(String), typeof(CanvasNode), new PropertyMetadata(String.Empty));

    /// <summary>Folded down to its title strip. A big graph is read by folding what is finished, and what is folded
    /// keeps its sockets: they move to the two edges of the strip rather than going away, because folding a node is
    /// exactly what you do to one you are still wiring.</summary>
    public static readonly AdamantiumProperty IsCollapsedProperty = AdamantiumProperty.Register(nameof(IsCollapsed),
        typeof(Boolean), typeof(CanvasNode),
        new PropertyMetadata(false,
            PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.BindsTwoWayByDefault, OnFoldChanged));

    public static readonly AdamantiumProperty AccentProperty = AdamantiumProperty.Register(nameof(Accent),
        typeof(Brush), typeof(CanvasNode),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

    /// <summary>A SOCKET DRAGGED TO ANOTHER PLACE on its own side - from where it was to where it was let go.
    /// <para>The node only says it happened: what the sockets ARE belongs to whoever handed them over, and moving one
    /// here would be moving the picture while the graph stayed as it was. Wires are untouched by a move either way - a
    /// wire sits on the socket itself, not on its place in the list.</para></summary>
    public event System.EventHandler<CanvasPinOrderEventArgs> PinsReordered;


    public static readonly AdamantiumProperty PinSizeProperty = AdamantiumProperty.Register(nameof(PinSize),
        typeof(Double), typeof(CanvasNode),
        new PropertyMetadata(10.0, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty PinColorProperty = AdamantiumProperty.Register(nameof(PinColor),
        typeof(Brush), typeof(CanvasNode),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender, OnPinColorChanged));

    // REGISTERED, not plain properties: a template binds to them with {TemplateBinding}, and that reads a registered
    // property and nothing else. As plain ones the two lists of sockets simply never arrived, and a node with no
    // sockets measured to nothing at all - a blank space on the plane where a node should be.
    public static readonly AdamantiumProperty InputPinsProperty = AdamantiumProperty.Register(nameof(InputPins),
        typeof(ObservableCollection<CanvasNodePin>), typeof(CanvasNode),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty OutputPinsProperty = AdamantiumProperty.Register(nameof(OutputPins),
        typeof(ObservableCollection<CanvasNodePin>), typeof(CanvasNode),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    public CanvasNode()
    {
        SetValue(InputPinsProperty, _inputs);
        SetValue(OutputPinsProperty, _outputs);

        Rebuild(_inputs, Inputs, true);
        Rebuild(_outputs, Outputs, false);

        MouseDown += OnGripPressed;
        MouseUp += OnGripReleased;
    }

    // THE MOVE BEING SHOWN, while the grip is held. None of it is a change to the node: the held row is drawn where
    // the hand is and the others step aside, and the sockets themselves are not touched until the hand lets go. A
    // socket taken out and put back in the middle of a gesture is a socket that, for that moment, is not on the node -
    // and everything reading the node by position believes it.
    private CanvasNodePin _moving;
    private int _movingFrom = -1;
    private int _movingTo = -1;
    private double _grabbed;
    private double _step;
    private readonly List<IUIComponent> _rows = new();
    private readonly List<double> _aside = new();

    // The rows still settling after the hand let go, and what is to be said once they are down.
    private List<IUIComponent> _landing;
    private CanvasPinOrderEventArgs _landed;

    private const double SlideSeconds = 0.12;

    /// <summary>The press that starts a MOVE - on the grip and nowhere else. The pin itself is where a wire is pulled
    /// from, and one target cannot mean two things; the grip is the second target, and it is the only one that moves a
    /// socket.
    /// <para>Taken on MouseDown, which BUBBLES: the canvas drags a node by a press on it, and a press this one keeps
    /// has to be a press the canvas never sees. MouseLeftButtonDown is Direct - the canvas is handed its own args and
    /// nothing marked handled here reaches them, which is how the grip dragged the whole node.</para></summary>
    private void OnGripPressed(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButtons.Left) return;
        if (Grip(e.OriginalSource as IUIComponent) is not { } held) return;

        // A move still settling is finished NOW: two of them at once would each be undoing the other's offsets.
        Down();

        var side = held.Pin.IsInput ? _inputs : _outputs;
        var from = side.IndexOf(held.Pin);

        if (from < 0 || !Line(held.Row, side.Count)) return;

        _moving = held.Pin;
        _movingFrom = from;
        _movingTo = from;
        _grabbed = e.GetPosition(this).Y;

        // The held row leads: it follows the hand exactly, so it is the one row that must NOT smooth its way there.
        Smooth(_rows[from], false);
        Raise(_rows[from], true);

        CaptureMouse();
        e.Handled = true;
    }

    // The rows of one side, in the order they stand in, and how far apart they are. Taken from the tree because the
    // template decides what a row IS - a node knows it has pins, not what drawing one looks like.
    private bool Line(IUIComponent row, int count)
    {
        _rows.Clear();
        _aside.Clear();

        if (row?.VisualParent is not { } panel) return false;

        foreach (var child in panel.VisualChildren)
        {
            if (child is IUIComponent visual && visual.Visibility == Visibility.Visible) _rows.Add(visual);
        }

        if (_rows.Count != count || _rows.Count == 0)
        {
            _rows.Clear();
            return false;
        }

        for (var i = 0; i < _rows.Count; i++) _aside.Add(0);

        // From where the rows were PLACED, which is a fact about the last arrangement - not from where they are drawn,
        // which is a fact about the last frame and is exactly what this gesture is about to start changing.
        _step = _rows.Count > 1
            ? _rows[1].Bounds.Y - _rows[0].Bounds.Y
            : _rows[0].Bounds.Height;

        return _step > 0;
    }

    /// <summary>Put on whatever element in a socket's row is its GRIP - the one place a press means "move this socket"
    /// instead of "pull a wire from it". A mark and not a control: what the grip LOOKS like belongs to whoever draws
    /// the row, and the node only needs to recognise it.</summary>
    public static readonly AdamantiumProperty IsSocketGripProperty = AdamantiumProperty.RegisterAttached("IsSocketGrip",
        typeof(Boolean), typeof(AdamantiumComponent), new PropertyMetadata(false));

    public static void SetIsSocketGrip(AdamantiumComponent element, Boolean value) =>
        element?.SetValue(IsSocketGripProperty, value);

    public static Boolean GetIsSocketGrip(AdamantiumComponent element) =>
        element?.GetValue<Boolean>(IsSocketGripProperty) ?? false;

    // What the press landed on, when it landed on a row's GRIP: the pin, and the row it is drawn as. Walked up from
    // what was actually hit, because the grip is an element of its own and the press lands on whatever is topmost
    // inside it; the row is the HIGHEST thing still standing for that pin, which is the one the list places.
    private static (CanvasNodePin Pin, IUIComponent Row)? Grip(IUIComponent from)
    {
        var grip = false;
        CanvasNodePin pin = null;
        IUIComponent row = null;

        for (var at = from; at != null; at = at.VisualParent)
        {
            if (at is AdamantiumComponent component && GetIsSocketGrip(component)) grip = true;
            if (!grip) continue;

            if (at is IFundamentalUIComponent { DataContext: CanvasNodePin found } && (pin == null || ReferenceEquals(found, pin)))
            {
                pin = found;
                row = at;
                continue;
            }

            if (pin != null) break;
        }

        return pin == null ? null : (pin, row);
    }

    /// <summary>...and the move itself, which is a PICTURE of one: the held row goes where the hand is, the rows it has
    /// passed step aside by exactly one row, and the gap left under the pointer is the place the socket will take.
    /// Nothing crosses to the other side - that would be a change of direction, which a drag along an edge cannot
    /// mean.</summary>
    protected override void OnMouseMove(object sender, MouseEventArgs e)
    {
        base.OnMouseMove(sender, e);

        if (_moving == null) return;

        var far = e.GetPosition(this).Y - _grabbed;
        var up = -_step * _movingFrom;
        var down = _step * (_rows.Count - 1 - _movingFrom);

        far = far < up ? up : far > down ? down : far;

        Aside(_rows[_movingFrom], far);

        var to = _movingFrom + (int)System.Math.Round(far / _step);
        to = to < 0 ? 0 : to > _rows.Count - 1 ? _rows.Count - 1 : to;

        if (to != _movingTo)
        {
            _movingTo = to;
            Part();
        }

        Moved();
    }

    // THE SOCKET WENT WITH ITS ROW, so the wires on it are drawn from places a frame out of date - and what draws them
    // is the canvas, which nothing about this node has told.
    private void Moved()
    {
        _placed = false;
        InvalidateRender(false);

        for (IUIComponent at = this; at != null; at = at.VisualParent)
        {
            if (at is not CanvasElementLayer { Owner: { } canvas }) continue;

            canvas.Repaint();
            return;
        }
    }

    // The rows the held one has passed, stepping aside by exactly one row each - which is what opens the gap it will
    // drop into. Only the ones whose place actually changed are told, or a slide would restart every frame.
    private void Part()
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            if (i == _movingFrom) continue;

            var aside = _movingFrom < _movingTo
                ? i > _movingFrom && i <= _movingTo ? -_step : 0
                : i >= _movingTo && i < _movingFrom ? _step : 0;

            if (System.Math.Abs(aside - _aside[i]) < 0.5) continue;

            _aside[i] = aside;
            Smooth(_rows[i], true);
            Aside(_rows[i], aside);
        }
    }

    /// <summary>Letting go settles the held row into the gap that was made for it, and only THEN is the move a fact:
    /// what the sockets are belongs to whoever handed them over, and until they have moved one, everything on screen is
    /// a picture of a move.</summary>
    private void OnGripReleased(object sender, MouseButtonEventArgs e)
    {
        if (_moving == null || e.ChangedButton != MouseButtons.Left) return;

        var input = _moving.IsInput;
        var from = _movingFrom;
        var to = _movingTo;
        var rows = new List<IUIComponent>(_rows);
        var held = rows[from];

        _moving = null;
        _movingFrom = -1;
        _movingTo = -1;
        _rows.Clear();
        _aside.Clear();

        ReleaseMouseCapture();
        e.Handled = true;

        Raise(held, false);

        _landing = rows;
        _landed = from == to ? null : new CanvasPinOrderEventArgs(input, from, to);

        Smooth(held, true);
        Aside(held, (to - from) * _step, Down);
    }

    // THE ROWS ARE DOWN: the sockets take their new order, and the offsets that were standing in for it go in the same
    // breath - the rows are in the right places now, and an offset would move them out of them.
    private void Down()
    {
        if (_landing == null) return;

        var rows = _landing;
        var said = _landed;

        _landing = null;
        _landed = null;

        foreach (var row in rows)
        {
            Smooth(row, false);
            Aside(row, 0);
        }

        if (said != null) PinsReordered?.Invoke(this, said);

        // ROWS AND EVERYTHING IN THEM ARE DRAWN AGAIN. In one frame these rows both MOVED (the offsets they were held
        // at go back to nothing) and CHANGED (each stands for another socket now, so a middle appears or goes) - and
        // what is already drawn for a row that only moved is what it looked like before it changed.
        InvalidateRender(true);

        Moved();
    }

    // WHERE A ROW IS DRAWN, which is not where it was placed. A transform and not a layout change: the list is not
    // rearranged until the move is a fact, and a row that moved by being re-measured would drag every wire on the node
    // through a layout pass per frame of the gesture.
    private static void Aside(IUIComponent row, double far, System.Action done = null)
    {
        var transform = Moves(row);

        if (transform == null)
        {
            done?.Invoke();
            return;
        }

        if (done == null)
        {
            transform.TranslateY = far;
            return;
        }

        // The LAST slide of a gesture is the one somebody waits on, so it is asked for outright rather than left to
        // the implicit transition - only an animation says when it is over.
        transform.BeginAnimation(Transform.TranslateYProperty, new DoubleAnimation
        {
            From = transform.TranslateY,
            To = far,
            Duration = System.TimeSpan.FromSeconds(SlideSeconds),
            Easing = new CubicEasing { Mode = EasingMode.Out }
        }, done);
    }

    private static Transform Moves(IUIComponent row)
    {
        if (row is not UIComponent ui) return null;

        if (ui.RenderTransform is { } already) return already;

        var transform = new Transform();
        ui.RenderTransformOrigin = Vector2.Zero;
        ui.RenderTransform = transform;

        return transform;
    }

    // Whether a row SLIDES to where it is put or simply appears there. The held row does not smooth - it is the hand,
    // and a hand that lags is a gesture that feels broken; every other row does, because a row that jumps a place is a
    // list that rearranged itself rather than one thing moving through it.
    private static void Smooth(IUIComponent row, bool on)
    {
        var transform = Moves(row);
        if (transform == null) return;

        var already = Sliding(transform);

        if (!on)
        {
            if (already != null) transform.Transitions.Remove(already);
            transform.CancelAnimation(Transform.TranslateYProperty);
            return;
        }

        if (already != null) return;

        transform.Transitions.Add(new DoubleTransition
        {
            Property = nameof(Transform.TranslateY),
            Duration = System.TimeSpan.FromSeconds(SlideSeconds),
            Easing = new CubicEasing { Mode = EasingMode.Out }
        });
    }

    private static Transition Sliding(Transform transform)
    {
        foreach (var transition in transform.Transitions)
        {
            if (transition.Property == nameof(Transform.TranslateY)) return transition;
        }

        return null;
    }

    // The row in hand is the one being read, so it is drawn a shade lighter than the ones it is passing.
    private static void Raise(IUIComponent row, bool on)
    {
        if (row is not UIComponent ui) return;

        ui.Opacity = on ? 0.75 : 1;
    }

    /// <summary>What the node is called - the strip across its top, which is the first thing read in a graph of fifty
    /// of them.</summary>
    public Object Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>What sort of node this is, for the application to look up. Never shown - see <see cref="KindProperty"/>.
    /// </summary>
    public String Kind
    {
        get => GetValue<String>(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>How many sockets down the left. Changing it keeps the pins that survive: a node that went from three
    /// inputs to four would otherwise lose the names and colours of the first three.</summary>
    public Int32 Inputs
    {
        get => GetValue<Int32>(InputsProperty);
        set => SetValue(InputsProperty, value);
    }

    /// <summary>How many sockets down the right.</summary>
    public Int32 Outputs
    {
        get => GetValue<Int32>(OutputsProperty);
        set => SetValue(OutputsProperty, value);
    }

    /// <summary>The colour of the title strip. What a graph editor says the KIND of a node with, and the reason a
    /// screenful of them can be read at a glance. Null takes the theme's accent.</summary>
    /// <summary>Whether the node is folded down to its title strip.</summary>
    public Boolean IsCollapsed
    {
        get => GetValue<Boolean>(IsCollapsedProperty);
        set => SetValue(IsCollapsedProperty, value);
    }

    public Brush Accent
    {
        get => GetValue<Brush>(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    /// <summary>How big a socket is drawn. Sockets sit ON the edge, half in and half out, so this is also how far the
    /// node reaches past its own box.</summary>
    public Double PinSize
    {
        get => GetValue<Double>(PinSizeProperty);
        set => SetValue(PinSizeProperty, value);
    }

    /// <summary>What colour a socket is drawn in when it has not been given one of its own. A pin that states its own
    /// <see cref="CanvasNodePin.Color"/> - because it carries a TYPE, the way a graph editor says what may be joined to
    /// what - keeps it.</summary>
    public Brush PinColor
    {
        get => GetValue<Brush>(PinColorProperty);
        set => SetValue(PinColorProperty, value);
    }

    /// <summary>The sockets down the left, in order. Held rather than made on demand, so that a name or a colour put on
    /// one stays there.</summary>
    public ObservableCollection<CanvasNodePin> InputPins => _inputs;

    /// <summary>The sockets down the right.</summary>
    public ObservableCollection<CanvasNodePin> OutputPins => _outputs;

    /// <summary>Takes ONE socket off, wherever it sits.
    /// <para>Not the same thing as asking for fewer, which is the only other way there is: a count can only take things
    /// off the END, so "drop the second of three" said as a count drops the third and leaves the second where it was.
    /// Everything said about the sockets that stay - their names, their colours, one day what is wired to them - stays
    /// with them.</para></summary>
    public Boolean Remove(CanvasNodePin pin)
    {
        if (pin == null) return false;

        if (_inputs.Remove(pin))
        {
            SetCurrentValue(InputsProperty, _inputs.Count);
            return true;
        }

        if (_outputs.Remove(pin))
        {
            SetCurrentValue(OutputsProperty, _outputs.Count);
            return true;
        }

        return false;
    }

    /// <summary>Whether this node is the one holding that socket. What an application asks when something hands it a
    /// pin and nothing else - a button in an inspector row, which knows the socket it sits on and no more.</summary>
    public Boolean Holds(CanvasNodePin pin) => pin != null && (_inputs.Contains(pin) || _outputs.Contains(pin));

    /// <summary>WHERE a socket is, in this node's own coordinates. Null before the node has been laid out, and for a
    /// pin that is not on it.
    /// <para>Asked of the node because the node is what knows: where a socket sits is a fact about the TEMPLATE - which
    /// side, how far down, how far it hangs over the edge - and each theme answers it differently. A connection that
    /// worked it out for itself would be a second copy of every template's arithmetic, wrong the moment a theme changed
    /// a margin.</para>
    /// <para>Answered from a record taken ONCE PER LAYOUT rather than by looking each time. What looking costs is a
    /// walk of the node's whole visual tree and a walk back up it per socket, and this is asked on every mouse move
    /// (for the cursor), on every press, and twice per wire per pass of the scene. Sockets do not move between
    /// arrangements, so asking again between them can only produce the same answer more slowly.</para></summary>
    public Vector2? Where(CanvasNodePin pin)
    {
        if (pin == null) return null;

        if (!_placed) Place();

        return _places.TryGetValue(pin, out var at) ? at : null;
    }

    /// <summary>Which socket is at a point given in this node's own coordinates, or null. What the gesture that draws
    /// connections asks: a socket is a small thing to aim at, so <paramref name="reach"/> widens it by however much the
    /// hand is allowed to miss by.</summary>
    public CanvasNodePin PinAt(Vector2 local, Double reach = 0)
    {
        // Not while it is FOLDED: every socket of a side is at one point then, so any answer here would be a guess at
        // which one was meant - and a wire quietly attached to the wrong socket is worse than one that would not go on.
        if (IsCollapsed) return null;

        if (!_placed) Place();

        foreach (var (pin, at) in _places)
        {
            var wide = _reach + reach;

            if (Math.Abs(local.X - at.X) <= wide && Math.Abs(local.Y - at.Y) <= wide) return pin;
        }

        return null;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _header = GetTemplateChild("PART_Header") as IUIComponent;
        _content = GetTemplateChild("PART_ContentPresenter") as IUIComponent;
        _fold = GetTemplateChild("PART_Fold") as IUIComponent;
        _foldedIn = GetTemplateChild("PART_FoldedIn") as CanvasNodeSocket;
        _foldedOut = GetTemplateChild("PART_FoldedOut") as CanvasNodeSocket;

        ShowStubs();
    }

    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();

        _header = null;
        _content = null;
        _fold = null;
        _foldedIn = null;
        _foldedOut = null;
    }

    /// <summary>Whether a press here means "pick the node up" rather than "use what is in the node".
    /// <para>THE STRIP IS THE HANDLE. A node has to be both - dragged about the plane and operated, because what is in
    /// one is a field, a switch, a list, and a node whose contents cannot be clicked is a picture of a node. Every graph
    /// editor answers this the same way: the title bar moves it, everything under the title is live.</para>
    /// <para>The node's own chrome counts as the strip: the frame round the body, the gap between the rows, the
    /// sockets. What does NOT count is anything the application put inside.</para></summary>
    public Boolean IsHandle(Object source)
    {
        if (source is not IUIComponent at) return true;

        for (; at != null; at = at.VisualParent)
        {
            // The fold is a BUTTON standing in the strip, and a button is pressed, not grabbed - asked about before the
            // strip it sits in, or the strip would answer for it.
            if (ReferenceEquals(at, _fold)) return false;
            if (ReferenceEquals(at, _header)) return true;
            if (ReferenceEquals(at, _content)) return false;
            if (ReferenceEquals(at, this)) return true;
        }

        return true;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);

        // The sockets have just been put somewhere, so whatever was recorded about where they were is now a guess. The
        // record is not rebuilt here - it is rebuilt when somebody next asks, which for a node nobody is wiring is
        // never.
        _placed = false;

        return size;
    }

    // ONE walk, recording every socket's middle. A socket is a CanvasNodeSocket - see the type's own note for why it is
    // a type at all - and it knows which pin it stands for.
    private void Place()
    {
        _places.Clear();
        _reach = 0;

        if (IsCollapsed) Converge();
        else Gather(this);

        _placed = true;
    }

    // A FOLDED node has no socket of its own for each pin - it has one stub a side, and every wire of that side comes
    // to it. Which is the honest picture: folded, the node no longer says which of its sockets a wire goes to.
    private void Converge()
    {
        Bunch(_inputs, _foldedIn);
        Bunch(_outputs, _foldedOut);
    }

    private void Bunch(ObservableCollection<CanvasNodePin> pins, CanvasNodeSocket stub)
    {
        if (stub is not { Visibility: Visibility.Visible } || stub.MiddleIn(this) is not { } middle) return;

        foreach (var pin in pins) _places[pin] = middle;

        _reach = Math.Max(_reach, Math.Max(stub.RenderSize.Width, stub.RenderSize.Height) / 2);
    }

    // Shown by the NODE and not by a trigger: whether a stub belongs depends on there being sockets on that side, and
    // a trigger cannot ask that.
    private void ShowStubs()
    {
        var folded = IsCollapsed;

        if (_foldedIn != null)
        {
            _foldedIn.Visibility = folded && _inputs.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        if (_foldedOut != null)
        {
            _foldedOut.Visibility = folded && _outputs.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // Only what is SHOWN. A folded node keeps a second set of sockets on its strip and puts the rows away, so both sets
    // exist in the tree at once - and the one that is not shown has no place, whatever its last arrangement said.
    private void Gather(IUIComponent within)
    {
        if (within.Visibility != Visibility.Visible) return;

        if (within is CanvasNodeSocket socket && socket.Pin != null && socket.MiddleIn(this) is { } middle)
        {
            _places[socket.Pin] = middle;
            _reach = Math.Max(_reach, Math.Max(socket.RenderSize.Width, socket.RenderSize.Height) / 2);
        }

        foreach (var child in within.VisualChildren)
        {
            if (child is IUIComponent visual) Gather(visual);
        }
    }

    // Folding moves every socket, and where they are is remembered - so the memory has to go with the fold rather than
    // waiting for a layout pass that may not come until somebody drags something.
    private static void OnFoldChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not CanvasNode node) return;

        Trace(node, e);

        node._placed = false;
        node.ShowStubs();
    }

    // WHO FOLDED IT. ADAM_FOLD_TRACE=<path> writes every change of this property with the stack that made it - a probe
    // for a fold nobody asked for, and off unless asked for.
    private static void Trace(CanvasNode node, AdamantiumPropertyChangedEventArgs e)
    {
        if (System.Environment.GetEnvironmentVariable("ADAM_FOLD_TRACE") is not { Length: > 0 } path) return;

        try
        {
            System.IO.File.AppendAllText(path,
                $"{DateTime.Now:HH:mm:ss.fff} {node.Title}: {e.OldValue} -> {e.NewValue}{System.Environment.NewLine}" +
                System.Environment.StackTrace + System.Environment.NewLine + System.Environment.NewLine);
        }
        catch (System.IO.IOException)
        {
        }
    }

    private static void OnPinCountChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not CanvasNode node) return;

        node.Rebuild(node._inputs, node.Inputs, true);
        node.Rebuild(node._outputs, node.Outputs, false);
        node.ShowStubs();
    }

    // The theme sets PinColor AFTER the pins are built, so this is what actually colours them. Only the pins still
    // wearing the previous default are touched: one given a colour of its own said something the theme did not, and a
    // theme change must not take that away.
    private static void OnPinColorChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not CanvasNode node) return;

        var was = e.OldValue as Brush;
        Repaint(node._inputs, was, node.PinColor);
        Repaint(node._outputs, was, node.PinColor);
    }

    // The lowest number nothing is already called. Counting instead would hand the new socket a name one of the others
    // has: drop "In 2" of three and the next one asked for is the third again.
    private static String Free(ObservableCollection<CanvasNodePin> pins, String prefix)
    {
        for (var number = 1; ; number++)
        {
            var name = prefix + number;
            var taken = false;

            foreach (var pin in pins)
            {
                if (pin.Name != name) continue;

                taken = true;
                break;
            }

            if (!taken) return name;
        }
    }

    private static void Repaint(ObservableCollection<CanvasNodePin> pins, Brush was, Brush now)
    {
        foreach (var pin in pins)
        {
            if (pin.Color == null || ReferenceEquals(pin.Color, was)) Wear(pin, now);
        }
    }

    // A DEFAULT, written as one. What a pin is coloured when nobody has said otherwise is the node's business, but a
    // socket that says what it carries beats it - and a plain write would not let it: a local value outranks a binding,
    // so the colour of a kind never reached a pin the node had already painted.
    private static void Wear(CanvasNodePin pin, Brush colour) =>
        pin.SetValue(CanvasNodePin.ColorProperty, colour, ValuePriority.Style);

    // Grown and trimmed rather than rebuilt: the pins that stay are the same objects, so what was said about them -
    // a name, a colour, and one day whatever is connected to them - survives a change of count.
    private void Rebuild(ObservableCollection<CanvasNodePin> pins, Int32 wanted, Boolean input)
    {
        wanted = Math.Max(0, wanted);

        while (pins.Count > wanted) pins.RemoveAt(pins.Count - 1);

        while (pins.Count < wanted)
        {
            var pin = new CanvasNodePin { IsInput = input };

            // A DEFAULT AND NOT A DECISION, so it is written BELOW where a binding writes. A plain set lands at Local,
            // which outranks a binding and masks it for good - the name a socket is called would never reach the pin
            // standing for it. A current value is no better here: it lands in the binding's own slot, and a two-way
            // binding hands what is there BACK to the socket - a node whose sockets are A, B, Amount came back from a
            // socket being moved called In 1, In 2, In 3.
            pin.SetValue(CanvasNodePin.NameProperty, Free(pins, input ? "In " : "Out "), ValuePriority.Style);

            Wear(pin, PinColor);
            pins.Add(pin);
        }
    }
}
