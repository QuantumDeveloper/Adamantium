using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Generators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
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
    {
        // Existing containers were projected through the old template: drop them and re-realize.
        ((ItemsControl)a)._presenter?.Refresh();
    }

    private static void OnItemsPanelChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        ((ItemsControl)a)._presenter?.Rebuild();
    }

    private void OnItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        _presenter?.OnItemsChanged(e);
    }

    // IContainer: markup children flow into Items.
    void IContainer.AddOrSetChildComponent(object component) => Items.Add(component);

    void IContainer.RemoveAllChildComponents() => Items.Clear();

    IReadOnlyList<object> IContainer.GetChildComponents() => Items.ToList();

    void IContainer.InsertChildComponent(int index, object component) => Items.Insert(index, component);

    void IContainer.RemoveChildComponentAt(int index) => Items.RemoveAt(index);
}
