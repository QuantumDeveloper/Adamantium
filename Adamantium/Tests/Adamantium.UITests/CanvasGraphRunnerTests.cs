using System.Threading;
using System.Threading.Tasks;
using Adamantium.Core.Collections;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UITests.Graph;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>Working a graph out, and keeping it worked out while somebody edits it: the order, what is owed, what a
/// change actually costs, and what happens to a pass whose answer nobody wants any more.</summary>
public class CanvasGraphRunnerTests
{
    private static GraphNode Node(TrackingCollection<ICanvasNode> graph, double value, int inputs = 0, int outputs = 1)
    {
        var node = new GraphNode { Specialization = new GraphWork { Value = value } };

        node.InputCount = inputs;
        node.OutputCount = outputs;

        graph.Add(node);

        return node;
    }

    private static GraphWork Work(ICanvasNode node) => (GraphWork)node.Specialization;

    private static void Join(ICanvasNode from, ICanvasNode to, int output = 0, int input = 0) =>
        _ = new CanvasConnection(from.Outputs[output], to.Inputs[input]);

    [Test]
    public async Task AValueTravelsDownTheWires()
    {
        var graph = new TrackingCollection<ICanvasNode>();
        using var runner = new CanvasGraphRunner(graph);

        var first = Node(graph, 2);
        var second = Node(graph, 10, inputs: 1);

        Join(first, second);

        await runner.Settle();

        Assert.That(runner.ValueOf(first), Is.EqualTo(2.0));
        Assert.That(runner.ValueOf(second), Is.EqualTo(12.0));
    }

    /// <summary>THE POINT OF THE WHOLE THING: a value set on one node works out that node and what it feeds, and asks
    /// nothing of the rest of the graph.</summary>
    [Test]
    public async Task OnlyWhatChangedAndWhatItFeedsIsAskedAgain()
    {
        var graph = new TrackingCollection<ICanvasNode>();
        using var runner = new CanvasGraphRunner(graph);

        var changed = Node(graph, 1);
        var fed = Node(graph, 0, inputs: 1);
        var elsewhere = Node(graph, 7);

        Join(changed, fed);

        await runner.Settle();

        var before = (Work(changed).Ran, Work(fed).Ran, Work(elsewhere).Ran);

        Work(changed).Value = 5;

        await runner.Settle();

        Assert.That(Work(changed).Ran, Is.EqualTo(before.Item1 + 1), "the node that changed");
        Assert.That(Work(fed).Ran, Is.EqualTo(before.Item2 + 1), "what it feeds");
        Assert.That(Work(elsewhere).Ran, Is.EqualTo(before.Item3), "a node nothing happened to");
        Assert.That(runner.ValueOf(fed), Is.EqualTo(5.0));
    }

    /// <summary>A DRAG sets a value at every pixel. The graph is worked out once for the burst, not once per write.
    /// </summary>
    [Test]
    public async Task ABurstOfChangesCostsOnePass()
    {
        var graph = new TrackingCollection<ICanvasNode>();
        using var runner = new CanvasGraphRunner(graph);

        var node = Node(graph, 0);

        await runner.Settle();

        var before = Work(node).Ran;

        for (var i = 1; i <= 60; i++) Work(node).Value = i;

        await runner.Settle();

        Assert.That(Work(node).Ran, Is.EqualTo(before + 1));
        Assert.That(runner.ValueOf(node), Is.EqualTo(60.0));
    }

    /// <summary>A GRAPH THAT FEEDS ITSELF - which a file somebody else wrote may well hold. The nodes of the loop have
    /// no value, and the walk ends.</summary>
    [Test]
    public async Task ALoopLeavesItsOwnNodesWithoutValues()
    {
        var graph = new TrackingCollection<ICanvasNode>();
        using var runner = new CanvasGraphRunner(graph);

        var first = Node(graph, 1, inputs: 1);
        var second = Node(graph, 1, inputs: 1);
        var apart = Node(graph, 3);

        Join(first, second);
        Join(second, first);

        await runner.Settle();

        Assert.That(runner.ValueOf(first), Is.Null);
        Assert.That(runner.ValueOf(second), Is.Null);
        Assert.That(runner.ValueOf(apart), Is.EqualTo(3.0), "the rest of the graph still works out");
    }

