using System.Collections.Generic;
using Adamantium.UI.Controls.Panels;

namespace Adamantium.UI.Controls;

/// <summary>What the user did to ONE column, in a form that can be written to a file and read back. Plain values only -
/// no brushes, no bindings, no column - because this is what outlives the page: the application saves it and hands it
/// back on the next run.</summary>
public sealed class DataGridColumnState
{
    /// <summary>Which column this is about - <see cref="DataGridColumn.StateKey"/>.</summary>
    public string Key { get; set; }

    /// <summary>Where it stood among the columns.</summary>
    public int Order { get; set; }

    public bool IsVisible { get; set; } = true;

    /// <summary>The width, said as a NUMBER and a KIND rather than as the text a markup width uses ("Auto", "2*"):
    /// that text is parsed with the current culture, so a layout saved where the decimal separator is a comma would
    /// not read back where it is a point.</summary>
    public double WidthValue { get; set; }

    public GridUnitType WidthKind { get; set; } = GridUnitType.Auto;

    public DataGridFrozenSide FrozenSide { get; set; } = DataGridFrozenSide.None;
}

/// <summary>The whole arrangement of a table's columns - what is shown, in what order, how wide, what is pinned and
/// what it is sorted by - as ONE value the application can persist.
/// <para>Saving it is what makes choosing columns worth anything: without it the user hides the same columns on every
/// run. WHERE it is saved is the application's business - a file, a profile, a server - so this carries no format and
/// no serializer of its own; every member is a plain value that any of them can write.</para></summary>
public sealed class DataGridColumnsState
{
    public List<DataGridColumnState> Columns { get; set; } = new();

    /// <summary>The key of the column the table was sorted by, or null for no sort.</summary>
    public string SortKey { get; set; }

    public bool SortDescending { get; set; }
}
