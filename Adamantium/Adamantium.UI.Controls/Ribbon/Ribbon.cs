using System;
using System.Collections.Specialized;
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

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _contentHost = GetTemplateChild("PART_SelectedContentHost") as IUIComponent;
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

        // An authored tab carries its own label; a data item IS the label and is drawn through the ribbon's ItemTemplate.
        var tab = item as RibbonTab;
        header.DataContext = item;
        header.Content = tab != null ? tab.Header : item;
        header.ContentTemplate = tab?.HeaderTemplate ?? ItemTemplate;
        header.ContentTemplateSelector = ItemTemplateSelector;
        ApplyContainerSelection(header, item);
    }

    protected internal override void ClearContainer(IUIComponent container)
    {
        if (container is not RibbonTabHeader header) return;

        header.DataContext = null;
        header.IsSelected = false;
    }
}
