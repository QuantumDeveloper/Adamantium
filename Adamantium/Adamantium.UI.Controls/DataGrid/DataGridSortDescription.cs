using Adamantium.Core.Collections;

namespace Adamantium.UI.Controls.DataGrid;

/// <summary>One key of the sort: a column, and which way it runs. IMMUTABLE - turning a key around replaces it, which
/// is what makes the collection announce the change; a direction quietly flipped in place would reorder nothing.
/// </summary>
public sealed class DataGridSortDescription
{
    public DataGridSortDescription(DataGridColumn column, bool descending = false)
    {
        Column = column;
        Descending = descending;
    }

    /// <summary>The column this key reads.</summary>
    public DataGridColumn Column { get; }

    /// <summary>Whether it runs the other way.</summary>
    public bool Descending { get; }
}

/// <summary>The keys the table is sorted by, in PRIORITY order: the first decides, the next breaks its ties. Empty
/// means the source's own order.</summary>
public class DataGridSortDescriptions : TrackingCollection<DataGridSortDescription>
{
}
