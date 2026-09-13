using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Controls;

/// <summary>The strip for a record that does not exist yet - what most people look for first, and the only way to fill
/// a table that is empty. Placed by the SAME numbers the rows, the header and the totals use, so a field can never
/// stand under another column.
/// <para>A STRIP and not a row in the list, which was the second attempt. As a row it had to be a node the source does
/// not contain: the virtualizer hands out containers by the source's own indices, one extra root at the end put them
/// out of step, and a record from the middle of the table was drawn over the blank row. It also had to be kept out of
/// the sort, the filters, the totals, the file and the clipboard - five separate "except this one"s for a thing that
/// is not data. Outside the list it is none of those, and it is always in view: nobody scrolls ten thousand rows to
/// add a record.</para></summary>
public class DataGridNewRowPresenter : Panel
{
    /// <summary>It scrolls sideways with the columns, so a field that has slid past the left edge is CUT there rather
    /// than drawn over the table's frame - the same reason the header and the totals clip.</summary>
    public DataGridNewRowPresenter()
    {
        ClipToBounds = true;
    }

    private TreeDataGrid _owner;

    /// <summary>The grid this strip adds to. Setting it REGISTERS the strip with that grid - which is what lets one
    /// CommitEdit answer for the table and for this.</summary>
    public TreeDataGrid Owner
    {
        get => _owner;
        internal set
        {
            if (ReferenceEquals(_owner, value)) return;
            _owner = value;
            _owner?.AdoptNewRow(this);
        }
    }

    private readonly Dictionary<int, DataGridCell> _cells = new();
    private readonly List<int> _leaving = new();

    // What has been filled in so far, by column. The record is made from ALL of it at once: a strip that only ever knew
    // the field standing open threw away the code the moment the owner was chosen, and filling a record in is exactly
    // the act of moving between its fields.
    private readonly Dictionary<int, object> _typed = new();
    private DataGridRowHeader _head;
    private int _editing = -1;

    /// <summary>Whether a field of the strip is open for typing.</summary>
    public bool IsEditing => _editing >= 0;

    /// <summary>Whether a record is being filled in here - a field open, or fields already filled with no editor
    /// standing in any of them. Closing the last field does not end the record: what was typed is still on screen
    /// waiting for the Enter that makes it, or the Escape that does not.</summary>
    public bool IsFilling => _editing >= 0 || _typed.Count > 0;

    /// <summary>The field standing for one column, or null where that column shows none.</summary>
    internal DataGridCell CellAt(int column) => _cells.GetValueOrDefault(column);

    internal void Sync()
    {
        var columns = Owner?.Columns;
        var count = columns?.Count ?? 0;

        SyncHead();

        _leaving.Clear();
        foreach (var pair in _cells)
        {
            if (pair.Key >= count || !columns[pair.Key].IsShown) _leaving.Add(pair.Key);
        }

        foreach (var index in _leaving)
        {
            var leaving = _cells[index];
            leaving.MouseLeftButtonDown -= OnCellPressed;
            Children.Remove(leaving);
            _cells.Remove(index);
        }

        for (var i = 0; i < count; i++)
        {
            if (!columns[i].IsShown) continue;

            if (!_cells.TryGetValue(i, out var cell))
            {
                cell = new DataGridCell();
                cell.MouseLeftButtonDown += OnCellPressed;
                _cells[i] = cell;
                Children.Add(cell);
            }

            // A field that has been FILLED IN is no longer standing in for nothing: it shows its value the way the
            // column shows values, so a tick box that was ticked reads as a tick box. Empty, it shows none of that -
            // a live-looking control over a record that does not exist answers to nothing. Told before the attach,
            // which is what reads it.
            cell.IsPlaceholder = !_typed.ContainsKey(i);

            // NO record: the cells are attached to nothing, which is exactly what they stand for. The hint goes on the
            // first column shown - a blank strip under the table is a gap, and nobody presses a gap.
            cell.Attach(columns[i], i, null);
            cell.IsEditing = i == _editing;

            // EMPTY, never null. A cell's template is a DataTemplate over its Content, so null content builds nothing at
            // all - the editor included: an edit would open on a field whose TextBox was never made, and the typing
            // would go nowhere.
            // What is already in this column, OPEN OR NOT. The editor is built over the cell's content - PrepareEditor
            // is handed exactly that - so a field opened a second time on an empty content came up empty and threw
            // away what had been typed into it.
            cell.Content = _typed.TryGetValue(i, out var kept) ? kept
                : !cell.IsEditing && _typed.Count == 0 && IsFirstShown(columns, i) ? Owner?.NewRowHint
                : string.Empty;
        }
    }

    // The SAME strip the rows carry, numberless. Without it the band began where the first column does and every row
    // above it began at the table's edge, so the placeholder read as a narrower, foreign thing with the ground showing
    // through where each row has its head. A blank DataGridRowHeader rather than a rectangle of our own: the head's
    // look belongs to the theme, and two ways to draw it is one too many.
    private void SyncHead()
    {
        // ONE blank across the WHOLE leading zone, where a row has a toggle and a number side by side. There is no
        // branch to open and no number to give a record that does not exist, so the two strips have nothing to say
        // separately here - what they have to do is start where the rows start.
        if (Owner is not { LeftStripsLeading: > 0 })
        {
            if (_head != null) _head.Visibility = Visibility.Collapsed;
            return;
        }

        if (_head == null)
        {
            // OVER the fields, the same way a row holds its own. The leading zone does not scroll and the fields do, so
            // a field slid under it has to be COVERED by it: without that the first column's text carried on over the
            // strip of numbers and stood where no column is.
            _head = new DataGridRowHeader { ZIndex = 3 };
            Children.Add(_head);
        }

        _head.Visibility = Visibility.Visible;
    }

