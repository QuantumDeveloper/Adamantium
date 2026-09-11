using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>The strip on the far left that opens and shuts a record's details panel - one of these per row, in a column
/// of its own.
/// <para>Its own column, and not a second meaning on the tree's expander: a row can have children AND a panel, and one
/// control that opened whichever the row happened to have would leave no way to ask for the other. Two questions, two
/// places to press.</para>
/// <para>It is pinned like the number strip and stands still while the columns scroll under it - a handle that slides
/// away from the row it belongs to is a handle nobody can hit.</para></summary>
public class DataGridRowDetailsToggle : ContentControl
{
    /// <summary>Whether this row's panel is open - the theme turns the sign from + to − on it.</summary>
    public static readonly AdamantiumProperty IsOpenProperty = AdamantiumProperty.Register(nameof(IsOpen),
        typeof(bool), typeof(DataGridRowDetailsToggle),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    /// <summary>This row has no panel to open: a group's caption, or the panel row itself. It keeps the column's width -
    /// a strip that closed up under some rows would make the table's left edge ragged - and shows no sign at all.</summary>
    public static readonly AdamantiumProperty IsBlankProperty = AdamantiumProperty.Register(nameof(IsBlank),
        typeof(bool), typeof(DataGridRowDetailsToggle),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    public bool IsOpen
    {
        get => GetValue<bool>(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public bool IsBlank
    {
        get => GetValue<bool>(IsBlankProperty);
        set => SetValue(IsBlankProperty, value);
    }
}
