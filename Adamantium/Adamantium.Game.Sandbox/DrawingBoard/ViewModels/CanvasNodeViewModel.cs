using System.Collections.Specialized;
using Adamantium.Core.Collections;
using Adamantium.Mathematics;
using Adamantium.MVVM;
using Adamantium.UI.Controls.DrawingBoard;

namespace Adamantium.Game.Sandbox.DrawingBoard.ViewModels;

/// <summary>A NODE OF THIS PAGE'S GRAPH - the application's own object, implementing the canvas's <see
/// cref="ICanvasNode"/>.
/// <para>It lives here and not in the engine, for the same reason a list's items do: a control works with the data it
/// is handed and generates what shows it. The canvas never makes one of these itself - it asks the catalogue of kinds,
/// which is this page's too.</para>
/// <para>A view-model said the way every view-model here is said: the framework's base and <c>[Bindable]</c> fields,
/// so what a property does when it changes is a partial method and not a hand-written setter.</para></summary>
[ViewModel]
public partial class CanvasNodeViewModel : ICanvasNode
{
    /// <summary>WHAT IT IS, as a word the catalogue looks up - and what a file names it by.</summary>
    [Bindable] private string _kind = string.Empty;

    /// <summary>What it is called on its strip.</summary>
    [Bindable] private string _title = string.Empty;

    /// <summary>Where it sits on the plane, in world units.</summary>
    [Bindable] private double _left;

    [Bindable] private double _top;

    /// <summary>How wide it is. The height is the node's own business - it is as tall as its sockets and its body.
    /// </summary>
    [Bindable] private double _width;

    /// <summary>Folded down to its title strip, or open.</summary>
    [Bindable] private bool _isCollapsed;

    /// <summary>The colour it is drawn in, or null for the theme's.</summary>
    [Bindable] private Color? _accent;

    /// <summary>What this node IS: the part a change of kind replaces, while the node itself - and every wire on it -
    /// stays the same object.</summary>
    [Bindable] private ICanvasNodeSpecialization _specialization;

    // Installing one lets it SHAPE the node: which sockets it has and what they carry. Said here rather than in a
    // setter, which is what the generator's hook is for.
    partial void OnSpecializationChanged(ICanvasNodeSpecialization value) => value?.Shape(this);

    public CanvasNodeViewModel()
    {
        Inputs.CollectionChanged += OnInputsChanged;
        Outputs.CollectionChanged += OnOutputsChanged;
    }

    public TrackingCollection<ICanvasSocket> Inputs { get; } = new();

    public TrackingCollection<ICanvasSocket> Outputs { get; } = new();

    /// <summary>A socket of THIS graph's own kind, for the inspector's plus - the canvas holds no socket type to fall
    /// back on, so the node that would own it is the one asked to make it.</summary>
    public ICanvasSocket NewSocket(string name) => new CanvasSocketViewModel { Name = name };

    /// <summary>HOW MANY sockets down each side, the number a person sets when they want one more rather than a
    /// particular one. Not a second store - it grows and trims the list itself, keeping the sockets that survive, so
    /// asking for a fourth does not take the names off the first three.</summary>
    public int InputCount
    {
        get => Inputs.Count;
        set => Fit(Inputs, value, Models.GraphWords.Sides.In);
    }

    public int OutputCount
    {
        get => Outputs.Count;
        set => Fit(Outputs, value, Models.GraphWords.Sides.Out, Branches);
    }

    /// <summary>What an OUTPUT takes: as many wires as anybody brings. One value feeding three inputs is the ordinary
    /// case, and a socket that displaced the last wire every time would make a graph impossible to build.</summary>
    public const int Branches = 0;

    /// <summary>Grows or trims a side to that many sockets, keeping the ones that survive. Public because a
    /// specialization shaping a node does exactly this, and writing it again per application is the same loop twice.
    /// </summary>
    /// <param name="capacity">How many wires each socket MADE here takes - one by default, which is what an input is;
    /// <see cref="Branches"/> for as many as come. Sockets already standing keep whatever they were given.</param>
    public static void Fit(TrackingCollection<ICanvasSocket> sockets, int count, string prefix, int capacity = 1)
    {
        if (count < 0) return;

        while (sockets.Count > count) sockets.RemoveAt(sockets.Count - 1);

        while (sockets.Count < count)
        {
            sockets.Add(new CanvasSocketViewModel(capacity) { Name = Free(sockets, prefix) });
        }
    }

    // A name NOBODY ON THIS SIDE HAS. Counting the sockets and adding one repeats a name as soon as one is taken out of
    // the middle - two sockets called "In 5", which is a graph nobody can read and a file that cannot say which is
    // which.
    private static string Free(TrackingCollection<ICanvasSocket> sockets, string prefix)
    {
        for (var number = 1; ; number++)
        {
            var name = $"{prefix} {number}";
            var taken = false;

            foreach (var socket in sockets)
            {
                if (socket.Name != name) continue;

                taken = true;
                break;
            }

            if (!taken) return name;
        }
    }

    private void OnInputsChanged(object sender, NotifyCollectionChangedEventArgs e) => Took(e, nameof(InputCount));

    private void OnOutputsChanged(object sender, NotifyCollectionChangedEventArgs e) => Took(e, nameof(OutputCount));

    // A socket JOINING is stamped with whose it is, and one leaving takes its wires with it: a wire sits on a socket,
    // and a socket that belongs to nobody is not somewhere a wire can be.
    private void Took(NotifyCollectionChangedEventArgs e, string count)
    {
        // A socket MOVED along its own side is reported as leaving and arriving at once, and it never left: it is the
        // same socket, on the same node, with the same wires on it. Reading that as a departure cut them.
        if (e.Action == NotifyCollectionChangedAction.Move) return;

        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems)
            {
                if (item is not ICanvasSocket socket) continue;

                while (socket.Connections.Count > 0) socket.Connections[0].Disconnect();
                socket.Node = null;
            }
        }

        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems)
            {
                if (item is ICanvasSocket socket) socket.Node = this;
            }
        }

        RaisePropertyChanged(count);
    }
}
