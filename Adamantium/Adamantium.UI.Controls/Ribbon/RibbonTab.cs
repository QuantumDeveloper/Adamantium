using System;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;
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

    /// <summary>The context this tab belongs to, or null for an ordinary tab. While the group is inactive the tab is
    /// not in the strip at all; while it is active the tab stands with the group's other tabs, under their ledge.
    /// <para>Settable directly (from code, or bound to a view model that owns the contexts). In markup a tab names its
    /// group by <see cref="ContextualGroupKey"/> instead, and the ribbon fills this in.</para></summary>
    public static readonly AdamantiumProperty ContextualGroupProperty = AdamantiumProperty.Register(
        nameof(ContextualGroup), typeof(RibbonContextualGroup), typeof(RibbonTab), new PropertyMetadata(null));

    /// <summary>The <see cref="RibbonContextualGroup.Key"/> of the group in <see cref="Ribbon.ContextualGroups"/> this
    /// tab belongs to. An explicitly set <see cref="ContextualGroup"/> wins over it.</summary>
    public static readonly AdamantiumProperty ContextualGroupKeyProperty = AdamantiumProperty.Register(
        nameof(ContextualGroupKey), typeof(string), typeof(RibbonTab), new PropertyMetadata(null));

    public RibbonContextualGroup ContextualGroup
    {
        get => GetValue<RibbonContextualGroup>(ContextualGroupProperty);
        set => SetValue(ContextualGroupProperty, value);
    }

    public string ContextualGroupKey
    {
        get => GetValue<string>(ContextualGroupKeyProperty);
        set => SetValue(ContextualGroupKeyProperty, value);
    }

    // --- Scrolling the row (§3.4) ------------------------------------------------------------------------------------
    //
    // The last resort, once shrinking and collapsing have run out. The tab owns the CHROME - two repeat buttons over the
    // row's edges and a fade under each - because the panel that does the scrolling lives inside the items presenter,
    // where a template cannot reach it. The arrows OVERLAY the row rather than taking width from it: reserving space
    // for them would make the width depend on the very answer it produces (see RibbonQuickAccessPanel for what that
    // costs), and paying two buttons' width on every tab that never scrolls is worse than drawing over an edge.

    public static readonly AdamantiumProperty CanScrollBackProperty = AdamantiumProperty.Register(nameof(CanScrollBack),
        typeof(bool), typeof(RibbonTab), new PropertyMetadata(false));

    public static readonly AdamantiumProperty CanScrollForwardProperty = AdamantiumProperty.Register(
        nameof(CanScrollForward), typeof(bool), typeof(RibbonTab), new PropertyMetadata(false));

    /// <summary>Whether anything of the row is off its left edge - what the theme shows the back arrow and the fade on.</summary>
    public bool CanScrollBack
    {
        get => GetValue<bool>(CanScrollBackProperty);
        private set => SetValue(CanScrollBackProperty, value);
    }

    public bool CanScrollForward
    {
        get => GetValue<bool>(CanScrollForwardProperty);
        private set => SetValue(CanScrollForwardProperty, value);
    }

    private Panels.RibbonGroupsPanel _row;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        AttachRow();

        if (GetTemplateChild("PART_ScrollBack") is Primitives.ButtonBase back) back.Click += (_, _) => _row?.ScrollBack();
        if (GetTemplateChild("PART_ScrollForward") is Primitives.ButtonBase forward) forward.Click += (_, _) => _row?.ScrollForward();
    }

    // The panel is built by the ItemsPanelTemplate, so it is not a named part - but the items host is exactly what
    // ItemsHostPanel answers. It can be rebuilt under us (the ItemsPanel setter), hence the re-checks below.
    private void AttachRow()
    {
        var row = ItemsHostPanel as Panels.RibbonGroupsPanel;
        if (ReferenceEquals(row, _row)) return;

        if (_row != null) _row.ScrollStateChanged -= OnScrollStateChanged;
        _row = row;
        if (_row != null) _row.ScrollStateChanged += OnScrollStateChanged;
        OnScrollStateChanged(this, EventArgs.Empty);
    }

    private void OnScrollStateChanged(object sender, EventArgs e)
    {
        CanScrollBack = _row?.CanScrollBack == true;
        CanScrollForward = _row?.CanScrollForward == true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // The row is generated with the items, which is later than the template - so look again once we are live.
        AttachRow();
    }

    public RibbonTab()
    {
        Items.CollectionChanged += (_, _) =>
        {
            RefreshSeparators();
            AttachRow();
        };
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
