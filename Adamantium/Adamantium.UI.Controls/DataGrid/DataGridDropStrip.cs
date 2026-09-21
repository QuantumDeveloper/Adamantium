namespace Adamantium.UI.Controls.DataGrid;

/// <summary>Where a carried column header would land. A header can be put down in more than one band now, so the
/// question is WHICH rather than whether - two booleans would have let both be true at once.</summary>
public enum DataGridDropStrip
{
    /// <summary>Back among the headers, which is where a carry that hits no strip ends.</summary>
    None,

    /// <summary>The strip that groups.</summary>
    Grouping,

    /// <summary>The strip that sorts.</summary>
    Sorting
}
