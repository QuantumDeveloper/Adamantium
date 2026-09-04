using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>
/// A non-editable single-select drop-down (WPF's ComboBox, minus the editable text box). Its header shows the selected
/// item; clicking it opens a popup list of the items, from which one is picked. The popup is edge-aware (opens upward
/// when there's not enough room below - see <see cref="Popup.FlipToFit"/>), light-dismisses on an outside click, and binds
/// painlessly to an enum via <see cref="EnumType"/> (no ObjectDataProvider dance). Items host in the popup as
/// <see cref="DropDownItem"/> containers.
/// </summary>
public class DropDown : Selector
{
    public static readonly AdamantiumProperty IsDropDownOpenProperty = AdamantiumProperty.Register(nameof(IsDropDownOpen),
        typeof(bool), typeof(DropDown), new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender, OnIsDropDownOpenChanged));

    public static readonly AdamantiumProperty MaxDropDownHeightProperty = AdamantiumProperty.Register(nameof(MaxDropDownHeight),
        typeof(double), typeof(DropDown), new PropertyMetadata(320.0));

    public static readonly AdamantiumProperty PlaceholderProperty = AdamantiumProperty.Register(nameof(Placeholder),
        typeof(object), typeof(DropDown), new PropertyMetadata(null, OnPlaceholderChanged));

    // What the header shows: the selected item, or the placeholder when nothing is selected. Read-only for the template.
    public static readonly AdamantiumProperty DisplayContentProperty = AdamantiumProperty.Register(nameof(DisplayContent),
        typeof(object), typeof(DropDown), new PropertyMetadata(null));

    // Bind ItemsSource to an enum's values with no ceremony: EnumType="{x:Type local:MyEnum}" (or {x:Enum ...}). Selecting
    // a row sets SelectedItem to the enum value; display is the value's ToString (use ItemTemplate for friendly names).
    public static readonly AdamantiumProperty EnumTypeProperty = AdamantiumProperty.Register(nameof(EnumType),
        typeof(Type), typeof(DropDown), new PropertyMetadata(null, OnEnumTypeChanged));

    // Header chrome (like the other controls': set by the theme, TemplateBound by the default template).
    // Header hover fill (projected by the template trigger), like ButtonBase.
    public static readonly AdamantiumProperty BackgroundPointerOverProperty = AdamantiumProperty.Register(
        nameof(BackgroundPointerOver), typeof(Brush), typeof(DropDown), new PropertyMetadata(default(Brush)));

    private Popup _popup;

    static DropDown()
    {
        // A drop-down is a keyboard-focus target (it Focus()es itself on open) - opt in, since the base default is false.
        FocusableProperty.OverrideMetadata(typeof(DropDown), new PropertyMetadata(true));
    }

    /// <summary>The whole keyboard contract of a drop-down, answered HERE, on the header, which keeps the focus the
    /// entire time: Enter/Space opens, the arrows move the highlighted row while it is open, Enter/Space then closes on
    /// that row, and Escape closes putting back what was chosen before.
    /// <para>The focus deliberately never goes INTO the list. The popup's contents hang on the overlay with no visual
    /// path back, so a key pressed with the focus down there never travels through this control - which is exactly how
    /// an earlier attempt left both Escape and the arrows dead once the list was open.</para></summary>
    /// <summary>The list closes when the keyboard leaves: an open popup is a modal-ish thing that belongs to the control
    /// being worked in, and tabbing away used to leave it hanging over the page with nothing driving it - the header no
    /// longer had the focus, so neither the arrows nor Escape reached it any more.</summary>
    /// <remarks>A click on a ROW also takes the focus off the header, and that is not leaving: closing there would pull
    /// the list out from under the click before the choice was made. So the row's owner is checked - only focus that
    /// went somewhere else counts as away.</remarks>
    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        if (!IsDropDownOpen) return;
        if (FocusManager.Focused is DropDownItem row && ReferenceEquals(row.Owner, this)) return;
        IsDropDownOpen = false;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsEnabled)
            return;

        if (!IsDropDownOpen)
        {
            // Enter and Space open it - NOT an arrow. In this engine the arrows are how the keyboard moves BETWEEN
            // controls, so a closed drop-down that answered one would both open itself unasked and swallow the key,
            // trapping the walk on it. Opening is a choice; passing over is not.
            if (e.Key is not (Key.Enter or Key.Space))
                return;

            IsDropDownOpen = true;   // opening sets the highlight to the current value (see OnIsDropDownOpenChanged)
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Key.DownArrow:
            case Key.UpArrow:
                StepHighlight(e.Key == Key.DownArrow ? 1 : -1);
                e.Handled = true;
                break;
            case Key.Enter or Key.Space:
                Commit();
                e.Handled = true;
                break;
            case Key.Escape:
                IsDropDownOpen = false;   // nothing to put back: walking the list never changed the value
                e.Handled = true;
                break;
        }
    }

    /// <summary>The row the arrows are on. -1 = none. Only meaningful while the list is open; committed by Enter.</summary>
    private int _highlightedIndex = -1;

    // One row along, clamped at the ends - the same step the wheel takes over a CLOSED drop-down, except that there the
    // step IS the choice (there is no list to walk and nothing to commit later).
    private void StepHighlight(int delta)
    {
        var count = Items.Count;
        if (count == 0) return;

        var from = _highlightedIndex < 0 ? (delta > 0 ? -1 : count) : _highlightedIndex;
        Highlight(Math.Clamp(from + delta, 0, count - 1));
    }

    private void Highlight(int index)
    {
        if (_highlightedIndex == index) return;
        SetHighlight(_highlightedIndex, false);
        _highlightedIndex = index;
        SetHighlight(_highlightedIndex, true);
    }

    private void SetHighlight(int index, bool highlighted)
    {
        if (index >= 0 && ItemContainerGenerator.ContainerFromIndex(index) is DropDownItem row)
            row.IsHighlighted = highlighted;
    }

    // Enter takes the highlighted row - which is the ONLY moment the value changes, so everything bound to it hears
    // about the choice once, not once per arrow key.
    private void Commit()
    {
        if (_highlightedIndex >= 0 && _highlightedIndex != SelectedIndex) SelectSingle(_highlightedIndex);
        IsDropDownOpen = false;
    }

    public DropDown()
    {
        SelectionChanged += (_, _) => UpdateDisplayContent();
        MouseWheel += OnHeaderMouseWheel;
    }

    // Wheel over the CLOSED drop-down steps the selection (WPF ComboBox behaviour): down = next item, up = previous.
    // When the list is open we leave the event alone so the popup can scroll instead.
    private void OnHeaderMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!IsEnabled || IsDropDownOpen) return;
        var count = Items.Count;
        if (count == 0) return;
        var from = SelectedIndex < 0 ? (e.Delta < 0 ? -1 : count) : SelectedIndex;   // start at an edge when unset
        var next = Math.Clamp(from + (e.Delta < 0 ? 1 : -1), 0, count - 1);
        if (next == SelectedIndex) return;   // already at that end of the list: leave the wheel unhandled so the page
                                             // under the cursor keeps scrolling - the same chaining rule ScrollViewer
                                             // follows at its own edge. Swallowing it here dead-ended the wheel on
                                             // every drop-down it passed over.
        SelectedIndex = next;
        e.Handled = true;
    }

    /// <summary>Whether the popup list is open. Toggled by clicking the header; set false on pick or outside click.</summary>
    public bool IsDropDownOpen
    {
        get => GetValue<bool>(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    /// <summary>Cap on the popup list height; a longer list scrolls. Default 320.</summary>
    public double MaxDropDownHeight
    {
        get => GetValue<double>(MaxDropDownHeightProperty);
        set => SetValue(MaxDropDownHeightProperty, value);
    }

    /// <summary>Shown in the header when nothing is selected (a hint like "Select...").</summary>
    public object Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>What the header presents: <see cref="Selector.SelectedItem"/>, else <see cref="Placeholder"/>. Read-only.</summary>
    public object DisplayContent
    {
        get => GetValue(DisplayContentProperty);
        private set => SetValue(DisplayContentProperty, value);
    }

    /// <summary>Set to an enum type to auto-fill the list with its values (SelectedItem becomes the chosen enum value).</summary>
    public Type EnumType
    {
        get => GetValue<Type>(EnumTypeProperty);
        set => SetValue(EnumTypeProperty, value);
    }

    public Brush BackgroundPointerOver
    {
        get => GetValue<Brush>(BackgroundPointerOverProperty);
        set => SetValue(BackgroundPointerOverProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        // The list card lives in the popup's lazily-built ChildTemplate, so base.OnApplyTemplate cannot find
        // PART_ItemsPresenter yet - it is connected when the content arrives on first open.
        _popup = GetTemplateChild("PART_Popup") as Popup;
        if (_popup != null)
        {
            _popup.PlacementTarget = this;              // position against the control (== the header)
            _popup.KeepOpen = false;                    // click-outside-to-close, owned by Popup now
            _popup.IgnoreTargetPress = true;            // a header press is handled by us (toggle) - don't dismiss+reopen
            _popup.DataContext = this;                  // the card binds our ActualWidth / MaxDropDownHeight through it
            _popup.Closed -= OnPopupClosed;
            _popup.Closed += OnPopupClosed;
            _popup.ContentBuilt -= OnPopupContentBuilt;
            _popup.ContentBuilt += OnPopupContentBuilt;
            _popup.IsOpen = IsDropDownOpen;
        }
        UpdateDisplayContent();
    }

    /// <summary>Let the template's parts go when the template does - see ScrollBar.OnRemoveTemplate.</summary>
    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();
        if (_popup != null)
        {
            _popup.Closed -= OnPopupClosed;
            _popup.ContentBuilt -= OnPopupContentBuilt;
        }
        _popup = null;
    }

    // The items host only exists once the popup has built its deferred content - connect it then.
    private void OnPopupContentBuilt(object sender, EventArgs e)
    {
        if (((Popup)sender).FindContentChild("PART_ItemsPresenter") is ItemsPresenter presenter)
        {
            ConnectPresenter(presenter);
        }
    }

    // Clicking the header toggles the list. The popup's contents live in the overlay layer (not our visual subtree), so a
    // click on an item is hit-tested there and never reaches this handler - only header clicks do.
    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(sender, e);
        if (!IsEnabled) return;
        e.Handled = true;
        Focus();
        IsDropDownOpen = !IsDropDownOpen;
    }

    // The pointer moved onto a row: the highlight goes WITH it.
    //
    // There is ONE highlight, and it belongs to whichever hand is steering. Before this the arrows had it and the mouse
    // had a separate hover state of its own, so a menu opened with the arrows on its current value and the pointer over
    // some other row showed TWO rows marked - and a theme cannot resolve that, because neither row knows the other
    // exists. Every menu on every platform behaves this way: moving the mouse moves the keyboard's place too, so that
    // pressing Enter after wandering with the pointer commits what is under it rather than something forgotten.
    internal void HighlightFromContainer(DropDownItem container)
    {
        var index = ItemContainerGenerator.IndexFromContainer(container);
        if (index >= 0) Highlight(index);
    }

    // A row was clicked: select its item and dismiss.
    internal void SelectFromContainer(DropDownItem container)
    {
        var index = ItemContainerGenerator.IndexFromContainer(container);
        if (index >= 0) SelectSingle(index);
        IsDropDownOpen = false;
    }

    // If a template (or selector) is applied AFTER the selection was formatted (property order in markup is not
    // guaranteed), DisplayContent could hold a stale friendly-string that the template would then try to bind against.
    // Re-format the header whenever the item template or its selector changes.
    protected override void OnPropertyChanged(AdamantiumPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ItemTemplateProperty || e.Property == ItemTemplateSelectorProperty)
            UpdateDisplayContent();
    }

    private void UpdateDisplayContent() => DisplayContent = SelectedItem != null ? FormatForDisplay(SelectedItem) : Placeholder;

    // What actually shows for an item. With a user ItemTemplate the raw item flows through (the template formats it). With
    // no template, an ENUM value shows its friendly name from [Display(Name)] / [Description] - the thing WPF ignored - so
    // an enum-bound dropdown reads well out of the box while SelectedItem stays the real enum value. Anything else ToStrings.
    private object FormatForDisplay(object item)
    {
        // An item authored as its own container is a CONTROL, and a control can only be in one place: handing it to the
        // header takes the row OUT of the list. Measured while arrowing through an open list - the highlighted row
        // vanished for a frame and the header jumped to its size. Show what the row holds, not the row itself.
        if (item is DropDownItem container) item = container.Content;

        // A template (fixed OR selector-chosen) renders the raw item itself, so pass it through untouched - flattening an
        // enum to its friendly string here would hand the template/selector a string instead of the real value.
        if (ItemTemplate != null || ItemTemplateSelector != null || item is not Enum e) return item;
        var field = e.GetType().GetField(e.ToString());
        if (field != null)
        {
            if (field.GetCustomAttribute<DisplayAttribute>()?.GetName() is { } displayName) return displayName;
            if (field.GetCustomAttribute<DescriptionAttribute>()?.Description is { } description) return description;
        }
        return e.ToString();
    }

    private static void OnPlaceholderChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
        => ((DropDown)a).UpdateDisplayContent();

    private static void OnEnumTypeChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is DropDown dd && e.NewValue is Type { IsEnum: true } enumType)
            dd.ItemsSource = Enum.GetValues(enumType);
    }

    private static void OnIsDropDownOpenChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        var dd = (DropDown)a;
        var open = (bool)e.NewValue;
        if (dd._popup != null) dd._popup.IsOpen = open;

        // The list always opens on the current value and closes with no highlight left behind - one place for it,
        // whether it was opened by the keyboard, by a click on the header, or set from code.
        if (open) dd.Highlight(dd.SelectedIndex);
        else dd.Highlight(-1);
    }

    // The popup light-dismissed (a click outside the control + list) - reflect it so the next header click reopens.
    private void OnPopupClosed(object sender, EventArgs e) => IsDropDownOpen = false;

    // --- Container seam: items host as DropDownItem in the popup list ----------------------------------------------

    protected internal override bool IsItemItsOwnContainer(object item) => item is DropDownItem;

    protected internal override IUIComponent GetContainerForItem(object item)
    {
        var container = new DropDownItem { Owner = this };   // back-ref: the popup detaches the container's visual tree
        
        if (ItemContainerStyle != null) 
            container.AttachStyles(ItemContainerStyle);
        
        return container;
    }

    protected internal override void PrepareContainer(IUIComponent container, object item)
    {
        if (container is DropDownItem row && !ReferenceEquals(row, item))
        {
            row.DataContext = item;
            row.ContentTemplate = ItemTemplate;
            row.ContentTemplateSelector = ItemTemplateSelector;
            row.Content = FormatForDisplay(item);   // friendly enum name when no ItemTemplate; raw item otherwise
            ApplyContainerSelection(row, item);
            // Rows are made when the popup opens, i.e. AFTER the highlight was set - so a container asks for its own
            // state on arrival rather than the highlight reaching for containers that do not exist yet.
            row.IsHighlighted = Items.IndexOf(item) == _highlightedIndex;
        }
    }

    protected internal override void ClearContainer(IUIComponent container)
    {
        if (container is DropDownItem row)
        {
            row.DataContext = null;
            row.IsSelected = false;
            row.IsHighlighted = false;   // containers are recycled: a stale highlight would follow one into another row
        }
    }
}
