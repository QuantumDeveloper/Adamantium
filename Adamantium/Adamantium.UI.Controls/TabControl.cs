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
/// A single-select <see cref="Selector"/> whose items are tabs. Each tab's <see cref="TabItem.Header"/> is
/// laid out in the tab strip (the template's ItemsPresenter); the selected tab's body is surfaced as
/// <see cref="SelectedContent"/> and shown by the template's <c>PART_SelectedContentHost</c>. Tabs may be authored
/// directly (<c>&lt;TabControl&gt;&lt;TabItem Header="A"&gt;…&lt;/TabItem&gt;…</c>) or data-bound via
/// <see cref="ItemsControl.ItemsSource"/> (the item becomes the header, and its content the body). The first tab is
/// selected automatically.
/// </summary>
public class TabControl : Selector
{
    public static readonly AdamantiumProperty SelectedContentProperty = AdamantiumProperty.Register(nameof(SelectedContent),
        typeof(object), typeof(TabControl), new PropertyMetadata(null));

    // Body templating for data-bound tabs. When ItemsSource is a collection of view-models, the selected one becomes
    // SelectedContent; the template's PART_SelectedContentHost renders it through these, so each tab VM shows its own
    // View. ContentTemplateSelector picks per VM type (a data-template selector), ContentTemplate is a single template
    // for all. Headers use the inherited ItemTemplate/ItemTemplateSelector (flowed onto each TabItem's HeaderTemplate).
    public static readonly AdamantiumProperty ContentTemplateProperty = AdamantiumProperty.Register(nameof(ContentTemplate),
        typeof(DataTemplate), typeof(TabControl), new PropertyMetadata(null));

    public static readonly AdamantiumProperty ContentTemplateSelectorProperty = AdamantiumProperty.Register(nameof(ContentTemplateSelector),
        typeof(DataTemplateSelector), typeof(TabControl), new PropertyMetadata(null));

    // What the content host actually renders SelectedContent with: the SELECTED TAB's template when it carries one,
    // this control's otherwise. A body belongs to the tab that holds it, so the template that turns it into a view
    // belongs there too - a tab whose content is a navigated view model brings its own. Binding the host straight to
    // ContentTemplate/Selector meant only a template stated once for the whole strip was ever used, and such a tab
    // rendered as its type name.
    public static readonly AdamantiumProperty SelectedContentTemplateProperty = AdamantiumProperty.Register(
        nameof(SelectedContentTemplate), typeof(DataTemplate), typeof(TabControl), new PropertyMetadata(null));

    public static readonly AdamantiumProperty SelectedContentTemplateSelectorProperty = AdamantiumProperty.Register(
        nameof(SelectedContentTemplateSelector), typeof(DataTemplateSelector), typeof(TabControl), new PropertyMetadata(null));

    // How the selected tab's body animates when the selection changes; flows to the content host's ContentPresenter
    // (which owns the slide). Default None so it is opt-in per usage/theme.
    public static readonly AdamantiumProperty ContentTransitionProperty = AdamantiumProperty.Register(nameof(ContentTransition),
        typeof(ContentTransition), typeof(TabControl), new PropertyMetadata(ContentTransition.None));

    public static readonly AdamantiumProperty ContentTransitionDurationProperty = AdamantiumProperty.Register(nameof(ContentTransitionDuration),
        typeof(double), typeof(TabControl), new PropertyMetadata(0.25));

    /// <summary>Which edge the tab strip sits on (default <see cref="TabStripPlacement.Top"/>). Each value selects its
    /// own control template via a theme trigger, so a placement can fully restyle the strip.</summary>
    public static readonly AdamantiumProperty TabStripPlacementProperty = AdamantiumProperty.Register(nameof(TabStripPlacement),
        typeof(TabStripPlacement), typeof(TabControl), new PropertyMetadata(TabStripPlacement.Top));

    /// <summary>Whether the tab strip is drawn at all. A control with ONE tab in a window of its own has no choice to
    /// offer, and a strip of one is a button that does what is already done - the window's title bar names it instead.
    /// <para>Lives here rather than on PaneGroup because the templates that have to hide the strip are the TabControl
    /// ones: a document group uses them unchanged, so a property the base template could not see hid nothing and a lone
    /// document floated in a window with a one-tab strip across the top.</para></summary>
    public static readonly AdamantiumProperty ShowTabStripProperty = AdamantiumProperty.Register(
        nameof(ShowTabStrip), typeof(bool), typeof(TabControl),
        new PropertyMetadata(true, PropertyMetadataOptions.AffectsMeasure));

    public bool ShowTabStrip
    {
        get => GetValue<bool>(ShowTabStripProperty);
        set => SetValue(ShowTabStripProperty, value);
    }

