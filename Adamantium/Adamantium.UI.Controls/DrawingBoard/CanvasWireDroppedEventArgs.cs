using System;
using Adamantium.Mathematics;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>A wire let go over EMPTY PLANE, offered to the application before it is thrown away.
/// <para>This is the gesture every node editor is known by: you pull a wire out of a socket, drop it where there is
/// nothing, and a list of nodes opens under the pointer - pick one and it arrives already wired. It is the fastest way
/// to build a graph there is, because the two things you were going to do anyway - make a node, join it - are one
/// motion.</para>
/// <para>WHAT nodes there are is the application's and can be nothing else: the engine has never heard of "Multiply".
/// So the canvas offers the moment and the application answers it - or does not, and the wire simply vanishes the way
/// an abandoned gesture should.</para></summary>
public sealed class CanvasWireDroppedEventArgs : EventArgs
{
    /// <summary>The node the wire came out of.</summary>
    public ElementItem FromItem { get; init; }

    /// <summary>The socket it came out of - which says what the new node has to offer to be worth making, and which
    /// end of it the wire should arrive at.</summary>
    public CanvasNodePin FromPin { get; init; }

    /// <summary>Where it was let go, in WORLD units - where the new node goes, and where a list of them should open.
    /// </summary>
    public Vector2 World { get; init; }

    /// <summary>Where it was let go on the SCREEN, in pixels - what a popup is placed by.</summary>
    public Vector2 Screen { get; init; }

    /// <summary>Set by whoever takes the offer. Nobody taking it is the ordinary case and not a failure: a wire
    /// dropped on nothing is a gesture abandoned.</summary>
    public bool Handled { get; set; }
}
