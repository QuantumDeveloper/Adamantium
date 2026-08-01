using System.Collections;
using System.Collections.Specialized;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Generators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>
/// Presents a collection as a list of generated, templated containers. Items come either from markup
/// (<c>&lt;ItemsControl&gt;&lt;Button/&gt;…</c>, the <see cref="Items"/> content property) or from <see cref="ItemsSource"/>.
/// Each item is projected through <see cref="ItemTemplate"/> into a container (see <see cref="ItemContainerGenerator"/>),
/// laid out by the panel from <see cref="ItemsPanel"/>. The control's template must contain an
/// <see cref="ItemsPresenter"/> named <c>PART_ItemsPresenter</c>.
/// </summary>
public class ItemsControl : Control, IContainer
{
    private ItemsPresenter _presenter;

    public static readonly AdamantiumProperty ItemsSourceProperty = AdamantiumProperty.Register(nameof(ItemsSource),
        typeof(IEnumerable), typeof(ItemsControl), new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly AdamantiumProperty ItemTemplateProperty = AdamantiumProperty.Register(nameof(ItemTemplate),
        typeof(DataTemplate), typeof(ItemsControl), new PropertyMetadata(null, OnItemTemplateChanged));

    public static readonly AdamantiumProperty ItemTemplateSelectorProperty = AdamantiumProperty.Register(nameof(ItemTemplateSelector),
        typeof(DataTemplateSelector), typeof(ItemsControl), new PropertyMetadata(null, OnRegenerateContainers));

    /// <summary>Visual shown in a virtualized slot whose real item hasn't been (re)bound yet this frame (a fast fling or a
    /// big fill exceeds the per-frame bind budget). Null = the panel's built-in themed skeleton (a muted rounded tile).</summary>
    public static readonly AdamantiumProperty ItemSkeletonTemplateProperty = AdamantiumProperty.Register(nameof(ItemSkeletonTemplate),
        typeof(DataTemplate), typeof(ItemsControl), new PropertyMetadata(null));

    /// <summary>True while the items host is showing loading SKELETON cards - i.e. a virtualized fill (or a fast fling) has
    /// deferred part of the realized window past the per-frame bind budget. Maintained by the panel
    /// (<see cref="Panels.VirtualizingPanel"/>); read-only to markup. This is the LIST-level loading state a theme keys the
    /// skeleton shimmer off: one trigger per list runs/stops the pulse on the shared skeleton brush, so a screenful of
    /// cards costs ONE animation instead of one per card.</summary>
    public static readonly AdamantiumProperty IsLoadingItemsProperty = AdamantiumProperty.RegisterReadOnly(nameof(IsLoadingItems),
        typeof(bool), typeof(ItemsControl), new PropertyMetadata(false));

    public static readonly AdamantiumProperty ItemContainerStyleProperty = AdamantiumProperty.Register(nameof(ItemContainerStyle),
        typeof(Style), typeof(ItemsControl), new PropertyMetadata(null, OnRegenerateContainers));

    public static readonly AdamantiumProperty ItemsPanelProperty = AdamantiumProperty.Register(nameof(ItemsPanel),
        typeof(ItemsPanelTemplate), typeof(ItemsControl), new PropertyMetadata(null, OnItemsPanelChanged));

    public ItemsControl()
    {
        Items = new ItemCollection();
        Items.CollectionChanged += OnItemsCollectionChanged;
        ItemContainerGenerator = new ItemContainerGenerator(this);
    }

    /// <summary>The effective item list — markup-authored items, or a view over <see cref="ItemsSource"/>.</summary>
    [Content]
    public ItemCollection Items { get; }

    public ItemContainerGenerator ItemContainerGenerator { get; }

    /// <summary>The realized items host panel (from the template's ItemsPresenter), or null before the template is applied.</summary>
    /// <summary>The panel that actually lays the item containers out, or null before the template is applied. Public
    /// because anything positioning against the items - a drag deciding where a drop lands, a dock target offering a
    /// slot - has to ask it, and while it was internal each of them reached it by walking the visual tree instead.</summary>
    /// <para>Virtual because a control may lay its items out in MORE THAN ONE list - a tab strip keeps pinned tabs in a
    /// row of their own - and then "the panel the items are in" is a question only that control can answer.</para>
    public virtual Panel ItemsHostPanel => _presenter?.Panel;

    public IEnumerable ItemsSource
    {
        get => GetValue<IEnumerable>(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public DataTemplate ItemTemplate
    {
        get => GetValue<DataTemplate>(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <summary>The card shown in the hole a drag opens at its insertion point - "what you are holding lands here".
    /// A DIFFERENT template from <see cref="ItemSkeletonTemplateProperty"/> on purpose: a loading skeleton says content
    /// is on its way, and dressing the two the same would merge two different messages. It also stays out of
    /// <see cref="IsLoadingItemsProperty"/>, so a drag never pulses the list as if it were fetching something.</summary>
    public static readonly AdamantiumProperty DropPlaceholderTemplateProperty = AdamantiumProperty.Register(
        nameof(DropPlaceholderTemplate), typeof(DataTemplate), typeof(ItemsControl), new PropertyMetadata(null));

    public DataTemplate DropPlaceholderTemplate
    {
        get => GetValue<DataTemplate>(DropPlaceholderTemplateProperty);
        set => SetValue(DropPlaceholderTemplateProperty, value);
    }

    /// <summary>Placeholder template for a not-yet-bound virtualized slot (see <see cref="ItemSkeletonTemplateProperty"/>).</summary>
    public DataTemplate ItemSkeletonTemplate
    {
        get => GetValue<DataTemplate>(ItemSkeletonTemplateProperty);
        set => SetValue(ItemSkeletonTemplateProperty, value);
    }

    /// <summary>Whether loading skeleton cards are on screen right now (see <see cref="IsLoadingItemsProperty"/>).</summary>
    public bool IsLoadingItems
    {
        get => GetValue<bool>(IsLoadingItemsProperty);
        internal set => SetValue(IsLoadingItemsProperty, value);
    }

    /// <summary>Picks the <see cref="DataTemplate"/> per item (when no <see cref="ItemTemplate"/> is set).</summary>
    public DataTemplateSelector ItemTemplateSelector
    {
        get => GetValue<DataTemplateSelector>(ItemTemplateSelectorProperty);
        set => SetValue(ItemTemplateSelectorProperty, value);
    }

    /// <summary>Style applied to every generated container (its real value is selection/state styling on the
    /// dedicated container types - ListBoxItem etc.; on the base it styles the ContentPresenter).</summary>
    public Style ItemContainerStyle
    {
        get => GetValue<Style>(ItemContainerStyleProperty);
        set => SetValue(ItemContainerStyleProperty, value);
    }

    public ItemsPanelTemplate ItemsPanel
    {
        get => GetValue<ItemsPanelTemplate>(ItemsPanelProperty);
        set => SetValue(ItemsPanelProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _presenter = GetTemplateChild("PART_ItemsPresenter") as ItemsPresenter;
        _presenter?.Connect(this);
    }

    /// <summary>Connect an items host that arrives AFTER OnApplyTemplate - e.g. a MenuItem whose PART_ItemsPresenter lives in
    /// its submenu Popup's lazily-built <see cref="Popup.ChildTemplate"/>, so GetTemplateChild couldn't find it up front.</summary>
    protected void ConnectPresenter(ItemsPresenter presenter)
    {
        _presenter = presenter;
        _presenter?.Connect(this);
    }

    private static void OnItemsSourceChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        ((ItemsControl)a).ApplyItemsSource(e.NewValue as IEnumerable);
    }

    /// <summary>Applies a new ItemsSource to the effective item list. Base points <see cref="Items"/> straight at the
    /// source; overridden where the source is projected into a different backing list (TreeView flattens a tree into its
    /// rows and points Items at THOSE).</summary>
    protected virtual void ApplyItemsSource(IEnumerable newValue) => Items.SetSource(newValue);

    private static void OnItemTemplateChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        ((ItemsControl)a).OnItemTemplateChangedCore();
        OnRegenerateContainers(a, e);
    }

    /// <summary>Called when ItemTemplate changes, before containers regenerate. Overridden where the template drives the
    /// backing list (TreeView resolves each node's children from the HierarchicalDataTemplate's ItemsSource path).</summary>
    protected virtual void OnItemTemplateChangedCore() { }

    // ItemTemplate / ItemTemplateSelector / ItemContainerStyle changed: existing containers were projected through the
    // old settings, so drop them and re-generate.
    private static void OnRegenerateContainers(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        ((ItemsControl)a)._presenter?.RegenerateContainers();
    }

    private static void OnItemsPanelChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        // Rebuilding throws the live items panel away and builds an identical replacement - the containers follow the
        // new one while the visual tree carries on arranging the old. So only do it when the template ACTUALLY changed:
        // re-applying a style re-assigns this property, and the same template arriving twice must not cost a panel.
        if (ReferenceEquals(e.OldValue, e.NewValue)) return;

        ((ItemsControl)a)._presenter?.Rebuild();
    }

    private void OnItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        _presenter?.OnItemsChanged(e);
    }

    // --- Container seam (override in selectable controls, e.g. ListBox -> ListBoxItem) -----------------------------

    /// <summary>True if the item is already a ready-to-host UI element (used as its own container, no wrapping).</summary>
    protected internal virtual bool IsItemItsOwnContainer(object item) => item is IUIComponent;

    /// <summary>Creates the container for <paramref name="item"/>. Base = a <see cref="ContentPresenter"/> carrying the
    /// ItemContainerStyle (the item lets a subclass pick a different container - e.g. a Separator for an ISeparatorItem).</summary>
    protected internal virtual IUIComponent GetContainerForItem(object item)
    {
        var presenter = new ContentPresenter();
        // Into the Styles collection (a user style applied AFTER the theme), not AttachStyles which runs before the
        // container is themed and would be overwritten by it - see ListBox.GetContainerForItem.
        if (ItemContainerStyle != null) presenter.Styles.Add(ItemContainerStyle);
        return presenter;
    }

    /// <summary>Binds a (new or recycled) container to <paramref name="item"/>: DataContext + content + item template.</summary>
    protected internal virtual void PrepareContainer(IUIComponent container, object item)
    {
        // A HierarchicalDataTemplate + a headered container (MenuItem / TreeViewItem) = one tree level: draw the item as
        // the header via the HDT, bind this container's OWN items to the node's children, and re-apply the SAME template to
        // them so the tree unrolls recursively. Set the child template + style BEFORE ItemsSource (setting the source is
        // what triggers child-container generation). See HierarchicalDataTemplate.
        if (ItemTemplate is HierarchicalDataTemplate hdt && container is IHeaderedItemsControl headered && container is ItemsControl childItems)
        {
            childItems.DataContext = item;
            headered.Header = item;
            headered.HeaderTemplate = hdt;
            childItems.ItemContainerStyle = hdt.ItemContainerStyle ?? ItemContainerStyle;
            childItems.ItemTemplate = hdt.ItemTemplate ?? hdt;
            if (hdt.ItemsSource != null) childItems.SetBinding(ItemsSourceProperty, (BindingBase)hdt.ItemsSource.Clone());
            return;
        }

        if (container is ContentPresenter presenter)
        {
            presenter.DataContext = item;
            presenter.ContentTemplate = ItemTemplate;
            presenter.ContentTemplateSelector = ItemTemplateSelector;
            presenter.Content = item;
        }
    }

    /// <summary>Releases a container's item so it can be recycled. Crucially does NOT clear Content - that would tear
    /// down the item template's visual (and its GPU buffers); the container keeps its visual and is rebound to a new
    /// item (its DataContext) on reuse, so the buffers are reused, not recreated every scroll frame.</summary>
    protected internal virtual void ClearContainer(IUIComponent container)
    {
        if (container is ContentPresenter presenter)
            presenter.DataContext = null;
        else if (container is IHeaderedItemsControl and ItemsControl headered)
            headered.DataContext = null;
    }

    // IContainer: markup children flow into Items.
    void IContainer.AddOrSetChildComponent(object component) => Items.Add(component);

    void IContainer.RemoveAllChildComponents() => Items.Clear();

    IReadOnlyList<object> IContainer.GetChildComponents() => Items.ToList();

    void IContainer.InsertChildComponent(int index, object component) => Items.Insert(index, component);

    void IContainer.RemoveChildComponentAt(int index) => Items.RemoveAt(index);
}
