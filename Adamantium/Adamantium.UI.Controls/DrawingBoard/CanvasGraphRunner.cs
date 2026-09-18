using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Adamantium.Core.Collections;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>WORKING A GRAPH OUT, and keeping it worked out while somebody edits it.
/// <para>Made over an application's own collection of nodes it needs nothing else: it hears the graph change - a value
/// set, a wire drawn, a socket added, a node dropped - and asks the nodes that are affected for their values again.
/// What a node computes is the application's and is said through <see cref="ICanvasNodeWork"/>; everything about WHEN
/// and IN WHAT ORDER is here, because that part is the same in every application and is where the mistakes live.</para>
/// <para>Four things it does that a plain walk does not, each of them the difference between a demonstration and a
/// graph somebody can work in:</para>
/// <para>IN ORDER, NOT BY RECURSION. The nodes are sorted so that everything feeding a node comes before it, and a pass
/// is then a walk down a list. A chain a thousand deep is a thousand steps and not a thousand stack frames, and a graph
/// that feeds back into itself - which a file somebody else wrote may well do - leaves the nodes of the loop unworked
/// instead of going round for ever.</para>
/// <para>ONLY WHAT CHANGED. Every node keeps the value it last had, and a change marks that node and spreads down the
/// wires from it. Moving a slider on one node of two hundred works out that node and what it feeds, not the other
/// hundred and ninety.</para>
/// <para>ONCE PER BURST. A drag sets a value at every pixel; the pass is put off to the end of the batch, so the graph
/// runs once for the whole gesture instead of sixty times a second.</para>
/// <para>AND IT LETS GO. A pass already running is dropped the moment what it was working out stops being what the
/// graph says - the token every node is handed says so - and a new one starts. That is what makes a node that renders
/// for half a second usable at all.</para>
/// <para>ONE THREAD, and the graph's own: a pass waits for a node's work but never leaves the thread it was started on
/// - it does not let go of the context, so wherever a node did its work, what comes back to the graph comes back here.
/// A node is free to go wide inside its own <see cref="ICanvasNodeWork.Evaluate"/>; nothing it hands back is read
/// anywhere but on this thread, so nothing anybody holds needs a lock.</para></summary>
public sealed class CanvasGraphRunner : IDisposable
{
    private readonly TrackingCollection<ICanvasNode> _nodes;
    private readonly List<ICanvasNode> _order = new();
    private readonly HashSet<ICanvasNode> _dirty = new();
    private readonly Dictionary<ICanvasNode, object> _values = new();

    // WHAT IS ACTUALLY HOOKED. Kept rather than worked out from the graph, because the graph is what changes: a
    // collection emptied in one go names nothing it dropped, and a subscription left on a node nobody holds any more
    // is both a leak and a pass run for a graph that is no longer on the plane.
    private readonly HashSet<ICanvasNode> _watched = new();
    private readonly HashSet<ICanvasSocket> _listened = new();

    private readonly Dictionary<ICanvasNode, (INotifyPropertyChanged State, PropertyChangedEventHandler Heard)> _states =
        new();

    private SynchronizationContext _context;
    private CancellationTokenSource _inFlight;
    private CancellationToken _asked;
    private Task _pass = Task.CompletedTask;
    private ICanvasNode _working;
    private bool _scheduled;
    private bool _sorted;
    private bool _everything;
    private bool _closed;

    /// <param name="nodes">The application's own graph. Held, not copied: nodes joining and leaving it ARE the graph
    /// changing, and a second account of what is on the plane is a second thing to keep in step.</param>
    public CanvasGraphRunner(TrackingCollection<ICanvasNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        _nodes = nodes;
        _context = SynchronizationContext.Current;

        _nodes.CollectionChanged += OnNodes;

        foreach (var node in _nodes) Hook(node);

        Restructured();
    }

    /// <summary>What a node was last worked out to be, and null for one that has no value - a node whose kind does no
    /// work, one caught in a loop, one a pass has not reached yet.</summary>
    public object ValueOf(ICanvasNode node) =>
        node != null && _values.TryGetValue(node, out var value) ? value : null;

