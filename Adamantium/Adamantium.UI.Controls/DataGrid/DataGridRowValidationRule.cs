using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.DataGrid;

/// <summary>Whether a whole RECORD is acceptable, and what to say when it is not - for what no single cell can answer:
/// a "from" later than a "to", parts that must add up to a total, a field required only when another is set. The fault
/// belongs to none of the cells involved, so it is not marked on one of them.
/// <para>A <see cref="FundamentalUIComponent"/> for the same reason a cell rule is: a rule written in markup can carry
/// bound properties of its own.</para>
/// <para>It returns a MESSAGE, not a colour - see <see cref="DataGridValidationRule"/>. Null (or empty) means the
/// record is fine.</para></summary>
public abstract class DataGridRowValidationRule : FundamentalUIComponent
{
    /// <summary>What is wrong with <paramref name="item"/> taken as a whole, or null when nothing is.</summary>
    public abstract string Validate(object item);
}
