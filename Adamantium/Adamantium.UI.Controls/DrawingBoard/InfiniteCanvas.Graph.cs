using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls.DrawingBoard;

// THE GRAPH SIDE: wires let go over empty plane, the list of node kinds they open, and what joining two nodes means.
public partial class InfiniteCanvas
{
    /// <summary>Raised when a wire is let go over empty plane. See <see cref="CanvasWireDroppedEventArgs"/> for why the
    /// canvas offers this instead of deciding it.</summary>
    public event EventHandler<CanvasWireDroppedEventArgs> WireDropped;

    /// <summary>Offers a dropped wire to whoever is listening. Called by the gesture; true when somebody took it.
    /// </summary>
    internal bool OfferWire(ElementItem item, CanvasNodePin pin, Vector2 world)
    {
        if (WireDropped != null)
        {
            var args = new CanvasWireDroppedEventArgs
            {
                FromItem = item,
                FromPin = pin,
                World = world,
                Screen = WorldToScreen(world)
            };

            WireDropped(this, args);
            if (args.Handled) return true;
        }

        // Nobody took it, so the canvas offers what it can: the list of kinds it was given. Without one the wire is
        // simply abandoned, which is what letting go over nothing usually means.
        return OpenPalette(item, pin, world);
    }

    /// <summary>Whether the list of node kinds is showing. Two-way: the application's own list closes itself by writing
    /// false here.</summary>
    public static readonly AdamantiumProperty IsPaletteOpenProperty = AdamantiumProperty.Register(
        nameof(IsPaletteOpen), typeof(Boolean), typeof(InfiniteCanvas),
        new PropertyMetadata(false, PropertyMetadataOptions.BindsTwoWayByDefault, OnPaletteOpenChanged));

    /// <summary>The same state as something markup can bind straight to a panel: a question in code and a visibility in
    /// markup are not the same shape, and keeping them in step here is a line rather than a converter in three themes.
    /// </summary>
    public static readonly AdamantiumProperty PaletteFaceProperty = AdamantiumProperty.RegisterReadOnly(
        nameof(PaletteFace), typeof(Visibility), typeof(InfiniteCanvas),
        new PropertyMetadata(Visibility.Collapsed));

    /// <summary>Where the list opens, in SCREEN pixels from the canvas's corner - under the pointer, which is where the
    /// hand already is.</summary>
    public static readonly AdamantiumProperty PaletteAtProperty = AdamantiumProperty.Register(nameof(PaletteAt),
        typeof(Vector2), typeof(InfiniteCanvas), new PropertyMetadata(Vector2.Zero));

    /// <summary>What was picked out of the list - an entry of <see cref="NodeKinds"/>, not a word. PICKING IS THE
    /// ANSWER: a list opened by a gesture is answered by choosing from it, so writing here puts that node on the plane
    /// and closes the list.</summary>
    public static readonly AdamantiumProperty PickedKindProperty = AdamantiumProperty.Register(nameof(PickedKind),
        typeof(ICanvasNodeKind), typeof(InfiniteCanvas),
        new PropertyMetadata(null, PropertyMetadataOptions.BindsTwoWayByDefault, OnPickedKindChanged));

    public Boolean IsPaletteOpen
    {
        get => GetValue<Boolean>(IsPaletteOpenProperty);
        set => SetValue(IsPaletteOpenProperty, value);
    }

    public Visibility PaletteFace => GetValue<Visibility>(PaletteFaceProperty);

    public Vector2 PaletteAt
    {
        get => GetValue<Vector2>(PaletteAtProperty);
        set => SetValue(PaletteAtProperty, value);
    }

    public ICanvasNodeKind PickedKind
    {
        get => GetValue<ICanvasNodeKind>(PickedKindProperty);
        set => SetValue(PickedKindProperty, value);
    }

    private ElementItem _askedFrom;
    private CanvasNodePin _askedPin;
    private Vector2 _askedWorld;

