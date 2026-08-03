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
        if (next != SelectedIndex) SelectedIndex = next;
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
        _popup = GetTemplateChild("PART_Popup") as Popup;
        if (_popup != null)
        {
            _popup.PlacementTarget = this;              // position against the control (== the header)
            _popup.KeepOpen = false;                    // click-outside-to-close, owned by Popup now
            _popup.IgnoreTargetPress = true;            // a header press is handled by us (toggle) - don't dismiss+reopen
            _popup.Closed -= OnPopupClosed;
            _popup.Closed += OnPopupClosed;
            _popup.IsOpen = IsDropDownOpen;
        }
        UpdateDisplayContent();
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
        if (dd._popup != null) dd._popup.IsOpen = (bool)e.NewValue;
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
        }
    }

    protected internal override void ClearContainer(IUIComponent container)
    {
        if (container is DropDownItem row)
        {
            row.DataContext = null;
            row.IsSelected = false;
        }
    }
}
