using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Converters;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>Keeps the NODES on a canvas in step with the application's collection of them, in both directions.
/// <para>One container per model, made and owned here: the application hands over its own objects and never a control.
/// What the person does on the plane comes back the same way - a node dragged writes its new place into the model, a
/// node deleted leaves the collection.</para>
/// <para>The WIRES are not a collection at all: they live on the sockets they join, so what is drawn here is a walk
/// over the graph rather than a second account of it.</para>
/// <para>Held by the canvas rather than written into it, because this is bookkeeping of a kind the canvas has none of
/// otherwise: a map from an application's object to the control standing for it.</para></summary>
internal sealed class CanvasGraphHost
{
    private readonly Dictionary<ICanvasNode, ElementItem> _placed = new();
    private readonly Dictionary<CanvasConnection, ConnectionItem> _wired = new();
    private readonly HashSet<ICanvasSocket> _watched = new();
    private readonly InfiniteCanvas _canvas;

    private IEnumerable _nodes;
    private IEnumerable _kinds;
    private IEnumerable _socketKinds;
    private SocketKindToBrushConverter _paint;
    private ICanvasScene _scene;
    private bool _reconciling;

    private Adamantium.UI.Core.Templates.DataTemplateSelector _selector;

    /// <summary>What draws the contents of every node here. Handed to each container as it is made, and to the ones
    /// already standing when it arrives - a selector set after the graph was filled must not leave those blank.
    /// </summary>
    public void SetContentSelector(Adamantium.UI.Core.Templates.DataTemplateSelector selector)
    {
        if (ReferenceEquals(_selector, selector)) return;

        _selector = selector;

        foreach (var (_, item) in _placed)
        {
            if (item.Element is CanvasNode node) node.ContentTemplateSelector = selector;
        }
    }

    /// <summary>The kinds a node here can be. Data, not a factory handed to the control: the shell is the engine's own
    /// and is made below, and what a kind MEANS is asked of the catalogue entry.</summary>
    public void SetKinds(IEnumerable kinds) => _kinds = kinds;

    /// <summary>What a SOCKET may carry, and what each of those looks like. A pin takes its colour from what flows
    /// through it, so every socket carrying the same thing looks the same across the whole graph.</summary>
    public void SetSocketKinds(IEnumerable kinds)
    {
        if (ReferenceEquals(_socketKinds, kinds)) return;

        _socketKinds = kinds;

        // WHAT PAINTS A PIN is made from that table and kept out of sight: the application says which word means which
        // colour, and how a pin ends up wearing it is nobody else's business. Put INTO the binding that paints, which is
        // the only way a converter is ever used - nothing here calls it.
        _paint = kinds == null ? null : new SocketKindToBrushConverter(kinds);

        Repaint();
    }

    // The pins already standing were painted by what was there before - or by nothing at all, which is what an
    // application arriving after the graph finds.
    private void Repaint()
    {
        foreach (var (model, item) in _placed)
        {
            if (item.Element is CanvasNode node) Sockets(model, node);
        }
    }

    /// <summary>A node of that kind, at that place, INTO the collection - which is the only way anything joins the
    /// graph. Null when there is no collection to add to.</summary>
    public ICanvasNode Add(string kind, Vector2 at)
    {
        if (_nodes is not IList<ICanvasNode> list) return null;

        // ASKED OF THE CATALOGUE. A node is the application's object - the canvas has none of its own to reach for, the
        // same way an items control has no item type - so a canvas told nothing about kinds cannot invent one, and says
        // so by making nothing.
        if (KindOf(kind)?.Make() is not { } made) return null;

        // WHERE it goes is the only thing here that is the canvas's: this is the press that put it there.
        made.Left = at.X;
        made.Top = at.Y;

        list.Add(made);

        return made;
    }

