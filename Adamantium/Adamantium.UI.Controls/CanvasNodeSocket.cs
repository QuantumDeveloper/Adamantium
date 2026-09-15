using Adamantium.Mathematics;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>The little disc on the edge of a <see cref="CanvasNode"/> that a connection docks into.
/// <para>A TYPE of its own, and that is the whole reason it exists: the disc used to be an unnamed border inside an
/// item template, which is a thing nothing outside can find. A connection is held by the two sockets it joins rather
/// than by two points, so somebody has to be able to ask WHERE a socket is and WHICH socket is under the pointer -
/// and the answer has to survive the node being moved, resized, given another socket or restyled by a theme.</para>
/// <para>It carries no behaviour. What it is is a place, and being findable is what a place has to be.</para></summary>
public class CanvasNodeSocket : Border
{
    /// <summary>The socket this disc stands for - its own <see cref="DataContext"/>, said out loud so that whoever
    /// found the disc does not have to know how the template was wired.</summary>
    public CanvasNodePin Pin => DataContext as CanvasNodePin;

    /// <summary>The middle of it, in the coordinates of <paramref name="within"/> - the node, usually. Null while the
    /// node has not been laid out, which is every frame before the first one.</summary>
    public Vector2? MiddleIn(IUIComponent within)
    {
        if (within == null || RenderSize.Width <= 0 || RenderSize.Height <= 0) return null;

        return this.TranslatePoint(new Vector2(RenderSize.Width / 2, RenderSize.Height / 2), within);
    }
}
