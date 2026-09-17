namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>An item that can be TURNED and LEANED.
/// <para>Its own contract rather than a member on every item, for the same reason points are: a stroke of ink and a
/// piece of text are not turned in this drawing, and a member every item had to answer would be a row of properties
/// that mean nothing. What DOES turn says so, and the canvas offers the gesture only to those.</para>
/// <para>The transform is about the middle of the item's own box, and <see cref="ICanvasItem.Bounds"/> stays the box
/// of the item UNTURNED - everything that reads bounds reads the shape's own size, and the frame asks the transform
/// separately where the corners went. A bounds that grew as a shape turned would make resizing it a different size
/// every time it was let go.</para></summary>
public interface ICanvasTransformed
{
    /// <summary>How it is turned and leaned.</summary>
    CanvasTransform Transform { get; set; }
}
