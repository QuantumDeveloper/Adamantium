using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>
/// One of a tab strip's lists - the pinned row or the ordinary one. It exists because a strip is TWO lists (see
/// <see cref="TabControl.PinnedItems"/>), and a list needs a control to lay it out; what it deliberately does NOT do is
/// decide anything about tabs.
/// <para>Every question about containers goes to the owning <see cref="TabControl"/>: what counts as a container, how to
/// make one, how to prepare it, how to let it go. That is what keeps a generated tab a real <see cref="TabItem"/> with
/// the owner's item template on it - a plain <see cref="ItemsControl"/> would build its own default containers, and the
/// strip would show items instead of tabs.</para>
/// </summary>
public class TabItemsControl : ItemsControl
{
    /// <summary>The strip this list belongs to. It is a template part of that control, so the templated parent IS the
    /// owner - no walking the tree and no guessing.</summary>
    private TabControl Owner => TemplatedParent as TabControl;

    protected internal override bool IsItemItsOwnContainer(object item) =>
        Owner?.IsItemItsOwnContainer(item) ?? base.IsItemItsOwnContainer(item);

    protected internal override IUIComponent GetContainerForItem(object item) =>
        Owner?.GetContainerForItem(item) ?? base.GetContainerForItem(item);

    protected internal override void PrepareContainer(IUIComponent container, object item)
    {
        if (Owner is { } owner) owner.PrepareContainer(container, item);
        else base.PrepareContainer(container, item);
    }

    protected internal override void ClearContainer(IUIComponent container)
    {
        if (Owner is { } owner) owner.ClearContainer(container);
        else base.ClearContainer(container);
    }
}
