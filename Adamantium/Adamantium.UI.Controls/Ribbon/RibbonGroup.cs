using System;
using System.Collections.Generic;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>A named cluster of commands inside a <see cref="RibbonTab"/>, its caption under them. Items are the
/// commands, laid out in columns by <see cref="Panels.RibbonGroupPanel"/>.</summary>
public class RibbonGroup : ItemsControl, IHeaderedItemsControl
{
    public static readonly AdamantiumProperty HeaderProperty = AdamantiumProperty.Register(nameof(Header),
        typeof(object), typeof(RibbonGroup), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty HeaderTemplateProperty = AdamantiumProperty.Register(nameof(HeaderTemplate),
        typeof(DataTemplate), typeof(RibbonGroup), new PropertyMetadata(null));

    /// <summary>Whether the rule dividing this group from the next is drawn. Maintained by the owning
    /// <see cref="RibbonTab"/>, which turns it off on the LAST group.</summary>
    public static readonly AdamantiumProperty ShowSeparatorProperty = AdamantiumProperty.Register(nameof(ShowSeparator),
        typeof(bool), typeof(RibbonGroup), new PropertyMetadata(true, PropertyMetadataOptions.AffectsMeasure));

    /// <summary>The group's caption, drawn under its commands.</summary>
    public object Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public bool ShowSeparator
    {
        get => GetValue<bool>(ShowSeparatorProperty);
        set => SetValue(ShowSeparatorProperty, value);
    }

    public DataTemplate HeaderTemplate
    {
        get => GetValue<DataTemplate>(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }

    // The packing panel derives the variants, but goes out of reach when the group collapses - so what is needed to
    // un-collapse it is kept here.
    private Panels.RibbonGroupPanel Packing => ItemsHostPanel as Panels.RibbonGroupPanel;

    private IReadOnlyList<RibbonGroupVariant> _variants = [];
    private double[] _widths;

    /// <summary>The ways this group can be drawn, roomiest first; empty until it has been templated.</summary>
    public IReadOnlyList<RibbonGroupVariant> Variants
    {
        get
        {
            var live = Packing?.Variants;
            if (live is { Count: > 0 } && !ReferenceEquals(live, _variants))
            {
                _variants = live;
                _widths = null;
            }

            return _variants;
        }
    }

    /// <summary>How wide this GROUP is at <paramref name="index"/> - its commands plus its own chrome. The panel knows
    /// only what the commands cost.</summary>
    public double WidthAt(int index)
    {
        var variants = Variants;
        if (variants.Count == 0) return double.NaN;
        if (index == variants.Count - 1) return CollapsedWidth;

        // Asked fresh - the panel caches and drops these itself. What is kept here is only the fallback for a collapsed
        // group, which has no panel to ask. Re-sized because the steps are rebuilt when the commands change.
        if (_widths is null || _widths.Length != variants.Count) _widths = NotMeasured(variants.Count);

        // Not probed while collapsed: the panel is inside the flyout, and measuring it re-lays out the overlay, which
        // lands back here - the flyout rebuilt itself without end.
        if (IsCollapsed) return _widths[index];

        var packed = Packing?.WidthAt(index) ?? double.NaN;
        if (double.IsNaN(packed)) return _widths[index];

        _widths[index] = packed + Chrome;
        return _widths[index];
    }

    // Padding and the dividing rule. Captured DURING the measure: read on demand, the two sizes come from different
    // variants while the search walks them, and their difference is not the chrome.
    private double _chrome;

    private double Chrome => _chrome;

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = base.MeasureOverride(availableSize);

        if (Packing is { } packing && packing.DesiredSize.Width > 0)
        {
            _chrome = Math.Max(0, size.Width - packing.DesiredSize.Width);
        }

        return size;
    }

    /// <summary>Draw this group as <paramref name="index"/> from now on.</summary>
    public void ApplyVariant(int index)
    {
        _current = index;

        if (IsCollapsedVariant(index))
        {
            IsCollapsed = true;
            return;
        }

        IsCollapsed = false;
        Packing?.Apply(index);
    }

    private int _current;

    private Decorators.Decorator _inlineHost;
    private Decorators.Decorator _popupHost;
    private IMeasurableComponent _content;
    private Popup _popup;
    private Primitives.ToggleButton _collapsedButton;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _inlineHost = GetTemplateChild("PART_InlineHost") as Decorators.Decorator;
        _popupHost = GetTemplateChild("PART_PopupHost") as Decorators.Decorator;
        _content = GetTemplateChild("PART_Content") as IMeasurableComponent;
        _popup = GetTemplateChild("PART_Popup") as Popup;

        if (_popup != null)
        {
            _popup.PlacementTarget = this;
            _popup.KeepOpen = false;          // click outside puts the flyout away, as any other does

            // ...but the button owns a press on itself: without this the flyout dismissed on the press and the toggle
            // re-opened it on the release, so a second click appeared to do nothing.
            _popup.IgnoreTargetPress = true;
            _popup.Closed -= OnFlyoutClosed;
            _popup.Closed += OnFlyoutClosed;
        }

        if (_collapsedButton != null) _collapsedButton.Click -= OnCollapsedButtonClick;
        _collapsedButton = GetTemplateChild("PART_CollapsedButton") as Primitives.ToggleButton;
        if (_collapsedButton != null) _collapsedButton.Click += OnCollapsedButtonClick;

        // A new template starts at the roomiest sizes, so the choice already made has to be re-stated.
        if (_current > 0) Packing?.Apply(Math.Min(_current, Variants.Count - 1));
        HostContent();
    }

