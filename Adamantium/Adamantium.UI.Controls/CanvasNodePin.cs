using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

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

    public static readonly AdamantiumProperty IsConnectedProperty = AdamantiumProperty.Register(nameof(IsConnected),
        typeof(Boolean), typeof(CanvasNodePin), new PropertyMetadata(false, OnLookChanged));

    public static readonly AdamantiumProperty FillProperty = AdamantiumProperty.Register(nameof(Fill),
        typeof(Brush), typeof(CanvasNodePin), new PropertyMetadata(null));

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
