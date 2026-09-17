using System;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>A WIRE between two sockets: the two ends and nothing else.
/// <para>A class and not a contract, deliberately. A wire stands in two sockets at once, and keeping those two in step
/// is exactly the kind of bookkeeping that becomes two collections somebody has to remember to update - so the wire
/// holds it itself: made, it puts itself in both; cut, it leaves both. One operation, one owner of the invariant, and
/// nothing for an application to implement wrongly.</para>
/// <para>No geometry: where a wire runs is worked out from where its nodes stand, so dragging one drags its wires with
/// nothing to keep in step. Its ends never change either - re-routing is cutting this one and making another, which is
/// also what the person doing it means.</para></summary>
public sealed class CanvasConnection
{
    /// <summary>The socket the wire LEAVES - an output.</summary>
    public ICanvasSocket From { get; }

    /// <summary>The socket it ARRIVES at - an input.</summary>
    public ICanvasSocket To { get; }

    /// <summary>The node it leaves, and the one it arrives at - the socket's own, so a walk never needs a second
    /// account of who holds what.</summary>
    public ICanvasNode FromNode => From?.Node;

    public ICanvasNode ToNode => To?.Node;

    /// <summary>Joins two sockets, and IS the joining: the wire is in both of them by the time this returns.
    /// <para>A socket already holding as many wires as it takes loses its oldest - which is what dropping a new wire on
    /// a taken input means everywhere.</para></summary>
    public CanvasConnection(ICanvasSocket from, ICanvasSocket to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        From = from;
        To = to;

        Seat(from);
        Seat(to);
    }

    /// <summary>Cuts it: gone from both ends, and nothing else holds it.</summary>
    public void Disconnect()
    {
        From?.Connections.Remove(this);
        To?.Connections.Remove(this);
    }

    private void Seat(ICanvasSocket socket)
    {
        // The OLDEST goes, and only as many as have to: a socket that takes three and holds three loses one, not all
        // three, which is the difference between a full socket and a single one.
        while (socket.Capacity > 0 && socket.Connections.Count >= socket.Capacity)
        {
            socket.Connections[0].Disconnect();
        }

        socket.Connections.Add(this);
    }
}
