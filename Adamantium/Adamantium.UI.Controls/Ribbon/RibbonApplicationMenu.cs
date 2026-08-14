using System;
using System.Linq;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>The "File" button at the head of the strip and the BACKSTAGE it opens: the whole window given over to a rail
/// of commands down the left and the chosen page beside it. Not a drop-down - Office stopped making it one in 2013, and
/// the reason holds here: what belongs behind "File" is pages (recent files, export, options), and a page does not fit
/// in a menu.
/// <para>A <see cref="Selector"/> because that is what the rail does: a row either OPENS A PAGE - and the selection is
/// which page - or it is a plain command that runs and closes. See docs/RIBBON_PLAN.md §7.</para></summary>
public class RibbonApplicationMenu : Selector
{
    /// <summary>What the button says - "File".</summary>
    public static readonly AdamantiumProperty HeaderProperty = AdamantiumProperty.Register(nameof(Header),
        typeof(object), typeof(RibbonApplicationMenu), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty HeaderTemplateProperty = AdamantiumProperty.Register(nameof(HeaderTemplate),
        typeof(DataTemplate), typeof(RibbonApplicationMenu), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    /// <summary>The backstage is showing. It covers the window, so nothing else needs to move aside for it.</summary>
    public static readonly AdamantiumProperty IsOpenProperty = AdamantiumProperty.Register(nameof(IsOpen),
        typeof(bool), typeof(RibbonApplicationMenu), new PropertyMetadata(false, OnIsOpenChanged));

    /// <summary>Which of the two shapes this menu takes: the window-wide backstage (Office 2013 and later), or the panel
    /// dropped under the button (Office 2007). The same rows and the same pages either way - only where they are shown
    /// differs, which is why one control answers for both.</summary>
    public static readonly AdamantiumProperty IsBackstageProperty = AdamantiumProperty.Register(nameof(IsBackstage),
        typeof(bool), typeof(RibbonApplicationMenu), new PropertyMetadata(true, PropertyMetadataOptions.AffectsMeasure));

    public bool IsBackstage
    {
        get => GetValue<bool>(IsBackstageProperty);
        set => SetValue(IsBackstageProperty, value);
    }

    /// <summary>How wide the rail of rows is. BOTH shapes read it - the number belongs to the control, not to whichever
    /// template is in use, or the backstage and the dropped panel drift apart the first time someone adjusts one. The
    /// theme restates it as a metric, the way the band's height and a collapsed group's width are stated.</summary>
    public static readonly AdamantiumProperty RailWidthProperty = AdamantiumProperty.Register(nameof(RailWidth),
        typeof(double), typeof(RibbonApplicationMenu),
        new PropertyMetadata(DefaultRailWidth, PropertyMetadataOptions.AffectsMeasure));

    /// <summary>An icon, a label, and room for a word longer than "Export".</summary>
    public const double DefaultRailWidth = 200;

    public double RailWidth
    {
        get => GetValue<double>(RailWidthProperty);
        set => SetValue(RailWidthProperty, value);
    }

    /// <summary>What the pane shows when no row is offering a page of its own - the recent files, in Office's version of
    /// this menu. Only the dropped panel has it: the backstage always stands on a chosen row.</summary>
    public static readonly AdamantiumProperty AuxiliaryPaneContentProperty = AdamantiumProperty.Register(
        nameof(AuxiliaryPaneContent), typeof(object), typeof(RibbonApplicationMenu),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure, OnAuxiliaryPaneContentChanged));

    public object AuxiliaryPaneContent
    {
        get => GetValue(AuxiliaryPaneContentProperty);
        set => SetValue(AuxiliaryPaneContentProperty, value);
    }

    private static void OnAuxiliaryPaneContentChanged(AdamantiumComponent sender, AdamantiumPropertyChangedEventArgs e)
    {
        (sender as RibbonApplicationMenu)?.UpdateSelectedPage();
    }

    /// <summary>The page of the chosen row, shown beside the rail. Read-only: it follows the selection.</summary>
    public static readonly AdamantiumProperty SelectedPageProperty = AdamantiumProperty.Register(nameof(SelectedPage),
        typeof(object), typeof(RibbonApplicationMenu), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty SelectedPageTemplateProperty = AdamantiumProperty.Register(
        nameof(SelectedPageTemplate), typeof(DataTemplate), typeof(RibbonApplicationMenu),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    /// <summary>How much the caption keeps for itself. The backstage takes the CLIENT area, not the window: covering the
    /// close button would trap whoever opened it, and Office leaves the caption reachable for the same reason.
    /// <para>Read-only, and worked out HERE rather than bound from inside the flyout: popup content sits on a detached
    /// subtree, where an <c>{Ancestor}</c> binding cannot reach the window at all.</para></summary>
    public static readonly AdamantiumProperty CaptionInsetProperty = AdamantiumProperty.Register(nameof(CaptionInset),
        typeof(double), typeof(RibbonApplicationMenu), new PropertyMetadata(0.0, PropertyMetadataOptions.AffectsMeasure));

    public double CaptionInset
    {
        get => GetValue<double>(CaptionInsetProperty);
        private set => SetValue(CaptionInsetProperty, value);
    }

    /// <summary>The foot of the rail, where Options and Exit sit by convention.</summary>
    public static readonly AdamantiumProperty FooterPaneContentProperty = AdamantiumProperty.Register(
        nameof(FooterPaneContent), typeof(object), typeof(RibbonApplicationMenu),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty FooterPaneContentTemplateProperty = AdamantiumProperty.Register(
        nameof(FooterPaneContentTemplate), typeof(DataTemplate), typeof(RibbonApplicationMenu),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    public RibbonApplicationMenu()
    {
        SelectionChanged += (_, _) => UpdateSelectedPage();
    }

    // A row can be prepared more than once (rebound, re-templated), so it is released first and then hooked - never
    // hooked twice.
    private void Wire(RibbonApplicationMenuItem row)
    {
        Unwire(row);

        row.Click += OnRowClick;
        row.MouseEnter += OnRowMouseEnter;
    }

    private void Unwire(RibbonApplicationMenuItem row)
    {
        row.Click -= OnRowClick;
        row.MouseEnter -= OnRowMouseEnter;
    }

    public object Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public DataTemplate HeaderTemplate
    {
        get => GetValue<DataTemplate>(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }

    public bool IsOpen
    {
        get => GetValue<bool>(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public object SelectedPage
    {
        get => GetValue(SelectedPageProperty);
        private set => SetValue(SelectedPageProperty, value);
    }

    public DataTemplate SelectedPageTemplate
    {
        get => GetValue<DataTemplate>(SelectedPageTemplateProperty);
        set => SetValue(SelectedPageTemplateProperty, value);
    }

    public object FooterPaneContent
    {
        get => GetValue(FooterPaneContentProperty);
        set => SetValue(FooterPaneContentProperty, value);
    }

    public DataTemplate FooterPaneContentTemplate
    {
        get => GetValue<DataTemplate>(FooterPaneContentTemplateProperty);
        set => SetValue(FooterPaneContentTemplateProperty, value);
    }

    private static void OnIsOpenChanged(AdamantiumComponent sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (sender is not RibbonApplicationMenu menu) return;

        if (menu.IsOpen && menu.IsBackstage)
        {
            // Opening onto a blank half is not a state anyone asked for: the first row that HAS a page opens with it.
            if (menu.SelectedItem == null)
            {
                menu.SelectFirstPage();
            }
            menu.MeasureCaption();
        }

        // Closing takes the pointer with it, and a stale preview would be what the panel opened on next time.
        if (!menu.IsOpen)
        {
            menu._pointedAt = null;
        }

        menu.UpdateSelectedPage();
        menu.ReflectOpenState();
    }

    private void MeasureCaption()
    {
        var window = this.GetVisualAncestors().OfType<WindowBase>().FirstOrDefault();
        CaptionInset = window is { UseCustomChrome: true } ? window.TitleBarHeight : 0;
    }

    private void SelectFirstPage()
    {
        for (var i = 0; i < Items.Count; i++)
        {
            if (Items[i] is RibbonApplicationMenuItem { PageContent: not null })
            {
                SelectSingle(i);
                return;
            }
        }
    }

    // The two shapes ask different questions of the same rows. The backstage stands on the row you CHOSE - it is a place
    // you navigated to. The dropped panel previews the row you are POINTING AT and falls back to its own pane, because a
    // menu is something you run your eye down without committing to anything.
    private void UpdateSelectedPage()
    {
        if (IsBackstage)
        {
            SelectedPage = (SelectedItem as RibbonApplicationMenuItem)?.PageContent;
            return;
        }

        SelectedPage = _pointedAt?.PageContent ?? AuxiliaryPaneContent;
    }

    private RibbonApplicationMenuItem _pointedAt;

    // Only ANOTHER row replaces the preview. Dropping it when the pointer leaves a row would take the page away while
    // someone is walking towards it - the pane is where the buttons are, and the way there is off the rail.
    private void OnRowMouseEnter(object sender, Core.Input.MouseEventArgs e)
    {
        if (IsBackstage || sender is not RibbonApplicationMenuItem { PageContent: not null } row) return;

        _pointedAt = row;
        UpdateSelectedPage();
    }

    private Popup _popup;
    private Primitives.ToggleButton _button;
    private Primitives.ButtonBase _backButton;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_button != null)
        {
            _button.Click -= OnButtonClick;
        }
        _button = GetTemplateChild("PART_Button") as Primitives.ToggleButton;
        if (_button != null)
        {
            _button.Click += OnButtonClick;
        }

        // The page itself - back button, rail, items host - is built on first open, so none of it is in this template's
        // namescope: it arrives with the content.
        if (_backButton != null)
        {
            _backButton.Click -= OnBackClick;
        }
        _backButton = null;

        if (_popup != null)
        {
            _popup.Closed -= OnPopupClosed;
            _popup.ContentBuilt -= OnPopupContentBuilt;
        }
        _popup = GetTemplateChild("PART_Popup") as Popup;
        if (_popup != null)
        {
            // Everything else about the popup - filling the window or dropping under the button, dismissing on an
            // outside press or not - belongs to whichever template is in use, and is stated there.
            _popup.PlacementTarget = this;
            _popup.Closed += OnPopupClosed;
            _popup.ContentBuilt += OnPopupContentBuilt;
        }

        ReflectOpenState();
    }

    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();
        if (_button != null) _button.Click -= OnButtonClick;
        if (_backButton != null) _backButton.Click -= OnBackClick;
        if (_popup != null)
        {
            _popup.Closed -= OnPopupClosed;
            _popup.ContentBuilt -= OnPopupContentBuilt;
            _popup.LayerPass -= OnLayerPass;
        }

        _button = null;
        _backButton = null;
        _popup = null;
    }

    // The page only exists once the popup has built its deferred content - take its parts then.
    private void OnPopupContentBuilt(object sender, EventArgs e)
    {
        var popup = (Popup)sender;

        _backButton = popup.FindContentChild("PART_BackButton") as Primitives.ButtonBase;
        if (_backButton != null)
        {
            _backButton.Click += OnBackClick;
        }

        if (popup.FindContentChild("PART_ItemsPresenter") is ItemsPresenter presenter)
        {
            ConnectPresenter(presenter);
        }
    }

    private void OnButtonClick(object sender, RoutedEventArgs e) => IsOpen = _button?.IsChecked == true;

    private void OnBackClick(object sender, RoutedEventArgs e) => IsOpen = false;

    private void OnPopupClosed(object sender, EventArgs e) => IsOpen = false;

    /// <summary>Opened, the backstage takes the window - so the keyboard has to come with it, or it is left standing on
    /// the button behind a page that now covers everything.
    /// <para>Not straight away: the rows are built when the overlay is first measured, and only the popup layer's pass
    /// can say that they exist - the window's own layout never touches this subtree. So listen to that pass and step in
    /// on the first tick where there is something to step into. Same seam a context menu needs.</para></summary>
    private void MoveKeyboardInside()
    {
        if (_popup == null) return;

        _popup.LayerPass -= OnLayerPass;
        _popup.LayerPass += OnLayerPass;
    }

    private void OnLayerPass(object sender, EventArgs e)
    {
        if (_popup?.Child is not { } content || !Core.Input.KeyboardNavigation.MoveInto(content)) return;

        _popup.LayerPass -= OnLayerPass;
    }

    private void ReflectOpenState()
    {
        if (_popup != null)
        {
            _popup.IsOpen = IsOpen;
            if (IsOpen) MoveKeyboardInside();
            else _popup.LayerPass -= OnLayerPass;
        }
        if (_button != null)
        {
            _button.SetCurrentValue(Primitives.ToggleButton.IsCheckedProperty, IsOpen);
        }
    }

    /// <summary>Escape leaves the backstage, the way it leaves any other layer that took the window.</summary>
    protected override void OnKeyDown(Core.Input.KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsOpen || e.Key != Core.Input.Key.Escape) return;

        IsOpen = false;
        e.Handled = true;
    }

    // A row with a page CHOOSES it and the backstage stays; a row without one is a plain command that has now run, and
    // the backstage has served its purpose.
    private void OnRowClick(object sender, RoutedEventArgs e)
    {
        if (sender is not RibbonApplicationMenuItem row) return;

        // A row that HAS a page is a way IN, not an answer - the answer is on the page it shows. Closing on it would put
        // away the very thing the press asked to see. The backstage additionally makes it the chosen row; the dropped
        // panel is already showing it, and pinning it is what the pointer does.
        if (row.PageContent != null)
        {
            if (IsBackstage)
            {
                SelectSingle(IndexOfItem(row));
            }
            else _pointedAt = row;

            UpdateSelectedPage();
            return;
        }

        IsOpen = false;
    }

    protected internal override bool IsItemItsOwnContainer(object item) => item is RibbonApplicationMenuItem;

    protected internal override IUIComponent GetContainerForItem(object item)
    {
        var row = new RibbonApplicationMenuItem();
        if (ItemContainerStyle != null)
        {
            row.AttachStyles(ItemContainerStyle);
        }
        return row;
    }

    protected internal override void PrepareContainer(IUIComponent container, object item)
    {
        if (container is not RibbonApplicationMenuItem row) return;

        Wire(row);

        // An authored row carries its own label, icon and page; a data item is drawn through the menu's ItemTemplate.
        if (!ReferenceEquals(row, item))
        {
            row.DataContext = item;
            row.Content = item;
            row.ContentTemplate = ItemTemplate;
            row.ContentTemplateSelector = ItemTemplateSelector;
        }

        ApplyContainerSelection(row, item);
    }

    protected internal override void ClearContainer(IUIComponent container)
    {
        if (container is not RibbonApplicationMenuItem row) return;

        Unwire(row);
        row.IsSelected = false;
    }
}
