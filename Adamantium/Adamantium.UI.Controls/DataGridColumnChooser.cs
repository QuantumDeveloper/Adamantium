using System.Collections.Generic;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>Says which columns the table shows: one switch per column, ticked while it is on screen. It lives in a
/// FLYOUT off the header band, not in a strip of its own - a strip costs vertical room permanently and wraps to several
/// lines on exactly the wide table the feature exists for.
/// <para>Hiding is not removing. The column stays in <see cref="TreeDataGrid.Columns"/> with its width, its sort and
/// its filter intact, so turning it back on restores the table as it was rather than a default of it - and everything
/// holding a column INDEX keeps finding it where it was.</para>
/// <para>A column the table is GROUPED BY is off the strip entirely, not shown as an unticked switch: it is not hidden,
/// it has moved into the group captions, and offering to "show" it would be offering something that cannot happen
/// while the grouping stands.</para></summary>
public class DataGridColumnChooser : Control
{
    private TreeDataGrid _owner;
    private Panel _items;
    private readonly List<CheckBox> _live = new();
    private readonly Dictionary<CheckBox, DataGridColumn> _of = new();

    /// <summary>The grid this strip chooses for. Setting it REGISTERS the strip with that grid, which is what lets a
    /// column added, removed or grouped by reach it.</summary>
    public TreeDataGrid Owner
    {
        get => _owner;
        internal set
        {
            if (ReferenceEquals(_owner, value)) return;
            _owner = value;
            _owner?.AdoptColumnChooser(this);
            Sync();
        }
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _items = GetTemplateChild("PART_Items") as Panel;
        Sync();
    }

    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();
        Drop();
        _items = null;
    }

    internal void Sync()
    {
        if (_items == null) return;

        // EVERY column the table is showing, including the ones that may not be hidden - those come ticked and
        // DISABLED rather than being left out. Left out, a column that cannot be hidden simply is not in the list, and
        // from the outside that reads as a list with something missing rather than as a rule; the first question it
        // gets is "where is Code?".
        // A column the table is GROUPED BY is a different matter and really is absent: it is not hidden, it has moved
        // into the group captions, and offering to show it would be offering something that cannot happen.
        var offered = new List<DataGridColumn>();
        var columns = Owner?.Columns;
        for (var i = 0; i < (columns?.Count ?? 0); i++)
        {
            var column = columns[i];
            if (Owner.GroupDescriptions.Contains(column)) continue;
            offered.Add(column);
        }

        while (_live.Count > offered.Count)
        {
            var last = _live.Count - 1;
            Release(_live[last]);
            _items.Children.Remove(_live[last]);
            _live.RemoveAt(last);
        }

        for (var i = 0; i < offered.Count; i++)
        {
            if (i == _live.Count)
            {
                var made = new CheckBox { Margin = new Thickness(0, 0, 0, 4) };
                made.Checked += OnToggled;
                made.Unchecked += OnToggled;
                _live.Add(made);
                _items.Children.Add(made);
            }

            var box = _live[i];
            _of[box] = offered[i];
            box.Content = offered[i].Header;
            box.IsEnabled = offered[i].CanUserHide;

            // SetCurrentValue, not the setter: this writes what the column says while the user's hand is on the switch,
            // and a plain write would land in the local slot and mask the binding a page may have put on it.
            box.SetCurrentValue(ToggleButton.IsCheckedProperty, (bool?)offered[i].IsVisible);
        }
    }

    private void OnToggled(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox box && _of.TryGetValue(box, out var column) && column.CanUserHide)
            column.IsVisible = box.IsChecked == true;
    }

    private void Drop()
    {
        foreach (var box in _live) Release(box);
        _live.Clear();
        _of.Clear();
    }

    private void Release(CheckBox box)
    {
        box.Checked -= OnToggled;
        box.Unchecked -= OnToggled;
        _of.Remove(box);
    }
}
