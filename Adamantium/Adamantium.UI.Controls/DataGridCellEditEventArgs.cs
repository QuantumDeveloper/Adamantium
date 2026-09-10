using System;

namespace Adamantium.UI.Controls;

/// <summary>A cell is about to be edited, or about to stop being edited. <see cref="Cancel"/> refuses it - the same
/// shape as TabControl's close request, and for the same reason: the application, not the control, knows whether the
/// thing may happen.</summary>
public class DataGridCellEditEventArgs : EventArgs
{
    public DataGridCellEditEventArgs(object row, DataGridColumn column, object value = null)
    {
        Row = row;
        Column = column;
        Value = value;
    }

    public object Row { get; }

    public DataGridColumn Column { get; }

    /// <summary>On ending: what the editor produced, and what will be written unless this is cancelled.</summary>
    public object Value { get; set; }

    public bool Cancel { get; set; }
}
