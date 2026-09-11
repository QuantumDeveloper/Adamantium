using System;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>The row that stands for a group: its expander, the value the rows under it share, and how many there are.
/// <para>A control of its own, and NOT a cell: a group row belongs to no column, so nothing about it should be read
/// through one.</para></summary>
public class DataGridGroupHeader : ContentControl
{
    /// <summary>The value the rows share.</summary>
    public static readonly AdamantiumProperty KeyProperty = AdamantiumProperty.Register(nameof(Key),
        typeof(object), typeof(DataGridGroupHeader),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender, OnCaptionChanged));

    /// <summary>What the rows were grouped BY - the column's header, so "Region: Iberia" reads as a sentence.</summary>
    public static readonly AdamantiumProperty GroupNameProperty = AdamantiumProperty.Register(nameof(GroupName),
        typeof(String), typeof(DataGridGroupHeader),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender, OnCaptionChanged));

    /// <summary>How many data rows are under it, however deep.</summary>
    public static readonly AdamantiumProperty CountProperty = AdamantiumProperty.Register(nameof(Count),
        typeof(Int32), typeof(DataGridGroupHeader),
        new PropertyMetadata(0, PropertyMetadataOptions.AffectsRender, OnCaptionChanged));

    /// <summary>Whether the group is open. The theme turns the glyph by it.</summary>
    public static readonly AdamantiumProperty IsExpandedProperty = AdamantiumProperty.Register(nameof(IsExpanded),
        typeof(bool), typeof(DataGridGroupHeader),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    public object Key
    {
        get => GetValue(KeyProperty);
        set => SetValue(KeyProperty, value);
    }

    public String GroupName
    {
        get => GetValue<String>(GroupNameProperty);
        set => SetValue(GroupNameProperty, value);
    }

    public Int32 Count
    {
        get => GetValue<Int32>(CountProperty);
        set => SetValue(CountProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue<bool>(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    // The caption IS the content, so a theme lays it out as it lays out any other content and needs to know nothing
    // about groups.
    private static void OnCaptionChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not DataGridGroupHeader header) return;

        var value = DataGridColumnFilter.Text(header.Key);
        if (string.IsNullOrEmpty(value)) value = "(blank)";

        header.Content = header.GroupName is { Length: > 0 } name
            ? $"{name}: {value}  ({header.Count})"
            : $"{value}  ({header.Count})";
    }
}
