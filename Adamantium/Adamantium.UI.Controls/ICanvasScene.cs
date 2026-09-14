using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>What is ON the canvas. Held by the APPLICATION, not by the control - the same arrangement the table has with
/// its rows, and for the same reason: undo, saving and whatever else a drawing is for belong to whoever owns the
/// drawing, and a control that owned them would have to grow a way to hand them back.
/// <para>The canvas asks it one thing - what falls inside the piece of world it can see - so the cost of a frame
/// follows what is VISIBLE rather than what exists. The first implementation walks everything and rejects by bounds;
/// a spatial index goes behind this same question when walking stops being fast enough, and the control never
/// learns.</para></summary>
public interface ICanvasScene
{
    /// <summary>The items whose bounds meet this piece of world. Order is paint order.</summary>
    IEnumerable<ICanvasItem> ItemsIn(Rect world);

    /// <summary>Puts an item in. What a tool calls when it has finished making one.</summary>
    void Add(ICanvasItem item);

    /// <summary>Takes an item out. Here rather than only on the concrete scene because a tool must be able to delete
    /// what is selected, and a tool only ever sees this.</summary>
    bool Remove(ICanvasItem item);

    /// <summary>Puts <paramref name="pieces"/> exactly WHERE <paramref name="item"/> was, and takes it out.
    /// <para>Order here is paint order, so "take it out and add what is left" is not the same thing: the pieces would go
    /// to the end and rise above everything put in after the original. Rubbing a hole in a stroke does not move it - a
    /// stroke that was behind a control has to stay behind it, in both halves.</para></summary>
    bool Replace(ICanvasItem item, IReadOnlyList<ICanvasItem> pieces);

    /// <summary>Moves an item to the front or the back of PAINT order - what "bring to front" and "send to back" mean.
    /// <para>Here rather than left to the application, because order in this scene IS paint order: taking an item out
    /// and putting it back would raise it, and there is no way at all to lower one from outside. Both return false for
    /// an item the scene does not hold, and do nothing for one already where it is asked to go.</para></summary>
    bool BringToFront(ICanvasItem item);

    bool SendToBack(ICanvasItem item);

    /// <summary>Says that something already in it has changed - an item moved or resized. The scene cannot notice on its
    /// own: what an item holds is the item's business.</summary>
    void Touch();

    /// <summary>Raised when what the canvas would draw has changed. The control repaints and asks again; it keeps no
    /// copy of the scene, so there is nothing for it to get out of step with.</summary>
    event EventHandler Changed;
}
