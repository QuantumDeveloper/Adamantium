using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>Whether a cell's value is acceptable, and what to say when it is not. A column names one; every row is asked
/// it, so a rule holds no per-row state.
/// <para>A <see cref="FundamentalUIComponent"/> so a rule written in markup can carry bound properties of its own - a
/// bound maximum, a limit read off the page - exactly as a column does.</para>
/// <para>It returns a MESSAGE, not a colour: what "invalid" looks like is the theme's business and the grid's
/// <see cref="TreeDataGrid.ValidationErrorBrush"/>, and a rule that handed out brushes would be wrong the moment the
/// theme changed. Null (or empty) means the value is fine.</para></summary>
public abstract class DataGridValidationRule : FundamentalUIComponent
{
    /// <summary>What is wrong with <paramref name="value"/>, or null when nothing is. <paramref name="item"/> is the
    /// whole record, so a rule can weigh one field against another.</summary>
    public abstract string Validate(object value, object item);
}
