using System;
using System.Collections.Generic;

namespace Adamantium.UI.Controls;

/// <summary>Turns the columns' declared widths into pixels ONCE per pass, for the whole grid - two independent
/// calculations are how a table's header and body drift apart. Fixed take their own, Auto take the widest realized
/// cell, the stars share what is left; with nothing left the stars fall back to MinWidth and the grid scrolls.</summary>
internal static class DataGridColumnLayout
{
    /// <summary>Writes <see cref="DataGridColumn.ActualWidth"/> and <see cref="DataGridColumn.Offset"/> on every column
    /// and returns the total. <paramref name="frozenWidth"/> comes back as the width of the LEFT pinned zone,
    /// <paramref name="rightFrozenWidth"/> the same for the right edge - returned rather than remembered, because this
    /// class serves every grid in the application and holds no state of any of them.</summary>
    public static double Arrange(IReadOnlyList<DataGridColumn> columns, double available, out double frozenWidth,
        out double rightFrozenWidth, double leading = 0)
    {
        frozenWidth = leading;
        rightFrozenWidth = 0;
        if (columns == null || columns.Count == 0) return leading;

        double fixedAndAuto = 0;
        double starWeight = 0;

        foreach (var column in columns)
        {
            // A column that is not shown takes no width and no share of the stars - see DataGridColumn.IsShown. It
            // still gets an offset below, so anything holding an index keeps finding it where the columns around it
            // are; it is simply a column of zero width that nothing realizes.
            if (!column.IsShown)
            {
                column.ActualWidth = 0;
                continue;
            }

            var width = column.Width;
            if (width.IsStar)
            {
                starWeight += Math.Max(0.0001, width.Value);
                continue;
            }

            var pixels = width.IsAuto ? column.MeasuredWidth : width.Value;
            column.ActualWidth = Clamp(column, pixels);
            fixedAndAuto += column.ActualWidth;
        }

        // The stars share whatever the named widths left behind. An unbounded pass has no remainder to speak of, and
        // neither does one where the fixed and Auto columns already overflow - both settle the stars at their minimum.
        var remainder = double.IsInfinity(available) ? 0 : Math.Max(0, available - fixedAndAuto);
        foreach (var column in columns)
        {
            if (!column.Width.IsStar || !column.IsShown) continue;
            var share = starWeight > 0 ? remainder * Math.Max(0.0001, column.Width.Value) / starWeight : 0;
            column.ActualWidth = Clamp(column, share);
        }

        // PINNED COLUMNS TAKE THEIR ZONE, in declared order, so "frozen" is a fact of the LAYOUT and not something
        // every reader of Offset has to remember. The row-number strip is the head of the left zone.
        var offset = leading;
        foreach (var column in columns)
        {
            if (!column.IsFrozenLeft) continue;

            column.Offset = offset;
            offset += column.ActualWidth;
        }

        var frozen = offset;
        foreach (var column in columns)
        {
            if (column.IsFrozen) continue;

            column.Offset = offset;
            offset += column.ActualWidth;
        }

        // The RIGHT zone is laid out at the END of the content, which is where it stands when the table is scrolled
        // fully across. Everywhere else the grid slides it back to the viewport's edge by ONE number
        // (TreeDataGrid.RightPinShift), so this pass keeps saying what it says everywhere: an offset in content space.
        var scrolling = offset;
        foreach (var column in columns)
        {
            if (!column.IsFrozenRight) continue;

            column.Offset = offset;
            offset += column.ActualWidth;
        }

        frozenWidth = frozen;
        rightFrozenWidth = offset - scrolling;
        return offset;
    }

    /// <summary>Records what a realized cell needed, for the Auto columns. Only ever GROWS within a scroll - recomputing
    /// downwards each pass makes the columns breathe under the pointer; <see cref="DataGridColumn.ResetMeasuredWidth"/>
    /// is the way back. Returns whether anything grew.</summary>
    public static bool RecordMeasured(DataGridColumn column, double desired)
    {
        if (!column.Width.IsAuto || !(desired > column.MeasuredWidth)) return false;
        column.MeasuredWidth = desired;
        return true;
    }

    private static double Clamp(DataGridColumn column, double value)
    {
        var min = double.IsNaN(column.MinWidth) ? 0 : Math.Max(0, column.MinWidth);
        var max = double.IsNaN(column.MaxWidth) ? double.PositiveInfinity : column.MaxWidth;
        if (max < min) max = min;
        return Math.Clamp(double.IsNaN(value) ? 0 : value, min, max);
    }
}