    private static void OnPaletteOpenChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not InfiniteCanvas canvas) return;

        var open = e.NewValue is true;
        canvas.SetValue(PaletteFaceProperty, open ? Visibility.Visible : Visibility.Collapsed);

        if (open) return;

        canvas._askedFrom = null;
        canvas._askedPin = null;
    }

    /// <summary>Opens the list of kinds at that point in the world, for whoever wants a node there and has no wire to
    /// join - the tool that places one, or a double click on empty plane.</summary>
    public Boolean AskForNode(Vector2 world) => OpenPalette(null, null, world);

    private bool OpenPalette(ElementItem item, CanvasNodePin pin, Vector2 world)
    {
        if (NodeKinds == null || Nodes == null) return false;

        _askedFrom = item;
        _askedPin = pin;
        _askedWorld = world;

        PaletteAt = WorldToScreen(world);
        _palette?.PlaceAt(PaletteAt);
        IsPaletteOpen = true;

        return true;
    }

    // A KIND was picked: the node goes where the list was opened, and the wire that opened it lands on it.
    private static void OnPickedKindChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not InfiniteCanvas canvas || e.NewValue is not ICanvasNodeKind picked) return;

        var kind = picked.Kind;

        var from = canvas._askedFrom;
        var pin = canvas._askedPin;

        canvas.IsPaletteOpen = false;

        canvas.BeginEdit("Add node");
        try
        {
            var made = canvas.AddNode(kind, canvas._askedWorld);
            if (made == null) return;

            // The wire arrives at the FIRST socket of the other side - the one an editor picks when a wire is dropped
            // on a node rather than on one of its sockets, because it is the only one it can pick without asking.
            if (pin != null && from != null && canvas.ItemOf(made) is { } item)
            {
                var node = item.Element as CanvasNode;
                var wanted = pin.IsInput ? node?.OutputPins : node?.InputPins;

                if (wanted is { Count: > 0 }) canvas.Join(from, pin, item, wanted[0]);
            }
        }
        finally
        {
            canvas.EndEdit();
            canvas.SetCurrentValue(PickedKindProperty, null);
        }
    }

    /// <summary>The thing on the plane standing for that node, or null while it has not been made yet.</summary>
    public ElementItem ItemOf(ICanvasNode node) => _graph.ItemOf(node);

    /// <summary>WHAT DRAWS what a node carries, chosen for the type of the thing it is carrying. Given to the canvas
    /// because the canvas is what makes the containers: it hands each new node its template at birth.</summary>
    public static readonly AdamantiumProperty NodeContentSelectorProperty = AdamantiumProperty.Register(
        nameof(NodeContentSelector), typeof(DataTemplateSelector), typeof(InfiniteCanvas),
        new PropertyMetadata(null, OnNodeContentSelectorChanged));

    public DataTemplateSelector NodeContentSelector
    {
        get => GetValue<DataTemplateSelector>(NodeContentSelectorProperty);
        set => SetValue(NodeContentSelectorProperty, value);
    }

    private static void OnNodeContentSelectorChanged(AdamantiumComponent component,
        AdamantiumPropertyChangedEventArgs e)
    {
        if (component is InfiniteCanvas canvas) canvas._graph.SetContentSelector(e.NewValue as DataTemplateSelector);
    }

    /// <summary>Joins two nodes, the way the gesture does - for whoever answered <see cref="WireDropped"/> and has just
    /// made the node the wire was reaching for. Here and not in the application: the rules about what may be joined to
    /// what belong to the graph, not to whoever is adding to it.</summary>
    public ConnectionItem Join(ElementItem fromItem, CanvasNodePin from, ElementItem toItem, CanvasNodePin to)
    {
        if (Scene is not { } scene || !ConnectGesture.Joinable(scene, fromItem, from, toItem, to)) return null;

        ConnectionItem.MakeRoom(scene, from.IsInput ? from : to);

        var wire = from.IsInput
            ? new ConnectionItem(toItem, to, fromItem, from)
            : new ConnectionItem(fromItem, from, toItem, to);

        scene.Add(wire);
        from.IsConnected = true;
        to.IsConnected = true;

        return wire;
    }
}
