using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.DrawingBoard;

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

    /// <summary>The other way round: takes <paramref name="gathered"/> out and puts <paramref name="into"/> where the
    /// TOPMOST of them stood - what making a group is.
    /// <para>ONE call, because the scene is read after every edit: a replace plus a removal apiece leaves it holding
    /// the group AND the things inside it, so a walk meets those twice.</para></summary>
    bool Fold(IReadOnlyList<ICanvasItem> gathered, ICanvasItem into);

    /// <summary>Moves an item to the front or the back of PAINT order - what "bring to front" and "send to back" mean.
    /// <para>Here rather than left to the application, because order in this scene IS paint order: taking an item out
    /// and putting it back would raise it, and there is no way at all to lower one from outside. Both return false for
    /// an item the scene does not hold, and do nothing for one already where it is asked to go.</para></summary>
    bool BringToFront(ICanvasItem item);

    bool SendToBack(ICanvasItem item);

    /// <summary>Puts an item straight after another in paint order, or straight before it - which is how a thing is
    /// put BETWEEN two others, and the ends above cannot do it.
    /// <para>Said as "next to THIS ONE" rather than as "one place along" on purpose: one place along is not something
    /// a person can see. A scene may hold a drawing and a graph at once, and a step that moved an item past something
    /// the current mode does not show is a press that appears to do nothing. The caller names the neighbour, which it
    /// knows and the scene does not.</para></summary>
    bool MoveNextTo(ICanvasItem item, ICanvasItem neighbour, bool after);

    /// <summary>Puts an item at a PLACE in paint order, counting from the back - what writing a layer number means.
    /// Clamped rather than refused: asked for the hundredth place in a scene of ten, a person means the top.</summary>
    bool Reposition(ICanvasItem item, int place);

    /// <summary>Puts the scene in EXACTLY this state: these items, in this order, and nothing else.
    /// <para>What undo needs, and the one thing the rest of this contract cannot express. Membership can be rebuilt
    /// from <see cref="Add"/> and <see cref="Remove"/>, but ORDER cannot - and order here is paint order, so a step put
    /// back in the wrong order is a different drawing. One call rather than a storm of them, so a scene with fifty
    /// thousand things in it raises <see cref="Changed"/> once.</para></summary>
    void Reset(IReadOnlyList<ICanvasItem> items);

    /// <summary>Says that something already in it has changed - an item moved or resized. The scene cannot notice on its
    /// own: what an item holds is the item's business.</summary>
    void Touch();

    /// <summary>Raised when what the canvas would draw has changed. The control repaints and asks again; it keeps no
    /// copy of the scene, so there is nothing for it to get out of step with.</summary>
    event EventHandler Changed;
}
