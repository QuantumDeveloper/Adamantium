using System;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>Which way the content opens, and therefore where the header sits.</summary>
public enum ExpandDirection
{
    /// <summary>Header on top, content below it - what a section of a form or an inspector is.</summary>
    Down,

    /// <summary>Header at the bottom, content above it - a drawer rising from an edge.</summary>
    Up,

    /// <summary>Header on the right, content to its left.</summary>
    Left,

    /// <summary>Header on the left, content to its right - a side rail that opens sideways.</summary>
    Right
}

/// <summary>A header with content that folds away under it. The unit a settings page, a tool panel and a property
/// inspector are all built out of.
/// <para>Collapsed, the content is not measured at all - the theme collapses the host rather than hiding it - so a page
/// of thirty folded sections costs thirty headers, not thirty pages.</para>
/// <para>Fully templated: PART_Header (pressed to toggle - the whole header, not just the glyph) and PART_Content.</para></summary>
public class Expander : ContentControl
{
    public static readonly AdamantiumProperty HeaderProperty = AdamantiumProperty.Register(nameof(Header),
        typeof(object), typeof(Expander), new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty HeaderTemplateProperty = AdamantiumProperty.Register(
        nameof(HeaderTemplate), typeof(DataTemplate), typeof(Expander),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsMeasure));

    public static readonly AdamantiumProperty IsExpandedProperty = AdamantiumProperty.Register(nameof(IsExpanded),
        typeof(bool), typeof(Expander),
        new PropertyMetadata(false, PropertyMetadataOptions.BindsTwoWayByDefault | PropertyMetadataOptions.AffectsMeasure,
            OnIsExpandedChanged));

    public static readonly AdamantiumProperty ExpandDirectionProperty = AdamantiumProperty.Register(
        nameof(ExpandDirection), typeof(ExpandDirection), typeof(Expander),
        new PropertyMetadata(ExpandDirection.Down, PropertyMetadataOptions.AffectsMeasure));

    /// <summary>Whether the header can be folded at all. A section that must stay open still wants its header.</summary>
    public static readonly AdamantiumProperty CanCollapseProperty = AdamantiumProperty.Register(nameof(CanCollapse),
        typeof(bool), typeof(Expander), new PropertyMetadata(true));

    private IInputComponent _header;

    static Expander()
    {
        FocusableProperty.OverrideMetadata(typeof(Expander), new PropertyMetadata(true));
    }

    /// <summary>What the header shows. A string, or anything <see cref="HeaderTemplate"/> knows how to draw.</summary>
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

    /// <summary>Open or folded. Two-way bindable: a view-model that remembers which sections its user left open is the
    /// ordinary case, and the control must not be the one holding that memory.</summary>
    public bool IsExpanded
    {
        get => GetValue<bool>(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public ExpandDirection ExpandDirection
    {
        get => GetValue<ExpandDirection>(ExpandDirectionProperty);
        set => SetValue(ExpandDirectionProperty, value);
    }

    public bool CanCollapse
    {
        get => GetValue<bool>(CanCollapseProperty);
        set => SetValue(CanCollapseProperty, value);
    }

    /// <summary>Raised after the content opens.</summary>
    public event EventHandler Expanded;

    /// <summary>Raised after the content folds away.</summary>
    public event EventHandler Collapsed;

    /// <summary>Flips <see cref="IsExpanded"/>, unless <see cref="CanCollapse"/> says the section stays open. True when
    /// the state actually changed.</summary>
    public bool Toggle()
    {
        if (!CanCollapse && IsExpanded) return false;

        IsExpanded = !IsExpanded;
        return true;
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_header != null) _header.MouseLeftButtonDown -= OnHeaderPressed;
        _header = GetTemplateChild("PART_Header") as IInputComponent;
        if (_header != null) _header.MouseLeftButtonDown += OnHeaderPressed;
    }

    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();

        if (_header != null) _header.MouseLeftButtonDown -= OnHeaderPressed;
        _header = null;
    }

    /// <summary>Space and Enter fold the section, as they do on any header that is a button in all but name.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsEnabled) return;

        if (e.Key is Key.Space or Key.Enter) e.Handled = Toggle();
    }

    private static void OnIsExpandedChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (d is not Expander expander) return;

        if (expander.IsExpanded) expander.Expanded?.Invoke(expander, EventArgs.Empty);
        else expander.Collapsed?.Invoke(expander, EventArgs.Empty);
    }

    private void OnHeaderPressed(object sender, MouseButtonEventArgs e)
    {
        if (!IsEnabled) return;

        Focus();
        if (Toggle()) e.Handled = true;
    }
}
