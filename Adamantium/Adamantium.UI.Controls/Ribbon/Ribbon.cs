using System;
using System.Collections.Specialized;
using Adamantium.Core.Commands;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>
/// The window's command band: a strip of tabs over an area of groups. Items are <see cref="RibbonTab"/>s; the strip
/// shows one <see cref="RibbonTabHeader"/> per item and <c>PART_SelectedContentHost</c> shows the selected tab itself,
/// which presents its own groups. A <see cref="Selector"/> and not a <see cref="TabControl"/> - see docs/RIBBON_PLAN.md §2.1.
/// </summary>
public class Ribbon : Selector
{
    // Sizing is ATTACHED because the commands share no base (Button vs ToggleButton), and so any control put in a
    // group - a drop-down, a slider - can state the same range.

    /// <summary>The size a command is CURRENTLY drawn at - the group's answer, never the author's.</summary>
    public static readonly AdamantiumProperty SizeProperty = AdamantiumProperty.RegisterAttached("Size",
        typeof(RibbonSize), typeof(AdamantiumComponent),
        new PropertyMetadata(RibbonSize.Large, PropertyMetadataOptions.AffectsMeasure));

    /// <summary>The size a command is drawn at while the tab has room - the author's intent, and the top of the ladder
    /// the collapse thresholds walk down.</summary>
    public static readonly AdamantiumProperty MaxSizeProperty = AdamantiumProperty.RegisterAttached("MaxSize",
        typeof(RibbonSize), typeof(AdamantiumComponent), new PropertyMetadata(RibbonSize.Large));

