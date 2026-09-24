using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Specialized;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Animation;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>
/// A single-select <see cref="Selector"/> of tabs: headers in the strip, the selected tab's body in
/// <see cref="SelectedContent"/>. Tabs are authored as TabItems or bound through ItemsSource.
/// </summary>
public class TabControl : Selector
{
    public static readonly AdamantiumProperty SelectedContentProperty = AdamantiumProperty.Register(nameof(SelectedContent),
        typeof(object), typeof(TabControl), new PropertyMetadata(null));

    public static readonly AdamantiumProperty ContentTemplateProperty = AdamantiumProperty.Register(nameof(ContentTemplate),
        typeof(DataTemplate), typeof(TabControl), new PropertyMetadata(null));

    public static readonly AdamantiumProperty ContentTemplateSelectorProperty = AdamantiumProperty.Register(nameof(ContentTemplateSelector),
        typeof(DataTemplateSelector), typeof(TabControl), new PropertyMetadata(null));

    public static readonly AdamantiumProperty SelectedContentTemplateProperty = AdamantiumProperty.Register(
        nameof(SelectedContentTemplate), typeof(DataTemplate), typeof(TabControl), new PropertyMetadata(null));

    public static readonly AdamantiumProperty SelectedContentTemplateSelectorProperty = AdamantiumProperty.Register(
        nameof(SelectedContentTemplateSelector), typeof(DataTemplateSelector), typeof(TabControl), new PropertyMetadata(null));

    public static readonly AdamantiumProperty ContentTransitionProperty = AdamantiumProperty.Register(nameof(ContentTransition),
        typeof(ContentTransition), typeof(TabControl), new PropertyMetadata(ContentTransition.None));

    public static readonly AdamantiumProperty ContentTransitionDurationProperty = AdamantiumProperty.Register(nameof(ContentTransitionDuration),
        typeof(double), typeof(TabControl), new PropertyMetadata(0.25));

    /// <summary>Builds a selected tab's body off the loop thread, showing <see cref="LoadingTemplate"/> meanwhile. See
    /// <see cref="ContentPresenter.DeferContent"/>.</summary>
    public static readonly AdamantiumProperty DeferContentProperty = AdamantiumProperty.Register(nameof(DeferContent),
        typeof(bool), typeof(TabControl), new PropertyMetadata(false));

    /// <summary>What stands in the body's place while it is being built.</summary>
    public static readonly AdamantiumProperty LoadingTemplateProperty = AdamantiumProperty.Register(nameof(LoadingTemplate),
        typeof(DataTemplate), typeof(TabControl), new PropertyMetadata(null));

    /// <summary>Which edge the tab strip sits on. Each value selects its own control template through a theme trigger.</summary>
    public static readonly AdamantiumProperty TabStripPlacementProperty = AdamantiumProperty.Register(nameof(TabStripPlacement),
        typeof(TabStripPlacement), typeof(TabControl), new PropertyMetadata(TabStripPlacement.Top));

    /// <summary>Whether the tab strip is drawn at all: a lone tab in a window of its own is named by the title bar instead.</summary>
    public static readonly AdamantiumProperty ShowTabStripProperty = AdamantiumProperty.Register(
        nameof(ShowTabStrip), typeof(bool), typeof(TabControl),
        new PropertyMetadata(true, PropertyMetadataOptions.AffectsMeasure));

    public bool ShowTabStrip
    {
        get => GetValue<bool>(ShowTabStripProperty);
        set => SetValue(ShowTabStripProperty, value);
    }

