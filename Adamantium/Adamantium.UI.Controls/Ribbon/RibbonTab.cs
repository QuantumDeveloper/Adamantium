using Adamantium.UI.Core;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>One page of the ribbon: a <see cref="Header"/> in the strip and a row of <see cref="RibbonGroup"/>s in the
/// groups area while it is selected. The tab is NOT its own strip container - see <see cref="RibbonTabHeader"/>.</summary>
public class RibbonTab : ItemsControl, IHeaderedItemsControl
{
    public static readonly AdamantiumProperty HeaderProperty = AdamantiumProperty.Register(nameof(Header),
        typeof(object), typeof(RibbonTab), new PropertyMetadata(null));

    public static readonly AdamantiumProperty HeaderTemplateProperty = AdamantiumProperty.Register(nameof(HeaderTemplate),
        typeof(DataTemplate), typeof(RibbonTab), new PropertyMetadata(null));

    /// <summary>The tab's label in the strip.</summary>
    public object Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>How a data <see cref="Header"/> is drawn in the strip. Falls back to the ribbon's ItemTemplate.</summary>
    public DataTemplate HeaderTemplate
    {
        get => GetValue<DataTemplate>(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }

    public RibbonTab()
    {
        Items.CollectionChanged += (_, _) => RefreshSeparators();
    }

    // Which group is last is a fact about the LIST, so only the tab can answer it.
    private void RefreshSeparators()
    {
        for (var i = 0; i < Items.Count; i++)
        {
            var group = Items[i] as RibbonGroup ?? ItemContainerGenerator?.ContainerFromIndex(i) as RibbonGroup;
            if (group != null) group.ShowSeparator = i < Items.Count - 1;
        }
    }

    // A HierarchicalDataTemplate needs a headered container (the base binds its Header + ItemsSource); a flat
    // ItemTemplate keeps the base ContentPresenter. Same seam as MenuItem.
    protected internal override IUIComponent GetContainerForItem(object item)
    {
        if (ItemTemplate is not HierarchicalDataTemplate) return base.GetContainerForItem(item);

        var group = new RibbonGroup();
        if (ItemContainerStyle != null) group.Styles.Add(ItemContainerStyle);
        return group;
    }

    // A generated group does not exist yet when the collection changes, so it learns it here.
    protected internal override void PrepareContainer(IUIComponent container, object item)
    {
        base.PrepareContainer(container, item);
        if (container is RibbonGroup group) group.ShowSeparator = IndexOf(item) < Items.Count - 1;
    }

    private int IndexOf(object item)
    {
        for (var i = 0; i < Items.Count; i++)
        {
            if (Equals(Items[i], item)) return i;
        }

        return -1;
    }
}
