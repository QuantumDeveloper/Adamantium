using System;
using System.Collections.Generic;

namespace Adamantium.UI.Controls;

/// <summary>Something is about to be taken out of the drawing, and whoever owns the drawing gets to answer first.
/// <para>Raised before the canvas removes anything, so a `Delete` pressed on the plane goes through the same question
/// as a button in a panel does - otherwise the question would be a lie the moment somebody used the keyboard.</para>
/// <para>Leave <see cref="Handled"/> alone and the canvas deletes as it always has, which is what every existing
/// application gets. Set it and the canvas does NOTHING: the deletion now belongs to the handler, which is what makes
/// an ASYNCHRONOUS answer possible - a dialog cannot be waited for inside a key press, so the handler asks, and calls
/// <see cref="InfiniteCanvas.DeleteSelection"/> itself when it has an answer.</para></summary>
public sealed class CanvasDeleteRequestedEventArgs : EventArgs
{
    public CanvasDeleteRequestedEventArgs(IReadOnlyList<ICanvasItem> items)
    {
        Items = items;
    }

    /// <summary>What would be removed.</summary>
    public IReadOnlyList<ICanvasItem> Items { get; }

    /// <summary>Set by a handler that has taken responsibility for the deletion. The canvas then removes nothing.</summary>
    public bool Handled { get; set; }
}
