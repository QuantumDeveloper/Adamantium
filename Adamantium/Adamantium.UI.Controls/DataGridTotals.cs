using System;
using System.Globalization;

namespace Adamantium.UI.Controls;

/// <summary>Writes a total out. One place, so the strip under the table and the header of a group phrase the same
/// number the same way.</summary>
internal static class DataGridTotals
{
    public static string Text(DataGridColumn column, object total)
    {
        if (column == null || total == null || column.Aggregate == DataGridAggregate.None) return null;

        if (column.AggregateFormat is { Length: > 0 } format)
        {
            return string.Format(CultureInfo.CurrentCulture, format, total);
        }

        // A sum and an average come back as doubles even over a column of whole numbers; printing 8711.00000001 for a
        // column that shows 8711 helps nobody, so the default rounds while an explicit format still wins.
        return total is double number
            ? number.ToString(Math.Abs(number % 1) < 0.0005 ? "N0" : "N2", CultureInfo.CurrentCulture)
            : Convert.ToString(total, CultureInfo.CurrentCulture);
    }
}