    /// <summary>A LONG CHAIN is a long walk and not a deep one. Worked out by recursion this is a stack overflow.
    /// </summary>
    [Test]
    public async Task ADeepChainCostsNoStack()
    {
        const int deep = 20000;

        var graph = new TrackingCollection<ICanvasNode>();
        using var runner = new CanvasGraphRunner(graph);

        var previous = Node(graph, 1);

        for (var i = 1; i < deep; i++)
        {
            var next = Node(graph, 1, inputs: 1);

            Join(previous, next);
            previous = next;
        }

        await runner.Settle();

        Assert.That(runner.ValueOf(previous), Is.EqualTo((double)deep));
    }

    /// <summary>A SOCKET ADDED AFTER the node joined the graph - what an inspector does. The wire dropped on it has to
    /// be heard, and the old watch-once-and-hope did not hear it.</summary>
    [Test]
    public async Task ASocketAddedLaterIsListenedTo()
    {
        var graph = new TrackingCollection<ICanvasNode>();
        using var runner = new CanvasGraphRunner(graph);

        var source = Node(graph, 3);
        var late = Node(graph, 0);

        await runner.Settle();

        Assert.That(runner.ValueOf(late), Is.EqualTo(0.0));

        late.InputCount = 1;
        Join(source, late);

        await runner.Settle();

        Assert.That(runner.ValueOf(late), Is.EqualTo(3.0));
    }

    /// <summary>A WIRE CUT changes what arrives, and everything past it.</summary>
    [Test]
    public async Task AWireCutIsHeardAllTheWayDown()
    {
        var graph = new TrackingCollection<ICanvasNode>();
        using var runner = new CanvasGraphRunner(graph);

        var source = Node(graph, 4);
        var middle = Node(graph, 0, inputs: 1);
        var last = Node(graph, 0, inputs: 1);

        Join(source, middle);
        Join(middle, last);

        await runner.Settle();

        Assert.That(runner.ValueOf(last), Is.EqualTo(4.0));

        source.Outputs[0].Connections[0].Disconnect();

        await runner.Settle();

        Assert.That(runner.ValueOf(last), Is.EqualTo(0.0));
    }

    /// <summary>A SOCKET THAT TAKES MANY brings all of them, and they are one socket's worth rather than several
    /// sockets.</summary>
    [Test]
    public async Task ASocketThatTakesManyBringsThemAll()
    {
        var graph = new TrackingCollection<ICanvasNode>();
        using var runner = new CanvasGraphRunner(graph);

        var first = Node(graph, 1);
        var second = Node(graph, 2);
        var merge = Node(graph, 0, inputs: 1);

        merge.Inputs[0].Capacity = GraphNode.Branches;

        Join(first, merge);
        Join(second, merge);

        await runner.Settle();

        Assert.That(merge.Inputs[0].Connections.Count, Is.EqualTo(2));
        Assert.That(runner.ValueOf(merge), Is.EqualTo(3.0));
    }

    /// <summary>A NODE GONE is not listened to any more - a graph that went on working out nodes nobody holds is both a
    /// leak and an answer about a document that is not open.</summary>
    [Test]
    public async Task ANodeTakenOutStopsBeingHeard()
    {
        var graph = new TrackingCollection<ICanvasNode>();
        using var runner = new CanvasGraphRunner(graph);

        var node = Node(graph, 1);

        await runner.Settle();

        graph.Remove(node);

        await runner.Settle();

        var before = Work(node).Ran;

        Work(node).Value = 99;

        await runner.Settle();

        Assert.That(Work(node).Ran, Is.EqualTo(before));
        Assert.That(runner.ValueOf(node), Is.Null);
    }

    /// <summary>A GRAPH EMPTIED IN ONE GO names nothing it dropped - which is what loading a file does before it puts
    /// the new one in.</summary>
    [Test]
    public async Task AGraphClearedStopsBeingHeard()
    {
        var graph = new TrackingCollection<ICanvasNode>();
        using var runner = new CanvasGraphRunner(graph);

        var node = Node(graph, 1);

        await runner.Settle();

        graph.Clear();

        await runner.Settle();

        var before = Work(node).Ran;

        Work(node).Value = 99;

        await runner.Settle();

        Assert.That(Work(node).Ran, Is.EqualTo(before));
    }

