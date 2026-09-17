using System;
using Adamantium.Core.Collections;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>The panes an <see cref="InfiniteCanvas"/> shows over its plane - see <see cref="InfiniteCanvas.Chrome"/>.
/// </summary>
public class CanvasPanes : TrackingCollection<CanvasPane>
{
    protected override void InsertItem(int index, CanvasPane item)
    {
        ArgumentNullException.ThrowIfNull(item);
        base.InsertItem(index, item);
    }
}