    /// <summary>What marks a command - DATA drawn by <see cref="IconTemplateProperty"/>. ATTACHED for the same reason
    /// the sizes are: the commands share no base, and the quick-access bar has to read the icon of whatever it was
    /// handed. One command may be drawn in two places at once, and a control can only be in one.</summary>
    public static readonly AdamantiumProperty IconProperty = AdamantiumProperty.RegisterAttached("Icon",
        typeof(object), typeof(AdamantiumComponent), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    /// <summary>How the icon is drawn. The theme default renders path data.</summary>
    public static readonly AdamantiumProperty IconTemplateProperty = AdamantiumProperty.RegisterAttached("IconTemplate",
        typeof(DataTemplate), typeof(AdamantiumComponent), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    public static object GetIcon(IAdamantiumComponent element) => element.GetValue(IconProperty);

    public static void SetIcon(IAdamantiumComponent element, object value) => element.SetValue(IconProperty, value);

    public static DataTemplate GetIconTemplate(IAdamantiumComponent element) =>
        element.GetValue<DataTemplate>(IconTemplateProperty);

    public static void SetIconTemplate(IAdamantiumComponent element, DataTemplate value) =>
        element.SetValue(IconTemplateProperty, value);

    /// <summary>How this command draws itself SMALL, in the quick-access bar. Unset means the bar's default: an icon
    /// button. A command that is not a button - a slider, a drop-down - states its own compact form here, which is what
    /// lets the bar hold it without the ribbon knowing a thing about its kind.
    /// <para>The visual is not moved and not shared: the bar builds its own from this template, so the command keeps
    /// standing in the ribbon at the same time.</para></summary>
    public static readonly AdamantiumProperty QuickAccessTemplateProperty = AdamantiumProperty.RegisterAttached(
        "QuickAccessTemplate", typeof(DataTemplate), typeof(AdamantiumComponent), new PropertyMetadata(null));

    public static DataTemplate GetQuickAccessTemplate(IAdamantiumComponent element) =>
        element.GetValue<DataTemplate>(QuickAccessTemplateProperty);

    public static void SetQuickAccessTemplate(IAdamantiumComponent element, DataTemplate value) =>
        element.SetValue(QuickAccessTemplateProperty, value);

    /// <summary>At which step of its GROUP this command drops its big icon for a small one beside the label.</summary>
    public static readonly AdamantiumProperty CollapseToMediumProperty = AdamantiumProperty.RegisterAttached("CollapseToMedium",
        typeof(RibbonCollapseThreshold), typeof(AdamantiumComponent),
        new PropertyMetadata(RibbonCollapseThreshold.WhenGroupIsMedium, PropertyMetadataOptions.AffectsMeasure));

    /// <summary>...and at which step it drops the label too. A command nobody recognises without its words says
    /// <see cref="RibbonCollapseThreshold.Never"/> here.</summary>
    public static readonly AdamantiumProperty CollapseToSmallProperty = AdamantiumProperty.RegisterAttached("CollapseToSmall",
        typeof(RibbonCollapseThreshold), typeof(AdamantiumComponent),
        new PropertyMetadata(RibbonCollapseThreshold.WhenGroupIsSmall, PropertyMetadataOptions.AffectsMeasure));

    public static RibbonSize GetSize(IAdamantiumComponent element) => element.GetValue<RibbonSize>(SizeProperty);

    public static void SetSize(IAdamantiumComponent element, RibbonSize value) => element.SetValue(SizeProperty, value);

    public static RibbonSize GetMaxSize(IAdamantiumComponent element) => element.GetValue<RibbonSize>(MaxSizeProperty);

    public static void SetMaxSize(IAdamantiumComponent element, RibbonSize value) => element.SetValue(MaxSizeProperty, value);

    public static RibbonCollapseThreshold GetCollapseToMedium(IAdamantiumComponent element) =>
        element.GetValue<RibbonCollapseThreshold>(CollapseToMediumProperty);

    public static void SetCollapseToMedium(IAdamantiumComponent element, RibbonCollapseThreshold value) =>
        element.SetValue(CollapseToMediumProperty, value);

    public static RibbonCollapseThreshold GetCollapseToSmall(IAdamantiumComponent element) =>
        element.GetValue<RibbonCollapseThreshold>(CollapseToSmallProperty);

    public static void SetCollapseToSmall(IAdamantiumComponent element, RibbonCollapseThreshold value) =>
        element.SetValue(CollapseToSmallProperty, value);

    // --- Putting a command in the quick-access bar -------------------------------------------------------------------
    //
    // The bar's collection belongs to the APPLICATION and holds whatever type it chose, so the ribbon never writes into
    // it. It only reports the request and states what the command looks like; the application builds its own kind of
    // item and answers. That also means the ribbon cannot know what is already in the bar - IsInQuickAccess is the
    // application's answer to that, not the ribbon's record.

    /// <summary>Whether this command may be offered to the bar at all. A separator, or a control that would be
    /// meaningless as one small button, says no.</summary>
    public static readonly AdamantiumProperty CanAddToQuickAccessProperty = AdamantiumProperty.RegisterAttached(
        "CanAddToQuickAccess", typeof(bool), typeof(AdamantiumComponent), new PropertyMetadata(true));

    /// <summary>Set BY THE APPLICATION: this command is in the bar already. The ribbon reads it to offer "remove"
    /// instead of "add" - it holds no list of its own to check against.</summary>
    public static readonly AdamantiumProperty IsInQuickAccessProperty = AdamantiumProperty.RegisterAttached(
        "IsInQuickAccess", typeof(bool), typeof(AdamantiumComponent), new PropertyMetadata(false));

    /// <summary>Run when a command asks to join the bar, with a <see cref="RibbonQuickAccessEventArgs"/> as its
    /// parameter. INHERITED, so it is bound once on the ribbon and every command in the band finds it.</summary>
    public static readonly AdamantiumProperty AddToQuickAccessCommandProperty = AdamantiumProperty.RegisterAttached(
        "AddToQuickAccessCommand", typeof(ICommand), typeof(AdamantiumComponent),
        new PropertyMetadata(null, PropertyMetadataOptions.Inherits));

    public static readonly AdamantiumProperty RemoveFromQuickAccessCommandProperty = AdamantiumProperty.RegisterAttached(
        "RemoveFromQuickAccessCommand", typeof(ICommand), typeof(AdamantiumComponent),
        new PropertyMetadata(null, PropertyMetadataOptions.Inherits));

    public static readonly RoutedEvent AddToQuickAccessRequestedEvent = EventManager.RegisterRoutedEvent(
        "AddToQuickAccessRequested", RoutingStrategy.Bubble, typeof(EventHandler<RibbonQuickAccessEventArgs>), typeof(Ribbon));

    public static readonly RoutedEvent RemoveFromQuickAccessRequestedEvent = EventManager.RegisterRoutedEvent(
        "RemoveFromQuickAccessRequested", RoutingStrategy.Bubble, typeof(EventHandler<RibbonQuickAccessEventArgs>), typeof(Ribbon));

    public static bool GetCanAddToQuickAccess(IAdamantiumComponent element) =>
        element.GetValue<bool>(CanAddToQuickAccessProperty);

    public static void SetCanAddToQuickAccess(IAdamantiumComponent element, bool value) =>
        element.SetValue(CanAddToQuickAccessProperty, value);

    public static bool GetIsInQuickAccess(IAdamantiumComponent element) =>
        element.GetValue<bool>(IsInQuickAccessProperty);

    public static void SetIsInQuickAccess(IAdamantiumComponent element, bool value) =>
        element.SetValue(IsInQuickAccessProperty, value);

    public static ICommand GetAddToQuickAccessCommand(IAdamantiumComponent element) =>
        element.GetValue<ICommand>(AddToQuickAccessCommandProperty);

    public static void SetAddToQuickAccessCommand(IAdamantiumComponent element, ICommand value) =>
        element.SetValue(AddToQuickAccessCommandProperty, value);

    public static ICommand GetRemoveFromQuickAccessCommand(IAdamantiumComponent element) =>
        element.GetValue<ICommand>(RemoveFromQuickAccessCommandProperty);

    public static void SetRemoveFromQuickAccessCommand(IAdamantiumComponent element, ICommand value) =>
        element.SetValue(RemoveFromQuickAccessCommandProperty, value);

    /// <summary>Every command in the band that may go in the bar, tab by tab and group by group. Walked over the ITEMS
    /// rather than the visual tree: only the open tab is ever realized, and a list that showed one tab's commands would
    /// be no use for choosing.
    /// <para>There is exactly one place to move commands from, and this is what furnishes it - a per-command context
    /// menu cannot be it, because <see cref="Base.InputUIComponent.ContextMenu"/> holds ONE menu and the author's would
    /// replace ours (or ours theirs).</para></summary>
    public IEnumerable<IUIComponent> QuickAccessCandidates
    {
        get
        {
            foreach (var item in Items)
            {
                if (item is not RibbonTab tab) continue;

                foreach (var groupItem in tab.Items)
                {
                    if (groupItem is not RibbonGroup group) continue;

                    foreach (var command in group.Items)
                    {
                        if (command is IUIComponent ui && GetCanAddToQuickAccess(ui))
                        {
                            yield return ui;
                        }
                    }
                }
            }
        }
    }

    /// <summary>Puts the command given as its parameter in the bar, or takes it out - whichever it is not. This is what
    /// a customisation page's rows run, so that asking is BINDABLE: without it the only way to ask from markup would be
    /// a view model reaching for control types, or a behaviour, and neither is a thing an ordinary screen should need.</summary>
    public ICommand ToggleQuickAccess => _toggleQuickAccess ??= new ToggleQuickAccessCommand();

    private ICommand _toggleQuickAccess;

    private sealed class ToggleQuickAccessCommand : ICommand
    {
        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter = null) =>
            parameter is IUIComponent command && GetCanAddToQuickAccess(command);

        public void Execute(object parameter = null)
        {
            if (parameter is not IUIComponent command) return;

            RequestQuickAccess(command, !GetIsInQuickAccess(command));
        }

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Asks for <paramref name="command"/> to be put in the bar (or taken out of it): raises the routed event
    /// and runs the bound command with the same argument. Both, because a host with code hears the event, and a view
    /// with only a view model binds the command.</summary>
    public static void RequestQuickAccess(IUIComponent command, bool add)
    {
        if (command == null || !GetCanAddToQuickAccess(command)) return;

        var args = new RibbonQuickAccessEventArgs(
            add ? AddToQuickAccessRequestedEvent : RemoveFromQuickAccessRequestedEvent, command);

        (command as IObservableComponent)?.RaiseEvent(args);

        var bound = add ? GetAddToQuickAccessCommand(command) : GetRemoveFromQuickAccessCommand(command);
        if (bound?.CanExecute(args) == true)
        {
            bound.Execute(args);
        }
    }

    /// <summary>The selected tab, shown by <c>PART_SelectedContentHost</c>. Read-only: it follows the selection.</summary>
    public static readonly AdamantiumProperty SelectedContentProperty = AdamantiumProperty.Register(nameof(SelectedContent),
        typeof(object), typeof(Ribbon), new PropertyMetadata(null));

    /// <summary>How a DATA tab (an item that is not a <see cref="RibbonTab"/>) is rendered in the groups area. An
    /// authored tab is hosted as itself and ignores this.</summary>
    public static readonly AdamantiumProperty ContentTemplateProperty = AdamantiumProperty.Register(nameof(ContentTemplate),
        typeof(DataTemplate), typeof(Ribbon), new PropertyMetadata(null));

    /// <summary>Height of the groups area, INCLUDING the group captions - constant on purpose: switching tabs must not
    /// change how tall the band is. The theme restates it as a metric; the default here is concrete rather than NaN
    /// because NaN sizes the band to whichever tab is open, and a bottom-anchored caption then rides up and down with
    /// the commands of the tab being shown.</summary>
    public static readonly AdamantiumProperty GroupsAreaHeightProperty = AdamantiumProperty.Register(
        nameof(GroupsAreaHeight), typeof(double), typeof(Ribbon),
        new PropertyMetadata(DefaultGroupsAreaHeight, PropertyMetadataOptions.AffectsMeasure));

    /// <summary>Three rows of small commands plus a caption.</summary>
    public const double DefaultGroupsAreaHeight = 106;

    public double GroupsAreaHeight
    {
        get => GetValue<double>(GroupsAreaHeightProperty);
        set => SetValue(GroupsAreaHeightProperty, value);
    }

    /// <summary>Only the strip is shown; the open tab's groups move to a flyout a click on a header opens. Does NOT
    /// change which tab is open - see docs/RIBBON_PLAN.md §5.</summary>
    public static readonly AdamantiumProperty IsMinimizedProperty = AdamantiumProperty.Register(nameof(IsMinimized),
        typeof(bool), typeof(Ribbon), new PropertyMetadata(false, PropertyMetadataOptions.AffectsMeasure, OnIsMinimizedChanged));

    public bool IsMinimized
    {
        get => GetValue<bool>(IsMinimizedProperty);
        set => SetValue(IsMinimizedProperty, value);
    }

    /// <summary>The "File" button at the head of the strip. A TYPED slot, unlike the footer's: nothing but an
    /// application menu belongs there.</summary>
    public static readonly AdamantiumProperty ApplicationMenuProperty = AdamantiumProperty.Register(nameof(ApplicationMenu),
        typeof(RibbonApplicationMenu), typeof(Ribbon),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure, OnApplicationMenuChanged));

    public RibbonApplicationMenu ApplicationMenu
    {
        get => GetValue<RibbonApplicationMenu>(ApplicationMenuProperty);
        set => SetValue(ApplicationMenuProperty, value);
    }

    // A logical child, so the menu is themed and the window's DataContext reaches its rows - the arrangement a
    // ContextMenu held by a control already uses.
    private static void OnApplicationMenuChanged(AdamantiumComponent sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (sender is not Ribbon ribbon) return;

        if (e.OldValue is RibbonApplicationMenu old)
        {
            ribbon.RemoveLogicalChild(old);
        }
        if (e.NewValue is RibbonApplicationMenu menu)
        {
            ribbon.AddLogicalChild(menu);
        }
    }

    /// <summary>A row of the band's own, under the groups: whatever the application puts there. Neutral on purpose -
    /// the quick-access bar moved below the ribbon is the first tenant, a search box could be the next.</summary>
    public static readonly AdamantiumProperty FooterContentProperty = AdamantiumProperty.Register(nameof(FooterContent),
        typeof(object), typeof(Ribbon), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    public object FooterContent
    {
        get => GetValue(FooterContentProperty);
        set => SetValue(FooterContentProperty, value);
    }

    private static void OnIsMinimizedChanged(AdamantiumComponent sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (sender is not Ribbon ribbon) return;

        ribbon.HostSelectedContent();
        if (!ribbon.IsMinimized)
        {
            ribbon.CloseFlyout();
        }
    }

    public Ribbon()
    {
        // Ctrl+Tab steps between the ribbon and what it sits above. A real write, as a window and an overlay do it:
        // the registry behind it is driven by the CHANGE, and a metadata default is not one.
        KeyboardNavigation.SetIsFocusArea(this, true);
        SelectionChanged += (_, _) => UpdateSelectedContent();
        Items.CollectionChanged += OnItemsChanged;
    }

    public object SelectedContent
    {
        get => GetValue(SelectedContentProperty);
        private set => SetValue(SelectedContentProperty, value);
    }

    public DataTemplate ContentTemplate
    {
        get => GetValue<DataTemplate>(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    // A ribbon always has a tab open. Honours a selection the source named before the items existed.
    private void OnItemsChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        var pending = TakePendingSelectionIndex();

        var index = Items.Count == 0 ? -1
            : pending >= 0 ? pending
            : SelectedIndex < 0 ? 0
            : SelectedIndex >= Items.Count ? Items.Count - 1
            : SelectedIndex;

        SelectSingle(index);
        UpdateSelectedContent();
    }

    private void UpdateSelectedContent() => SelectedContent = SelectedItem;

    /// <summary>Selects the tab whose header was clicked.</summary>
    internal void SelectTab(RibbonTabHeader header)
    {
        var index = IndexOfHeader(header);
        if (index >= 0) SelectedIndex = index;
    }

    /// <summary>Whether <paramref name="header"/> stands for the selected tab - asked by a header realized after the
    /// selection was made, since the selection only reflects onto containers when it changes.</summary>
    internal bool IsHeaderSelected(RibbonTabHeader header)
    {
        var index = IndexOfHeader(header);
        return index >= 0 && index == SelectedIndex;
    }

    private int IndexOfHeader(RibbonTabHeader header)
    {
        if (header == null) return -1;

        for (var i = 0; i < Items.Count; i++)
        {
            if (ReferenceEquals(ItemContainerGenerator.ContainerFromIndex(i), header)) return i;
        }

        return -1;
    }

    private IUIComponent _contentHost;
    private RibbonTabHeader _enterFrom;
    private IUIComponent _enterReplacing;
    private int _enterTries;
    private LayoutManager _hookedManager;

    private Decorators.Decorator _bandHost;
    private Decorators.Decorator _flyoutHost;
    private Popup _flyout;
    private Primitives.ToggleButton _minimizeButton;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _contentHost = GetTemplateChild("PART_SelectedContentHost") as IUIComponent;
        _bandHost = GetTemplateChild("PART_BandHost") as Decorators.Decorator;
        _flyoutHost = GetTemplateChild("PART_FlyoutHost") as Decorators.Decorator;

        _flyout = GetTemplateChild("PART_Flyout") as Popup;
        if (_flyout != null)
        {
            _flyout.PlacementTarget = this;
            _flyout.KeepOpen = false;

            // The strip is the target, and a press on a header is what OPENS the flyout - dismissing on that same press
            // would close it before the click that asked for it arrived.
            _flyout.IgnoreTargetPress = true;
        }

        if (_minimizeButton != null)
        {
            _minimizeButton.Click -= OnMinimizeButtonClick;
        }
        _minimizeButton = GetTemplateChild("PART_MinimizeButton") as Primitives.ToggleButton;
        if (_minimizeButton != null)
        {
            _minimizeButton.Click += OnMinimizeButtonClick;
        }

        HostSelectedContent();
    }

    private void OnMinimizeButtonClick(object sender, RoutedEventArgs e) => IsMinimized = !IsMinimized;

    // Minimizing MOVES the open tab rather than swapping the template, so the groups keep the variants and widths they
    // worked out - the same trade a collapsed group makes.
    private void HostSelectedContent()
    {
        if (_contentHost is not IMeasurableComponent content) return;

        if (IsMinimized)
        {
            if (_flyoutHost != null)
            {
                _flyoutHost.Child = content;
            }
            return;
        }

        if (_bandHost != null)
        {
            _bandHost.Child = content;
        }
    }

    /// <summary>A press on a header opens that tab; while the band is minimized it also drops the tab's groups down
    /// over the content. Pressing the header of the tab already showing puts them away again.</summary>
    internal void ClickTab(RibbonTabHeader header)
    {
        var wasShowing = _flyout is { IsOpen: true } && IsHeaderSelected(header);

        SelectTab(header);

        if (!IsMinimized || _flyout == null) return;

        _flyout.IsOpen = !wasShowing;
    }

    internal void ToggleMinimized() => IsMinimized = !IsMinimized;

    private void CloseFlyout()
    {
        if (_flyout != null)
        {
            _flyout.IsOpen = false;
        }
    }

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

    private void OnLayoutSettled(object sender, EventArgs e) => EnterSelectedContent();

    /// <summary>Enter on a header OPENS that tab: picks it, then focuses its first command (Tab goes on walking the
    /// headers, so this is the way in). The page does not exist yet, so the step in stays PENDING until layout settles.</summary>
    internal void EnterTab(RibbonTabHeader header)
    {
        // The tab about to be replaced. Until the next measure builds the new one the host still holds the previous
        // one, and the focus would land on ITS first command - which the swap then detaches.
        _enterReplacing = IsHeaderSelected(header) ? null : FirstPage();
        SelectTab(header);
        _enterFrom = header;
        _enterTries = 120;
        EnterSelectedContent();   // an already-open tab has its groups - no waiting needed
    }

    private IUIComponent FirstPage()
    {
        if (_contentHost == null) return null;

        foreach (var child in _contentHost.VisualChildren) return child;

        return null;
    }

    private void EnterSelectedContent()
    {
        if (_enterFrom == null) return;

        // The focus moved on (a click, another key): the step in was for whoever pressed Enter, not for where they went.
        if (!ReferenceEquals(FocusManager.Focused, _enterFrom) || --_enterTries < 0)
        {
            _enterFrom = null;
            return;
        }

        // Still the tab being replaced - the host has not built the new one yet.
        var page = FirstPage();
        if (page == null || ReferenceEquals(page, _enterReplacing)) return;

        if (KeyboardNavigation.MoveInto(_contentHost)) _enterFrom = null;
    }

    // The strip hosts one header per tab. An authored <RibbonTab> is NOT its own container - it is the body, and one
    // control cannot be in the strip and in the groups area at once.
    protected internal override bool IsItemItsOwnContainer(object item) => item is RibbonTabHeader;

    protected internal override IUIComponent GetContainerForItem(object item)
    {
        var header = new RibbonTabHeader();
        if (ItemContainerStyle != null) header.AttachStyles(ItemContainerStyle);
        return header;
    }

    protected internal override void PrepareContainer(IUIComponent container, object item)
    {
        if (container is not RibbonTabHeader header) return;

        // A header written in markup already says what it says; only its SELECTED state is the strip's to reflect.
        if (!ReferenceEquals(header, item))
        {
            // An authored tab carries its own label; a data item IS the label and is drawn through the ItemTemplate.
            var tab = item as RibbonTab;
            header.DataContext = item;
            header.Content = tab != null ? tab.Header : item;
            header.ContentTemplate = tab?.HeaderTemplate ?? ItemTemplate;
            header.ContentTemplateSelector = ItemTemplateSelector;
        }

        ApplyContainerSelection(header, item);
    }

    protected internal override void ClearContainer(IUIComponent container)
    {
        if (container is not RibbonTabHeader header) return;

        header.DataContext = null;
        header.IsSelected = false;
    }
}
