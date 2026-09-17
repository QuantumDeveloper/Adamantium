using System.ComponentModel;
using Adamantium.Core.Collections;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>One socket of a node, as the application holds it - and one END OF THE GRAPH's edges.
/// <para>A wire arrives at a socket, not at a node, so the wires live here. That is also what makes the graph walkable
/// both ways: up from an input to whatever feeds it, down from an output to everything it feeds, each in the time it
/// takes to read that socket's own wires.</para>
/// <para>A colour is not here - that is how it LOOKS, which the template gives, reading it off the kind.</para>
/// </summary>
public interface ICanvasSocket : INotifyPropertyChanged
{
    /// <summary>What it is called. A wire is written down as the two names it joins, so this is also its address.
    /// </summary>
    string Name { get; set; }

    /// <summary>What flows through it - "Color", "Number". Two sockets join when their kinds agree; one that says
    /// nothing takes anything. Whether they agree is the application's rule, not the engine's.</summary>
    string Kind { get; set; }

    /// <summary>The node this belongs to, stamped when it joins one. Without it a walk up the graph stops at the far
    /// socket with no way of asking whose it is.</summary>
    ICanvasNode Node { get; set; }

    /// <summary>HOW MANY wires may sit here, and <c>0</c> for as many as anybody brings.
    /// <para>One is what an input usually is - a value comes from one place - and a wire dropped on a full socket
    /// displaces the oldest, which is what dropping one on a taken input means everywhere. Many-to-one is real though:
    /// a flow graph's exec pins, a merge node, a join that takes a list. So it is a number said per socket, not a rule
    /// the engine imposes - and settable, because which sockets take several is the application's to decide and an
    /// inspector's to edit.</para></summary>
    int Capacity { get; set; }

    /// <summary>The wires sitting on it. A LIST and not a set: in a socket that takes many, the order of the wires is
    /// part of the answer.</summary>
    TrackingCollection<CanvasConnection> Connections { get; }
}