    private static bool IsFirstShown(DataGridColumns columns, int index)
    {
        for (var i = 0; i < index; i++)
        {
            if (columns[i].IsShown) return false;
        }

        return true;
    }

    private void OnCellPressed(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridCell cell || Owner == null) return;

        // ONE click, where the table wants two. There is nothing here to select, so the only thing a press can mean is
        // "I am filling this in" - asking for a second click would be asking twice for one intention.
        Begin(cell.ColumnIndex);
        e.Handled = true;
    }

    /// <summary>Opens one field for typing, closing whatever the table had open - two editors at once is two current
    /// cells at once. False when the column refuses edits.</summary>
    internal bool Begin(int column)
    {
        var columns = Owner?.Columns;
        if (columns == null || column < 0 || column >= columns.Count || columns[column].IsReadOnly) return false;

        // Already open: a press inside it is a CARET, not a new edit. Re-opening would rebuild the editor and put the
        // caret back at the value's end - clicking into the middle of what you typed would jump away from where you
        // aimed.
        if (column == _editing) return true;

        if (Owner.IsEditing && !Owner.CommitEdit()) return false;

        Remember();
        _editing = column;
        Sync();
        InvalidateMeasure();

        // A field off to the right is a field the user cannot fill in: stepping into one has to bring it here.
        Owner.ScrollColumnIntoView(column);
        return true;
    }

    /// <summary>Moves to the next field that takes a value, or the previous one going backwards. Stops at the ends
    /// rather than walking out of the strip: the record is not made yet, and leaving would throw away what is in it.
    /// </summary>
    internal bool Step(int delta)
    {
        var columns = Owner?.Columns;
        if (columns == null || _editing < 0 || delta == 0) return false;

        for (var i = _editing + delta; i >= 0 && i < columns.Count; i += delta)
        {
            if (columns[i].IsShown && !columns[i].IsReadOnly) return Begin(i);
        }

        return true;
    }

    // What the field standing open holds, kept under its column. Called on the way OUT of a field - while it is open the
    // editor is the truth, and asking it before it is left would freeze the value at whatever was typed so far.
    private void Remember()
    {
        if (_editing < 0 || !_cells.TryGetValue(_editing, out var cell)) return;

        var value = cell.ValueForCommit();
        if (value == null || (value as string)?.Length == 0) _typed.Remove(_editing);
        else _typed[_editing] = value;
    }

    /// <summary>Makes the record from what was typed. The record is made HERE and not a keystroke earlier: typing
    /// something and thinking better of it has to leave nothing behind, and only a commit says the user meant it.
    /// </summary>
    internal bool Commit()
    {
        if (Owner == null) return true;

        Remember();
        if (_typed.Count == 0)
        {
            Clear();
            return true;
        }

        var columns = Owner.Columns;
        var values = new List<KeyValuePair<DataGridColumn, object>>(_typed.Count);
        foreach (var pair in _typed)
        {
            if (pair.Key < columns.Count) values.Add(new KeyValuePair<DataGridColumn, object>(columns[pair.Key], pair.Value));
        }

        if (Owner.CreateFromNewRow(values) == null) return false;

        Clear();
        return true;
    }

    /// <summary>Closes the field without making anything, and throws the whole strip away with it - what Escape means.
    /// </summary>
    internal void Cancel() => Clear();

    /// <summary>Closes the open field and KEEPS what is in the strip - what losing the focus means. A click on another
    /// field moves the focus before it opens anything, so discarding here emptied the strip on the way to the very
    /// field the user was reaching for. Nothing is created either way: only a commit says the record was meant.
    /// </summary>
    internal void Close()
    {
        if (_editing < 0) return;

        Remember();
        _editing = -1;
        Sync();
        InvalidateMeasure();
    }

    private void Clear()
    {
        if (_editing >= 0 && _cells.TryGetValue(_editing, out var cell))
        {
            cell.EditedValue = null;
            cell.PendingText = null;
        }

        _typed.Clear();
        _editing = -1;
        Sync();
        InvalidateMeasure();

        // The field the focus was in has just been taken away, and focus does not fall back by itself: without this the
        // next keystroke - an undo of the very record just made, most of all - goes nowhere at all.
        Owner?.Focus();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Sync();

        var columns = Owner?.Columns;
        if (columns == null || columns.Count == 0) return new Size();

        double height = 0;
        foreach (var pair in _cells)
        {
            if (pair.Key >= columns.Count) continue;

            pair.Value.Measure(new Size(columns[pair.Key].ActualWidth, availableSize.Height));
            height = System.Math.Max(height, pair.Value.DesiredSize.Height);
        }

        if (_head is { Visibility: Visibility.Visible })
        {
            _head.Measure(new Size(Owner.LeftStripsLeading, availableSize.Height));
        }

        return new Size(Owner.ColumnsWidth, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = Owner?.Columns;
        if (columns == null) return finalSize;

        // The same three placements the header and the totals use, for the same reason: the strip sits outside the
        // rows' scroller and carries the offset itself, and a pinned column is not subject to it.
        var offset = Owner.HorizontalOffset;
        foreach (var pair in _cells)
        {
            if (pair.Key >= columns.Count) continue;

            var column = columns[pair.Key];
            var x = column.IsFrozenLeft ? column.Offset
                : column.IsFrozenRight ? column.Offset + Owner.RightPinShift - offset
                : column.Offset - offset;
            pair.Value.Arrange(new Rect(x, 0, column.ActualWidth, finalSize.Height));
        }

        // Furthest left of all and NOT subject to the offset, exactly as the rows place theirs.
        if (_head is { Visibility: Visibility.Visible })
        {
            _head.Arrange(new Rect(0, 0, Owner.LeftStripsLeading, finalSize.Height));
        }

        return finalSize;
    }
}