    /// <summary>The catalogue entry for a kind, or null when the catalogue says nothing about it - a node of a kind
    /// nobody offers is still a node, it just has nothing inside.</summary>
    public ICanvasNodeKind KindOf(string kind)
    {
        if (_kinds == null) return null;

        foreach (var item in _kinds)
        {
            if (item is ICanvasNodeKind entry && entry.Kind == kind) return entry;
        }

        return null;
    }

    public CanvasGraphHost(InfiniteCanvas canvas) => _canvas = canvas;

    /// <summary>The application's collection of nodes. Followed while it is set, so a node added to it appears on the
    /// plane and one taken out of it leaves.</summary>
    public void SetNodes(IEnumerable nodes)
    {
        if (ReferenceEquals(_nodes, nodes)) return;

        if (_nodes is INotifyCollectionChanged was) was.CollectionChanged -= OnNodesChanged;

        Clear();
        _nodes = nodes;

        if (_nodes is INotifyCollectionChanged now) now.CollectionChanged += OnNodesChanged;

        Fill();
    }

    /// <summary>The scene the containers are put in. Also where the answer comes BACK from: a drag moves the item, and
    /// items carry no notifications of their own, so the scene saying "something changed" is what there is.</summary>
    public void SetScene(ICanvasScene scene)
    {
        if (ReferenceEquals(_scene, scene)) return;

        if (_scene is CanvasScene was) was.Changed -= OnSceneChanged;

        _scene = scene;

        if (_scene is CanvasScene now) now.Changed += OnSceneChanged;

        Fill();
    }

