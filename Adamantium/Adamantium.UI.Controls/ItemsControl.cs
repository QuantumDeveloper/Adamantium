using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Generators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
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
    internal Panel ItemsHostPanel => _presenter?.Panel;

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

    /// <summary>Placeholder template for a not-yet-bound virtualized slot (see <see cref="ItemSkeletonTemplateProperty"/>).</summary>
    public DataTemplate ItemSkeletonTemplate
    {
        get => GetValue<DataTemplate>(ItemSkeletonTemplateProperty);
        set => SetValue(ItemSkeletonTemplateProperty, value);
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

    private static void OnItemsSourceChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        ((ItemsControl)a).Items.SetSource(e.NewValue as IEnumerable);
    }

    private static void OnItemTemplateChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
        => OnRegenerateContainers(a, e);

    // ItemTemplate / ItemTemplateSelector / ItemContainerStyle changed: existing containers were projected through the
    // old settings, so drop them and re-generate.
    private static void OnRegenerateContainers(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        ((ItemsControl)a)._presenter?.RegenerateContainers();
    }

    private static void OnItemsPanelChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        ((ItemsControl)a)._presenter?.Rebuild();
    }

    private void OnItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        _presenter?.OnItemsChanged(e);
    }

    // --- Container seam (override in selectable controls, e.g. ListBox -> ListBoxItem) -----------------------------

    /// <summary>True if the item is already a ready-to-host UI element (used as its own container, no wrapping).</summary>
    protected internal virtual bool IsItemItsOwnContainer(object item) => item is IUIComponent;

    /// <summary>Creates the container for one item. Base = a <see cref="ContentPresenter"/> carrying the ItemContainerStyle.</summary>
    protected internal virtual IUIComponent GetContainerForItem()
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
    }

    // IContainer: markup children flow into Items.
    void IContainer.AddOrSetChildComponent(object component) => Items.Add(component);

    void IContainer.RemoveAllChildComponents() => Items.Clear();

    IReadOnlyList<object> IContainer.GetChildComponents() => Items.ToList();

    void IContainer.InsertChildComponent(int index, object component) => Items.Insert(index, component);

    void IContainer.RemoveChildComponentAt(int index) => Items.RemoveAt(index);
}
