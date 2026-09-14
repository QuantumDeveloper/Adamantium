using System;
using Adamantium.Core.Collections;

namespace Adamantium.UI.Controls;

/// <summary>The tools an <see cref="InfiniteCanvas"/> offers - see <see cref="InfiniteCanvas.Tools"/>.</summary>
public class CanvasTools : TrackingCollection<ICanvasTool>
{
    protected override void InsertItem(int index, ICanvasTool item)
    {
        ArgumentNullException.ThrowIfNull(item);
        base.InsertItem(index, item);
    }
}