    /// <summary>Whether a lone tab fills the whole strip instead of its own width. Off by default; the document area
    /// turns it on.</summary>
    public static readonly AdamantiumProperty StretchSingleTabProperty = AdamantiumProperty.Register(
        nameof(StretchSingleTab), typeof(bool), typeof(TabControl),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsArrange, OnStretchSingleTabChanged));

    private static void OnStretchSingleTabChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not TabControl control) return;

        // The panel lays the tabs out, not this control.
        control.ItemsHostPanel?.InvalidateArrange();
        control.SyncStretchedTab();
    }

    public static readonly AdamantiumProperty ReorderAnimationDurationProperty = AdamantiumProperty.Register(
        nameof(ReorderAnimationDuration), typeof(TimeSpan), typeof(TabControl),
        new PropertyMetadata(TimeSpan.FromMilliseconds(180)));

    public static readonly AdamantiumProperty ReorderEasingProperty = AdamantiumProperty.Register(
        nameof(ReorderEasing), typeof(IEasingFunction), typeof(TabControl), new PropertyMetadata(null));

    public static readonly AdamantiumProperty SelectionIndicatorBrushProperty = AdamantiumProperty.Register(
        nameof(SelectionIndicatorBrush), typeof(Brush), typeof(TabControl),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty SelectionIndicatorThicknessProperty = AdamantiumProperty.Register(
        nameof(SelectionIndicatorThickness), typeof(double), typeof(TabControl), new PropertyMetadata(3.0));

    public static readonly AdamantiumProperty SelectionIndicatorPlacementProperty = AdamantiumProperty.Register(
        nameof(SelectionIndicatorPlacement), typeof(TabIndicatorPlacement), typeof(TabControl),
        new PropertyMetadata(TabIndicatorPlacement.Inner));

    public static readonly AdamantiumProperty SelectionAnimationDurationProperty = AdamantiumProperty.Register(
        nameof(SelectionAnimationDuration), typeof(TimeSpan), typeof(TabControl),
        new PropertyMetadata(TimeSpan.FromMilliseconds(250)));

    /// <summary>Accent brush of the sliding selection indicator.</summary>
    public Brush SelectionIndicatorBrush
    {
        get => GetValue<Brush>(SelectionIndicatorBrushProperty);
        set => SetValue(SelectionIndicatorBrushProperty, value);
    }

    /// <summary>Thickness (height for a top/bottom strip, width for a left/right strip) of the selection indicator bar.</summary>
    public double SelectionIndicatorThickness
    {
        get => GetValue<double>(SelectionIndicatorThicknessProperty);
        set => SetValue(SelectionIndicatorThicknessProperty, value);
    }

    /// <summary>Which side of the strip the indicator runs along: beside the content (default) or the strip's outer edge.</summary>
    public TabIndicatorPlacement SelectionIndicatorPlacement
    {
        get => GetValue<TabIndicatorPlacement>(SelectionIndicatorPlacementProperty);
        set => SetValue(SelectionIndicatorPlacementProperty, value);
    }

    /// <summary>How long the indicator takes to slide/resize to a newly selected tab. Theme-settable.</summary>
    public TimeSpan SelectionAnimationDuration
    {
        get => GetValue<TimeSpan>(SelectionAnimationDurationProperty);
        set => SetValue(SelectionAnimationDurationProperty, value);
    }

    public bool StretchSingleTab
    {
        get => GetValue<bool>(StretchSingleTabProperty);
        set => SetValue(StretchSingleTabProperty, value);
    }

    public TabControl()
    {
        SelectionChanged += (_, _) =>
        {
            UpdateSelectedContent();
            // Pending: a view-model can select before the container exists. A collection-driven reselection snaps from
            // PlaceIndicator instead of sliding, since the strip is about to reflow.
            _scrollPending = true;

            if (!_reselecting) UpdateIndicator(animate: true);
            if (_overflow?.IsChecked == true) _overflow.IsChecked = false;
        };
        Items.CollectionChanged += OnItemsChanged;

        PinnedItems = [];
        UnpinnedItems = [];
        Items.CollectionChanged += (_, _) => WatchPinning();
    }

    // Properties rather than CLR ones: {TemplateBinding} resolves only an AdamantiumProperty.
    public static readonly AdamantiumProperty PinnedItemsProperty = AdamantiumProperty.RegisterReadOnly(
        nameof(PinnedItems), typeof(ObservableCollection<object>), typeof(TabControl), new PropertyMetadata(null));

    public static readonly AdamantiumProperty UnpinnedItemsProperty = AdamantiumProperty.RegisterReadOnly(
        nameof(UnpinnedItems), typeof(ObservableCollection<object>), typeof(TabControl), new PropertyMetadata(null));

    /// <summary>The pinned tabs, in the order they were added. Only an authored TabItem can be pinned: a data item has
    /// no IsPinned to read.</summary>
    public ObservableCollection<object> PinnedItems
    {
        get => GetValue<ObservableCollection<object>>(PinnedItemsProperty);
        private set => SetValue(PinnedItemsProperty, value);
    }

    /// <summary>Everything else - the tabs that come and go.</summary>
    public ObservableCollection<object> UnpinnedItems
    {
        get => GetValue<ObservableCollection<object>>(UnpinnedItemsProperty);
        private set => SetValue(UnpinnedItemsProperty, value);
    }

    /// <summary>Whether tabs carry a pin button. Off by default.</summary>
    public static readonly AdamantiumProperty ShowPinButtonProperty = AdamantiumProperty.Register(
        nameof(ShowPinButton), typeof(bool), typeof(TabControl), new PropertyMetadata(false));

    /// <summary>The look of the pin button, a standalone template like <see cref="CloseButtonTemplate"/>.</summary>
    public static readonly AdamantiumProperty PinButtonTemplateProperty = AdamantiumProperty.Register(
        nameof(PinButtonTemplate), typeof(ControlTemplate), typeof(TabControl), new PropertyMetadata(null));

    public bool ShowPinButton
    {
        get => GetValue<bool>(ShowPinButtonProperty);
        set => SetValue(ShowPinButtonProperty, value);
    }

    public ControlTemplate PinButtonTemplate
    {
        get => GetValue<ControlTemplate>(PinButtonTemplateProperty);
        set => SetValue(PinButtonTemplateProperty, value);
    }

    public static readonly AdamantiumProperty PinnedTabsPlacementProperty = AdamantiumProperty.Register(
        nameof(PinnedTabsPlacement), typeof(PinnedTabsPlacement), typeof(TabControl),
        new PropertyMetadata(Controls.PinnedTabsPlacement.SeparateRow));

    /// <summary>Whether pinned tabs get a row of their own (default) or share the one row with the others.</summary>
    public PinnedTabsPlacement PinnedTabsPlacement
    {
        get => GetValue<PinnedTabsPlacement>(PinnedTabsPlacementProperty);
        set => SetValue(PinnedTabsPlacementProperty, value);
    }

    // Watched from here: an item is in Items long before its tab is in any tree.
    private readonly HashSet<TabItem> _watchedForPinning = [];

    private void WatchPinning()
    {
        foreach (var tab in _watchedForPinning) tab.PropertyChanged -= OnTabPinningChanged;
        _watchedForPinning.Clear();

        foreach (var item in Items)
        {
            if (item is not TabItem tab || !_watchedForPinning.Add(tab)) continue;

            tab.PropertyChanged += OnTabPinningChanged;
        }

        SplitByPinned();
    }

    private void OnTabPinningChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.Property != TabItem.IsPinnedProperty) return;

        SplitByPinned();
        if (sender is TabItem tab) AnimateRowChange(tab);
    }

    internal void SplitByPinned()
    {
        PinnedItems.Clear();
        UnpinnedItems.Clear();
        _unpinnedAll.Clear();

        foreach (var item in Items)
        {
            if (item is TabItem { IsPinned: true }) PinnedItems.Add(item);
            else _unpinnedAll.Add(item);
        }

        foreach (var item in _unpinnedAll) UnpinnedItems.Add(item);

        HasPinnedTabs = PinnedItems.Count > 0;
        HasUnpinnedTabs = UnpinnedItems.Count > 0;
    }

    private readonly List<object> _unpinnedAll = new();

    // Oldest first; selection does not touch it.
    private readonly List<object> _openOrder = new();
    private readonly HashSet<object> _openOrderSet = new();
    private bool _evicting;

    // Reconciled rather than journalled: a bound collection reports a Reset that names nothing.
    private void SyncOpenOrder()
    {
        var live = new HashSet<object>(Items);
        if (_openOrder.RemoveAll(i => !live.Contains(i)) > 0)
        {
            _openOrderSet.Clear();
            foreach (var known in _openOrder) _openOrderSet.Add(known);
        }

        foreach (var item in Items)
            if (_openOrderSet.Add(item)) _openOrder.Add(item);
    }

    private void EnforceTabLimit()
    {
        var max = MaxOpenedTabs;
        if (max <= 0 || _evicting) return;

        _evicting = true;
        try
        {
            while (Items.Count(i => i is not TabItem { IsPinned: true }) > max)
            {
                var victim = _openOrder.FirstOrDefault(i => IndexOfItem(i) >= 0
                                                            && i is not TabItem { IsPinned: true }
                                                            && !ReferenceEquals(i, SelectedItem));
                if (victim == null) break;

                RequestCloseItem(victim);
                if (IndexOfItem(victim) >= 0) break;   // vetoed: do not spin on it
            }
        }
        finally { _evicting = false; }
    }


    /// <summary>The most tabs open at once; past it the oldest-opened is closed. Zero (default) switches it off; pinned
    /// and selected tabs are never closed.</summary>
    public static readonly AdamantiumProperty MaxOpenedTabsProperty = AdamantiumProperty.Register(
        nameof(MaxOpenedTabs), typeof(Int32), typeof(TabControl),
        new PropertyMetadata(0, PropertyMetadataOptions.AffectsMeasure, OnMaxOpenedTabsChanged));

    public Int32 MaxOpenedTabs
    {
        get => GetValue<Int32>(MaxOpenedTabsProperty);
        set => SetValue(MaxOpenedTabsProperty, value);
    }

    private static void OnMaxOpenedTabsChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (d is not TabControl tabs) return;
        tabs.SyncOpenOrder();
        tabs.EnforceTabLimit();
    }

    public static readonly AdamantiumProperty HasUnpinnedTabsProperty = AdamantiumProperty.RegisterReadOnly(
        nameof(HasUnpinnedTabs), typeof(bool), typeof(TabControl), new PropertyMetadata(false));

    /// <summary>Whether anything is unpinned; with every tab pinned the ordinary row collapses.</summary>
    public bool HasUnpinnedTabs
    {
        get => GetValue<bool>(HasUnpinnedTabsProperty);
        private set => SetValue(HasUnpinnedTabsProperty, value);
    }

    // A fade, not a slide: the strip is reflowing underneath and a slide would fight the layout.
    private void AnimateRowChange(TabItem tab)
    {
        tab.Opacity = 0;
        tab.BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            From = 0, To = 1, Duration = ReorderAnimationDuration, Easing = ReorderEasing ?? DefaultReorderEasing
        });
    }

    public static readonly AdamantiumProperty HasPinnedTabsProperty = AdamantiumProperty.RegisterReadOnly(
        nameof(HasPinnedTabs), typeof(bool), typeof(TabControl), new PropertyMetadata(false));

    /// <summary>Whether anything is pinned - what the theme shows the pinned row on.</summary>
    public bool HasPinnedTabs
    {
        get => GetValue<bool>(HasPinnedTabsProperty);
        private set => SetValue(HasPinnedTabsProperty, value);
    }

    /// <summary>The selected tab's body, derived from the selection.</summary>
    public object SelectedContent
    {
        get => GetValue(SelectedContentProperty);
        private set => SetValue(SelectedContentProperty, value);
    }

    /// <summary>Template for the selected tab's body when items are data. <see cref="ContentTemplateSelector"/> varies it
    /// per item type.</summary>
    public DataTemplate ContentTemplate
    {
        get => GetValue<DataTemplate>(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    /// <summary>Picks the body template per selected item (its view-model type), e.g. one View per tab view-model.</summary>
    public DataTemplateSelector ContentTemplateSelector
    {
        get => GetValue<DataTemplateSelector>(ContentTemplateSelectorProperty);
        set => SetValue(ContentTemplateSelectorProperty, value);
    }

    /// <summary>The template <see cref="SelectedContent"/> is rendered with: the selected tab's own, else
    /// <see cref="ContentTemplate"/>.</summary>
    public DataTemplate SelectedContentTemplate
    {
        get => GetValue<DataTemplate>(SelectedContentTemplateProperty);
        private set => SetValue(SelectedContentTemplateProperty, value);
    }

    /// <summary>The selector <see cref="SelectedContent"/> is rendered through: the selected tab's own, else this control's.</summary>
    public DataTemplateSelector SelectedContentTemplateSelector
    {
        get => GetValue<DataTemplateSelector>(SelectedContentTemplateSelectorProperty);
        private set => SetValue(SelectedContentTemplateSelectorProperty, value);
    }

    /// <summary>How the selected tab's body animates on selection change (default None). E.g. SlideLeft/SlideRight.</summary>
    public ContentTransition ContentTransition
    {
        get => GetValue<ContentTransition>(ContentTransitionProperty);
        set => SetValue(ContentTransitionProperty, value);
    }

    public bool DeferContent
    {
        get => GetValue<bool>(DeferContentProperty);
        set => SetValue(DeferContentProperty, value);
    }

    public DataTemplate LoadingTemplate
    {
        get => GetValue<DataTemplate>(LoadingTemplateProperty);
        set => SetValue(LoadingTemplateProperty, value);
    }

    /// <summary>Duration (seconds) of the tab-content transition.</summary>
    public double ContentTransitionDuration
    {
        get => GetValue<double>(ContentTransitionDurationProperty);
        set => SetValue(ContentTransitionDurationProperty, value);
    }

    public TabStripPlacement TabStripPlacement
    {
        get => GetValue<TabStripPlacement>(TabStripPlacementProperty);
        set => SetValue(TabStripPlacementProperty, value);
    }

    /// <summary>How long a drag-reorder slide/settle takes. Theme-settable.</summary>
    public TimeSpan ReorderAnimationDuration
    {
        get => GetValue<TimeSpan>(ReorderAnimationDurationProperty);
        set => SetValue(ReorderAnimationDurationProperty, value);
    }

    /// <summary>Easing for the drag-reorder animations; null uses a decelerate cubic. Theme-settable.</summary>
    public IEasingFunction ReorderEasing
    {
        get => GetValue<IEasingFunction>(ReorderEasingProperty);
        set => SetValue(ReorderEasingProperty, value);
    }

    // Set while MoveItem removes and re-inserts the same item: it restores the selection itself.
    private bool _reordering;

    private void OnItemsChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        SyncOpenOrder();

        if (_reordering) return;

        ParkedVisuals.ReleaseAbsent(this, Items.Contains);

        // Re-run the selection, not just clamp the index: closing the selected tab leaves the index valid but on another
        // item. A tab named before the strip had any wins over the first one.
        var pending = TakePendingSelectionIndex();

        var index = Items.Count == 0 ? -1
            : pending >= 0 ? pending
            : SelectedIndex < 0 ? (RequiresSelection ? 0 : -1)
            : SelectedIndex >= Items.Count ? Items.Count - 1
            : SelectedIndex;
        _reselecting = true;
        SelectSingle(index);
        _reselecting = false;
        UpdateSelectedContent();
        SyncStretchedTab();

        // Last: the cap must know which tab is being read.
        EnforceTabLimit();
    }

    /// <summary>True while one tab fills the whole strip (see <see cref="StretchSingleTab"/>); it wears the accent itself
    /// and the indicator hides.</summary>
    public static readonly AdamantiumProperty HasStretchedTabProperty = AdamantiumProperty.Register(
        nameof(HasStretchedTab), typeof(bool), typeof(TabControl), new PropertyMetadata(false));

    public bool HasStretchedTab
    {
        get => GetValue<bool>(HasStretchedTabProperty);
        private set => SetValue(HasStretchedTabProperty, value);
    }

    // Told to the containers too: the tab's own template paints the accent.
    private void SyncStretchedTab()
    {
        // The base constructor fires this before ItemsControl has made the collection.
        if (Items == null) return;

        var stretched = StretchSingleTab && Items.Count == 1;
        HasStretchedTab = stretched;

        for (var i = 0; i < Items.Count; i++)
        {
            if (ContainerOfTab(i) is TabItem tab) tab.IsStretched = stretched;
        }
    }

    private void UpdateSelectedContent()
    {
        var item = SelectedItem;
        var tab = item as TabItem;
        SelectedContent = tab != null ? tab.Content : item;

        SelectedContentTemplate = tab?.ContentTemplate ?? ContentTemplate;
        SelectedContentTemplateSelector = tab?.ContentTemplateSelector ?? ContentTemplateSelector;
    }

    internal void SelectTab(TabItem container)
    {
        var index = IndexOfTab(container);
        if (index >= 0) SelectedIndex = index;
    }

    private IUIComponent _contentHost;
    private TabItem _enterFrom;
    private IUIComponent _enterReplacing;
    private int _enterTries;

    internal void EnterTab(TabItem container)
    {
        // The page on screen now: stepping in must wait until the swap replaces it.
        _enterReplacing = IsContainerSelected(container) ? null : FirstPage();
        SelectTab(container);
        _enterFrom = container;
        _enterTries = 120;   // settles, not frames
        EnterSelectedContent();
    }

    private IUIComponent FirstPage()
    {
        if (_contentHost == null)
            return null;

        foreach (var child in _contentHost.VisualChildren)
            return child;

        return null;
    }

    private void EnterSelectedContent()
    {
        if (_enterFrom == null)
            return;

        // Focus moved on: the step in was for whoever pressed Enter.
        if (!ReferenceEquals(FocusManager.Focused, _enterFrom) || --_enterTries < 0)
        {
            _enterFrom = null;
            return;
        }

        var page = FirstPage();
        if (page == null || ReferenceEquals(page, _enterReplacing))
            return;

        if (KeyboardNavigation.MoveInto(_contentHost))
            _enterFrom = null;
    }

    /// <summary>Both rows: a strip's containers are realized by the two lists in its template, not by its own generator.</summary>
    protected override IEnumerable<(IUIComponent Container, object Item)> RealizedContainers()
    {
        foreach (var pair in base.RealizedContainers()) yield return pair;

        foreach (var host in (ItemsControl[])[_tabsHost, _pinnedHost])
        {
            if (host?.ItemContainerGenerator is not { } generator) continue;

            foreach (var index in generator.RealizedIndices.ToList())
            {
                if (index < 0 || index >= host.Items.Count) continue;

                if (generator.ContainerFromIndex(index) is { } container) yield return (container, host.Items[index]);
            }
        }
    }

    internal IUIComponent ContainerOfTab(int index)
    {
        if (index < 0 || index >= Items.Count) return null;
        if (ItemContainerGenerator.ContainerFromIndex(index) is { } own) return own;

        var item = Items[index];
        foreach (var host in (ItemsControl[])[_tabsHost, _pinnedHost])
        {
            var local = host?.Items.IndexOf(item) ?? -1;
            if (local < 0) continue;

            if (host.ItemContainerGenerator.ContainerFromIndex(local) is { } realized) return realized;
        }

        return item as IUIComponent;
    }

    internal int IndexOfTab(IUIComponent container)
    {
        var index = ItemContainerGenerator.IndexFromContainer(container);
        if (index >= 0) return index;

        foreach (var host in (ItemsControl[])[_tabsHost, _pinnedHost])
        {
            var local = host?.ItemContainerGenerator.IndexFromContainer(container) ?? -1;
            if (local < 0 || local >= host.Items.Count) continue;

            var owner = Items.IndexOf(host.Items[local]);
            if (owner >= 0) return owner;
        }

        return Items.IndexOf(container);
    }

    // Drag reorder: Items is untouched during the drag; tabs move by RenderTransform and the reorder is committed once,
    // on release.

    private TabItem _dragged;
    private bool _dragVertical;
    private double _grabOffset;
    private double _draggedExtent;
    private int _dragStartIndex;
    private int _targetIndex;

    private static readonly IEasingFunction DefaultReorderEasing = new CubicEasing { Mode = EasingMode.Out };

    // Asked of the panel, not TabStripPlacement: a folded tool group keeps its placement and turns its panel.
    private bool StripIsVertical => ItemsHostPanel is TabPanel panel
        ? panel.Orientation == Orientation.Vertical
        : TabStripPlacement is TabStripPlacement.Left or TabStripPlacement.Right;

    internal void BeginDrag(TabItem tab, MouseEventArgs e)
    {
        if (ItemsHostPanel is not { } panel) return;
        var pos = e.GetPosition(panel);
        BeginDrag(tab, StripIsVertical ? pos.Y : pos.X);
    }

    private bool _draggedPinned;

    // Only tabs in the dragged tab's row: the pinned and ordinary rows share one index range.
    private TabItem Reorders(int index) =>
        ContainerOfTab(index) is TabItem tab && tab.IsPinned == _draggedPinned ? tab : null;

    internal void BeginDrag(TabItem tab, double along)
    {
        _dragVertical = StripIsVertical;
        IsTearingOff = false;   // per gesture: left set, it disables every later tear-off
        _dragged = tab;
        _draggedPinned = tab.IsPinned;
        _dragStartIndex = _targetIndex = IndexOfTab(tab);
        _grabOffset = along - SlotStart(tab);
        _draggedExtent = Extent(tab);
        tab.ZIndex = 1;
        SetOffset(tab, 0);
    }

    internal void UpdateDrag(TabItem tab, MouseEventArgs e)
    {
        if (ItemsHostPanel is not { } panel) return;
        var pos = e.GetPosition(panel);
        UpdateDrag(tab, _dragVertical ? pos.Y : pos.X);

        // Distance off the strip across its axis: reordering along it keeps this at zero, so direction tells the two
        // gestures apart.
        var size = panel.RenderSize;
        var across = _dragVertical ? pos.X : pos.Y;
        var extent = _dragVertical ? size.Width : size.Height;
        var outside = across < 0 ? -across : across > extent ? across - extent : 0;

        // Unset means the strip's own extent, so it scales with the theme and the DPI.
        var threshold = double.IsNaN(TearOffDistance) ? extent : TearOffDistance;
        UpdateTearOff(tab, outside, threshold, across < 0 ? -1 : 1);

        AutoScrollStrip(e);
    }

    private void AutoScrollStrip(MouseEventArgs e)
    {
        // The field: asking GetTemplateChild again mid-drag found nothing.
        if (_tabStrip is not { } strip) return;

        // In the scroller's space: the panel is as long as all the tabs.
        var point = e.GetPosition(strip);
        strip.PanNear(_dragVertical ? point.Y : point.X, strip.AutoScrollMargin, strip.AutoScrollRate);
    }

    /// <summary>How far past the strip, across its axis, the pointer must go before a drag becomes a tear-off. NaN
    /// (default) means the strip's own extent.</summary>
    public static readonly AdamantiumProperty TearOffDistanceProperty = AdamantiumProperty.Register(
        nameof(TearOffDistance), typeof(double), typeof(TabControl), new PropertyMetadata(double.NaN));

    public double TearOffDistance
    {
        get => GetValue<double>(TearOffDistanceProperty);
        set => SetValue(TearOffDistanceProperty, value);
    }

    /// <summary>Whether a tab is always selected. False allows none, for a strip whose panel is put away.</summary>
    public static readonly AdamantiumProperty RequiresSelectionProperty = AdamantiumProperty.Register(
        nameof(RequiresSelection), typeof(bool), typeof(TabControl), new PropertyMetadata(true));

    public bool RequiresSelection
    {
        get => GetValue<bool>(RequiresSelectionProperty);
        set => SetValue(RequiresSelectionProperty, value);
    }

    /// <summary>Whether tabs can be dragged to reorder or tear off. False leaves them only selectable.</summary>
    public static readonly AdamantiumProperty AllowTabDragProperty = AdamantiumProperty.Register(
        nameof(AllowTabDrag), typeof(bool), typeof(TabControl), new PropertyMetadata(true));

    public bool AllowTabDrag
    {
        get => GetValue<bool>(AllowTabDragProperty);
        set => SetValue(AllowTabDragProperty, value);
    }

    /// <summary>True while the current drag has pulled far enough off the strip to mean "into its own window".</summary>
    public bool IsTearingOff { get; private set; }

    /// <summary>Raised when a tab is torn off. The application opens its window; unhandled, the tab stays where it was.</summary>
    public event EventHandler<TabTearOffEventArgs> TabTornOff;

    // Crossing the threshold is the tear-off: the window must exist while the gesture still aims it.
    private void UpdateTearOff(TabItem tab, double outside, double threshold, int side)
    {
        if (IsTearingOff || outside <= threshold) return;

        IsTearingOff = true;
        var args = new TabTearOffEventArgs(tab.DataContext, tab, Mouse.ScreenCoordinates);
        TabTornOff?.Invoke(this, args);
        if (!args.Handled)
        {
            IsTearingOff = false;
            return;
        }

        // The window carries the gesture from here, through the platform's move loop.
        CancelDrag(tab);
    }

    // Without the reorder: the tab is not ours any more.
    private void CancelDrag(TabItem tab)
    {
        _dragged = null;
        for (var i = 0; i < Items.Count; i++)
        {
            if (ContainerOfTab(i) is TabItem other) SetOffset(other, 0);
        }

        _dragStartIndex = _targetIndex = -1;
        tab.ZIndex = 0;

        // A torn-off tab never sees button-up, so its own drag state would tear it out again after docking back.
        tab.AbandonDrag();

        // Or PlaceIndicator short-circuits and the bar stays on the tab that left.
        _lastAlong = _lastExtent = double.NaN;
    }

    internal void UpdateDrag(TabItem tab, double along)
    {
        if (!ReferenceEquals(tab, _dragged)) return;

        SetOffset(tab, along - _grabOffset - SlotStart(tab));

        // The tab moves by transform, so no layout pass places the bar. Not while torn off: it no longer belongs here.
        if (!IsTearingOff)
            UpdateIndicator(animate: false);

        var centre = along - _grabOffset + _draggedExtent / 2;
        var target = _dragStartIndex;
        for (var i = 0; i < Items.Count; i++)
        {
            if (i == _dragStartIndex || Reorders(i) is not { } other) continue;
            if (!SlotOfIndex(i, other, out var otherStart, out var otherExtent)) continue;
            var otherCentre = otherStart + otherExtent / 2;
            if (i > _dragStartIndex && centre > otherCentre) target = Math.Max(target, i);
            else if (i < _dragStartIndex && centre < otherCentre) target = Math.Min(target, i);
        }

        if (target != _targetIndex)
        {
            _targetIndex = target;
            ApplyGapOffsets();
        }
    }

    internal void EndDrag(TabItem tab)
    {
        if (!ReferenceEquals(tab, _dragged)) return;
        _dragged = null;
        var start = _dragStartIndex;
        var target = _targetIndex;
        AnimateOffsetTo(tab, GapShift(start, target), () =>
        {
            MoveItem(start, target);
            ClearDragOffsets();
            tab.ZIndex = 0;
            // Or the bar stays at the drop point.
            _lastAlong = _lastExtent = double.NaN;
        });

        // Drives the bar while the tab settles; the ticker expires with the settle.
        var settleElapsed = 0.0;
        var settleDuration = ReorderAnimationDuration.TotalSeconds;
        AnimationManager.AddTicker(dt =>
        {
            UpdateIndicator(animate: false);
            settleElapsed += dt;
            return settleElapsed >= settleDuration;
        });
    }

    private void ApplyGapOffsets()
    {
        for (var i = 0; i < Items.Count; i++)
        {
            if (i == _dragStartIndex || Reorders(i) is not { } other) continue;

            double gap = 0;
            if (_targetIndex > _dragStartIndex && i > _dragStartIndex && i <= _targetIndex) gap = -_draggedExtent;
            else if (_targetIndex < _dragStartIndex && i >= _targetIndex && i < _dragStartIndex) gap = +_draggedExtent;
            AnimateOffsetTo(other, gap);
        }
    }

    private double GapShift(int start, int target)
    {
        double shift = 0;
        if (target > start)
            for (var i = start + 1; i <= target; i++)
            {
                if (Reorders(i) is { } t) shift += Extent(t);
            }
        else if (target < start)
            for (var i = target; i < start; i++)
            {
                if (Reorders(i) is { } t) shift -= Extent(t);
            }
        return shift;
    }

    private void ClearDragOffsets()
    {
        for (var i = 0; i < Items.Count; i++)
            if (ContainerOfTab(i) is TabItem t && t.RenderTransform is Transform)
                SetOffset(t, 0);
    }

    // Asked of the panel first: an authored tab reports Bounds of zero until arranged, which flung a drag to the end.
    private bool SlotOfIndex(int index, TabItem container, out double start, out double extent)
    {
        if (ItemsHostPanel is Panels.VirtualizingPanel panel && panel.TryGetItemRect(index, out var rect))
        {
            start = _dragVertical ? rect.Y : rect.X;
            extent = _dragVertical ? rect.Height : rect.Width;
            return extent > 0;
        }

        start = SlotStart(container);
        extent = Extent(container);
        return extent > 0;
    }

    private double SlotStart(TabItem tab) => _dragVertical ? tab.Bounds.Y : tab.Bounds.X;

    private double Extent(TabItem tab) => _dragVertical ? tab.Bounds.Height : tab.Bounds.Width;

    // Cancel the slide first, or its animated value masks this one.
    private void SetOffset(TabItem tab, double offset)
    {
        var transform = EnsureTransform(tab);
        var prop = _dragVertical ? Transform.TranslateYProperty : Transform.TranslateXProperty;
        transform.CancelAnimation(prop);
        if (_dragVertical) transform.TranslateY = offset; else transform.TranslateX = offset;
    }

    private double CurrentOffset(TabItem tab) =>
        tab.RenderTransform is not Transform t ? 0 : (_dragVertical ? t.TranslateY : t.TranslateX);

    private void AnimateOffsetTo(TabItem tab, double to, Action completed = null)
    {
        var transform = EnsureTransform(tab);
        var prop = _dragVertical ? Transform.TranslateYProperty : Transform.TranslateXProperty;
        transform.BeginAnimation(prop, new DoubleAnimation
        {
            From = CurrentOffset(tab), To = to, Duration = ReorderAnimationDuration, Easing = ReorderEasing ?? DefaultReorderEasing
        }, completed);
    }

    private static Transform EnsureTransform(UIComponent element)
    {
        if (element.RenderTransform is Transform t) return t;
        var transform = new Transform();
        element.RenderTransform = transform;
        return transform;
    }

    private void MoveItem(int from, int to)
    {
        if (from == to) return;
        var selected = SelectedItem;

        _reordering = true;
        try
        {
            if (ItemsSource is IList { IsReadOnly: false, IsFixedSize: false } src && to < src.Count)
            {
                var item = src[from];
                src.RemoveAt(from);
                src.Insert(to, item);
            }
            else
            {
                var item = Items[from];
                Items.RemoveAt(from);
                Items.Insert(to, item);
            }
        }
        finally { _reordering = false; }

        // SelectSingle, not SelectedItem: the item is unchanged, so assigning it would leave the index stale.
        if (selected != null) SelectSingle(IndexOfItem(selected));
    }

    internal bool IsContainerSelected(TabItem container) =>
        SelectedItem != null &&
        (ReferenceEquals(SelectedItem, container) || ReferenceEquals(SelectedItem, container.DataContext));

    // The indicator: a 1px bar driven entirely by its RenderTransform - translated to the tab, scaled to its extent.

    private UIComponent _indicator;
    private UIComponent _rowIndicator;
    private UIComponent _pinnedRowIndicator;
    private bool _indicatorPlaced;

    // One bar per row: a bar cannot point across rows.
    private void UseIndicatorOfSelectedRow()
    {
        var pinned = SelectedIndex >= 0 && SelectedIndex < Items.Count && PinnedItems.Contains(Items[SelectedIndex]);
        var wanted = pinned ? _pinnedRowIndicator : _rowIndicator;
        var other = pinned ? _rowIndicator : _pinnedRowIndicator;

        if (other != null) other.Visibility = Visibility.Collapsed;
        if (ReferenceEquals(_indicator, wanted)) return;

        _indicator = wanted;
        _indicatorPlaced = false;
        _animatingIndicator = false;
    }
    private bool _animatingIndicator;
    private bool _reselecting;   // a collection-driven reselection: snap, don't slide
    private LayoutManager _hookedManager;
    private double _lastAlong, _lastExtent;

    // PART_Tabs and PART_PinnedTabs; a theme with neither falls back to the single presenter.
    private ItemsControl _tabsHost;
    private ItemsControl _pinnedHost;

    /// <summary>The panel of the ordinary tabs; pinned tabs have a list of their own.</summary>
    public override Panel ItemsHostPanel => _tabsHost?.ItemsHostPanel ?? base.ItemsHostPanel;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _tabsHost = GetTemplateChild("PART_Tabs") as ItemsControl;
        _pinnedHost = GetTemplateChild("PART_PinnedTabs") as ItemsControl;
        _contentHost = GetTemplateChild("PART_SelectedContentHost") as IUIComponent;


        _rowIndicator = GetTemplateChild("PART_SelectionIndicator") as UIComponent;
        _pinnedRowIndicator = GetTemplateChild("PART_PinnedSelectionIndicator") as UIComponent;
        if (_pinnedRowIndicator != null) _pinnedRowIndicator.IsHitTestVisible = false;

        UseIndicatorOfSelectedRow();
        if (_indicator != null)
        {
            _indicator.IsHitTestVisible = false;
            _indicatorPlaced = false;
            if (Items.Count == 0 || SelectedIndex < 0) _indicator.Visibility = Visibility.Collapsed;
            // A slide on the old template's bar never completes, so the flag would stay stuck.
            _animatingIndicator = false;
        }

        WireTabStripAffordances();
    }

    /// <summary>Clears the realized containers on removal, not on apply: a template is applied more than once, and
    /// clearing then threw away containers the strip's hosts had just built.</summary>
    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();
        DetachTabStripAffordances();
        ItemContainerGenerator.Clear();
        _tabStrip = null;
        _overflow = null;
        _overflowPopup = null;
        _overflowList = null;
        _tabsHost = null;
        _pinnedHost = null;
        _rowIndicator = null;
        _pinnedRowIndicator = null;
        _indicator = null;
        _indicatorPlaced = false;
        _animatingIndicator = false;
    }

    // Placed from two hooks: LayoutUpdated sees the final slots after a reorder; ArrangeOverride keeps up during a
    // resize drag, which never settles.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_hookedManager == null)
        {
            _hookedManager = LayoutManager.For(this);
            _hookedManager.LayoutUpdated += OnLayoutSettled;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_hookedManager != null)
        {
            _hookedManager.LayoutUpdated -= OnLayoutSettled;
            _hookedManager = null;
        }
    }

    private void OnLayoutSettled(object sender, EventArgs e)
    {
        PlaceIndicator();
        ScrollSelectedIntoView();
        EnterSelectedContent();
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        PlaceIndicator();
        return size;
    }

    private bool _scrollPending;

    // Retried after layout settles, never mid-arrange: an arrange scheduled mid-arrange runs without a measure and stacks
    // every tab at x=0.
    private void ScrollSelectedIntoView()
    {
        if (!_scrollPending) return;
        if (_tabStrip == null || SelectedIndex < 0) return;

        // Cleared only once fully in view: the overflow button appearing narrows the viewport.
        if (ContainerOfTab(SelectedIndex) is IUIComponent container
            && (container.Bounds.Width > 0 || container.Bounds.Height > 0))
        {
            _scrollPending = !_tabStrip.ScrollIntoView(container);
            return;
        }

        // Virtualized: the tab being scrolled to is off screen by definition, so ask the panel where it would be.
        if (ItemsHostPanel is Panels.VirtualizingPanel panel && panel.TryGetItemRect(SelectedIndex, out var slot))
        {
            var start = StripIsVertical ? slot.Y : slot.X;
            var size = StripIsVertical ? slot.Height : slot.Width;
            if (size > 0) _scrollPending = !_tabStrip.ScrollIntoView(start, size);
        }
    }

    private void PlaceIndicator()
    {
        UseIndicatorOfSelectedRow();
        // A pure selection slide keeps running; a reflow that moves the selected tab re-places the bar even mid-slide.
        if (_indicator == null) return;
        if (_animatingIndicator && TryGetIndicatorTarget(out var along, out var extent, out _)
            && along == _lastAlong && extent == _lastExtent)
            return;
        UpdateIndicator(animate: false);
    }

    private bool TryGetIndicatorTarget(out double along, out double extent, out bool vertical)
    {
        along = extent = 0;

        // Same rule as the drag: the panel decides the axis.
        vertical = StripIsVertical;
        if (_indicator == null || _indicator.VisualParent == null) return false;
        if (ContainerOfTab(SelectedIndex) is not TabItem container) return false;

        var bounds = container.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return false;

        var reference = _indicator.VisualParent;
        along = -(vertical ? _indicator.Bounds.Y : _indicator.Bounds.X);

        // A pinned tab sits in the other row: nothing here to point at.
        var reached = false;
        for (IUIComponent n = container; n != null; n = n.VisualParent)
        {
            if (ReferenceEquals(n, reference))
            {
                reached = true;
                break;
            }

            along += vertical ? n.Bounds.Y : n.Bounds.X;
            if (n is UIComponent uc && uc.RenderTransform is Transform pan)
                along += vertical ? pan.TranslateY : pan.TranslateX;
        }

        if (!reached)
        {
            _indicator.Visibility = Visibility.Collapsed;
            return false;
        }
        extent = vertical ? bounds.Height : bounds.Width;
        return true;
    }

    private void UpdateIndicator(bool animate)
    {
        UseIndicatorOfSelectedRow();

        // Nothing selected: hide the bar, and snap rather than slide when a tab is next selected.
        if (_indicator != null && (Items.Count == 0 || SelectedIndex < 0))
        {
            _indicator.Visibility = Visibility.Collapsed;
            _indicatorPlaced = false;
            return;
        }

        if (!TryGetIndicatorTarget(out var along, out var extent, out var vertical)) return;
        if (_indicator != null) _indicator.Visibility = Visibility.Visible;

        if (!animate && _indicatorPlaced && along == _lastAlong && extent == _lastExtent) return;
        _lastAlong = along;
        _lastExtent = extent;

        var transform = EnsureTransform(_indicator);
        var posProp = vertical ? Transform.TranslateYProperty : Transform.TranslateXProperty;
        var scaleProp = vertical ? Transform.ScaleYProperty : Transform.ScaleXProperty;

        // A strip that turned leaves the other axis stretched: reset it.
        if (vertical)
        {
            transform.CancelAnimation(Transform.ScaleXProperty);
            transform.ScaleX = 1;
            transform.TranslateX = 0;
        }
        else
        {
            transform.CancelAnimation(Transform.ScaleYProperty);
            transform.ScaleY = 1;
            transform.TranslateY = 0;
        }

        if (animate && _indicatorPlaced)
        {
            var easing = ReorderEasing ?? DefaultReorderEasing;
            var fromPos = vertical ? transform.TranslateY : transform.TranslateX;
            var fromScale = vertical ? transform.ScaleY : transform.ScaleX;
            _animatingIndicator = true;
            transform.BeginAnimation(posProp,
                new DoubleAnimation { From = fromPos, To = along, Duration = SelectionAnimationDuration, Easing = easing },
                () => _animatingIndicator = false);
            transform.BeginAnimation(scaleProp,
                new DoubleAnimation { From = fromScale, To = extent, Duration = SelectionAnimationDuration, Easing = easing });
        }
        else
        {
            transform.CancelAnimation(posProp);
            transform.CancelAnimation(scaleProp);
            if (vertical) { transform.TranslateY = along; transform.ScaleY = extent; }
            else { transform.TranslateX = along; transform.ScaleX = extent; }
            _animatingIndicator = false;   // CancelAnimation fires no completion
        }

        _indicatorPlaced = true;
    }

    protected internal override bool IsItemItsOwnContainer(object item) => item is TabItem;

    protected internal override IUIComponent GetContainerForItem(object item)
    {
        var container = new TabItem();
        if (ItemContainerStyle != null) container.AttachStyles(ItemContainerStyle);
        return container;
    }

    protected internal override void PrepareContainer(IUIComponent container, object item)
    {
        if (container is TabItem tab && !ReferenceEquals(tab, item))
        {
            tab.DataContext = item;
            tab.Header = item;
            tab.Content = item;
            // ItemTemplate is the header template (WPF semantics); the body uses ContentTemplate.
            tab.HeaderTemplate = ItemTemplate;
            tab.HeaderTemplateSelector = ItemTemplateSelector;
            ApplyContainerSelection(tab, item);
        }
    }

    protected internal override void ClearContainer(IUIComponent container)
    {
        if (container is TabItem tab)
        {
            tab.DataContext = null;
            tab.IsSelected = false;
        }
    }

    public static readonly AdamantiumProperty ShowCloseButtonProperty = AdamantiumProperty.Register(
        nameof(ShowCloseButton), typeof(bool), typeof(TabControl), new PropertyMetadata(false));

    /// <summary>Whether the strip builds only the headers on screen. Off by default; it makes the tabs uniform
    /// (<see cref="TabWidth"/>/<see cref="TabHeight"/>) and pays off past a few hundred tabs.</summary>
    public static readonly AdamantiumProperty IsVirtualizingProperty = AdamantiumProperty.Register(
        nameof(IsVirtualizing), typeof(bool), typeof(TabControl),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsMeasure, OnStripLayoutKnobChanged));

    // The strip's panel is a measure boundary with its own cache, so it is told directly.
    private static void OnStripLayoutKnobChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e) =>
        ((d as TabControl)?.ItemsHostPanel as IMeasurableComponent)?.InvalidateMeasure();

    public bool IsVirtualizing
    {
        get => GetValue<bool>(IsVirtualizingProperty);
        set => SetValue(IsVirtualizingProperty, value);
    }

    /// <summary>One width for every tab, or NaN (default) to size each to its header. On the control because the panel
    /// comes from an ItemsPanelTemplate.</summary>
    public static readonly AdamantiumProperty TabWidthProperty = AdamantiumProperty.Register(
        nameof(TabWidth), typeof(Double), typeof(TabControl),
        new PropertyMetadata(Double.NaN, PropertyMetadataOptions.AffectsMeasure, OnStripLayoutKnobChanged));

    public Double TabWidth
    {
        get => GetValue<Double>(TabWidthProperty);
        set => SetValue(TabWidthProperty, value);
    }

    /// <summary>One height for every tab on a side strip, or NaN (default) - the vertical counterpart of
    /// <see cref="TabWidth"/>.</summary>
    public static readonly AdamantiumProperty TabHeightProperty = AdamantiumProperty.Register(
        nameof(TabHeight), typeof(Double), typeof(TabControl),
        new PropertyMetadata(Double.NaN, PropertyMetadataOptions.AffectsMeasure, OnStripLayoutKnobChanged));

    public Double TabHeight
    {
        get => GetValue<Double>(TabHeightProperty);
        set => SetValue(TabHeightProperty, value);
    }

    public static readonly AdamantiumProperty CloseButtonTemplateProperty = AdamantiumProperty.Register(
        nameof(CloseButtonTemplate), typeof(ControlTemplate), typeof(TabControl), new PropertyMetadata(null));

    /// <summary>Show a close button on every tab (a tab can still opt out via <see cref="TabItem.IsClosable"/>). Default false.</summary>
    public bool ShowCloseButton
    {
        get => GetValue<bool>(ShowCloseButtonProperty);
        set => SetValue(ShowCloseButtonProperty, value);
    }

    /// <summary>Template for the tab close button; a click raises <see cref="TabCloseRequested"/>.</summary>
    public ControlTemplate CloseButtonTemplate
    {
        get => GetValue<ControlTemplate>(CloseButtonTemplateProperty);
        set => SetValue(CloseButtonTemplateProperty, value);
    }

    /// <summary>How every tab's <see cref="TabItem.Icon"/> is drawn; a tab may override it.</summary>
    public static readonly AdamantiumProperty IconTemplateProperty = AdamantiumProperty.Register(
        nameof(IconTemplate), typeof(DataTemplate), typeof(TabControl), new PropertyMetadata(null));

    public DataTemplate IconTemplate
    {
        get => GetValue<DataTemplate>(IconTemplateProperty);
        set => SetValue(IconTemplateProperty, value);
    }

    public static readonly AdamantiumProperty ShowTabOverflowMenuProperty = AdamantiumProperty.Register(
        nameof(ShowTabOverflowMenu), typeof(bool), typeof(TabControl), new PropertyMetadata(true, OnAffordanceToggleChanged));

    /// <summary>Show an overflow ▾ menu listing every tab when they overflow the strip. Default true.</summary>
    public bool ShowTabOverflowMenu { get => GetValue<bool>(ShowTabOverflowMenuProperty); set => SetValue(ShowTabOverflowMenuProperty, value); }

    private TabStripScroller _tabStrip;
    private ToggleButton _overflow;
    private Popup _overflowPopup;
    private ListBox _overflowList;

    private static void OnAffordanceToggleChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
        => (a as TabControl)?.RefreshTabStripAffordances();

    private void WireTabStripAffordances()
    {
        DetachTabStripAffordances();

        _tabStrip = GetTemplateChild("PART_TabStrip") as TabStripScroller;
        _overflow = GetTemplateChild("PART_TabOverflow") as ToggleButton;
        _overflowPopup = GetTemplateChild("PART_TabOverflowPopup") as Popup;

        // The flyout's list is built on first open, outside this template's namescope.
        _overflowList = null;

        if (_tabStrip != null) _tabStrip.ScrollStateChanged += OnTabStripScrollStateChanged;
        if (_overflow != null) { _overflow.Checked += OnOverflowToggled; _overflow.Unchecked += OnOverflowToggled; }
        if (_overflowPopup != null)
        {
            _overflowPopup.PlacementTarget = _overflow;
            _overflowPopup.KeepOpen = false;
            _overflowPopup.IgnoreTargetPress = true;     // the ▾ itself closes it
            _overflowPopup.Closed += OnOverflowClosed;
            _overflowPopup.ContentBuilt += OnOverflowContentBuilt;
        }
        RefreshTabStripAffordances();
    }

    private void DetachTabStripAffordances()
    {
        if (_tabStrip != null)
        {
            _tabStrip.ScrollStateChanged -= OnTabStripScrollStateChanged;
        }

        if (_overflow != null)
        {
            _overflow.Checked -= OnOverflowToggled;
            _overflow.Unchecked -= OnOverflowToggled;
        }

        if (_overflowPopup != null)
        {
            _overflowPopup.Closed -= OnOverflowClosed;
            _overflowPopup.ContentBuilt -= OnOverflowContentBuilt;
        }

        if (_overflowList != null)
        {
            _overflowList.SelectionChanged -= OnOverflowRowPicked;
        }
    }

    private void OnOverflowContentBuilt(object sender, EventArgs e)
    {
        _overflowList = ((Popup)sender).FindContentChild("PART_TabOverflowList") as ListBox;
        if (_overflowList == null)
        {
            return;
        }

        // The rows are a projection of the tabs (see BuildOverflowRows), so a pick maps back by position.
        _overflowList.SelectionChanged += OnOverflowRowPicked;
    }

    private void OnOverflowToggled(object sender, RoutedEventArgs e)
    {
        var opening = _overflow?.IsChecked == true;

        if (_overflowPopup != null)
        {
            _overflowPopup.IsOpen = opening;
        }

        if (opening)
        {
            FillOverflowList();
        }
    }

    internal IReadOnlyList<object> BuildOverflowRows()
    {
        var rows = new List<object>(Items.Count);
        for (var i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            var tab = item as TabItem ?? ContainerOfTab(i) as TabItem;

            // What a tab says, never the tab: a control listed as a row leaves the strip. A control header is listed
            // as text for the same reason.
            var header = item is TabItem authored
                ? authored.Header is IUIComponent visual ? visual.ToString() : authored.Header
                : item;

            rows.Add(new TabOverflowItem(this, tab, item, header, ItemTemplate));
        }
        return rows;
    }

    internal void SelectOverflowRow(int index)
    {
        if (index < 0 || index >= Items.Count) return;
        SelectedIndex = index;
    }

    private bool _fillingOverflow;

    private void FillOverflowList()
    {
        if (_overflowList == null) return;

        _fillingOverflow = true;   // not a user pick
        _overflowList.ItemsSource = BuildOverflowRows();
        // A new container each open: re-apply the index.
        _overflowList.SelectedIndex = -1;
        _overflowList.SelectedIndex = SelectedIndex;
        _fillingOverflow = false;
    }

    private void OnOverflowRowPicked(object sender, EventArgs e)
    {
        if (_fillingOverflow || _overflowList == null) return;

        SelectOverflowRow(_overflowList.SelectedIndex);
        if (_overflow != null)
            _overflow.IsChecked = false;
    }

    // Light-dismissed: un-press the ▾ so its next click reopens.
    private void OnOverflowClosed(object sender, EventArgs e)
    {
        if (_overflow?.IsChecked == true) _overflow.IsChecked = false;
    }

    private void OnTabStripScrollStateChanged(object sender, EventArgs e) => RefreshTabStripAffordances();

    // Stated here, not read off the scroller: a theme trigger watches the templated control.
    public static readonly AdamantiumProperty CanScrollTabsBackProperty = AdamantiumProperty.RegisterReadOnly(
        nameof(CanScrollTabsBack), typeof(bool), typeof(TabControl), new PropertyMetadata(false));

    public static readonly AdamantiumProperty CanScrollTabsForwardProperty = AdamantiumProperty.RegisterReadOnly(
        nameof(CanScrollTabsForward), typeof(bool), typeof(TabControl), new PropertyMetadata(false));

    /// <summary>Whether tabs have gone past the strip's near edge.</summary>
    public bool CanScrollTabsBack
    {
        get => GetValue<bool>(CanScrollTabsBackProperty);
        private set => SetValue(CanScrollTabsBackProperty, value);
    }

    /// <summary>...and past its far edge.</summary>
    public bool CanScrollTabsForward
    {
        get => GetValue<bool>(CanScrollTabsForwardProperty);
        private set => SetValue(CanScrollTabsForwardProperty, value);
    }

    private void RefreshTabStripAffordances()
    {
        CanScrollTabsBack = _tabStrip?.CanScrollBack ?? false;
        CanScrollTabsForward = _tabStrip?.CanScrollForward ?? false;

        if (_overflow == null) return;
        var overflowing = CanScrollTabsBack || CanScrollTabsForward;
        var visibility = ShowTabOverflowMenu && overflowing ? Visibility.Visible : Visibility.Collapsed;
        if (_overflow.Visibility == visibility) return;

        _overflow.Visibility = visibility;
        // The ▾ sits in an Auto track, and its own re-measure reuses a stale constraint: the grid has to be told.
        (_overflow.VisualParent as IMeasurableComponent)?.InvalidateMeasure();
    }

    /// <summary>Raised when a tab's close button is clicked. Cancelable; if not canceled the tab is removed by default.</summary>
    public event EventHandler<TabCloseRequestedEventArgs> TabCloseRequested;

    internal void RequestCloseItem(object item)
    {
        var index = IndexOfItem(item);
        if (index < 0 || index >= Items.Count) return;
        RequestClose(ContainerOfTab(index) as TabItem, index);
    }

    internal void RequestClose(TabItem tab) => RequestClose(tab, IndexOfTab(tab));

    private void RequestClose(TabItem tab, int index)
    {
        if (index < 0 || index >= Items.Count) return;

        var args = new TabCloseRequestedEventArgs(tab, Items[index]);
        TabCloseRequested?.Invoke(this, args);
        if (args.Cancel) return;

        if (!RemoveOnClose(tab, index)) return;

        if (ItemsSource is IList { IsReadOnly: false, IsFixedSize: false } src && index < src.Count)
            src.RemoveAt(index);
        else
            Items.RemoveAt(index);
    }

    /// <summary>Whether closing takes the tab out of this strip. A docking group says no: its layout model is the truth
    /// and the strip follows it.</summary>
    protected virtual bool RemoveOnClose(TabItem tab, int index) => true;
}
