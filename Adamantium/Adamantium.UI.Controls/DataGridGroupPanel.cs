using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>The strip above the table that a column header is dropped into to group by it, and that shows what the
/// table is grouped by once it is.
/// <para>It is a DROP TARGET rather than a menu because that is the gesture the header band already teaches: a header
/// can be picked up and carried, and this is one more place it can be put down.</para></summary>
public class DataGridGroupPanel : Control
{
    private TreeDataGrid _owner;
    private Panel _chips;
    private UIComponent _hint;
    private ButtonBase _expandAll;
    private ButtonBase _collapseAll;
    private MeasurableUIComponent _dropMark;
    private readonly List<DataGridGroupChip> _live = new();

    /// <summary>Lights up while a carried header is over the strip, so the drop reads as available before it happens.</summary>
    public static readonly AdamantiumProperty IsDropTargetProperty = AdamantiumProperty.Register(nameof(IsDropTarget),
        typeof(bool), typeof(DataGridGroupPanel),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    public bool IsDropTarget
    {
        get => GetValue<bool>(IsDropTargetProperty);
        internal set => SetValue(IsDropTargetProperty, value);
    }

    /// <summary>The grid this strip groups. Setting it REGISTERS the strip with that grid, which is what lets a change
    /// of grouping reach it.</summary>
    public TreeDataGrid Owner
    {
        get => _owner;
        internal set
        {
            if (ReferenceEquals(_owner, value)) return;
            _owner = value;
            _owner?.AdoptGroupPanel(this);
            Sync();
        }
    }

    /// <summary>How far the pointer must travel before a press on a chip becomes a CARRY rather than a click.</summary>
    private const double DragThreshold = 4;

    private int _pressed = -1;
    private int _dragging = -1;
    private int _dropTarget = -1;
    private double _pressFrom;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _chips = GetTemplateChild("PART_Chips") as Panel;
        _hint = GetTemplateChild("PART_Hint") as UIComponent;
        _dropMark = GetTemplateChild("PART_DropMark") as MeasurableUIComponent;

        // The two commands live HERE, beside the chips, because that is where grouping is: a table folded two columns
        // deep has hundreds of captions, and nothing else in the window has anything to say about them.
        _expandAll = GetTemplateChild("PART_ExpandAll") as ButtonBase;
        _collapseAll = GetTemplateChild("PART_CollapseAll") as ButtonBase;
        if (_expandAll != null) _expandAll.Click += OnExpandAll;
        if (_collapseAll != null) _collapseAll.Click += OnCollapseAll;

        Sync();
    }

    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();
        if (_expandAll != null) _expandAll.Click -= OnExpandAll;
        if (_collapseAll != null) _collapseAll.Click -= OnCollapseAll;
        _expandAll = null;
        _collapseAll = null;
    }

    private void OnExpandAll(object sender, RoutedEventArgs e) => Owner?.ExpandAllGroups();

    private void OnCollapseAll(object sender, RoutedEventArgs e) => Owner?.CollapseAllGroups();

    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(sender, e);

        var chip = ChipAt(e.GetPosition(this));
        if (chip < 0) return;

        // The × is the way OUT of the grouping, and the only one: a chip pressed anywhere else is being picked up, or
        // being pressed for no reason, and neither must ungroup anything.
        if (_live[chip].PressedRemove(e.OriginalSource))
        {
            Owner?.GroupBy(_live[chip].Column);
            e.Handled = true;
            return;
        }

        // Only REMEMBERED here. The same press may turn out to be the start of a carry - the header band learned this
        // first, where a press that sorted on the way down sorted every time a column was moved.
        _pressed = chip;
        _pressFrom = ChipPoint(e.GetPosition(this)).X;
        e.Handled = true;
    }

    protected override void OnMouseMove(object sender, MouseEventArgs e)
    {
        base.OnMouseMove(sender, e);
        if (Owner == null) return;

        var x = ChipPoint(e.GetPosition(this)).X;

        if (_dragging >= 0)
        {
            ShowDropLine(DropTargetAt(x));
            return;
        }

        if (_pressed >= 0 && System.Math.Abs(x - _pressFrom) > DragThreshold)
        {
            CaptureMouse();
            BeginCarry(_pressed, x);
        }
    }

    protected override void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(sender, e);

        if (_dragging >= 0)
        {
            EndCarry(ChipPoint(e.GetPosition(this)).X);
            ReleaseMouseCapture();
        }

        // A press that went nowhere does NOTHING. The chip is a statement of what the table is grouped by and a handle
        // for moving it; taking a column out is what its × is for.
        _pressed = -1;
    }

    /// <summary>Picks a chip up: marks it, and puts the drop mark where it would land.</summary>
    internal void BeginCarry(int chip, double x)
    {
        _dragging = chip;

        // An OVERRIDE, for the same reason the header band uses one: a carried chip is over whatever it is being
        // carried across, and a per-element cursor only ever applies to the element the pointer is on.
        Mouse.OverrideCursor = Cursors.SizeAll;
        if (chip < _live.Count) _live[chip].IsDragging = true;
        ShowDropLine(DropTargetAt(x));
    }

    /// <summary>Drops the carried chip where the mark stands, changing how deep that column groups.</summary>
    internal void EndCarry(double x)
    {
        if (_dragging < 0) return;

        var target = DropTargetAt(x);
        if (_dragging < _live.Count) _live[_dragging].IsDragging = false;
        Owner?.MoveGrouping(_dragging, target > _dragging ? target - 1 : target);

        _dragging = -1;
        HideDropLine();
        Mouse.OverrideCursor = null;
    }

    /// <summary>Where a chip dropped at <paramref name="x"/> would land: an index in 0..Count, counting BOUNDARIES
    /// rather than chips - dropping past the last one is a place of its own.</summary>
    internal int DropTargetAt(double x)
    {
        for (var i = 0; i < _live.Count; i++)
        {
            var bounds = _live[i].Bounds;
            if (x < bounds.X + bounds.Width / 2) return i;
        }

        return _live.Count;
    }

    // The chips sit inside PART_Chips, which sits inside whatever the theme wrapped it in, so a point taken against
    // THIS strip has to be carried into their space rather than compared to their bounds as it is.
    private Vector2 ChipPoint(Vector2 point) =>
        _chips == null ? point : this.TranslatePoint(point, _chips);

    private int ChipAt(Vector2 point)
    {
        var at = ChipPoint(point);
        for (var i = 0; i < _live.Count; i++)
        {
            var bounds = _live[i].Bounds;
            if (at.X >= bounds.X && at.X < bounds.X + bounds.Width) return i;
        }

        return -1;
    }

    private void ShowDropLine(int target)
    {
        if (_dropMark == null || _live.Count == 0 || _dropTarget == target) return;

        _dropTarget = target;

        var at = target < _live.Count
            ? _live[target].Bounds.X
            : _live[^1].Bounds.X + _live[^1].Bounds.Width;

        var width = double.IsNaN(_dropMark.Width) ? 2 : _dropMark.Width;
        _dropMark.Margin = new Thickness(_chips.Bounds.X + at - width / 2, 0, 0, 0);
        _dropMark.Visibility = Visibility.Visible;
    }

    private void HideDropLine()
    {
        _dropTarget = -1;
        if (_dropMark != null) _dropMark.Visibility = Visibility.Collapsed;
    }

    internal void Sync()
    {
        if (_chips == null) return;

        var columns = Owner?.GroupDescriptions;
        var count = columns?.Count ?? 0;

        while (_live.Count > count)
        {
            var last = _live.Count - 1;
            _chips.Children.Remove(_live[last]);
            _live.RemoveAt(last);
        }

        for (var i = 0; i < count; i++)
        {
            if (i == _live.Count)
            {
                var made = new DataGridGroupChip();
                _live.Add(made);
                _chips.Children.Add(made);
            }

            _live[i].Attach(columns[i], i + 1);
        }

        if (_hint != null) _hint.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Nothing to fold while nothing is grouped, so the commands are not offered then.
        var folding = count == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (_expandAll != null) _expandAll.Visibility = folding;
        if (_collapseAll != null) _collapseAll.Visibility = folding;
    }
}