    /// <summary>Works the whole graph out again from nothing, forgetting every value it remembers. For when something a
    /// node READS has changed while the graph did not - a file on disk, a setting kept elsewhere - which nothing here
    /// can hear.</summary>
    public void Invalidate()
    {
        _values.Clear();
        Restructured();
    }

    /// <summary>Runs what is owed and waits until nothing is. A pass is normally not waited for - that it does not hold
    /// up the window is the whole point - so this is for a test, or for anything that must have the answer before it
    /// goes on: saving a render, closing a document.</summary>
    /// <param name="token">Given up on. It reaches the NODES as well, so a wait abandoned stops the work being waited
    /// for rather than leaving a texture rendering for an answer nobody will read. What was owed stays owed.</param>
    public async Task Settle(CancellationToken token = default)
    {
        _asked = token;

        try
        {
            while (!_closed)
            {
                Start();

                await _pass;

                token.ThrowIfCancellationRequested();

                if (!_scheduled) return;
            }
        }
        finally
        {
            _asked = default;
        }
    }

    public void Dispose()
    {
        if (_closed) return;

        _closed = true;
        _inFlight?.Cancel();
        _nodes.CollectionChanged -= OnNodes;

        foreach (var node in new List<ICanvasNode>(_watched)) Drop(node);
    }

    // WHAT IS LISTENED TO, and it is listened to ONCE. A node is hooked when it joins and unhooked when it leaves, and
    // nothing in between takes it all apart and puts it back: a socket added is one subscription added, a kind swapped
    // is one handler moved off one object and onto another.
    private void Hook(ICanvasNode node)
    {
        if (node == null || !_watched.Add(node)) return;

        node.PropertyChanged += OnNode;
        node.Inputs.CollectionChanged += OnSockets;
        node.Outputs.CollectionChanged += OnSockets;

        foreach (var socket in node.Inputs) Hook(socket);
        foreach (var socket in node.Outputs) Hook(socket);

        HookState(node);
    }

    private void Drop(ICanvasNode node)
    {
        if (node == null || !_watched.Remove(node)) return;

        node.PropertyChanged -= OnNode;
        node.Inputs.CollectionChanged -= OnSockets;
        node.Outputs.CollectionChanged -= OnSockets;

        foreach (var socket in node.Inputs) Drop(socket);
        foreach (var socket in node.Outputs) Drop(socket);

        DropState(node);

        _values.Remove(node);
        _dirty.Remove(node);
    }

    private void Hook(ICanvasSocket socket)
    {
        if (socket == null || !_listened.Add(socket)) return;

        socket.Connections.CollectionChanged += OnWires;
    }

    private void Drop(ICanvasSocket socket)
    {
        if (socket == null || !_listened.Remove(socket)) return;

        socket.Connections.CollectionChanged -= OnWires;
    }

    // The node is CAPTURED rather than looked up from the sender: a state object does not know whose it is, and asking
    // every node in the graph whose it might be would be a search per keystroke.
    private void HookState(ICanvasNode node)
    {
        if (node.Specialization is not INotifyPropertyChanged state) return;

        void Heard(object sender, PropertyChangedEventArgs e) => OnState(node);

        _states[node] = (state, Heard);
        state.PropertyChanged += Heard;
    }

    private void DropState(ICanvasNode node)
    {
        if (!_states.Remove(node, out var hooked)) return;

        hooked.State.PropertyChanged -= hooked.Heard;
    }

