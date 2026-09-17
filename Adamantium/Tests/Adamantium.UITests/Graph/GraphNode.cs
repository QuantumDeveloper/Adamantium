using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Adamantium.Core.Collections;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.DrawingBoard;

namespace Adamantium.UITests.Graph;

/// <summary>AN APPLICATION'S NODE, as a test has to supply one: the canvas holds no node type of its own - a node is
/// the application's object, like a list's items - so everything that asks anything of a graph brings its own.</summary>
public class GraphNode : ICanvasNode
{
    private string _kind = string.Empty;
    private string _title = string.Empty;
    private double _left;
    private double _top;
    private double _width;
    private bool _isCollapsed;
    private ICanvasNodeSpecialization _specialization;
    private Adamantium.Mathematics.Color? _accent;

    public event PropertyChangedEventHandler PropertyChanged;

    public string Kind
    {
        get => _kind;
        set => Set(ref _kind, value);
    }

    public string Title
    {
        get => _title;
        set => Set(ref _title, value);
    }

    public double Left
    {
        get => _left;
        set => Set(ref _left, value);
    }

    public double Top
    {
        get => _top;
        set => Set(ref _top, value);
    }

    public double Width
    {
        get => _width;
        set => Set(ref _width, value);
    }

    public bool IsCollapsed
    {
        get => _isCollapsed;
        set => Set(ref _isCollapsed, value);
    }

    public Adamantium.Mathematics.Color? Accent
    {
        get => _accent;
        set => Set(ref _accent, value);
    }

    public TrackingCollection<ICanvasSocket> Inputs { get; } = new();

    public TrackingCollection<ICanvasSocket> Outputs { get; } = new();

    public GraphNode()
    {
        Inputs.CollectionChanged += OnInputsChanged;
        Outputs.CollectionChanged += OnOutputsChanged;
    }

    /// <summary>HOW MANY sockets down each side, the number a person sets when they want one more rather than a
    /// particular one. Not a second store - it grows and trims the list itself, keeping the sockets that survive, so
    /// asking for a fourth does not take the names off the first three.</summary>
    public int InputCount
    {
        get => Inputs.Count;
        set => Fit(Inputs, value, "In");
    }

    public int OutputCount
    {
        get => Outputs.Count;
        set => Fit(Outputs, value, "Out", Branches);
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
            sockets.Add(new GraphSocket(capacity) { Name = Free(sockets, prefix) });
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

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(count));
    }

    /// <summary>What this node is. Installing one lets it shape the sockets; what the old one had is gone with it,
    /// which is what changing a node's kind means.</summary>
    public ICanvasNodeSpecialization Specialization
    {
        get => _specialization;
        set
        {
            if (ReferenceEquals(_specialization, value)) return;

            _specialization = value;

            OnChanged(nameof(Specialization));
            value?.Shape(this);

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Specialization)));
        }
    }

    protected void Set<T>(ref T field, T value, [CallerMemberName] string name = null)
    {
        if (Equals(field, value)) return;

        field = value;

        OnChanged(name);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>Something about this node changed, BEFORE anybody is told. Where a node that has to stay consistent
    /// with itself does it - one told it is now a Texture has to become what a Texture is, and only the application
    /// knows what that means.</summary>
    protected virtual void OnChanged(string name)
    {
    }
}

/// <summary>The engine's plain <see cref="ICanvasSocket"/>, for the same reason as <see cref="GraphNode"/>.
/// </summary>
public class GraphSocket : ICanvasSocket
{
    private string _name = string.Empty;
    private string _kind = string.Empty;
    private ICanvasNode _node;

    public event PropertyChangedEventHandler PropertyChanged;

    private int _capacity = 1;

    /// <param name="capacity">How many wires it takes - one by default, <see cref="GraphNode.Branches"/> for
    /// as many as come.</param>
    public GraphSocket(int capacity = 1) => _capacity = capacity;

    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    public string Kind
    {
        get => _kind;
        set => Set(ref _kind, value);
    }

    public ICanvasNode Node
    {
        get => _node;
        set
        {
            if (ReferenceEquals(_node, value)) return;

            _node = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Node)));
        }
    }

    /// <summary>How many wires it takes. Lowered under wires that are already there, the oldest of them go - the socket
    /// says what it holds, so it cannot be left holding more than it says.</summary>
    public int Capacity
    {
        get => _capacity;
        set
        {
            if (_capacity == value || value < 0) return;

            _capacity = value;

            while (_capacity > 0 && Connections.Count > _capacity) Connections[0].Disconnect();

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Capacity)));
        }
    }

    public TrackingCollection<CanvasConnection> Connections { get; } = new();

    private void Set(ref string field, string value, [CallerMemberName] string name = null)
    {
        if (field == value) return;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
