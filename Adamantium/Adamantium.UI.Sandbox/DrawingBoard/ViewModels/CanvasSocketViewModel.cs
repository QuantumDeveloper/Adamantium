using Adamantium.Core.Collections;
using Adamantium.MVVM;
using Adamantium.UI.Controls.DrawingBoard;

namespace Adamantium.UI.Sandbox.DrawingBoard.ViewModels;

/// <summary>A SOCKET of one of this page's nodes - the application's own <see cref="ICanvasSocket"/>, for the same
/// reason as <see cref="CanvasNodeViewModel"/>.</summary>
[ViewModel]
public partial class CanvasSocketViewModel : ICanvasSocket
{
    /// <param name="capacity">How many wires it takes - one by default, <see cref="CanvasNodeViewModel.Branches"/> for
    /// as many as come.</param>
    public CanvasSocketViewModel(int capacity = 1) => _capacity = capacity;

    /// <summary>What it is called beside its disc.</summary>
    [Bindable] private string _name = string.Empty;

    /// <summary>What flows through it. Two sockets are joined when they agree, and an empty one takes anything.
    /// </summary>
    [Bindable] private string _kind = string.Empty;

    /// <summary>Whose it is. Stamped by the node as the socket joins it.</summary>
    [Bindable] private ICanvasNode _node;

    /// <summary>How many wires it takes; zero for as many as come.</summary>
    [Bindable] private int _capacity = 1;

    // LOWERED UNDER WIRES THAT ARE ALREADY THERE, the oldest of them go: the socket says what it holds, so it cannot be
    // left holding more than it says. A negative number is not a count at all and is read as zero.
    partial void OnCapacityChanged(int value)
    {
        if (value < 0)
        {
            Capacity = 0;
            return;
        }

        while (value > 0 && Connections.Count > value) Connections[0].Disconnect();
    }

    public TrackingCollection<CanvasConnection> Connections { get; } = new();
}