    private void OnNodes(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Move) return;

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            Resync();
        }
        else
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is ICanvasNode node) Drop(node);
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is ICanvasNode node) Hook(node);
                }
            }
        }

        // A NODE GONE takes its place in the order and whatever it fed with it, and nothing left behind can be asked
        // what that was - so this is the one change that is never narrowed down.
        Restructured();
    }

    private void OnNode(object sender, PropertyChangedEventArgs e)
    {
        // WHERE a node sits, what it is called and what colour it wears are not what it computes. Hearing those would
        // work the graph out at every pixel of a drag across the plane.
        if (e.PropertyName != nameof(ICanvasNode.Specialization) || sender is not ICanvasNode node) return;

        DropState(node);
        HookState(node);

        Touch(node);
    }

    // A SOCKET COMING OR GOING changes which values arrive where, and one that has gone cannot say whose it was - the
    // node lets go of it before anybody is told. So the sockets are re-hooked exactly and the pass is a whole one.
    private void OnSockets(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Move) return;

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            Resync();
        }
        else
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is ICanvasSocket socket) Drop(socket);
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is ICanvasSocket socket) Hook(socket);
                }
            }
        }

        Restructured();
    }

    // A WIRE changes the order, and changes the value of the end it ARRIVES at; everything past that is reached by
    // spreading down the wires during the pass itself. A change that cannot say where it arrives - a socket emptied in
    // one go - is the only one that costs a whole pass.
    private void OnWires(object sender, NotifyCollectionChangedEventArgs e)
    {
        _sorted = false;

        if (e.Action == NotifyCollectionChangedAction.Reset) _everything = true;
        else if (!(Narrow(e.OldItems) & Narrow(e.NewItems))) _everything = true;

        Schedule();
    }

    private bool Narrow(System.Collections.IList wires)
    {
        if (wires == null) return true;

        var narrowed = true;

        foreach (var item in wires)
        {
            if (item is CanvasConnection { ToNode: { } node }) _dirty.Add(node);
            else narrowed = false;
        }

        return narrowed;
    }

    // A NODE'S OWN STATE: a number typed, a colour picked. That node is worth working out again, and what it feeds
    // follows from it.
    private void OnState(ICanvasNode node)
    {
        // THE NODE BEING WORKED OUT RIGHT NOW is telling us what it has just worked out - a result written back into
        // its own state, which its body shows. Taking that for a reason to run again is a loop, and one that never
        // stops on any graph whose last node writes its answer down.
        if (ReferenceEquals(node, _working)) return;

        Touch(node);
    }

    private void Touch(ICanvasNode node)
    {
        _dirty.Add(node);
        Schedule();
    }

    private void Restructured()
    {
        _sorted = false;
        _everything = true;

        Schedule();
    }

    // The hooks brought level with what the graph actually holds. Only for a change that named nothing it dropped -
    // a collection emptied in one call - so the ordinary add and remove stay one subscription each.
    private void Resync()
    {
        foreach (var node in new List<ICanvasNode>(_watched))
        {
            if (!_nodes.Contains(node)) Drop(node);
        }

        foreach (var node in _nodes) Hook(node);

        // A NODE THAT STAYED may have lost sockets in the same breath, and a socket dropped that way named itself no
        // more than the node did.
        var live = new HashSet<ICanvasSocket>();

        foreach (var node in _watched)
        {
            foreach (var socket in node.Inputs) live.Add(socket);
            foreach (var socket in node.Outputs) live.Add(socket);
        }

        foreach (var socket in new List<ICanvasSocket>(_listened))
        {
            if (!live.Contains(socket)) Drop(socket);
        }

        foreach (var socket in live) Hook(socket);
    }

    // PUT OFF TO THE END OF THE BATCH, and the pass already running told to stop: what it is working out is not what
    // the graph says any more, so finishing it is work thrown away - and, for a node that renders, work standing in
    // front of the answer somebody is waiting for.
    private void Schedule()
    {
        if (_closed) return;

        _inFlight?.Cancel();

        if (_scheduled) return;

        _scheduled = true;

        // ONTO THE LOOP THREAD, where the graph itself lives. A SynchronizationContext here is the DISPATCHER's, and
        // that one marshals onto the message-pump thread - so the pass walked the very sockets and nodes the loop
        // thread was editing. Two threads in one graph is corruption waiting to be noticed; it showed up first as a
        // freeze, with the pump inside a pass and the loop inside a wire being cut.
        if (UIAppContext.Current?.Dispatcher is { } dispatcher)
        {
            dispatcher.Post(() => Pump(null));
            return;
        }

        // Captured late as well as early: an object built before its thread has a context of its own would otherwise
        // never find one.
        _context ??= SynchronizationContext.Current;
        _context?.Post(Pump, null);
    }

    private void Pump(object state)
    {
        Start();

        // An exception a node threw must not sink with a pass nobody awaited. Thrown back onto this thread it reaches
        // whatever the host does with unhandled ones, which is the only place it can be seen.
        Surface(_pass);
    }

    private static async void Surface(Task pass) => await pass;

    private void Start()
    {
        if (!_scheduled || _closed) return;

        _scheduled = false;
        _pass = Pass();
    }

    private async Task Pass()
    {
        if (!_sorted) Sort();

        // WHOEVER IS WAITING can give up, and giving up reaches the node that is working: a pass carries both reasons
        // to stop - the graph moved on, or nobody wants the answer any more.
        using var cancel = _asked.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(_asked)
            : new CancellationTokenSource();

        _inFlight = cancel;

        try
        {
            var token = cancel.Token;

            foreach (var node in _order)
            {
                if (token.IsCancellationRequested) return;
                if (!_everything && !_dirty.Contains(node)) continue;

                await Work(node, token);

                if (token.IsCancellationRequested) return;

                _dirty.Remove(node);
                Spread(node);
            }

            _everything = false;

            // What is left is not in the order at all - the nodes of a loop. Cleared, because nothing will ever reach
            // them and dirt that can never be worked off makes every later pass look owed.
            _dirty.Clear();
        }
        finally
        {
            _inFlight = null;
        }
    }

    private async Task Work(ICanvasNode node, CancellationToken token)
    {
        if (node.Specialization is not ICanvasNodeWork work)
        {
            _values.Remove(node);
            return;
        }

        _working = node;

        try
        {
            _values[node] = await work.Evaluate(Arrivals(node), token);
        }
        catch (OperationCanceledException)
        {
            // Asked to stop, and it did. The node stays owed, and the pass that follows picks it up.
        }
        finally
        {
            _working = null;
        }
    }

    private IReadOnlyList<CanvasArrival> Arrivals(ICanvasNode node)
    {
        var arrived = new List<CanvasArrival>(node.Inputs.Count);

        foreach (var socket in node.Inputs)
        {
            var brought = new List<object>(socket.Connections.Count);

            foreach (var wire in socket.Connections)
            {
                // Whatever feeds it is worked out already - that is what the order is FOR - so this is a read and
                // never a walk.
                brought.Add(wire.FromNode is { } feeding && _values.TryGetValue(feeding, out var value) ? value : null);
            }

            arrived.Add(new CanvasArrival(socket.Kind ?? string.Empty, brought));
        }

        return arrived;
    }

    // DOWN THE WIRES from a node that has just changed. Everything it feeds stands later in the order, so marking them
    // here is enough for this same pass to reach them.
    private void Spread(ICanvasNode node)
    {
        foreach (var socket in node.Outputs)
        {
            foreach (var wire in socket.Connections)
            {
                if (wire.ToNode is { } fed) _dirty.Add(fed);
            }
        }
    }

    // EVERYTHING BEFORE WHAT IT FEEDS, found by taking the nodes nothing arrives at, then the ones only those fed, and
    // so on. Nodes that never come free are in a loop and are left out of the order entirely: they have no value, which
    // is the honest answer, and the walk ends.
    private void Sort()
    {
        _sorted = true;
        _order.Clear();

        var known = new HashSet<ICanvasNode>(_nodes);
        var waiting = new Dictionary<ICanvasNode, int>(known.Count);
        var free = new Queue<ICanvasNode>();

        foreach (var node in _nodes)
        {
            var owed = 0;

            foreach (var socket in node.Inputs)
            {
                foreach (var wire in socket.Connections)
                {
                    // A wire from somewhere this graph does not hold is nothing to wait for. Counted, it would leave
                    // the node owed for ever and read as a loop.
                    if (wire.FromNode is { } feeding && known.Contains(feeding)) owed++;
                }
            }

            waiting[node] = owed;

            if (owed == 0) free.Enqueue(node);
        }

        while (free.Count > 0)
        {
            var node = free.Dequeue();
            _order.Add(node);

            foreach (var socket in node.Outputs)
            {
                foreach (var wire in socket.Connections)
                {
                    if (wire.ToNode is not { } fed || !waiting.TryGetValue(fed, out var owed)) continue;

                    waiting[fed] = --owed;

                    if (owed == 0) free.Enqueue(fed);
                }
            }
        }
    }
}
