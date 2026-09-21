namespace Adamantium.UI.Controls.DataGrid;

/// <summary>What the table does with a value its rules refuse. Not one behaviour, because the right one depends on the
/// application: a form being filled in has to let a half-finished record stand and say what is wrong with it; a ledger
/// must not take a figure that cannot be true.</summary>
public enum DataGridValidationMode
{
    /// <summary>Write it and MARK it. The record carries its own fault, the table stays usable, and nothing traps the
    /// user in a cell they may not know how to satisfy.</summary>
    Mark,

    /// <summary>Refuse the write and keep the editor open until the value is acceptable. The cell says why for as long
    /// as it holds on.</summary>
    Block
}