    /// <summary>Whether a LONE tab fills the whole strip instead of taking only its own width. Off by default and turned
    /// on where it reads right - the document area does it, so a single open editor is titled across the top rather than
    /// tagged in the corner; a second tab arriving makes them ordinary tabs again.
    /// <para>Setting rather than a hard rule because it is a matter of taste that differs per strip: a browser does it,
    /// a settings dialog does not.</para></summary>
    public static readonly AdamantiumProperty StretchSingleTabProperty = AdamantiumProperty.Register(
        nameof(StretchSingleTab), typeof(bool), typeof(TabControl),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsArrange, OnStretchSingleTabChanged));

    private static void OnStretchSingleTabChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not TabControl control) return;

        // The PANEL is what lays the tabs out, and it is not the one the property changed on.
        control.ItemsHostPanel?.InvalidateArrange();
        control.SyncStretchedTab();
    }

    // Drag-reorder animation feel - theme-settable so the motion is declared, not hard-coded. Duration drives both the
    // neighbour "slide out of the way" and the dragged tab's "settle into its slot" (and slide-home on a short drag);
    // Easing shapes them (null -> a decelerate cubic).
    public static readonly AdamantiumProperty ReorderAnimationDurationProperty = AdamantiumProperty.Register(
        nameof(ReorderAnimationDuration), typeof(TimeSpan), typeof(TabControl),
        new PropertyMetadata(TimeSpan.FromMilliseconds(180)));

    public static readonly AdamantiumProperty ReorderEasingProperty = AdamantiumProperty.Register(
        nameof(ReorderEasing), typeof(IEasingFunction), typeof(TabControl), new PropertyMetadata(null));

    // The selection indicator: the accent bar that slides under the selected tab. Brush + thickness are themed (a
    // {ThemeResource} accent, so an accent/theme swap recolours it live via the template's TemplateBinding); the slide's
    // duration/easing are theme-settable like the reorder feel. The bar's geometry is driven in code (RenderTransform).
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

    /// <summary>Accent brush of the sliding selection indicator. Themed via {ThemeResource}; a TemplateBinding in the
    /// control template paints the bar, so an accent/theme swap recolours it live.</summary>
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

    /// <summary>Which side of the strip the indicator runs along - beside the content (default) or along the strip's
    /// outer edge. Only the side ACROSS the strip is a choice; where it sits along it is the selected tab.</summary>
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

    // Content-area chrome (the panel below the tab strip that hosts the selected body). Set by the theme, TemplateBound
    // by the default template - so a theme/accent swap restyles it and a host can retheme it.
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
            // Slide the indicator for a USER selection (layout is stable). A reselection driven by a COLLECTION change
            // (close/add) is about to reflow the strip, so a slide would head to the pre-reflow slot - defer to
            // PlaceIndicator, which authoritatively places the bar from the next arrange pass (see _reselecting).
            // The same gate covers the scroll-into-view: bring the selected tab (possibly hidden - e.g. picked from the
            // overflow flyout) into view; a no-op when it is already visible.
            // The selected tab has to become VISIBLE however the selection was made - by a click, from the overflow
            // flyout, or by a view-model assignment. Recorded as pending rather than done here: a view-model can select
            // before the template exists, before the container is generated and before anything is laid out, and a
            // scroll attempted then simply does nothing and is never retried. See ScrollSelectedIntoView.
            _scrollPending = true;

            if (!_reselecting) UpdateIndicator(animate: true);
            if (_overflow?.IsChecked == true) _overflow.IsChecked = false;   // a pick from the flyout closes it
        };
        Items.CollectionChanged += OnItemsChanged;

        PinnedItems = [];
        UnpinnedItems = [];
        Items.CollectionChanged += (_, _) => WatchPinning();
    }

    // --- Pinned tabs: TWO sources, not one list that has to be sorted ------------------------------------------------
    // A pinned tab does not belong among the tabs that come and go: it must not be pushed out of view by them, and a
    // strip that scrolls must not carry it away. Two collections say that directly - each tab goes into the one it
    // belongs to - and the ORDER falls out for free, with nothing to keep sorted.
    //
    // Split by the ITEM, which for a tab strip authored in markup IS the TabItem (a docking Pane goes straight into
    // Items). An item that is DATA has no container yet at this point, and nothing to read IsPinned off - such an item
    // stays in the ordinary row. Data-bound pinning needs the flag to come from the DATA, which is a separate decision
    // and not one to guess at here.

    // Read-only PROPERTIES, not plain CLR ones: a theme has to be able to point a list at them with a
    // {TemplateBinding}, and that resolves an AdamantiumProperty by name - against a plain property it finds nothing at
    // all (measured: the application died on startup with "Value cannot be null. (Parameter 'property')"). Read-only
    // because the split is the control's own answer; nothing outside assigns these.
    public static readonly AdamantiumProperty PinnedItemsProperty = AdamantiumProperty.RegisterReadOnly(
        nameof(PinnedItems), typeof(ObservableCollection<object>), typeof(TabControl), new PropertyMetadata(null));

    public static readonly AdamantiumProperty UnpinnedItemsProperty = AdamantiumProperty.RegisterReadOnly(
        nameof(UnpinnedItems), typeof(ObservableCollection<object>), typeof(TabControl), new PropertyMetadata(null));

    /// <summary>The pinned tabs, in the order they were added.</summary>
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

    /// <summary>Whether tabs carry a pin button. Off by default: pinning is an editor's affordance, not something every
    /// strip wants on every tab.</summary>
    public static readonly AdamantiumProperty ShowPinButtonProperty = AdamantiumProperty.Register(
        nameof(ShowPinButton), typeof(bool), typeof(TabControl), new PropertyMetadata(false));

    /// <summary>The look of that button - a standalone template, like <see cref="CloseButtonTemplate"/>, so a host
    /// restyles pinning by handing over ONE template instead of copying the whole tab template.</summary>
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

    // The OWNER watches its tabs, rather than each tab looking upward for its owner: a tab reaches its strip through the
    // visual tree, and an item is in Items long before it is in any tree (measured - pinning simply did nothing until
    // the control had a template). Re-subscribed wholesale on every change: a strip holds tens of tabs, not thousands.
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

    /// <summary>Re-reads which tabs are pinned. Called on a collection change and when a tab's
    /// <see cref="TabItem.IsPinned"/> changes.</summary>
    internal void SplitByPinned()
    {
        PinnedItems.Clear();
        UnpinnedItems.Clear();

        foreach (var item in Items)
        {
            if (item is TabItem { IsPinned: true }) PinnedItems.Add(item);
            else UnpinnedItems.Add(item);
        }

        HasPinnedTabs = PinnedItems.Count > 0;
        HasUnpinnedTabs = UnpinnedItems.Count > 0;
    }

    public static readonly AdamantiumProperty HasUnpinnedTabsProperty = AdamantiumProperty.RegisterReadOnly(
        nameof(HasUnpinnedTabs), typeof(bool), typeof(TabControl), new PropertyMetadata(false));

    /// <summary>Whether anything is UNpinned. Pin every tab and the ordinary row has nothing left to show - it collapses
    /// rather than standing there as a band of empty strip.</summary>
    public bool HasUnpinnedTabs
    {
        get => GetValue<bool>(HasUnpinnedTabsProperty);
        private set => SetValue(HasUnpinnedTabsProperty, value);
    }

    // A tab that changed rows is taken out of one list and put into another, so it appears in its new row at once, in
    // full - which reads as a jump rather than a move. A short fade says "this is the same tab, it went there"; the
    // strip is already reflowing underneath, and racing that with a slide would only fight the layout.
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

    /// <summary>Whether anything is pinned - what the theme shows the pinned row on. An empty row still costs its
    /// padding and its border, which reads as a strip that has grown for no reason.</summary>
    public bool HasPinnedTabs
    {
        get => GetValue<bool>(HasPinnedTabsProperty);
        private set => SetValue(HasPinnedTabsProperty, value);
    }

    /// <summary>The body of the selected tab, shown by the template's <c>PART_SelectedContentHost</c>. Read-only: the
    /// control derives it from the selection.</summary>
    public object SelectedContent
    {
        get => GetValue(SelectedContentProperty);
        private set => SetValue(SelectedContentProperty, value);
    }

    /// <summary>Template for the selected tab's body when items are data (view-models). Rendered by the template's
    /// <c>PART_SelectedContentHost</c>. Use <see cref="ContentTemplateSelector"/> to vary it per item type.</summary>
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

    /// <summary>The template <see cref="SelectedContent"/> is actually rendered with: the selected tab's own when it has
    /// one, this control's <see cref="ContentTemplate"/> otherwise. What a theme's content host binds.</summary>
    public DataTemplate SelectedContentTemplate
    {
        get => GetValue<DataTemplate>(SelectedContentTemplateProperty);
        private set => SetValue(SelectedContentTemplateProperty, value);
    }

    /// <summary>The selector <see cref="SelectedContent"/> is actually rendered through - the selected tab's own when it
    /// has one, this control's otherwise.</summary>
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

    // Set while MoveItem is mid-reorder (RemoveAt + Insert of the SAME item): the reorder restores the selection itself
    // afterwards, so OnItemsChanged must NOT re-run the selection on the intermediate remove/insert. Doing so transiently
    // re-selected a NEIGHBOUR, which then made MoveItem's SelectedItem restore look like a real selection CHANGE and fire a
    // spurious indicator slide toward the pre-reorder slot (the bar "jumping to the old place" on drop).
    private bool _reordering;

    private void OnItemsChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (_reordering) return;   // a drag-reorder move is mid-flight; MoveItem re-points the selection itself afterwards

        // Keep a valid tab selected as the collection mutates (WPF selects the first tab by default and never leaves the
        // selection dangling past the end), and RE-RUN the selection rather than only clamping the index. Closing the
        // SELECTED tab leaves SelectedIndex numerically valid but now pointing at a DIFFERENT item (e.g. index 1 was B,
        // is now C after B is removed); clamping alone never re-set the index, so the base selection machinery never ran
        // and SelectedItem stayed on the removed tab - the content host kept showing the closed tab's body while the
        // indicator slid to the neighbour. SelectSingle re-derives the item at the index, updates SelectedItem + the
        // container highlight and raises SelectionChanged (-> UpdateSelectedContent + UpdateIndicator) when it changed.
        // A tab named before the strip had any (a view-model that opens on its own tab) is honoured ahead of "the first
        // one": auto-selecting index 0 over it would write that back through a TwoWay SelectedItem binding and overrule
        // the source that asked first.
        var pending = TakePendingSelectionIndex();

        var index = Items.Count == 0 ? -1
            : pending >= 0 ? pending
            : SelectedIndex < 0 ? (RequiresSelection ? 0 : -1)
            : SelectedIndex >= Items.Count ? Items.Count - 1
            : SelectedIndex;
        // Snap (don't slide) the indicator for this reselection: the strip is about to reflow, so a slide would target the
        // pre-reflow slot; PlaceIndicator places the bar authoritatively from the next arrange pass.
        _reselecting = true;
        SelectSingle(index);
        _reselecting = false;
        UpdateSelectedContent();
        SyncStretchedTab();
    }

    /// <summary>True while ONE tab is filling the whole strip (see <see cref="StretchSingleTab"/>). A tab that spans its
    /// strip is a title, not a choice: it wears the accent itself and the sliding indicator under it - which exists to
    /// say WHICH of several is current - has nothing left to point out.</summary>
    public static readonly AdamantiumProperty HasStretchedTabProperty = AdamantiumProperty.Register(
        nameof(HasStretchedTab), typeof(bool), typeof(TabControl), new PropertyMetadata(false));

    public bool HasStretchedTab
    {
        get => GetValue<bool>(HasStretchedTabProperty);
        private set => SetValue(HasStretchedTabProperty, value);
    }

    // Told to the CONTAINER as well as to this control: the strip's template hides the indicator, and the tab's own
    // template paints itself in the accent - two different templates asking the same question.
    private void SyncStretchedTab()
    {
        // The base constructor seeds every property with its default and fires the changed callback, so this can run
        // before ItemsControl has made the collection. The strip that fills it calls here again.
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
        // An authored TabItem carries its own body in Content; a data item IS the body (shown via the host's ContentTemplate).
        var tab = item as TabItem;
        SelectedContent = tab != null ? tab.Content : item;

        // ...and with the TAB's template when it brought one: the body and the template that renders it belong together.
        SelectedContentTemplate = tab?.ContentTemplate ?? ContentTemplate;
        SelectedContentTemplateSelector = tab?.ContentTemplateSelector ?? ContentTemplateSelector;
    }

    /// <summary>Selects the tab hosted by <paramref name="container"/> (called when its header is clicked).</summary>
    internal void SelectTab(TabItem container)
    {
        var index = IndexOfTab(container);
        if (index >= 0) SelectedIndex = index;
    }

    private IUIComponent _contentHost;
    private TabItem _enterFrom;
    private IUIComponent _enterReplacing;
    private int _enterTries;

    /// <summary>Enter on a header OPENS that tab: picks it, then puts the focus on the first control of its page. Tab
    /// goes on walking the headers, so this is the way in - otherwise reaching a page means tabbing past every other
    /// tab in the strip.
    /// <para>The page does not exist yet when the key is pressed: the selection only hands the presenter new content,
    /// and the visual for it is built on the next measure. So the step in stays PENDING and is retried once layout has
    /// settled - the same shape <see cref="ScrollSelectedIntoView"/> already has, and for the same reason.</para></summary>
    internal void EnterTab(TabItem container)
    {
        // The page ON SCREEN right now - the one this is about to replace. Stepping in has to wait for it to GO: the
        // selection only hands the presenter new content, and until the next measure builds a visual for it the host
        // still holds the previous page. Measured: without this the focus landed on the OLD page's first button, which
        // was then detached by the swap - and Tab went on walking a tree that was no longer on screen.
        _enterReplacing = IsContainerSelected(container) ? null : FirstPage();
        SelectTab(container);
        _enterFrom = container;
        _enterTries = 120;   // settles, not frames: a slide transition holds the outgoing page for a moment
        EnterSelectedContent();   // an already-selected tab has its page - no waiting needed
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

        // The focus moved on (a click, another key): the step in was for the person who pressed Enter, not for wherever
        // they went next.
        if (!ReferenceEquals(FocusManager.Focused, _enterFrom) || --_enterTries < 0)
        {
            _enterFrom = null;
            return;
        }

        // Still the page being replaced - the presenter has not built the new one yet.
        var page = FirstPage();
        if (page == null || ReferenceEquals(page, _enterReplacing))
            return;

        if (KeyboardNavigation.MoveInto(_contentHost))
            _enterFrom = null;
    }

    /// <summary>Both rows, so the selection lights up a pinned tab as readily as an ordinary one. A strip's containers
    /// are realized by the two lists in its template, not by this control's own generator.</summary>
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

    /// <summary>The container standing for <see cref="ItemsControl.Items"/>[<paramref name="index"/>], whichever of the
    /// strip's lists realized it - the counterpart of <see cref="IndexOfTab"/>. Everything that positions against a tab
    /// (the selection bar, the reorder animation) asks in indices of the ONE item list and gets the live control back.
    /// </summary>
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

        // An authored tab is its own container, so it never passes through a generator at all.
        return item as IUIComponent;
    }

    /// <summary>Where a tab stands in <see cref="ItemsControl.Items"/>, whoever realized it. A strip is laid out as two
    /// lists (pinned and ordinary), so a container may have been generated by either of them - and everything that acts
    /// on a tab (select, close, reorder) still speaks in indices of the ONE list of items this control has.</summary>
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

        // An authored tab is its own container and was never generated by anyone.
        return Items.IndexOf(container);
    }

    // --- Drag reorder (animated, visual-first) --------------------------------------------------------------------
    // During a drag the Items collection is NOT reordered - tab layout stays put (stable Bounds), nothing is rebuilt. The
    // dragged tab tracks the cursor via its RenderTransform; the tabs between its start slot and the current target slot
    // slide aside (animated) to open a gap. On release the dragged tab animates into the gap, the reorder is committed
    // ONCE, and the transforms are cleared - so the committed layout matches where everything already sits. If the target
    // is still the start index (the cursor never passed a neighbour's centre) it just slides home. Tabs are content-sized,
    // so a tab makes room by exactly the dragged tab's extent, and the dragged tab's drop shift is the summed extents of
    // the tabs it crossed.

    private TabItem _dragged;
    private bool _dragVertical;
    private double _grabOffset;      // where along the dragged tab the pointer grabbed
    private double _draggedExtent;   // the dragged tab's extent along the strip axis
    private int _dragStartIndex;
    private int _targetIndex;

    private static readonly IEasingFunction DefaultReorderEasing = new CubicEasing { Mode = EasingMode.Out };

    /// <summary>Which way the strip runs, asked of the panel that actually lays the tabs out rather than deduced from
    /// <see cref="TabStripPlacement"/>. The two disagree: a tool group folded against a side edge keeps its placement
    /// (its tabs still belong at the Bottom of the panel) and turns its PANEL on its side, so a drag that trusted the
    /// placement slid the tabs sideways out of the narrow column that was left. Placement answers only when the panel
    /// has no orientation of its own to state.</summary>
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

    /// <summary>The tab at <paramref name="index"/> WHEN it shares the dragged tab's row, and null otherwise. A strip is
    /// laid out as two lists - pinned and ordinary - and a drag belongs to exactly one of them: the pinned tabs are a
    /// row of their own (or their own run of the shared row), and nothing in the other row is being reordered.
    /// <para>Without this the reorder walked the ONE item list and slid every tab whose index fell between the start and
    /// the target - including tabs in the other row, which moved in lockstep with tabs they have nothing to do with.
    /// The two rows only looked synchronised; they were sharing one index range.</para></summary>
    private TabItem Reorders(int index) =>
        ContainerOfTab(index) is TabItem tab && tab.IsPinned == _draggedPinned ? tab : null;

    // Core (position given as the coordinate ALONG the strip axis, in the items-host panel's space) - unit-testable.
    internal void BeginDrag(TabItem tab, double along)
    {
        _dragVertical = StripIsVertical;
        IsTearingOff = false;   // per-gesture state: left set, it kills the indicator and every later tear-off
        _dragged = tab;
        _draggedPinned = tab.IsPinned;   // the ROW this gesture belongs to - see Reorders
        _dragStartIndex = _targetIndex = IndexOfTab(tab);
        _grabOffset = along - SlotStart(tab);
        _draggedExtent = Extent(tab);
        tab.ZIndex = 1;   // float above its siblings for the drag
        SetOffset(tab, 0);
    }

    /// <summary>Each mouse-move while dragging: keep the tab under the cursor and slide the passed tabs aside to open the
    /// gap at the current target index.</summary>
    internal void UpdateDrag(TabItem tab, MouseEventArgs e)
    {
        if (ItemsHostPanel is not { } panel) return;
        var pos = e.GetPosition(panel);
        UpdateDrag(tab, _dragVertical ? pos.Y : pos.X);

        // How far the pointer has left the strip ACROSS its axis. Dragging ALONG the strip - reordering - keeps this at
        // zero however fast or far it goes, which is what makes a tear-off impossible to trigger by accident: the two
        // gestures are told apart by direction, not by a guessed speed or a timeout. See UpdateTearOff.
        var size = panel.RenderSize;
        var across = _dragVertical ? pos.X : pos.Y;
        var extent = _dragVertical ? size.Width : size.Height;
        var outside = across < 0 ? -across : across > extent ? across - extent : 0;

        // How far off the strip counts as "away". Unset means the strip's OWN extent - pull a whole strip-height clear
        // of it and the tab leaves. Derived rather than a number picked by hand, so it scales with the theme and the
        // DPI; and it is what makes reordering usable, since a hand that wanders a few pixels vertically while dragging
        // ALONG the strip must not tear the tab out.
        var threshold = double.IsNaN(TearOffDistance) ? extent : TearOffDistance;
        UpdateTearOff(tab, outside, threshold, across < 0 ? -1 : 1);

        AutoScrollStrip(e);
    }

    /// <summary>Pans the strip when a REORDER drag nears its edge. The thresholds live on the strip - it is panned by
    /// three different drags and only one of them is a tab's.</summary>
    private void AutoScrollStrip(MouseEventArgs e)
    {
        // The FIELD, not another GetTemplateChild: the part is found once when the template is applied, and asking again
        // mid-drag answered nothing at all - measured, this method never got past this line.
        if (_tabStrip is not { } strip) return;

        // Measured in the SCROLLER's own space, not the items panel's: the panel is as long as all the tabs together,
        // so "near the edge" there is a point the pointer never reaches.
        var point = e.GetPosition(strip);
        strip.PanNear(_dragVertical ? point.Y : point.X, strip.AutoScrollMargin, strip.AutoScrollRate);
    }

    /// <summary>How far past the tab strip the pointer must go, ACROSS the strip's axis, before the drag becomes a
    /// tear-off. NaN (the default) means the strip's own extent: pull a whole strip-height clear of it and the tab goes
    /// with you.
    /// <para>Not zero. Zero means the strip's very edge, and a hand that wanders a pixel or two vertically while
    /// dragging ALONG the strip then tears the tab out instead of reordering it - the two gestures are told apart by
    /// direction, but only if leaving takes a deliberate movement. Set a number to demand more or less.</para></summary>
    public static readonly AdamantiumProperty TearOffDistanceProperty = AdamantiumProperty.Register(
        nameof(TearOffDistance), typeof(double), typeof(TabControl), new PropertyMetadata(double.NaN));

    public double TearOffDistance
    {
        get => GetValue<double>(TearOffDistanceProperty);
        set => SetValue(TearOffDistanceProperty, value);
    }

    /// <summary>Whether this strip insists on having a tab selected. True (a normal tab control) means the selection never
    /// dangles: emptying it, or filling a strip that had none, picks the first tab. False allows NO selection - which is
    /// what a strip whose panel is put away needs, since a highlighted tab there would claim a panel is open when none
    /// is.</summary>
    public static readonly AdamantiumProperty RequiresSelectionProperty = AdamantiumProperty.Register(
        nameof(RequiresSelection), typeof(bool), typeof(TabControl), new PropertyMetadata(true));

    public bool RequiresSelection
    {
        get => GetValue<bool>(RequiresSelectionProperty);
        set => SetValue(RequiresSelectionProperty, value);
    }

    /// <summary>Whether tabs in this strip may be picked up at all - to reorder them, or to tear one off. False leaves
    /// them selectable and nothing more, which is what a strip reduced to a row of buttons wants: a tool panel folded
    /// against an edge shows its tabs only so you can bring it back, and a strip you cannot see the panel behind is not
    /// one you can meaningfully rearrange in.</summary>
    public static readonly AdamantiumProperty AllowTabDragProperty = AdamantiumProperty.Register(
        nameof(AllowTabDrag), typeof(bool), typeof(TabControl), new PropertyMetadata(true));

    public bool AllowTabDrag
    {
        get => GetValue<bool>(AllowTabDragProperty);
        set => SetValue(AllowTabDragProperty, value);
    }

    /// <summary>True while the current drag has pulled far enough off the strip to mean "into its own window".</summary>
    public bool IsTearingOff { get; private set; }

    /// <summary>Raised when a tab is dropped while torn off: the item left this control and wants a window of its own.
    /// The CONTROL does not open one - what kind of window a torn-off tab deserves (its chrome, its size, whether it
    /// keeps a tab strip at all) is the application's decision, and a docking host will answer it differently from a
    /// plain tabbed view. Handled = the item was taken; unhandled leaves it where it was, so a control with nobody
    /// listening simply behaves as it always did.</summary>
    public event EventHandler<TabTearOffEventArgs> TabTornOff;

    // Crossing the threshold IS the tear-off - the tab becomes a window there and then, it does not hover half-out of the
    // strip waiting for the button to come up. Anything else is incoherent: the strip would go on holding a slot and
    // sliding its neighbours aside for something that has already left, and the window a docking gesture needs to aim at
    // would not exist until after the aiming was over.
    private void UpdateTearOff(TabItem tab, double outside, double threshold, int side)
    {
        if (IsTearingOff || outside <= threshold) return;

        IsTearingOff = true;
        var args = new TabTearOffEventArgs(tab.DataContext, tab, Mouse.ScreenCoordinates);
        TabTornOff?.Invoke(this, args);
        if (!args.Handled)
        {
            IsTearingOff = false;   // nobody took it: carry on reordering, exactly as before
            return;
        }

        // Taken. The strip closes over the space it held and stops tracking: the WINDOW carries the gesture from here,
        // through the platform's own move loop (WindowBase.DragMove), so nothing here recomputes a position per move.
        CancelDrag(tab);
    }

    // End the drag WITHOUT the reorder EndDrag would commit: the tab is not ours to move any more.
    private void CancelDrag(TabItem tab)
    {
        _dragged = null;
        // Clear the gap the reorder had opened: the remaining tabs go back to their own slots and layout closes over the
        // one that left (removing the item is what actually closes it - this only drops the transforms that were holding
        // its neighbours aside).
        for (var i = 0; i < Items.Count; i++)
        {
            if (ContainerOfTab(i) is TabItem other) SetOffset(other, 0);
        }

        _dragStartIndex = _targetIndex = -1;
        tab.ZIndex = 0;

        // The TAB's own gesture state has to go too, not just this control's. It is cleared on button-up, and a torn-off
        // tab never sees one - the platform's move loop has the button - so it would stay "mid-drag" forever and tear
        // itself out again on the first mouse move after being docked back.
        tab.AbandonDrag();

        // Drop the indicator's position cache, exactly as EndDrag does after a reorder. It holds where the dragged tab
        // was under the cursor; leaving it means PlaceIndicator short-circuits on "along == _lastAlong" and the bar
        // stays at the slot of a tab that has just left the strip.
        _lastAlong = _lastExtent = double.NaN;
    }

    internal void UpdateDrag(TabItem tab, double along)
    {
        if (!ReferenceEquals(tab, _dragged)) return;

        SetOffset(tab, along - _grabOffset - SlotStart(tab));   // Bounds are stable during the drag -> exact follow

        // The dragged tab IS the selected one; it moves by RenderTransform (no layout pass), so PlaceIndicator won't
        // fire - drive the indicator here so the accent bar rides along with the tab under the cursor. NOT while the
        // tab is torn off, though: the bar marks which tab is selected IN THE STRIP, and a tab on its way out of the
        // strip has it chasing the pointer across a control it no longer belongs to.
        if (!IsTearingOff) 
            UpdateIndicator(animate: false);

        // Target index = how far the dragged tab's centre has passed the OTHER tabs' (stable) centres.
        var centre = along - _grabOffset + _draggedExtent / 2;
        var target = _dragStartIndex;
        for (var i = 0; i < Items.Count; i++)
        {
            if (i == _dragStartIndex || Reorders(i) is not { } other) continue;
            var otherCentre = SlotStart(other) + Extent(other) / 2;
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
        // Slide into the gap (target == start => slide home), then commit the reorder once and clear the transforms so
        // the committed layout matches where everything already is. Drop the raised z-order after it settles.
        AnimateOffsetTo(tab, GapShift(start, target), () =>
        {
            MoveItem(start, target);
            ClearDragOffsets();
            tab.ZIndex = 0;
            // The indicator tracked the tab under the cursor during the drag, so _lastAlong/_lastExtent hold that drop
            // position. Invalidate that cache so the post-reorder layout pass re-places the bar on the selected tab's
            // FINAL slot instead of the PlaceIndicator snap short-circuiting on "along == _lastAlong".
            _lastAlong = _lastExtent = double.NaN;
        });

        // Drive the indicator each frame WHILE the tab settles: it reads the tab's animating offset, so the bar slides to
        // the final slot in lockstep with the tab instead of freezing at the drop point and popping a frame after the
        // reorder commits. The ticker self-expires at the settle duration (then PlaceIndicator keeps it pinned).
        var settleElapsed = 0.0;
        var settleDuration = ReorderAnimationDuration.TotalSeconds;
        AnimationManager.AddTicker(dt =>
        {
            UpdateIndicator(animate: false);
            settleElapsed += dt;
            return settleElapsed >= settleDuration;
        });
    }

    // Each other tab slides aside by the dragged tab's extent while it sits between the start and target slots (opening
    // the gap the dragged tab will drop into); everything else returns to 0.
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

    // The dragged tab's net shift from its start slot to the target slot = the summed extents of the tabs it crosses.
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

    private double SlotStart(TabItem tab) => _dragVertical ? tab.Bounds.Y : tab.Bounds.X;

    private double Extent(TabItem tab) => _dragVertical ? tab.Bounds.Height : tab.Bounds.Width;

    // Direct offset write (no animation): cancel any running slide first, else the Animation-priority value masks it.
    private void SetOffset(TabItem tab, double offset)
    {
        var transform = EnsureTransform(tab);
        var prop = _dragVertical ? Transform.TranslateYProperty : Transform.TranslateXProperty;
        transform.CancelAnimation(prop);
        if (_dragVertical) transform.TranslateY = offset; else transform.TranslateX = offset;
    }

    private double CurrentOffset(TabItem tab) =>
        tab.RenderTransform is not Transform t ? 0 : (_dragVertical ? t.TranslateY : t.TranslateX);

    // Animate the offset from its current value to `to`.
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

    // Reorder in place, preserving which item is selected (its index shifts). Mutates ItemsSource when data-bound to a
    // writable list, otherwise the authored Items collection.
    private void MoveItem(int from, int to)
    {
        if (from == to) return;
        var selected = SelectedItem;

        // Suppress OnItemsChanged's reselection across the remove+insert (it would transiently select a neighbour); the
        // restore below re-points the selection to the moved item without a spurious selection-change indicator slide.
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

        // Re-point SelectedIndex at the moved item's NEW slot. Assigning SelectedItem = selected is a no-op when it hasn't
        // changed (it hasn't - the reorder kept the same item selected), so it would leave SelectedIndex stale on the item's
        // OLD index and the indicator would sit on the old slot. SelectSingle writes the fresh index directly and, because
        // the item is unchanged, raises no SelectionChanged (no spurious indicator slide).
        if (selected != null) SelectSingle(IndexOfItem(selected));
    }

    /// <summary>Whether <paramref name="container"/> is the selected tab - by the item it hosts, so it holds for both an
    /// authored TabItem (it IS the selected item) and a generated one (its DataContext is).</summary>
    internal bool IsContainerSelected(TabItem container) =>
        SelectedItem != null &&
        (ReferenceEquals(SelectedItem, container) || ReferenceEquals(SelectedItem, container.DataContext));

    // --- Selection indicator (the accent bar that slides under the selected tab) ----------------------------------
    // A single template part PART_SelectionIndicator (a 1px-base rectangle) is driven ENTIRELY by its RenderTransform:
    // TranslateX = the selected tab's offset along the strip, ScaleX = the tab's extent (Transform scales about the
    // origin, so a 1px bar becomes exactly the tab's width). Both animate on a selection change (the slide) and snap on
    // layout (initial place / resize / reorder). It lives in the strip's scroll content, so it pans and clips with the
    // tabs for free. Vertical strips use Y/ScaleY. Same DoubleAnimation infra as the drag-reorder above.

    private UIComponent _indicator;         // the bar of the row the selection is in - what everything below drives
    private UIComponent _rowIndicator;      // the ordinary row's bar
    private UIComponent _pinnedRowIndicator;// the pinned row's bar
    private bool _indicatorPlaced;

    /// <summary>Points <see cref="_indicator"/> at the bar belonging to the row the selected tab is in, and hides the
    /// other one. A pinned tab is in a different row from the scrolling strip, and a bar in the wrong row has nothing it
    /// could point at - which is why there is one per row rather than one that tries to reach both.</summary>
    private void UseIndicatorOfSelectedRow()
    {
        var pinned = SelectedIndex >= 0 && SelectedIndex < Items.Count && PinnedItems.Contains(Items[SelectedIndex]);
        var wanted = pinned ? _pinnedRowIndicator : _rowIndicator;
        var other = pinned ? _rowIndicator : _pinnedRowIndicator;

        if (other != null) other.Visibility = Visibility.Collapsed;
        if (ReferenceEquals(_indicator, wanted)) return;

        _indicator = wanted;
        _indicatorPlaced = false;   // a bar that has just taken over has never been placed in its row
        _animatingIndicator = false;
    }
    private bool _animatingIndicator;
    private bool _reselecting;   // a collection-change reselection is in flight -> snap the bar via PlaceIndicator, don't slide
    private LayoutManager _hookedManager;
    private double _lastAlong, _lastExtent;

    // The two lists a strip is made of. PART_Tabs holds the ordinary tabs (and is what "the items host" means for
    // everything that positions against them - a reorder drag, a drop target); PART_PinnedTabs holds the pinned ones in
    // their own row. A theme that states neither leaves both null, and the strip falls back to the single presenter.
    private ItemsControl _tabsHost;
    private ItemsControl _pinnedHost;

    /// <summary>The panel the ORDINARY tabs are laid out in. Pinned tabs sit in a list of their own, and nothing that
    /// measures a drag along the strip has any business there.</summary>
    public override Panel ItemsHostPanel => _tabsHost?.ItemsHostPanel ?? base.ItemsHostPanel;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _tabsHost = GetTemplateChild("PART_Tabs") as ItemsControl;
        _pinnedHost = GetTemplateChild("PART_PinnedTabs") as ItemsControl;
        _contentHost = GetTemplateChild("PART_SelectedContentHost") as IUIComponent;


        // One bar per ROW: each lives inside the row it marks, so it moves and clips with those tabs. Which of the two is
        // in use follows the selected tab (see UseIndicatorOfSelectedRow) - a bar cannot point across rows.
        _rowIndicator = GetTemplateChild("PART_SelectionIndicator") as UIComponent;
        _pinnedRowIndicator = GetTemplateChild("PART_PinnedSelectionIndicator") as UIComponent;
        if (_pinnedRowIndicator != null) _pinnedRowIndicator.IsHitTestVisible = false;

        UseIndicatorOfSelectedRow();
        if (_indicator != null)
        {
            _indicator.IsHitTestVisible = false;   // a thin overlay bar must never eat a tab click
            _indicatorPlaced = false;
            // Start hidden when there's nothing selected, so an empty strip shows no stray 1px accent bar (UpdateIndicator
            // makes it visible once a tab is selected + placed).
            if (Items.Count == 0 || SelectedIndex < 0) _indicator.Visibility = Visibility.Collapsed;
            // A fresh indicator (new template) has no animation running on it. If a selection-slide was mid-flight on the
            // OLD indicator when the template swapped, its completion callback never fires (that indicator is gone), so
            // this flag would stay stuck true and gate PlaceIndicator off forever - the bar would never re-place after a
            // placement change until a click. Clear it here.
            _animatingIndicator = false;
        }

        WireTabStripAffordances();
    }

    /// <summary>The containers this control realized belonged to the template being taken away, so they go with it.
    /// Kept here, on the REMOVAL hook, and not where the new template arrives: a template is applied more than once,
    /// and clearing on the later pass threw away containers the strip's own hosts had just built - which emptied a
    /// whole window built on a TabControl.</summary>
    /// <remarks>Without this the generator kept answering with a TabItem whose tree went away, and everything measured
    /// from one lands nowhere: the selection bar could not walk from the tab up to itself, concluded it had nothing to
    /// point at, and hid for good - which is why moving the strip to another edge and back left no bar at all.</remarks>
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

    // The indicator is placed from TWO complementary hooks, because neither alone covers every case:
    //  - LayoutUpdated (OnLayoutSettled): fires when a layout pass fully SETTLES - the only signal that sees the FINAL
    //    positions after a drag-REORDER, where the moved tab's container is re-arranged a frame or two AFTER the drop (and
    //    each control's Bounds is written INSIDE ArrangeCore, AFTER ArrangeOverride returns). An arrange-time hook reads the
    //    stale slot and strands the bar there; settle placement is exact.
    //  - ArrangeOverride: fires every frame the control re-arranges, which a resize-DRAG does continuously WITHOUT ever
    //    settling (budget-deferred) - so LayoutUpdated stays silent mid-drag. This keeps the bar tracking during a resize
    //    instead of freezing until release.
    // Both funnel into PlaceIndicator, which early-outs when the target hasn't moved, so the overlap is free.
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

    /// <summary>Brings the selected tab into view once everything it needs exists. Stays PENDING until then, and that is
    /// the whole point: a selection assigned by a view-model arrives before the template is applied, before the
    /// container is generated and before the strip is laid out, so a scroll issued at that moment silently does nothing.
    /// Retried once layout has SETTLED, never from inside an arrange pass: scrolling invalidates arrange, and an
    /// arrange scheduled mid-arrange runs without a measure - the tab panel then stacks every tab on DesiredSize zero,
    /// which is all of them at x=0, on top of each other. Measured, twice.</summary>
    private void ScrollSelectedIntoView()
    {
        if (!_scrollPending) return;
        if (_tabStrip == null || SelectedIndex < 0) return;

        if (ContainerOfTab(SelectedIndex) is not IUIComponent container) return;
        // Not laid out yet - scrolling to a tab with no bounds scrolls to nowhere. The next pass will find it placed.
        if (container.Bounds.Width <= 0 && container.Bounds.Height <= 0) return;

        // Cleared only once the tab is ALL the way in view. The overflow button's visibility is decided after a layout
        // pass, and when it appears the strip's viewport narrows - so an offset computed before that leaves the tail of
        // the tab, its close button, just past the new edge. Retrying until nothing needs moving converges in a pass or
        // two and cannot strand a clipped tail.
        _scrollPending = !_tabStrip.ScrollIntoView(container);
    }

    private void PlaceIndicator()
    {
        UseIndicatorOfSelectedRow();
        // Authoritative placement: the bar must always end up on the selected tab. A pure selection slide animates the
        // bar's transform while the tabs' Bounds stay put, so its target is unchanged - leave that slide running. But ANY
        // layout reflow that MOVES the selected tab (a tab closed/opened/resized/reordered, the strip scrolled) changes the
        // target, so re-place the bar even mid-slide - it can then never strand at a stale spot (the highlight failing to
        // follow the active tab after a close). A non-animating pass just snaps to the (possibly moved) target.
        if (_indicator == null) return;
        if (_animatingIndicator && TryGetIndicatorTarget(out var along, out var extent, out _)
            && along == _lastAlong && extent == _lastExtent)
            return;
        UpdateIndicator(animate: false);
    }

    // The selected tab's placement in the indicator's own coordinate space: offset ALONG the strip + its EXTENT, plus
    // whether the strip is vertical. False when there's no indicator/selection or the selected tab isn't laid out yet
    // (PlaceIndicator re-runs once it is). Walks up to the indicator's parent, summing each node's slot offset plus any
    // RenderTransform pan (the strip's scroll, or a tab mid drag-reorder) - robust to how the strip nests the panel.
    private bool TryGetIndicatorTarget(out double along, out double extent, out bool vertical)
    {
        along = extent = 0;

        // The panel that lays the tabs out decides which way the bar runs - the same one rule the drag uses (StripIsVertical).
        // Read from TabStripPlacement instead, a folded tool strip (placement Bottom, panel turned vertical) got a bar
        // stretched along the WRONG axis: sized to the tab's width, it made the Auto column as wide as a flat label and
        // the narrow strip stopped being narrow.
        vertical = StripIsVertical;
        if (_indicator == null || _indicator.VisualParent == null) return false;
        if (ContainerOfTab(SelectedIndex) is not TabItem container) return false;

        var bounds = container.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return false;   // not laid out yet; PlaceIndicator will place it

        var reference = _indicator.VisualParent;
        along = -(vertical ? _indicator.Bounds.Y : _indicator.Bounds.X);

        // Walk up to the bar's own parent, adding each offset on the way. The selected tab may not be under it at all -
        // a pinned tab sits in the OTHER row, and the bar lives in the scrolling one. Then there is nothing here to
        // point at, and summing the walk to the root instead sent the bar off sideways (seen on pinning a tab).
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

        // No selected tab (an empty TabControl, or a deselected strip): hide the bar. Otherwise its default 1px template
        // rectangle lingers as a stray accent pixel in the strip's corner (it is only ever positioned/sized via its
        // RenderTransform, which UpdateIndicator skips when there's nothing to underline). Reset _indicatorPlaced so it
        // SNAPS (not slides from a stale spot) when a tab is next selected.
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

        // The bar is a 1px rectangle STRETCHED along the strip by this transform, so a strip that TURNED leaves the other
        // axis stretched by the extent it had before: a 3x1 bar still scaled 79 along X draws as a block covering the tab,
        // not as a line beside it. Placement has to describe both axes, not just the one currently in use.
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
            _animatingIndicator = false;   // we just cancelled + snapped: nothing is in flight (CancelAnimation fires no completion)
        }

        _indicatorPlaced = true;
    }

    // --- Container seam: TabControl hosts items in TabItem containers ---------------------------------------------

    protected internal override bool IsItemItsOwnContainer(object item) => item is TabItem;

    protected internal override IUIComponent GetContainerForItem(object item)
    {
        var container = new TabItem();
        if (ItemContainerStyle != null) container.AttachStyles(ItemContainerStyle);
        return container;
    }

    protected internal override void PrepareContainer(IUIComponent container, object item)
    {
        // Only for GENERATED containers (data items). An authored TabItem is its own container and is never prepared -
        // it already carries its Header/Content from markup.
        if (container is TabItem tab && !ReferenceEquals(tab, item))
        {
            tab.DataContext = item;
            tab.Header = item;
            tab.Content = item;
            // A TabControl's ItemTemplate is the HEADER template (WPF semantics): flow it onto the tab so the strip label
            // renders the item via a template, while the body uses ContentTemplate/ContentTemplateSelector.
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

    // --- Closable tabs (an optional per-tab close button) ---------------------------------------------------------
    // ShowCloseButton turns the button on for every tab; a tab can still opt out with TabItem.IsClosable. The button's
    // LOOK is a separate CloseButtonTemplate (themed default = a small "x"), so it restyles without touching the tab
    // template. Each TabItem pulls both from its owner on attach (authored + generated tabs alike) - see TabItem.

    public static readonly AdamantiumProperty ShowCloseButtonProperty = AdamantiumProperty.Register(
        nameof(ShowCloseButton), typeof(bool), typeof(TabControl), new PropertyMetadata(false));

    public static readonly AdamantiumProperty CloseButtonTemplateProperty = AdamantiumProperty.Register(
        nameof(CloseButtonTemplate), typeof(ControlTemplate), typeof(TabControl), new PropertyMetadata(null));

    /// <summary>Show a close button on every tab (a tab can still opt out via <see cref="TabItem.IsClosable"/>). Default false.</summary>
    public bool ShowCloseButton
    {
        get => GetValue<bool>(ShowCloseButtonProperty);
        set => SetValue(ShowCloseButtonProperty, value);
    }

    /// <summary>Template for the tab close button - swap it to restyle the button without touching the tab template. The
    /// theme provides a default "x". Its DataContext/TemplatedParent is the button; a click raises <see cref="TabCloseRequested"/>.</summary>
    public ControlTemplate CloseButtonTemplate
    {
        get => GetValue<ControlTemplate>(CloseButtonTemplateProperty);
        set => SetValue(CloseButtonTemplateProperty, value);
    }

    /// <summary>How every tab's <see cref="TabItem.Icon"/> is drawn - one template for the whole strip, which each tab
    /// may override with its own. Icons are DATA (see TabItem.Icon), so the same icon is free to appear in the strip and
    /// in the overflow flyout at once: each place builds its own visual from this.</summary>
    public static readonly AdamantiumProperty IconTemplateProperty = AdamantiumProperty.Register(
        nameof(IconTemplate), typeof(DataTemplate), typeof(TabControl), new PropertyMetadata(null));

    public DataTemplate IconTemplate
    {
        get => GetValue<DataTemplate>(IconTemplateProperty);
        set => SetValue(IconTemplateProperty, value);
    }

    // --- Tab-strip overflow menu ----------------------------------------------------------------------------------
    // When the headers overflow the strip (wheel to scroll them), a ▾ overflow button appears listing every tab, the
    // current one highlighted - pick any (even a hidden one) to switch to it. PART_TabStrip is the TabStripScroller; its
    // CanScrollBack/Forward tell us it overflows. Toggleable (default ON). Prep for a docking control's tab groups.

    public static readonly AdamantiumProperty ShowTabOverflowMenuProperty = AdamantiumProperty.Register(
        nameof(ShowTabOverflowMenu), typeof(bool), typeof(TabControl), new PropertyMetadata(true, OnAffordanceToggleChanged));

    /// <summary>Show an overflow ▾ menu listing every tab when they overflow the strip. Default true.</summary>
    public bool ShowTabOverflowMenu { get => GetValue<bool>(ShowTabOverflowMenuProperty); set => SetValue(ShowTabOverflowMenuProperty, value); }

    private TabStripScroller _tabStrip;
    private ToggleButton _overflow;        // the ▾ icon; its checked state opens the flyout
    private Popup _overflowPopup;
    private ListBox _overflowList;         // the flyout list of every tab, current highlighted

    private static void OnAffordanceToggleChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
        => (a as TabControl)?.RefreshTabStripAffordances();

    // Find the overflow parts (a custom template may omit them), (re)subscribe, and set the initial state.
    private void WireTabStripAffordances()
    {
        DetachTabStripAffordances();

        _tabStrip = GetTemplateChild("PART_TabStrip") as TabStripScroller;
        _overflow = GetTemplateChild("PART_TabOverflow") as ToggleButton;
        _overflowPopup = GetTemplateChild("PART_TabOverflowPopup") as Popup;

        // The flyout's card is built on first open, so the list is not in this template's namescope - it arrives with the
        // content and is taken then.
        _overflowList = null;

        // The ▾ visibility tracks the strip's overflow state; the scroller flips CanScroll* during its own arrange and now,
        // with Visibility marked AffectsParentMeasure, a mid-arrange show/hide correctly reflows the strip Grid (no deferral).
        if (_tabStrip != null) _tabStrip.ScrollStateChanged += OnTabStripScrollStateChanged;
        if (_overflow != null) { _overflow.Checked += OnOverflowToggled; _overflow.Unchecked += OnOverflowToggled; }
        if (_overflowPopup != null)
        {
            _overflowPopup.PlacementTarget = _overflow;
            _overflowPopup.KeepOpen = false;             // click-outside-to-close, owned by Popup now (no per-control hook)
            _overflowPopup.IgnoreTargetPress = true;     // a ▾ press is the toggle - it handles the close, don't dismiss+reopen
            _overflowPopup.Closed += OnOverflowClosed;   // un-press the ▾ when the flyout light-dismisses
            _overflowPopup.ContentBuilt += OnOverflowContentBuilt;
        }
        RefreshTabStripAffordances();
    }

    // Give back everything WireTabStripAffordances took, so a template (or theme) swap leaves nothing subscribed.
    private void DetachTabStripAffordances()
    {
        if (_tabStrip != null) _tabStrip.ScrollStateChanged -= OnTabStripScrollStateChanged;
        if (_overflow != null) { _overflow.Checked -= OnOverflowToggled; _overflow.Unchecked -= OnOverflowToggled; }
        if (_overflowPopup != null)
        {
            _overflowPopup.Closed -= OnOverflowClosed;
            _overflowPopup.ContentBuilt -= OnOverflowContentBuilt;
        }

        if (_overflowList != null) _overflowList.SelectionChanged -= OnOverflowRowPicked;
    }

    // The flyout's list only exists once the popup has built its deferred content.
    private void OnOverflowContentBuilt(object sender, EventArgs e)
    {
        _overflowList = ((Popup)sender).FindContentChild("PART_TabOverflowList") as ListBox;
        if (_overflowList == null) return;

        // The flyout mirrors the tabs BY PROJECTION, not by sharing the item list - see BuildOverflowRows. Selection
        // is therefore mapped by position: picking a row (even one whose tab is scrolled out of sight) selects that
        // tab, and TabControl's own SelectionChanged then scrolls it into view. The rows' LOOK is the theme's
        // (ListBox.TabOverflowList), so nothing is assigned here that would outrank it.
        _overflowList.SelectionChanged += OnOverflowRowPicked;
    }

    // Open/close the flyout with the ▾. The list's own template (a pixel-scrolling ScrollViewer capped by MaxHeight) handles
    // the height + scrolling, so there is nothing to size here.
    private void OnOverflowToggled(object sender, RoutedEventArgs e)
    {
        var opening = _overflow?.IsChecked == true;
        if (_overflowPopup != null)
            _overflowPopup.IsOpen = opening;   // opening BUILDS the list on first use, so the rows go in after this
        if (opening) FillOverflowList();       // built fresh on each open, so it needs no subscription and cannot go stale
    }

    /// <summary>
    /// What the overflow flyout lists: what each tab SAYS, never the tab itself. An authored TabItem is a live control,
    /// and a list handed it as an item hosts it as a row's content - which takes it out of the tab strip. The strip then
    /// correctly gives it up, so opening the flyout emptied the whole strip and left only the selected tab's body.
    /// <para>A data-bound tab's item is plain data and goes in as it is, so the flyout presents it through the very same
    /// ItemTemplate the strip uses. LIMITATION: a tab whose Header is itself a control cannot be shown twice either, so
    /// such a header is listed as text.</para>
    /// </summary>
    internal IReadOnlyList<object> BuildOverflowRows()
    {
        var rows = new List<object>(Items.Count);
        for (var i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            var tab = item as TabItem ?? ContainerOfTab(i) as TabItem;

            // An authored tab is named by its header; a data-bound one by the item itself, drawn through the very
            // template the strip uses. A header that is ITSELF a control cannot be shown in two places any more than the
            // tab can, so it is listed as text - which is the reason Icon is data and not a control.
            var header = item is TabItem authored
                ? authored.Header is IUIComponent visual ? visual.ToString() : authored.Header
                : item;

            rows.Add(new TabOverflowItem(this, tab, header, ItemTemplate));
        }
        return rows;
    }

    /// <summary>Picking row <paramref name="index"/> selects the tab at that position - the rows are a projection, so the
    /// mapping back is positional. Out of range selects nothing (the list can outlive a tab that was just closed).</summary>
    internal void SelectOverflowRow(int index)
    {
        if (index < 0 || index >= Items.Count) return;
        SelectedIndex = index;
    }

    private bool _fillingOverflow;

    private void FillOverflowList()
    {
        if (_overflowList == null) return;

        _fillingOverflow = true;   // our own write must not read back as the user picking a row (which closes the flyout)
        _overflowList.ItemsSource = BuildOverflowRows();
        _overflowList.SelectedIndex = SelectedIndex;
        _fillingOverflow = false;
    }

    private void OnOverflowRowPicked(object sender, EventArgs e)
    {
        if (_fillingOverflow || _overflowList == null) return;

        SelectOverflowRow(_overflowList.SelectedIndex);
        if (_overflow != null) 
            _overflow.IsChecked = false;   // picking one closes the flyout
    }

    // The popup light-dismissed (a click outside it) - un-press the ▾ so its NEXT click reopens, not just un-presses.
    private void OnOverflowClosed(object sender, EventArgs e)
    {
        if (_overflow?.IsChecked == true) _overflow.IsChecked = false;
    }

    private void OnTabStripScrollStateChanged(object sender, EventArgs e) => RefreshTabStripAffordances();

    // What the theme fades the strip's edges on. Stated HERE rather than read off the scroller, because a trigger
    // watches the templated control - and the fade has to be a sibling OVER the scroller, or it would pan with the tabs.
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

    // The overflow ▾ shows whenever the strip overflows (can scroll either way) and the toggle is on.
    private void RefreshTabStripAffordances()
    {
        // The edge fades come FIRST: they belong to the strip whether or not this template has a ▾ to hide.
        CanScrollTabsBack = _tabStrip?.CanScrollBack ?? false;
        CanScrollTabsForward = _tabStrip?.CanScrollForward ?? false;

        if (_overflow == null) return;
        var overflowing = CanScrollTabsBack || CanScrollTabsForward;
        var visibility = ShowTabOverflowMenu && overflowing ? Visibility.Visible : Visibility.Collapsed;
        if (_overflow.Visibility == visibility) return;

        _overflow.Visibility = visibility;
        // The ▾ lives in an Auto column/row of the strip Grid: showing/hiding it changes that track's size, so the GRID must
        // re-measure. A bare Visibility flip only invalidates the ▾'s OWN measure (and re-measuring it alone reuses its stale
        // collapsed constraint, staying 0-sized), so nudge the parent directly. Deliberately SCOPED to this one, rare
        // (overflow-state) change - a global "Visibility invalidates the parent" re-measures every parent on every
        // visibility change everywhere, which tanks a heavy board that re-runs per-pass work (TilesHost.LayoutUpdated).
        (_overflow.VisualParent as IMeasurableComponent)?.InvalidateMeasure();
    }

    /// <summary>Raised when a tab's close button is clicked. Cancelable; if not canceled the tab is removed by default.</summary>
    public event EventHandler<TabCloseRequestedEventArgs> TabCloseRequested;

    // Called by a TabItem when its close button is clicked: raise the cancelable event, and unless vetoed remove the tab
    // (mutating ItemsSource when data-bound to a writable list, else the authored Items) - mirroring MoveItem's source rule.
    internal void RequestClose(TabItem tab)
    {
        var index = IndexOfTab(tab);
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

    /// <summary>Whether closing a tab means taking it out of THIS strip. True for a plain tab control, where the strip IS
    /// the truth about what is open.
    /// <para>A docking group says no: there the layout is the truth and the strip is a view of it, so closing goes through
    /// the model and the strip follows on the next rebuild. Removing it here as well would take the pane out twice - once
    /// from a collection that is about to be rebuilt from a model that still lists it.</para></summary>
    protected virtual bool RemoveOnClose(TabItem tab, int index) => true;
}
