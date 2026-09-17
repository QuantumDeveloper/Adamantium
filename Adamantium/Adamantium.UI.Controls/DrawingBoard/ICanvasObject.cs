namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>A THING ON THE PLANE, as the application holds it - the drawing's answer to <see cref="ICanvasNode"/>.
/// <para>The canvas takes a collection of these and makes what draws them: the application never constructs a scene
/// item and never a control. What it holds is data - where the thing is, and what it is - and what that turns into is
/// the canvas's business.</para>
/// <para>Written from both sides, like the nodes: something drawn on the plane appears in the collection, something
/// taken out of it leaves the plane, and dragging one writes its new place here.</para></summary>
public interface ICanvasObject : ICanvasPlaced
{
    double Height { get; set; }

    /// <summary>WHAT IT IS. A description the canvas understands - <see cref="ICanvasShapeDescription"/> - draws as
    /// that shape; anything else is the application's own object and is drawn by the template chosen for its type, in a
    /// control the canvas builds.</summary>
    object Content { get; set; }
}
