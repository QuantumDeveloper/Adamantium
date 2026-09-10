using System;

namespace Adamantium.UI.Controls;

/// <summary>Which rules a table draws between its cells. The two directions are separate because they say different
/// things: horizontal rules group a row's values into a record, vertical ones line a column up down the page - and a
/// dense table usually wants one of them, not both.</summary>
[Flags]
public enum DataGridGridLines
{
    /// <summary>No rules at all. What a table with banded rows uses - the bands do the separating.</summary>
    None = 0,

    /// <summary>A rule under every row.</summary>
    Horizontal = 1,

    /// <summary>A rule down the right of every column.</summary>
    Vertical = 2,

    /// <summary>Both.</summary>
    All = Horizontal | Vertical
}
