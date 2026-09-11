using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>One column's total, in the strip under the table. A control of its own so a theme can set it apart from a
/// cell - a total is a conclusion, not another value.</summary>
public class DataGridFooterCell : ContentControl
{
    /// <summary>Whether this column asked for a total at all. A column without one leaves its place in the strip blank
    /// rather than borrowing the neighbour's number.</summary>
    public static readonly AdamantiumProperty HasTotalProperty = AdamantiumProperty.Register(nameof(HasTotal),
        typeof(bool), typeof(DataGridFooterCell),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    public bool HasTotal
    {
        get => GetValue<bool>(HasTotalProperty);
        set => SetValue(HasTotalProperty, value);
    }
}
