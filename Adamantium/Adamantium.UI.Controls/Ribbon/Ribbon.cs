using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using Adamantium.Core.Commands;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media.Animation;
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

    /// <summary>What the APPLICATION calls this command. The bar is handed a description, never the control, so an
    /// application that has to recognise the command it was asked about - to point its own item at the same state, say -
    /// needs a name for it that outlives the visual. Reaches the application as
    /// <see cref="RibbonQuickAccessEventArgs.Key"/>.
    /// <para>INHERITS, so the bar can stamp it once on the container it builds for an item and every visual inside that
    /// container - the button the user actually right-clicks - answers with the same key.</para></summary>
    public static readonly AdamantiumProperty QuickAccessKeyProperty = AdamantiumProperty.RegisterAttached(
        "QuickAccessKey", typeof(object), typeof(AdamantiumComponent),
        new PropertyMetadata(null, PropertyMetadataOptions.Inherits));

    public static object GetQuickAccessKey(IAdamantiumComponent element) =>
        element.GetValue<object>(QuickAccessKeyProperty);

    public static void SetQuickAccessKey(IAdamantiumComponent element, object value) =>
        element.SetValue(QuickAccessKeyProperty, value);

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
    // item and answers.
    //
    // What is in the bar already, the ribbon READS: point it at the same collection the bar shows (QuickAccessItems) and
    // it recognises its own commands there by their key. The application is left holding no record of the ribbon at all -
    // which matters, because the only record it could keep is a reference to a control, and a view model that holds a
    // control has stopped being a view model.

    /// <summary>Whether this command may be offered to the bar at all. A separator, or a control that would be
    /// meaningless as one small button, says no.</summary>
    public static readonly AdamantiumProperty CanAddToQuickAccessProperty = AdamantiumProperty.RegisterAttached(
        "CanAddToQuickAccess", typeof(bool), typeof(AdamantiumComponent), new PropertyMetadata(true));

    /// <summary>The right-click menu a command in a group is given when it wrote none of its own. A TEMPLATE and not a
    /// menu: a <see cref="ContextMenu"/> is a logical child with a single <see cref="ContextMenu.PlacementTarget"/>, so
    /// every command needs its OWN - and a style setter hands them all the same object. A template builds a fresh one per
    /// command, which is what puts the menu's contents back in the theme instead of in code.
    /// <para>INHERITED, so it is stated once on the ribbon (the theme does) and every group finds it. Unset means a
    /// command is given no menu at all.</para></summary>
    public static readonly AdamantiumProperty CommandContextMenuTemplateProperty = AdamantiumProperty.RegisterAttached(
        "CommandContextMenuTemplate", typeof(DataTemplate), typeof(AdamantiumComponent),
        new PropertyMetadata(null, PropertyMetadataOptions.Inherits));

    public static DataTemplate GetCommandContextMenuTemplate(IAdamantiumComponent element) =>
        element.GetValue<DataTemplate>(CommandContextMenuTemplateProperty);

    public static void SetCommandContextMenuTemplate(IAdamantiumComponent element, DataTemplate value) =>
        element.SetValue(CommandContextMenuTemplateProperty, value);

    /// <summary>States outright that this visual stands for a command already in the bar - what the bar's OWN buttons
    /// say, being in it. A command in the ribbon does not need it: it is recognised by its
    /// <see cref="QuickAccessKeyProperty"/> in <see cref="QuickAccessItemsProperty"/>.</summary>
    public static readonly AdamantiumProperty IsInQuickAccessProperty = AdamantiumProperty.RegisterAttached(
        "IsInQuickAccess", typeof(bool), typeof(AdamantiumComponent), new PropertyMetadata(false));

    /// <summary>The bar's collection, as the ribbon sees it - bind it to the same list the
    /// <see cref="RibbonQuickAccess"/> shows. INHERITED, so it is bound once on the ribbon and every command in the band
    /// can ask whether it is in there. Read only: the ribbon never writes into a collection it does not own.</summary>
    public static readonly AdamantiumProperty QuickAccessItemsProperty = AdamantiumProperty.RegisterAttached(
        "QuickAccessItems", typeof(IEnumerable), typeof(AdamantiumComponent),
        new PropertyMetadata(null, PropertyMetadataOptions.Inherits));

    public static IEnumerable GetQuickAccessItems(IAdamantiumComponent element) =>
        element.GetValue<IEnumerable>(QuickAccessItemsProperty);

    public static void SetQuickAccessItems(IAdamantiumComponent element, IEnumerable value) =>
        element.SetValue(QuickAccessItemsProperty, value);

    /// <summary>Whether this command is in the bar right now: either the visual says so outright, or an item with its key
    /// is in the collection the ribbon was pointed at. Asked afresh every time the menu opens, so no one has to keep the
    /// answer in step.</summary>
    public static bool IsShownInQuickAccess(IAdamantiumComponent command)
    {
        if (command == null) return false;
        if (GetIsInQuickAccess(command)) return true;

        var items = GetQuickAccessItems(command);
        if (items == null) return false;

        var key = GetQuickAccessKey(command);
        var action = (command as Primitives.ButtonBase)?.Command;
        if (key == null && action == null) return false;

        foreach (var item in items)
        {
            if (item is not IQuickAccessItem quick) continue;

            if (key != null && Equals(quick.Key, key)) return true;
            if (action != null && ReferenceEquals(quick.Action, action)) return true;
        }

        return false;
    }

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

            RequestQuickAccess(command, !IsShownInQuickAccess(command));
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

    /// <summary>How long the minimized band takes to slide out and back. A THEME metric - the motion is part of how the
    /// ribbon looks, not of what it does. Zero makes it appear at once.</summary>
    public static readonly AdamantiumProperty FlyoutTransitionDurationProperty = AdamantiumProperty.Register(
        nameof(FlyoutTransitionDuration), typeof(TimeSpan), typeof(Ribbon),
        new PropertyMetadata(TimeSpan.FromMilliseconds(160)));

    public TimeSpan FlyoutTransitionDuration
    {
        get => GetValue<TimeSpan>(FlyoutTransitionDurationProperty);
        set => SetValue(FlyoutTransitionDurationProperty, value);
    }

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
        PropertyChanged += OnOwnDataContextChanged;
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

    /// <summary>The strip's container for the open tab, or null before one is realized. What Up out of the band comes
    /// back to.</summary>
    internal RibbonTabHeader SelectedHeader =>
        SelectedIndex >= 0 ? ItemContainerGenerator?.ContainerFromIndex(SelectedIndex) as RibbonTabHeader : null;

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
    private IUIComponent _strip;                 // the tab headers - a key-tip level, without the tab bodies
    private IUIComponent _applicationMenuHost;   // "File", which stands at the head of the strip

    /// <summary>Where the selected tab is shown - what a tab header hands the key-tip session as its next level, since
    /// a tab's commands live here and not under the strip.</summary>
    internal IUIComponent SelectedContentHost => _contentHost;

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
        _strip = GetTemplateChild("PART_ItemsPresenter") as IUIComponent;
        _applicationMenuHost = GetTemplateChild("PART_ApplicationMenuHost") as IUIComponent;
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

        if (wasShowing) CloseFlyout();
        else OpenFlyout();
    }

    internal void ToggleMinimized() => IsMinimized = !IsMinimized;

    // --- Showing and putting away the minimized band ------------------------------------------------------------------
    //
    // It SLIDES, because a band that appears whole in one frame reads as a different surface arriving rather than as the
    // ribbon's own band coming back. The theme frames it in a fixed-height clip; what moves is a transform on the host
    // inside it - a compositor channel, so the motion costs nothing per frame and never re-runs the adaptive pass.

    // The transform the theme put on PART_FlyoutHost. Null = a theme that did not ask for the motion; then it just shows.
    private Core.Media.Transform FlyoutSlide => _flyoutHost?.RenderTransform;

    private bool _flyoutClosing;

    private void OpenFlyout()
    {
        if (_flyout == null) return;

        _flyoutClosing = false;

        var slide = FlyoutSlide;
        if (slide == null)
        {
            _flyout.IsOpen = true;
            return;
        }

        // Start it hidden, and only begin moving once the overlay has actually been laid out: content raised THIS frame
        // is still 0x0, and a transform on nothing plays out unseen (the same trap the theme-swap spinner hit).
        slide.TranslateY = -GroupsAreaHeight;
        _flyout.LayerPass += OnFlyoutFirstPass;
        _flyout.IsOpen = true;
    }

    private void OnFlyoutFirstPass(object sender, EventArgs e)
    {
        _flyout.LayerPass -= OnFlyoutFirstPass;
        Animate(0);
    }

    private void CloseFlyout()
    {
        if (_flyout is not { IsOpen: true }) return;

        var slide = FlyoutSlide;
        if (slide == null)
        {
            _flyout.IsOpen = false;
            return;
        }

        // The popup has to OUTLIVE the motion, so it is taken off the layer by the animation's completion - and only if
        // nothing re-opened it in the meantime.
        _flyoutClosing = true;
        _flyout.LayerPass -= OnFlyoutFirstPass;
        Animate(-GroupsAreaHeight, () =>
        {
            if (_flyoutClosing && _flyout != null) _flyout.IsOpen = false;
        });
    }

    private void Animate(double to, Action completed = null)
    {
        var slide = FlyoutSlide;
        if (slide == null) return;

        slide.BeginAnimation(Core.Media.Transform.TranslateYProperty, new DoubleAnimation
        {
            From = slide.TranslateY,
            To = to,
            Duration = FlyoutTransitionDuration,
            Easing = new CubicEasing { Mode = EasingMode.Out }
        }, completed);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (_hookedManager == null)
        {
            _hookedManager = LayoutManager.For(this);
            _hookedManager.LayoutUpdated += OnLayoutSettled;
        }

        HookKeyTips();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_hookedManager != null)
        {
            _hookedManager.LayoutUpdated -= OnLayoutSettled;
            _hookedManager = null;
        }

        UnhookKeyTips();
    }

    private void OnLayoutSettled(object sender, EventArgs e)
    {
        EnterSelectedContent();
        // A tab's commands are not in the tree at the moment its header is pressed - the band shows them on the pass
        // that follows - so the level is re-read once layout has settled.
        _keyTips?.Refresh();
    }

    // ---- key tips (Office's Alt) -----------------------------------------------------------------------------------

    private KeyTipSession _keyTips;
    private IInputComponent _keyTipRoot;
    private KeyEventHandler _keyTipKeys;
    private KeyEventHandler _keyTipUp;
    private TextInputEventHandler _keyTipText;
    private MouseButtonEventHandler _keyTipPress;

    /// <summary>Alt is heard at the WINDOW, not at the band: the mode starts wherever the keyboard happens to be, and
    /// its first level covers the caption too - the quick-access bar wears badges there, as in Office.</summary>
    private void HookKeyTips()
    {
        if (_keyTipRoot != null || RootVisual is not IInputComponent root) return;

        _keyTipRoot = root;
        _keyTipKeys ??= OnKeyTipKey;
        _keyTipPress ??= OnKeyTipPress;
        _keyTipText ??= OnKeyTipText;
        _keyTipUp ??= OnKeyTipKeyUp;
        _keyTipRoot.AddHandler(Keyboard.PreviewKeyDownEvent, _keyTipKeys, handledEventsToo: true);
        _keyTipRoot.AddHandler(Keyboard.PreviewKeyUpEvent, _keyTipUp, handledEventsToo: true);
        _keyTipRoot.AddHandler(Keyboard.PreviewTextInputEvent, _keyTipText, handledEventsToo: true);
        _keyTipRoot.AddHandler(Mouse.PreviewMouseDownEvent, _keyTipPress, handledEventsToo: true);
    }

    private void UnhookKeyTips()
    {
        if (_keyTipRoot == null) return;

        _keyTipRoot.RemoveHandler(Keyboard.PreviewKeyDownEvent, _keyTipKeys);
        _keyTipRoot.RemoveHandler(Keyboard.PreviewKeyUpEvent, _keyTipUp);
        _keyTipRoot.RemoveHandler(Keyboard.PreviewTextInputEvent, _keyTipText);
        _keyTipRoot.RemoveHandler(Mouse.PreviewMouseDownEvent, _keyTipPress);
        _keyTipRoot = null;
        _keyTips?.End();
    }

    /// <summary>Letters come from the TEXT stream, not from the key: what a key produces depends on the keyboard
    /// layout, and matching a virtual key against the badge would mean the letters shown could only ever be typed on a
    /// Latin layout. Office matches what was actually typed, and so does this.</summary>
    private void OnKeyTipText(object sender, TextInputEventArgs e)
    {
        if (_keyTips is not { IsActive: true } || string.IsNullOrEmpty(e.Text)) return;

        // While Alt is still HELD a letter is half of a shortcut (Alt+H), not a key tip - Office acts on the letters
        // only once Alt is off. Holding it and typing let every repeat of the pair walk a level deeper and then wipe
        // the badges, which read as the ribbon running through them by itself.
        // AUTOREPEAT is not a second keystroke either, for the same reason. Both are still swallowed: they were aimed
        // at the mode.
        if (_altDown || _lastKeyRepeated)
        {
            e.Handled = true;
            return;
        }

        // Both readings of the same keystroke: what it typed, and the letter that key carries on a Latin keyboard. On a
        // Russian layout the first is "р" and the second "H" - and a band labelled in English answers only to the
        // second, so without it the key tips could not be reached at all.
        e.Handled = _keyTips.Press(e.Text[0], _lastKeyLetter);
        _lastKeyLetter = null;
    }

    private char? _lastKeyLetter;
    private bool _lastKeyRepeated;
    private Key? _heldKey;   // the key that has not come back up - see OnKeyTipKey

    // Alt went down with nothing pressed since - so its RELEASE is a request for key tips rather than half a shortcut.
    private bool _altAlone;
    private bool _altDown;

    private void OnKeyTipKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key == _heldKey) _heldKey = null;
        if (e.Key is not (Key.Alt or Key.LeftAlt or Key.RightAlt)) return;

        _altDown = false;
        if (_altAlone) Toggle();
        _altAlone = false;
        e.Handled = true;
    }

    // Reaching for the pointer means the keyboard route was abandoned - Office drops the badges on any press.
    private void OnKeyTipPress(object sender, MouseButtonEventArgs e) => _keyTips?.End();

    private void OnKeyTipKey(object sender, KeyEventArgs e)
    {
        // What counts as a REPEAT is worked out here rather than taken from the event: measured, the platform's own
        // flag misses the first repetition entirely - a second KeyDown arrives 500ms after the press (the system's
        // autorepeat delay) still claiming IsRepeated=false, and that one descended into a tab and then ran a command
        // inside it off a single held key. A key that was never released cannot be a new keystroke, whatever the flag
        // says. Set BEFORE any branch: an early return left it stale from the keystroke before.
        _lastKeyRepeated = e.IsRepeated || e.Key == _heldKey;
        _heldKey = e.Key;
        // Alt does NOT act on the way down. Holding it repeats KeyDown at the system's autorepeat rate, and toggling on
        // each one flicked the badges on and off many times a second - which read as "it chose something for me".
        // The mode turns over when the key is RELEASED, and only if nothing was pressed in between: Alt+F4 is a
        // shortcut, not a request for key tips. Key.Alt is what actually arrives (the virtual key, 0x12); the sided
        // ones are listed so a platform that distinguishes them still works.
        if (e.Key is Key.Alt or Key.LeftAlt or Key.RightAlt)
        {
            _altDown = true;
            if (!e.IsRepeated) _altAlone = true;
            e.Handled = true;
            return;
        }

        // Something else went down while Alt was held: that is a shortcut, so the release must not show key tips.
        _altAlone = false;

        if (_keyTips is not { IsActive: true }) return;

        if (e.Key == Key.Escape)
        {
            _keyTips.Escape();
            e.Handled = true;
            return;
        }

        // Letters are acted on in the text stream (see OnKeyTipText), which is where the LAYOUT is honoured - but the
        // Latin letter this key carries is remembered here, as the second reading of the same keystroke. The key itself
        // must not travel further, or a key tip would also type into whatever holds the keyboard.
        _lastKeyLetter = KeyChar(e.Key);
        if (_lastKeyLetter != null) e.Handled = true;
    }

    private void Toggle()
    {
        if (_keyTips is { IsActive: true })
        {
            _keyTips.End();
            return;
        }

        _keyTips = new KeyTipSession(TopLevelRoots(), (RootVisual as Adorners.IAdornerHost)?.AdornerLayer);
        _keyTips.Begin();
    }

    /// <summary>What the FIRST level is gathered from - named places, not "the window": the tab strip, the application
    /// menu, and every quick-access bar the window shows. Walking a common ancestor would badge the open tab's commands
    /// too, and those are the level below.</summary>
    private IReadOnlyList<IUIComponent> TopLevelRoots()
    {
        var roots = new List<IUIComponent>();
        if (_strip != null) roots.Add(_strip);
        if (_applicationMenuHost != null) roots.Add(_applicationMenuHost);

        foreach (var bar in (RootVisual as IUIComponent)?.GetVisualDescendants().OfType<RibbonQuickAccess>()
                            ?? [])
        {
            if (bar.Visibility == Visibility.Visible) roots.Add(bar);
        }

        return roots;
    }

    private static char? KeyChar(Key key) => key switch
    {
        >= Key.A and <= Key.Z => (char)('A' + (key - Key.A)),
        >= Key.D0 and <= Key.D9 => (char)('0' + (key - Key.D0)),
        _ => null
    };

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

        // A tab is a LEVEL of key tips, not a command: its letters select it and then show what it holds.
        KeyTipService.SetIsScope(header, true);

        // A header written in markup already says what it says; only its SELECTED state is the strip's to reflect.
        if (!ReferenceEquals(header, item))
        {
            // An authored tab carries its own label; a data item IS the label and is drawn through the ItemTemplate.
            var tab = item as RibbonTab;
            header.DataContext = item;
            header.Content = tab != null ? tab.Header : item;
            header.ContentTemplate = tab?.HeaderTemplate ?? ItemTemplate;
            header.ContentTemplateSelector = ItemTemplateSelector;

            // The letters are stated on the TAB, where the author writes it; the header is what wears the badge.
            if (tab != null && KeyTipService.GetKeyTip(tab) is { Length: > 0 } stated)
                KeyTipService.SetKeyTip(header, stated);
        }

        // The STRIP needs to know the context: the panel cuts its ledges from neighbouring headers and the theme paints
        // the header in the group's colour. The strip holds headers, so the tab's group is copied onto its header.
        header.ContextualGroup = GroupOf(item as RibbonTab);
        Watch(header.ContextualGroup);
        header.Visibility = IsShown(header.ContextualGroup) ? Visibility.Visible : Visibility.Collapsed;

        ApplyContainerSelection(header, item);
    }

    protected internal override void ClearContainer(IUIComponent container)
    {
        if (container is not RibbonTabHeader header) return;

        header.DataContext = null;
        header.IsSelected = false;
    }

    // --- Contextual groups (docs/RIBBON_PLAN.md §4) --------------------------------------------------------------------
    //
    // A group is a DESCRIPTION several tabs point at, so the ribbon does not own a list of them: it learns which groups
    // exist from the tabs themselves and watches each one it meets. Activation is the group's own business - the ribbon
    // only answers it, by putting the tabs in the strip and stamping WHEN so the panel can order them.

    /// <summary>The contexts this ribbon knows about, declared once here. A tab names the one it belongs to by key.</summary>
    public static readonly AdamantiumProperty ContextualGroupsProperty = AdamantiumProperty.Register(
        nameof(ContextualGroups), typeof(RibbonContextualGroups), typeof(Ribbon),
        new PropertyMetadata(null, OnContextualGroupsChanged));

    private RibbonContextualGroups _contextualGroups;

    public RibbonContextualGroups ContextualGroups
    {
        get
        {
            if (_contextualGroups == null) SetValue(ContextualGroupsProperty, new RibbonContextualGroups());
            return _contextualGroups;
        }
        set => SetValue(ContextualGroupsProperty, value);
    }

    private static void OnContextualGroupsChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not Ribbon ribbon) return;

        if (e.OldValue is RibbonContextualGroups old)
        {
            old.CollectionChanged -= ribbon.OnContextualGroupsCollectionChanged;
            foreach (var group in old) ribbon.Release(group);
        }

        ribbon._contextualGroups = e.NewValue as RibbonContextualGroups;
        if (ribbon._contextualGroups == null) return;

        ribbon._contextualGroups.CollectionChanged += ribbon.OnContextualGroupsCollectionChanged;
        foreach (var group in ribbon._contextualGroups) ribbon.Adopt(group);
    }

    private void OnContextualGroupsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems)
            {
                if (item is RibbonContextualGroup group) Release(group);
            }
        }

        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems)
            {
                if (item is RibbonContextualGroup group) Adopt(group);
            }
        }

        RefreshContextualTabs();
    }

    /// <summary>A group stands OUTSIDE the tree, so nothing hands it a DataContext - and an <c>IsActive="{Binding}"</c>
    /// would have nothing to resolve against and would never fire, leaving its tabs hidden for good. What a component
    /// off the tree reaches a DataContext through is its inheritance parent, the same seam a Transform uses.</summary>
    private void Adopt(RibbonContextualGroup group)
    {
        if (group == null) return;

        group.InheritanceParent = this;

        // A group is BUILT and bound before it is added here, so its bindings were established with no parent and
        // therefore no DataContext - and nothing else would ever re-run them: a group has no attach event of its own.
        // Re-establish now, and again when the ribbon's own DataContext arrives (the usual order is: build the tree,
        // then hand it its data). Exactly the seam Transform needs for <Transform ScaleX="{Binding Zoom}"/>.
        Core.Data.BindingEngine.RefreshBindings(group);
        Watch(group);
    }

    private void OnOwnDataContextChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.Property?.Name != "DataContext" || _contextualGroups == null) return;

        foreach (var group in _contextualGroups) Core.Data.BindingEngine.RefreshBindings(group);
    }

    private void Release(RibbonContextualGroup group)
    {
        if (group == null || !_watched.Remove(group)) return;

        group.PropertyChanged -= OnContextualGroupChanged;
        group.InheritanceParent = null;
    }

    private readonly HashSet<RibbonContextualGroup> _watched = [];
    private long _activations;

    private static bool IsShown(RibbonContextualGroup group) => group == null || group.IsActive;

    // The tab states its group directly (code, or a view model that owns the contexts) or names it by key. The object
    // wins: a key is only how MARKUP points at one, since a group is not a visual and cannot be named as an element.
    private RibbonContextualGroup GroupOf(RibbonTab tab)
    {
        if (tab == null) return null;
        if (tab.ContextualGroup is { } stated) return stated;

        var key = tab.ContextualGroupKey;
        if (string.IsNullOrEmpty(key) || _contextualGroups == null) return null;

        foreach (var group in _contextualGroups)
        {
            if (group.Key == key) return group;
        }

        return null;
    }

    private void Watch(RibbonContextualGroup group)
    {
        if (group == null || !_watched.Add(group)) return;

        // Stamped now if it arrived already active, so a group switched on before the ribbon was built still has an
        // order - otherwise every such group sorts as "oldest" and the strip's order depends on nothing.
        if (group.IsActive) group.ActivatedAt = ++_activations;
        group.PropertyChanged += OnContextualGroupChanged;
    }

    private void OnContextualGroupChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (sender is not RibbonContextualGroup group) return;

        // What the STRIP draws from the group - its title, its colour, whether it has a ledge at all - is read during
        // the panel's own measure, so a change to any of it has to ask for one. Nothing else would: the group is not in
        // the tree, and a property on it invalidates nothing by itself.
        if (e.Property == RibbonContextualGroup.ShowHeaderProperty ||
            e.Property == RibbonContextualGroup.HeaderProperty ||
            e.Property == RibbonContextualGroup.AccentProperty)
        {
            RefreshContextualTabs();
            (ItemsHostPanel as IMeasurableComponent)?.InvalidateMeasure();
            return;
        }

        if (e.Property != RibbonContextualGroup.IsActiveProperty) return;

        if (group.IsActive) group.ActivatedAt = ++_activations;

        RefreshContextualTabs();
    }

    private void RefreshContextualTabs()
    {
        var selectionLost = false;

        for (var i = 0; i < Items.Count; i++)
        {
            if (ItemContainerGenerator?.ContainerFromIndex(i) is not RibbonTabHeader header) continue;

            var shown = IsShown(header.ContextualGroup);
            header.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
            header.Accent = header.ContextualGroup?.Accent;

            if (!shown && i == SelectedIndex) selectionLost = true;
        }

        // VisibilityProperty carries AffectsMeasure but NOT AffectsParentMeasure, so hiding a tab invalidates only its
        // OWN measure - and the strip's whole shape (which tabs are in it, where the runs fall, how tall the ledge row
        // is) is decided in the PANEL's. Ask for it, or the strip keeps the arrangement it had before the context came.
        (ItemsHostPanel as IMeasurableComponent)?.InvalidateMeasure();

        // Appearing is an OFFER, not an order: a group switching on must not pull the open tab out from under someone
        // mid-edit. Only losing the open tab forces a move - and to the last ORDINARY tab, never to a neighbouring
        // contextual one, which may be the next to go.
        if (selectionLost) SelectedIndex = LastOrdinaryTab();
    }

    private int LastOrdinaryTab()
    {
        for (var i = Items.Count - 1; i >= 0; i--)
        {
            if (Items[i] is RibbonTab tab && GroupOf(tab) == null) return i;
        }

        return -1;
    }
}
