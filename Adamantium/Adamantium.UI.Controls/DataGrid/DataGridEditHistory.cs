using System.Collections;
using System.Collections.Specialized;

namespace Adamantium.UI.Controls.DataGrid;

/// <summary>One change the table can take back. What it holds is the CHANGE, never the gesture that caused it: a value
/// written by an editor, by a paste and by a filled-in placeholder are the same change and come back the same way.
/// </summary>
internal abstract class DataGridEdit
{
    public abstract void Undo();

    public abstract void Redo();

    /// <summary>Whether putting this back changes which rows the table HAS, rather than what one of them holds - the
    /// flat list has to be built again for the first and only re-read for the second.</summary>
    public virtual bool IsStructural => false;
}

/// <summary>A value in one cell. Both sides are read off the OBJECT, before and after the write, so putting it back
/// writes exactly what the object held - no second guess at what a converter would have made of the text.</summary>
internal sealed class DataGridValueEdit : DataGridEdit
{
    public DataGridValueEdit(DataGridColumn column, object item, object before, object after)
    {
        _column = column;
        _item = item;
        _before = before;
        _after = after;
    }

    private readonly DataGridColumn _column;
    private readonly object _item;
    private readonly object _before;
    private readonly object _after;

    public override void Undo() => _column.Write(_item, _before);

    public override void Redo() => _column.Write(_item, _after);
}

/// <summary>A row coming or going. The INDEX is kept as well as the item: a record put back at the end of the list is
/// not the record the user took out of the middle of it.</summary>
internal sealed class DataGridRowEdit : DataGridEdit
{
    public DataGridRowEdit(IList owner, object item, int index, bool added)
    {
        _owner = owner;
        _item = item;
        _index = index;
        _added = added;
    }

    private readonly IList _owner;
    private readonly object _item;
    private readonly int _index;
    private readonly bool _added;

    // A collection that announces its own changes brings the table with it; one that does not has to be re-read whole.
    public override bool IsStructural => _owner is not INotifyCollectionChanged;

    public override void Undo()
    {
        if (_added) Take(); else Put();
    }

    public override void Redo()
    {
        if (_added) Put(); else Take();
    }

    private void Put()
    {
        if (_owner is { IsReadOnly: false } && !_owner.Contains(_item))
        {
            _owner.Insert(Math.Clamp(_index, 0, _owner.Count), _item);
        }
    }

    private void Take()
    {
        if (_owner is { IsReadOnly: false }) _owner.Remove(_item);
    }
}

/// <summary>What the table has done, in the order it did it, and what it has taken back. Changes are grouped into ACTS:
/// a paste of two hundred cells is one act, because one paste is one thing the user did and one Ctrl+Z is what they
/// will reach for.</summary>
internal sealed class DataGridEditHistory
{
    private readonly List<List<DataGridEdit>> _done = new();
    private readonly List<List<DataGridEdit>> _undone = new();
    private List<DataGridEdit> _open;
    private int _depth;

    /// <summary>How many acts are kept. Zero keeps none - the history is off, and nothing is recorded at all.</summary>
    public int Limit { get; set; } = 100;

    /// <summary>True while an act is being put back or done again. Recording is DEAF then: an undo that recorded its own
    /// writes would bury the very act it was undoing under the reversal of it.</summary>
    public bool IsApplying { get; private set; }

    public bool CanUndo => _done.Count > 0;

    public bool CanRedo => _undone.Count > 0;

    /// <summary>Opens an act. Nested calls belong to the outermost one - a paste that writes cell by cell is still one
    /// paste.</summary>
    public void Begin() => _depth++;

    public void End()
    {
        if (_depth > 0) _depth--;
        if (_depth == 0) Close();
    }

    public void Record(DataGridEdit edit)
    {
        if (IsApplying || edit == null || Limit <= 0) return;

        _open ??= new List<DataGridEdit>();
        _open.Add(edit);

        // A write nobody wrapped is an act of its own: typing in one cell needs no ceremony to be undoable.
        if (_depth == 0) Close();
    }

    public bool Undo(out bool structural) => Apply(_done, _undone, undoing: true, out structural);

    public bool Redo(out bool structural) => Apply(_undone, _done, undoing: false, out structural);

    public void Clear()
    {
        _done.Clear();
        _undone.Clear();
        _open = null;
        _depth = 0;
    }

    private void Close()
    {
        if (_open is not { Count: > 0 })
        {
            _open = null;
            return;
        }

        _done.Add(_open);
        _open = null;

        // Doing something new throws away what was taken back: the table went one way, and the other way no longer
        // leads anywhere it has been.
        _undone.Clear();

        if (Limit > 0 && _done.Count > Limit) _done.RemoveAt(0);
    }

    private bool Apply(List<List<DataGridEdit>> from, List<List<DataGridEdit>> to, bool undoing, out bool structural)
    {
        structural = false;
        if (from.Count == 0) return false;

        var act = from[^1];
        from.RemoveAt(from.Count - 1);

        IsApplying = true;
        try
        {
            // BACKWARDS when undoing: the changes were made in order, and two of them touching the same cell only come
            // out right in the order they went in, reversed.
            if (undoing)
            {
                for (var i = act.Count - 1; i >= 0; i--)
                {
                    act[i].Undo();
                    structural |= act[i].IsStructural;
                }
            }
            else
            {
                foreach (var edit in act)
                {
                    edit.Redo();
                    structural |= edit.IsStructural;
                }
            }
        }
        finally
        {
            IsApplying = false;
        }

        to.Add(act);
        return true;
    }
}
