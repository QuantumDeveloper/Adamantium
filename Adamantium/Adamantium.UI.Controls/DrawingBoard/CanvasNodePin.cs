using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>One socket on a <see cref="CanvasNode"/> - a place a connection can start or end.
/// <para>An object and not just a number, because a pin has things to say that a count cannot: what it is called, which
/// side it is on, what colour it wears, and whether anything is docked to it. A graph editor tells types apart by the
/// colour of the socket long before anybody reads the label, which is why the colour is here from the start.</para>
/// <para>An EMPTY socket is drawn hollow and a taken one solid, the way a blueprint editor does it. That is a real
/// reading of the graph and not a decoration: it says at a glance which ends are still loose, which is the question
/// somebody wiring a graph asks most often.</para></summary>
public class CanvasNodePin : AdamantiumComponent
{
    public static readonly AdamantiumProperty NameProperty = AdamantiumProperty.Register(nameof(Name),
        typeof(String), typeof(CanvasNodePin), new PropertyMetadata(String.Empty));

    public static readonly AdamantiumProperty IsInputProperty = AdamantiumProperty.Register(nameof(IsInput),
        typeof(Boolean), typeof(CanvasNodePin), new PropertyMetadata(true));

    public static readonly AdamantiumProperty ColorProperty = AdamantiumProperty.Register(nameof(Color),
        typeof(Brush), typeof(CanvasNodePin), new PropertyMetadata(null, OnLookChanged));

    /// <summary>WHAT FLOWS through this socket, as a word the application chooses - "float", "image", "event". Two
    /// sockets may be joined when they agree about it.
    /// <para>A word and not the colour. The colour is how a person tells types apart at a glance and is the right thing
    /// to show one with, but a colour is a presentation: two shades of the same idea, or one shade shared by two ideas,
    /// are both things an application is entitled to do, and neither should change what may be wired to what.</para>
    /// <para>EMPTY means "anything", which is what a socket says when the application has not been asked to think about
    /// types at all - so a graph that never sets this behaves exactly as it did before there were any.</para></summary>
    public static readonly AdamantiumProperty KindProperty = AdamantiumProperty.Register(nameof(Kind),
        typeof(String), typeof(CanvasNodePin), new PropertyMetadata(String.Empty));

    public static readonly AdamantiumProperty IsConnectedProperty = AdamantiumProperty.Register(nameof(IsConnected),
        typeof(Boolean), typeof(CanvasNodePin), new PropertyMetadata(false, OnLookChanged));

    /// <summary>HOW MANY wires may sit on it, and <c>0</c> for as many as anybody brings. One is what an input usually
    /// is; a wire dropped on a full socket displaces the oldest one there.
    /// <para>On the socket rather than ruled by the gesture, because many-to-one is a real thing a graph may mean - a
    /// flow graph's exec pins, a merge - and only the application knows which of its sockets are like that.</para>
    /// </summary>
    public static readonly AdamantiumProperty CapacityProperty = AdamantiumProperty.Register(nameof(Capacity),
        typeof(Int32), typeof(CanvasNodePin), new PropertyMetadata(1));

    public Int32 Capacity
    {
        get => GetValue<Int32>(CapacityProperty);
        set => SetValue(CapacityProperty, value);
    }

    public static readonly AdamantiumProperty FillProperty = AdamantiumProperty.Register(nameof(Fill),
        typeof(Brush), typeof(CanvasNodePin), new PropertyMetadata(null));

    /// <summary>What flows through this socket. Empty means anything.</summary>
    public String Kind
    {
        get => GetValue<String>(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>Whether a wire may run between this socket and another, as far as WHAT FLOWS is concerned. One of them
    /// saying nothing is a yes: a graph that has never been told about types wires up as it always did.</summary>
    public Boolean Fits(CanvasNodePin other)
    {
        if (other == null) return false;
        if (String.IsNullOrEmpty(Kind) || String.IsNullOrEmpty(other.Kind)) return true;

        return String.Equals(Kind, other.Kind, StringComparison.Ordinal);
    }

    /// <summary>What the socket is called, beside it.</summary>
    public String Name
    {
        get => GetValue<String>(NameProperty);
        set => SetValue(NameProperty, value);
    }

    /// <summary>Which side it sits on: an input is on the left and takes a connection, an output is on the right and
    /// gives one.</summary>
    public Boolean IsInput
    {
        get => GetValue<Boolean>(IsInputProperty);
        set => SetValue(IsInputProperty, value);
    }

    /// <summary>The colour of the socket - its ring always, and its middle once something is docked. Null takes the
    /// node's own, which is what an editor with only one kind of connection wants.</summary>
    public Brush Color
    {
        get => GetValue<Brush>(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <summary>Whether anything is docked here. False leaves the socket hollow - a ring with the node showing through
    /// it - and true fills it in.</summary>
    public Boolean IsConnected
    {
        get => GetValue<Boolean>(IsConnectedProperty);
        set => SetValue(IsConnectedProperty, value);
    }

    /// <summary>What paints the INSIDE of the socket: the colour when something is docked, and nothing at all when the
    /// socket is empty. Derived from <see cref="Color"/> and <see cref="IsConnected"/> - setting it directly is
    /// overwritten by the next change to either.</summary>
    public Brush Fill
    {
        get => GetValue<Brush>(FillProperty);
        private set => SetValue(FillProperty, value);
    }

    private static void OnLookChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is CanvasNodePin pin) pin.Fill = pin.IsConnected ? pin.Color : null;
    }
}
