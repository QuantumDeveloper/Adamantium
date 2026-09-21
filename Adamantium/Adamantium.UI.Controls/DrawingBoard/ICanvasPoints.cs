using System.Collections.Generic;
using Adamantium.Mathematics;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>An item reshaped by its OWN POINTS rather than by a box round it - a curve, a polyline, a connection with
/// a bend in it.
/// <para>Separate from <see cref="ICanvasItem"/> because most things on a plane are not like this, and a contract every
/// item had to answer would be a list of empty implementations. An item that offers points usually offers no resize
/// grips either (<see cref="ICanvasItem.Handles"/>): two ways to reshape one thing fight each other, and the box is
/// the one that means less.</para></summary>
public interface ICanvasPoints
{
    /// <summary>The points, in WORLD units, in the order they are joined.</summary>
    IReadOnlyList<Vector2> Points { get; }

    /// <summary>Moves one of them. The canvas calls this while a point handle is dragged; anything the item has to
    /// rebuild because of it - a cached curve, a bounding box - it rebuilds here.</summary>
    void MovePoint(int index, Vector2 world);
}
