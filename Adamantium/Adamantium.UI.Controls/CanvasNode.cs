using System.Collections.Generic;
using System.Collections.ObjectModel;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>A NODE of a graph: a titled block with sockets down its sides - the thing a blueprint editor is made of.
/// <para>A real control and not something drawn on the plane, which is the decision this whole editor rests on: what a
/// node is FOR is the editable things inside it - fields, switches, lists - and a control has those already. Put on an
/// <see cref="InfiniteCanvas"/> through an <see cref="ElementItem"/>, it moves, scales and is selected like anything
/// else there, and nothing about the canvas has to learn what a node is.</para>
/// <para>How many sockets it has is a NUMBER here, because that is what an application usually knows: three inputs and
/// one output. Anything more - what they are called, what colour each is - is said on the pins themselves, which stay
/// put when the count does not change.</para></summary>
public class CanvasNode : Control
{
    private readonly ObservableCollection<CanvasNodePin> _inputs = new();
    private readonly ObservableCollection<CanvasNodePin> _outputs = new();
    private readonly Dictionary<CanvasNodePin, Vector2> _places = new();

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

    public static readonly AdamantiumProperty AccentProperty = AdamantiumProperty.Register(nameof(Accent),
        typeof(Brush), typeof(CanvasNode),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

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
    }

    /// <summary>What the node is called - the strip across its top, which is the first thing read in a graph of fifty
    /// of them.</summary>
    public Object Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
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
        if (!_placed) Place();

        foreach (var (pin, at) in _places)
        {
            var wide = _reach + reach;

            if (Math.Abs(local.X - at.X) <= wide && Math.Abs(local.Y - at.Y) <= wide) return pin;
        }

        return null;
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

        Gather(this);

        _placed = true;
    }

    private void Gather(IUIComponent within)
    {
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

    private static void OnPinCountChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not CanvasNode node) return;

        node.Rebuild(node._inputs, node.Inputs, true);
        node.Rebuild(node._outputs, node.Outputs, false);
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
            if (pin.Color == null || ReferenceEquals(pin.Color, was)) pin.Color = now;
        }
    }

    // Grown and trimmed rather than rebuilt: the pins that stay are the same objects, so what was said about them -
    // a name, a colour, and one day whatever is connected to them - survives a change of count.
    private void Rebuild(ObservableCollection<CanvasNodePin> pins, Int32 wanted, Boolean input)
    {
        wanted = Math.Max(0, wanted);

        while (pins.Count > wanted) pins.RemoveAt(pins.Count - 1);

        while (pins.Count < wanted)
        {
            pins.Add(new CanvasNodePin
            {
                IsInput = input,
                Name = Free(pins, input ? "In " : "Out "),
                Color = PinColor
            });
        }
    }
}
