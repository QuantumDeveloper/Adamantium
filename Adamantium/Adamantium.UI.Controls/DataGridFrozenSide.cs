namespace Adamantium.UI.Controls;

/// <summary>Which edge of the table a column is pinned to, if any.
/// <para>A SIDE rather than a flag, because "pinned" without an edge is only half an answer: a wide table wants its
/// key on the left and its totals or its actions on the right, and both must stand still while the middle scrolls.</para></summary>
public enum DataGridFrozenSide
{
    /// <summary>Scrolls with the rest of the table.</summary>
    None,

    /// <summary>Pinned to the LEFT edge - the identifying columns, so a row can still be told apart when it is
    /// scrolled far to the right.</summary>
    Left,

    /// <summary>Pinned to the RIGHT edge - totals, status, the buttons that act on the row.</summary>
    Right
}
