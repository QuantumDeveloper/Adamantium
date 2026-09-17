using System;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>A SOCKET MOVED ALONG ITS OWN SIDE: which side, where it was, where it is now.
/// <para>Said in places rather than in objects, because that is what a move IS - the socket is the same socket, and so
/// is everything wired to it. Whoever owns the sockets makes the move; the node only reports the gesture.</para>
/// </summary>
public class CanvasPinOrderEventArgs : EventArgs
{
    public CanvasPinOrderEventArgs(bool isInput, int from, int to)
    {
        IsInput = isInput;
        From = from;
        To = to;
    }

    /// <summary>Which side it moved along - the two lists are separate, and a socket never crosses between them.
    /// </summary>
    public bool IsInput { get; }

    /// <summary>Where it was, and where it goes - both in the list it belongs to, after taking it out.</summary>
    public int From { get; }

    public int To { get; }
}
