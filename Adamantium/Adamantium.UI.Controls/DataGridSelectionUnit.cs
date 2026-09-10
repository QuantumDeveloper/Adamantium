namespace Adamantium.UI.Controls;

/// <summary>What a click takes: one cell, or the whole row it is in.</summary>
public enum DataGridSelectionUnit
{
    /// <summary>The cell under the pointer - a spreadsheet. Rows can still be taken whole by their number.</summary>
    Cell,

    /// <summary>The whole row, wherever in it the click landed - a list of records rather than a sheet of values.</summary>
    FullRow
}