    // Collapsing MOVES the content rather than swapping the template - that is what keeps the packing panel, and the
    // variants and widths it holds, alive across a collapse.
    private void HostContent()
    {
        if (_content == null) return;

        if (IsCollapsed)
        {
            if (_popupHost != null) _popupHost.Child = _content;

            // The flyout has room to spare, so the group is drawn there the way its author asked.
            Packing?.Apply(0);
            return;
        }

        // ...and back to the variant the band chose for it.
        Packing?.Apply(Math.Min(Math.Max(_current, 0), Variants.Count - 1));

        if (_inlineHost != null) _inlineHost.Child = _content;
    }

    private void OnCollapsedButtonClick(object sender, RoutedEventArgs e)
    {
        if (_popup != null) _popup.IsOpen = _collapsedButton?.IsChecked == true;
    }

    private void OnFlyoutClosed(object sender, EventArgs e)
    {
        if (_collapsedButton != null) _collapsedButton.IsChecked = false;
    }

    /// <summary>The variant currently drawn - 0 while there is room for everything.</summary>
    public int CurrentVariant
    {
        get
        {
            var variants = Variants;
            if (variants.Count == 0) return 0;

            // Clamped: the steps are rebuilt when the commands change.
            return Math.Min(Math.Max(_current, 0), variants.Count - 1);
        }
    }

    private bool IsCollapsedVariant(int index)
    {
        var variants = Variants;
        return variants.Count > 0 && index == variants.Count - 1;
    }

    private static double[] NotMeasured(int count)
    {
        var widths = new double[count];
        Array.Fill(widths, double.NaN);
        return widths;
    }

    /// <summary>One button now, its commands in the flyout it opens. Decided by the row of groups, never by an author.</summary>
    public static readonly AdamantiumProperty IsCollapsedProperty = AdamantiumProperty.RegisterReadOnly(nameof(IsCollapsed),
        typeof(bool), typeof(RibbonGroup),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange,
            OnIsCollapsedChanged));

    private static void OnIsCollapsedChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not RibbonGroup group) return;

        // Put the flyout away on the way back: a group that is no longer a button has nothing to drop.
        if (Equals(e.NewValue, false) && group._popup != null) group._popup.IsOpen = false;
        group.HostContent();
    }

    public bool IsCollapsed
    {
        get => GetValue<bool>(IsCollapsedProperty);
        private set => SetValue(IsCollapsedProperty, value);
    }

    /// <summary>Whether the collapsed group's flyout is showing.</summary>
    public static readonly AdamantiumProperty IsDropDownOpenProperty = AdamantiumProperty.Register(nameof(IsDropDownOpen),
        typeof(bool), typeof(RibbonGroup), new PropertyMetadata(false, PropertyMetadataOptions.BindsTwoWayByDefault));

    public bool IsDropDownOpen
    {
        get => GetValue<bool>(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    /// <summary>What the collapsed button is worth in width - a THEME metric, not a measurement.</summary>
    public static readonly AdamantiumProperty CollapsedWidthProperty = AdamantiumProperty.Register(nameof(CollapsedWidth),
        typeof(double), typeof(RibbonGroup), new PropertyMetadata(64.0, PropertyMetadataOptions.AffectsMeasure));

    public double CollapsedWidth
    {
        get => GetValue<double>(CollapsedWidthProperty);
        set => SetValue(CollapsedWidthProperty, value);
    }

    /// <summary>What marks the group when collapsed - DATA drawn by <see cref="IconTemplate"/>.</summary>
    public static readonly AdamantiumProperty IconProperty = AdamantiumProperty.Register(nameof(Icon),
        typeof(object), typeof(RibbonGroup), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty IconTemplateProperty = AdamantiumProperty.Register(nameof(IconTemplate),
        typeof(DataTemplate), typeof(RibbonGroup), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    public object Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public DataTemplate IconTemplate
    {
        get => GetValue<DataTemplate>(IconTemplateProperty);
        set => SetValue(IconTemplateProperty, value);
    }

    /// <summary>Which group gives way first. Lower goes first; equals are taken right to left.</summary>
    public static readonly AdamantiumProperty ShrinkPriorityProperty = AdamantiumProperty.Register(nameof(ShrinkPriority),
        typeof(int), typeof(RibbonGroup), new PropertyMetadata(0, PropertyMetadataOptions.AffectsArrange));

    public int ShrinkPriority
    {
        get => GetValue<int>(ShrinkPriorityProperty);
        set => SetValue(ShrinkPriorityProperty, value);
    }
}
