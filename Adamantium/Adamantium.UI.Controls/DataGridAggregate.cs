namespace Adamantium.UI.Controls;

/// <summary>What a column's totals row shows for it.</summary>
public enum DataGridAggregate
{
    /// <summary>Nothing - the column has no total.</summary>
    None,

    /// <summary>How many rows there are. The only one that works on a column of anything.</summary>
    Count,

    /// <summary>The sum of the values. Numbers only; a row whose value is not a number is skipped.</summary>
    Sum,

    /// <summary>The smallest value, by the same comparison the sort uses - so a date or a string has one too.</summary>
    Min,

    /// <summary>The largest value, by the same comparison the sort uses.</summary>
    Max,

    /// <summary>The mean of the values. Numbers only, like <see cref="Sum"/>.</summary>
    Average
}