    /// <summary>A KIND SWAPPED: the state that went is let go of, and the one that came is listened to.</summary>
    [Test]
    public async Task AKindSwappedMovesTheWatchWithIt()
    {
        var graph = new TrackingCollection<ICanvasNode>();
        using var runner = new CanvasGraphRunner(graph);

        var node = Node(graph, 1);
        var gone = Work(node);

        node.Specialization = new GraphWork { Value = 5 };

        await runner.Settle();

        Assert.That(runner.ValueOf(node), Is.EqualTo(5.0));

        var before = gone.Ran;

        gone.Value = 99;

        await runner.Settle();

        Assert.That(gone.Ran, Is.EqualTo(before), "the state that was replaced");
        Assert.That(runner.ValueOf(node), Is.EqualTo(5.0));
    }

    /// <summary>A NODE THAT DOES NO WORK is drawn and wired like any other and simply has no value - which is what a
    /// comment node is. It must not stop the walk.</summary>
    [Test]
    public async Task ANodeWithNothingToComputeHasNoValue()
    {
        var graph = new TrackingCollection<ICanvasNode>();
        using var runner = new CanvasGraphRunner(graph);

        var quiet = new GraphNode();
        quiet.OutputCount = 1;
        graph.Add(quiet);

        var after = Node(graph, 2, inputs: 1);

        Join(quiet, after);

        await runner.Settle();

        Assert.That(runner.ValueOf(quiet), Is.Null);
        Assert.That(runner.ValueOf(after), Is.EqualTo(2.0));
    }

    /// <summary>A PASS WHOSE ANSWER IS ALREADY STALE is dropped where it stands: the node doing the work is told, which
    /// is the only thing that makes a node that renders for half a second bearable.</summary>
    [Test]
    public async Task APassIsDroppedWhenTheGraphMovesOn()
    {
        var graph = new TrackingCollection<ICanvasNode>();
        using var runner = new CanvasGraphRunner(graph);

        var slow = Node(graph, 1);
        var other = Node(graph, 1);

        var told = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Work(slow).WaitsOnce = async token =>
        {
            using var _ = token.Register(() => told.TrySetResult(true));

            await Task.Delay(Timeout.Infinite, token);
        };

        var pass = runner.Settle();

        Work(other).Value = 2;

        Assert.That(await told.Task, Is.True, "the node was told to stop");

        await pass;

        Assert.That(runner.ValueOf(slow), Is.EqualTo(1.0), "the pass that followed finished the work");
        Assert.That(runner.ValueOf(other), Is.EqualTo(2.0));
    }

    /// <summary>WHOEVER IS WAITING can give up, and the graph stays owed rather than half-done.</summary>
    [Test]
    public void AWaitGivenUpOnStopsTheWork()
    {
        var graph = new TrackingCollection<ICanvasNode>();
        using var runner = new CanvasGraphRunner(graph);

        var slow = Node(graph, 1);
        var giveUp = new CancellationTokenSource();

        Work(slow).WaitsOnce = async token =>
        {
            giveUp.Cancel();

            await Task.Delay(Timeout.Infinite, token);
        };

        Assert.ThrowsAsync<System.OperationCanceledException>(async () => await runner.Settle(giveUp.Token));
    }

    /// <summary>NOTHING A NODE IS DRAWN BY is a reason to work the graph out. Dragging one across the plane is the
    /// commonest thing anybody does on a canvas, and it computes nothing.</summary>
    [Test]
    public async Task MovingANodeCostsNothing()
    {
        var graph = new TrackingCollection<ICanvasNode>();
        using var runner = new CanvasGraphRunner(graph);

        var node = Node(graph, 1);

        await runner.Settle();

        var before = Work(node).Ran;

        for (var i = 0; i < 100; i++)
        {
            node.Left = i;
            node.Top = i;
        }

        node.Title = "moved";
        node.IsCollapsed = true;

        await runner.Settle();

        Assert.That(Work(node).Ran, Is.EqualTo(before));
    }
}
