using System.Collections.Generic;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>WHAT CAME IN ON ONE SOCKET: what that socket carries, and the values its wires brought - none, one, or as
/// many as a socket that takes many was given.
/// <para>A list per socket and never one flat list: a socket may take several wires, and flattening them moves every
/// socket after it along. The KIND travels with the values, so a node reads what arrived by what it IS - "the colours",
/// "the amount" - and a socket somebody added by hand counts like any other of its kind rather than being one the node
/// has never heard of.</para></summary>
public readonly struct CanvasArrival
{
    public CanvasArrival(string kind, IReadOnlyList<object> values)
    {
        Kind = kind;
        Values = values;
    }

    /// <summary>What flows through the socket, in the application's own words.</summary>
    public string Kind { get; }

    /// <summary>What its wires brought, in the order the wires sit on it - empty for a socket nothing is joined to.
    /// </summary>
    public IReadOnlyList<object> Values { get; }
}