    private void OnNodesChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            Clear();
            Fill();
            return;
        }

        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems)
            {
                if (item is ICanvasNode model) Drop(model);
            }
        }

        if (e.NewItems == null) return;

        foreach (var item in e.NewItems)
        {
            if (item is ICanvasNode model) Put(model);
        }
    }

    private void Fill()
    {
        if (_nodes == null || _scene == null) return;

        foreach (var item in _nodes)
        {
            if (item is ICanvasNode model) Put(model);
        }

        // The wires AFTER the nodes, always: a wire is the two nodes it joins, and one whose ends are not on the plane
        // yet has nothing to be made of.
        RebuildWires();
    }

    // EVERY WIRE OF THE GRAPH, found by walking the nodes' output sockets - each wire is reached once, from the end it
    // leaves. Cheap enough to do whole: it costs one pass over the edges, and the alternative is a second account of
    // the graph kept in step by hand.
    private void RebuildWires()
    {
        if (_reconciling || _scene == null) return;

        // HELD while the scene is being changed, because the scene answers back: taking the old lines off it looks
        // exactly like somebody cutting them, and the graph would be told its own bookkeeping was an edit.
        _reconciling = true;
        try
        {
            foreach (var (_, item) in _wired) _scene.Remove(item);

            _wired.Clear();

            foreach (var (model, _) in _placed)
            {
                foreach (var socket in model.Outputs)
                {
                    foreach (var wire in socket.Connections) PutWire(wire);
                }
            }

            foreach (var (model, item) in _placed)
            {
                Taken(item, model.Inputs, true);
                Taken(item, model.Outputs, false);
            }
        }
        finally
        {
            _reconciling = false;
        }
    }

    // WHETHER A SOCKET IS TAKEN is not a flag for anyone to set - it is whether a wire sits on it, and the socket is
    // the only account of that. Said here because this is where the wires are counted anyway: a socket filled in by
    // hand somewhere else is one that stays filled after the wire is cut.
    private static void Taken(ElementItem item, Adamantium.Core.Collections.TrackingCollection<ICanvasSocket> sockets,
        bool input)
    {
        if (item.Element is not CanvasNode node) return;

        var pins = input ? node.InputPins : node.OutputPins;

        for (var i = 0; i < pins.Count && i < sockets.Count; i++)
        {
            pins[i].IsConnected = sockets[i].Connections.Count > 0;
        }

        Told(node, pins, sockets, input);
    }

    // TEMP instrument (ADAM_GRAPH_TRACE=<path>): what the graph says about every socket once the wires have been
    // counted - which socket a pin stands for, whether anything is on it, and whether it has a middle to draw. To a
    // FILE, and only when asked for.
    private static readonly string Trace = Environment.GetEnvironmentVariable("ADAM_GRAPH_TRACE");

    private static void Told(CanvasNode node,
        System.Collections.ObjectModel.ObservableCollection<CanvasNodePin> pins,
        Adamantium.Core.Collections.TrackingCollection<ICanvasSocket> sockets, bool input)
    {
        if (Trace is not { Length: > 0 }) return;

        var said = new System.Text.StringBuilder($"{node.Title} {(input ? "in" : "out")}:");

        for (var i = 0; i < pins.Count && i < sockets.Count; i++)
        {
            said.Append($" [{i}] {sockets[i].Name}/{pins[i].Name} wires={sockets[i].Connections.Count}" +
                        $" taken={pins[i].IsConnected} fill={(pins[i].Fill == null ? "none" : "yes")}" +
                        $" colour={(pins[i].Color == null ? "none" : "yes")};");
        }

        try
        {
            System.IO.File.AppendAllText(Trace, said.ToString() + Environment.NewLine);
        }
        catch (System.IO.IOException)
        {
            // A trace that cannot be written is not worth failing a frame over.
        }
    }

    private void PutWire(CanvasConnection wire)
    {
        if (_scene == null || wire == null || _wired.ContainsKey(wire)) return;

        if (wire.FromNode == null || wire.ToNode == null) return;
        if (!_placed.TryGetValue(wire.FromNode, out var from) || !_placed.TryGetValue(wire.ToNode, out var to)) return;
        if (Pin(from, wire.FromNode, wire.From, false) is not { } fromPin) return;
        if (Pin(to, wire.ToNode, wire.To, true) is not { } toPin) return;

        var item = new ConnectionItem(from, fromPin, to, toPin);

        _wired[wire] = item;
        _scene.Add(item);
    }

    // The container's pin standing for the model's socket, found by WHERE the socket sits in the model's own list: the
    // two lists are made together and kept in step, so the n-th socket is the n-th pin.
    private static CanvasNodePin Pin(ElementItem item, ICanvasNode model, ICanvasSocket socket, bool input)
    {
        if (item.Element is not CanvasNode node || socket == null) return null;

        var sockets = input ? model.Inputs : model.Outputs;
        var pins = input ? node.InputPins : node.OutputPins;

        var at = sockets.IndexOf(socket);

        return at >= 0 && at < pins.Count ? pins[at] : null;
    }

    private void Clear()
    {
        foreach (var (model, item) in _placed)
        {
            Unfollow(model);
            _scene?.Remove(item);
        }

        _placed.Clear();

        foreach (var (_, item) in _wired) _scene?.Remove(item);

        _wired.Clear();
    }

    private void Put(ICanvasNode model)
    {
        if (_scene == null || _placed.ContainsKey(model)) return;

        var node = new CanvasNode();
        var item = new ElementItem(node, new Rect(model.Left, model.Top, Math.Max(0, model.Width), 0))
        {
            SizeFollowsContent = true,
            Model = model
        };

        _placed[model] = item;
        node.ContentTemplateSelector = _selector;

        // A SOCKET DRAGGED to another place along its side: the container says it happened, the MOVE is made here, in
        // the application's own list. Nothing happens to the wires - a wire sits on the socket, not on its place.
        node.PinsReordered += OnPinsReordered;

        // BOUND, not copied. Everything the container shows that the node says is a binding to the node's own object -
        // which is what bindings are for, and what keeps the two from being two answers to one question. What is left
        // below is the one thing a binding cannot do: turning a collection of sockets into a collection of pins.
        Bind(node, CanvasNode.KindProperty, model, nameof(ICanvasNode.Kind));
        Bind(node, CanvasNode.TitleProperty, model, nameof(ICanvasNode.Title));
        Bind(node, CanvasNode.IsCollapsedProperty, model, nameof(ICanvasNode.IsCollapsed));

        // ITS COLOUR, through a converter: the node holds a colour and the strip needs a brush, and the brush is made
        // per node here. A colour nobody chose converts to nothing at all, which leaves the strip wearing the theme's.
        node.SetBinding(CanvasNode.AccentProperty, new Adamantium.UI.Core.Data.Binding(nameof(ICanvasNode.Accent))
        {
            Source = model,
            Mode = Adamantium.UI.Core.Data.BindingMode.OneWay,
            Converter = new ColorToBrushConverter()
        });
        Bind(node, ContentControl.ContentProperty, model, nameof(ICanvasNode.Specialization));

        Sockets(model, node);
        Follow(model);

        _scene.Add(item);
        RebuildWires();
    }

    private void OnPinsReordered(object sender, CanvasPinOrderEventArgs e)
    {
        if (sender is not CanvasNode node) return;

        foreach (var (model, item) in _placed)
        {
            if (!ReferenceEquals(item.Element, node)) continue;

            var sockets = e.IsInput ? model.Inputs : model.Outputs;
            if (e.From < 0 || e.From >= sockets.Count) return;

            var to = e.To < 0 ? 0 : e.To > sockets.Count - 1 ? sockets.Count - 1 : e.To;

            // ONE move. Taken out and put back would be a moment in which the socket is not on the node at all - and
            // everything downstream believes it: the pins are fitted to the sockets by position, so a pin would be
            // destroyed and made again, and the wires rebuilt against a node one socket short.
            sockets.Move(e.From, to);

            return;
        }
    }

    private static void Bind(CanvasNode node, AdamantiumProperty property, ICanvasNode model, string path) =>
        node.SetBinding(property, new Adamantium.UI.Core.Data.Binding(path)
        {
            Source = model,
            Mode = Adamantium.UI.Core.Data.BindingMode.TwoWay
        });

    private void Drop(ICanvasNode model)
    {
        if (!_placed.Remove(model, out var item)) return;

        if (item.Element is CanvasNode going) going.PinsReordered -= OnPinsReordered;

        // A NODE LEAVING TAKES ITS WIRES, and both ends of each: the wire knows where it sits, so nothing has to walk
        // the graph looking for what pointed at this one.
        Cut(model.Inputs);
        Cut(model.Outputs);

        Unfollow(model);
        _scene?.Remove(item);

        RebuildWires();
    }

    private static void Cut(Adamantium.Core.Collections.TrackingCollection<ICanvasSocket> sockets)
    {
        foreach (var socket in sockets)
        {
            while (socket.Connections.Count > 0) socket.Connections[0].Disconnect();
        }
    }

    // A PIN PER SOCKET, which is the one thing here a binding cannot do: it is a collection turned into another
    // collection. Each pin is then bound to its socket, so a name or a kind edited in an inspector needs nothing of
    // this to run again.
    private void Sockets(ICanvasNode model, CanvasNode node)
    {
        Fit(node.InputPins, model.Inputs, true, node, _paint);
        Fit(node.OutputPins, model.Outputs, false, node, _paint);

        Watch(model.Inputs);
        Watch(model.Outputs);
    }

    private static void Fit(System.Collections.ObjectModel.ObservableCollection<CanvasNodePin> pins,
        Adamantium.Core.Collections.TrackingCollection<ICanvasSocket> sockets, bool input, CanvasNode node,
        SocketKindToBrushConverter paint)
    {
        if (input) node.Inputs = sockets.Count;
        else node.Outputs = sockets.Count;

        for (var i = 0; i < pins.Count && i < sockets.Count; i++)
        {
            pins[i].SetBinding(CanvasNodePin.NameProperty, new Adamantium.UI.Core.Data.Binding(nameof(ICanvasSocket.Name))
            {
                Source = sockets[i],
                Mode = Adamantium.UI.Core.Data.BindingMode.TwoWay
            });

            pins[i].SetBinding(CanvasNodePin.KindProperty, new Adamantium.UI.Core.Data.Binding(nameof(ICanvasSocket.Kind))
            {
                Source = sockets[i],
                Mode = Adamantium.UI.Core.Data.BindingMode.TwoWay
            });

            // HOW MANY WIRES it takes, on the pin as well - the gesture that draws one works on pins and would
            // otherwise have to invent the rule itself, which is how "one per input" got hard-coded there.
            pins[i].SetBinding(CanvasNodePin.CapacityProperty,
                new Adamantium.UI.Core.Data.Binding(nameof(ICanvasSocket.Capacity))
                {
                    Source = sockets[i],
                    Mode = Adamantium.UI.Core.Data.BindingMode.TwoWay
                });

            // ...and WHAT IT LOOKS LIKE, read off what flows through it. Bound to the kind rather than set once, so a
            // socket told it now carries a colour changes colour; a kind the catalogue says nothing about leaves the
            // pin wearing the theme's.
            if (paint == null) continue;

            pins[i].SetBinding(CanvasNodePin.ColorProperty,
                new Adamantium.UI.Core.Data.Binding(nameof(ICanvasSocket.Kind))
                {
                    Source = sockets[i],
                    Mode = Adamantium.UI.Core.Data.BindingMode.OneWay,
                    Converter = paint
                });
        }
    }

    // A wire made or cut by the APPLICATION is a line appearing or going on the plane, and the only place that says so
    // is the socket it sits on.
    private void Watch(Adamantium.Core.Collections.TrackingCollection<ICanvasSocket> sockets)
    {
        foreach (var socket in sockets)
        {
            if (!_watched.Add(socket)) continue;

            socket.Connections.CollectionChanged += OnWiresChanged;
        }
    }

    private void OnWiresChanged(object sender, NotifyCollectionChangedEventArgs e) => RebuildWires();

    // ...and a socket ADDED or DROPPED is a pin appearing or going, which no binding does: the two collections have to
    // be brought level again.
    private void Follow(ICanvasNode model)
    {
        model.Inputs.CollectionChanged += OnSocketsChanged;
        model.Outputs.CollectionChanged += OnSocketsChanged;
        model.PropertyChanged += OnModelChanged;
    }

    private void Unfollow(ICanvasNode model)
    {
        model.Inputs.CollectionChanged -= OnSocketsChanged;
        model.Outputs.CollectionChanged -= OnSocketsChanged;
        model.PropertyChanged -= OnModelChanged;

        Forget(model.Inputs);
        Forget(model.Outputs);
    }

    private void Forget(Adamantium.Core.Collections.TrackingCollection<ICanvasSocket> sockets)
    {
        foreach (var socket in sockets)
        {
            if (_watched.Remove(socket)) socket.Connections.CollectionChanged -= OnWiresChanged;
        }
    }

    private void OnSocketsChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        // A MOVED socket is reported as leaving and arriving at once, and it is still there - letting go of it here
        // would stop the wires on it being heard of again.
        if (e.Action != NotifyCollectionChangedAction.Move && e.OldItems != null)
        {
            foreach (var item in e.OldItems)
            {
                if (item is ICanvasSocket socket && _watched.Remove(socket))
                {
                    socket.Connections.CollectionChanged -= OnWiresChanged;
                }
            }
        }

        foreach (var (model, item) in _placed)
        {
            if (!ReferenceEquals(sender, model.Inputs) && !ReferenceEquals(sender, model.Outputs)) continue;
            if (item.Element is CanvasNode node) Sockets(model, node);

            break;
        }

        RebuildWires();
    }

    // Nothing is read out of the node here and nothing is written onto the container: the place a node was moved to is
    // already the item's, and the scene is simply told that a rectangle on it is not where it was. Without it a node
    // moved from a field stays drawn where it stood, because the plane is laid out from the items and nobody asked it
    // to do that again.
    private void OnModelChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ICanvasNode.Left):
            case nameof(ICanvasNode.Top):
            case nameof(ICanvasNode.Width):
                (_scene as CanvasScene)?.Touch();
                break;

            // TOLD IT IS NOW A TEXTURE, it becomes one: the word changed, and what that word means is in the catalogue
            // the canvas was handed. Here and not in the node, because the node has no catalogue - and not only where
            // the palette makes one, or the same pick from an inspector would change the label and nothing else.
            case nameof(ICanvasNode.Kind) when sender is ICanvasNode model:
                model.Specialization = KindOf(model.Kind)?.Create();
                break;
        }
    }

    // Nothing about a node is copied back here: its place is the model's own field, written through the item, and what
    // the container shows is bound. What IS left is the wires, which nothing on the plane writes into the graph by
    // itself - a gesture leaves one lying there, and the graph has to hear about it.
    private void OnSceneChanged(object sender, EventArgs e)
    {
        if (_reconciling) return;

        _reconciling = true;
        try
        {
            Reconcile();
        }
        finally
        {
            _reconciling = false;
        }
    }

    // WIRES DRAWN ON THE PLANE, into the graph - and wires cut on it, out of it. A wire is made by a gesture that knows
    // nothing about the application's objects, so what it leaves behind is found here and joined properly; seating
    // itself in both sockets is the wire's own business, so there is nothing here to keep in step.
    private void Reconcile()
    {
        if (_scene == null) return;

        var all = new Rect(double.MinValue / 4, double.MinValue / 4, double.MaxValue / 2, double.MaxValue / 2);

        var drawn = new HashSet<ConnectionItem>();
        foreach (var item in _scene.ItemsIn(all))
        {
            if (item is ConnectionItem wire) drawn.Add(wire);
        }

        // Gone from the plane: the wire was cut, so the graph no longer has it.
        var cut = new List<CanvasConnection>();
        foreach (var (model, item) in _wired)
        {
            if (!drawn.Contains(item)) cut.Add(model);
        }

        foreach (var model in cut)
        {
            _wired.Remove(model);
            model.Disconnect();
        }

        // New on the plane: somebody drew it, and the graph has not heard yet.
        foreach (var item in drawn)
        {
            if (_wired.ContainsValue(item)) continue;

            if (Model(item.FromItem) is not { } from || Model(item.ToItem) is not { } to) continue;
            if (Socket(from, item.FromItem, item.FromPin, false) is not { } fromSocket) continue;
            if (Socket(to, item.ToItem, item.ToPin, true) is not { } toSocket) continue;

            _wired[new CanvasConnection(fromSocket, toSocket)] = item;
        }
    }

    /// <summary>The container standing for that node, or null while there is none.</summary>
    public ElementItem ItemOf(ICanvasNode node) =>
        node != null && _placed.TryGetValue(node, out var item) ? item : null;

    private ICanvasNode Model(ElementItem item)
    {
        foreach (var (model, placed) in _placed)
        {
            if (ReferenceEquals(placed, item)) return model;
        }

        return null;
    }

    // The mirror of Pin: the model's socket standing for the container's pin, found by where it sits.
    private static ICanvasSocket Socket(ICanvasNode model, ElementItem item, CanvasNodePin pin, bool input)
    {
        if (item.Element is not CanvasNode node || pin == null) return null;

        var pins = input ? node.InputPins : node.OutputPins;
        var sockets = input ? model.Inputs : model.Outputs;

        var at = pins.IndexOf(pin);

        return at >= 0 && at < sockets.Count ? sockets[at] : null;
    }
}
