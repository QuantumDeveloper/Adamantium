using System;
using System.Collections.Generic;
using System.Globalization;

namespace Adamantium.UI.Controls;

/// <summary>Computes a column's total over a set of values - the one place the footer and every group header ask, so a
/// grand total and a group total can never be worked out two different ways.</summary>
internal static class DataGridAggregates
{
    /// <summary>The total, or null when there is nothing to show: no aggregate asked for, no rows, or a numeric
    /// aggregate over a column that holds nothing numeric. Null rather than zero - a column of names has no sum, and
    /// printing 0 would be an answer where there is none.</summary>
    public static object Compute(DataGridAggregate aggregate, IEnumerable<object> values)
    {
        if (aggregate == DataGridAggregate.None || values == null) return null;

        if (aggregate == DataGridAggregate.Count)
        {
            var rows = 0;
            foreach (var _ in values) rows++;
            return rows;
        }

        if (aggregate is DataGridAggregate.Min or DataGridAggregate.Max) return Extreme(aggregate, values);

        var wanted = aggregate == DataGridAggregate.Average;
        double total = 0;
        var counted = 0;

        foreach (var value in values)
        {
            if (!TryNumber(value, out var number)) continue;

            total += number;
            counted++;
        }

        if (counted == 0) return null;

        return wanted ? total / counted : total;
    }

    // BY THE SAME COMPARISON THE SORT USES, so the smallest of a date column is a date and the smallest of a name
    // column is a name - a numeric-only Min would answer nothing on either.
    private static object Extreme(DataGridAggregate aggregate, IEnumerable<object> values)
    {
        object best = null;
        var wantsLarger = aggregate == DataGridAggregate.Max;

        foreach (var value in values)
        {
            if (value == null) continue;

            if (best == null)
            {
                best = value;
                continue;
            }

            var order = Compare(value, best);
            if (wantsLarger ? order > 0 : order < 0) best = value;
        }

        return best;
    }

    private static int Compare(object a, object b) =>
        a is IComparable comparable && a.GetType() == b.GetType()
            ? comparable.CompareTo(b)
            : string.Compare(a.ToString(), b.ToString(), StringComparison.CurrentCulture);

    // A cell's value arrives as whatever the binding produced - a boxed int, a decimal, the string a converter made.
    // The string is worth trying: a column that formats its numbers still has numbers in it.
    private static bool TryNumber(object value, out double number)
    {
        switch (value)
        {
            case null:
                number = 0;
                return false;
            case double d: number = d; return true;
            case float f: number = f; return true;
            case decimal m: number = (double)m; return true;
            case int i: number = i; return true;
            case long l: number = l; return true;
            case short s: number = s; return true;
            case byte b: number = b; return true;
            case uint ui: number = ui; return true;
            case ulong ul: number = ul; return true;
            default:
                return double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.CurrentCulture, out number)
                       || double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out number);
        }
    }
}
