using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>The filter editor a column header drops down: the list of values to tick, and under it two conditions
/// joined by AND or OR - the shape a spreadsheet uses. Nothing is applied until the buttons at the bottom, so a filter
/// can be composed without the rows moving under the pointer.
/// <para>Fully templated (DataGridFilterStyleSet). PART_SelectAll, PART_Values, PART_FirstOperator, PART_FirstValue,
/// PART_FirstMatchCase, PART_Logic, PART_SecondOperator, PART_SecondValue, PART_SecondMatchCase, PART_Apply,
/// PART_Clear.</para></summary>
public class DataGridFilterView : Control
{
    private static readonly string[] OperatorNames =
    [
        "Is equal to", "Is not equal to", "Contains", "Does not contain", "Starts with", "Ends with",
        "Is greater than", "Is less than", "Is empty", "Is not empty"
    ];

    private static readonly string[] LogicNames = ["And", "Or"];

    private CheckBox _selectAll;
    private ListBox _valueList;
    private DropDown _firstOperator;
    private TextBox _firstValue;
    private ToggleButton _firstMatchCase;
    private DropDown _logic;
    private DropDown _secondOperator;
    private TextBox _secondValue;
    private ToggleButton _secondMatchCase;
    private ButtonBase _apply;
    private ButtonBase _clear;
    private bool _syncing;

    /// <summary>The grid being filtered, and the column this editor belongs to. Set by <see cref="Open"/>.</summary>
    public TreeDataGrid Owner { get; private set; }

    public DataGridColumn Column { get; private set; }

    /// <summary>The value list, one line per distinct value the column holds.</summary>
    public ObservableCollection<DataGridFilterValue> Values { get; } = new();

    /// <summary>Raised when the editor is done with - applied or cleared - so whatever opened it can close it.</summary>
    public event EventHandler Closed;

    /// <summary>Points the editor at a column and fills it from that column's current filter, so re-opening it shows
    /// what is in force rather than a blank form.</summary>
    public void Open(TreeDataGrid owner, DataGridColumn column)
    {
        Owner = owner;
        Column = column;
        if (owner == null || column == null) return;

        var filter = owner.FilterFor(column);

        // NOTHING is ticked until a filter says otherwise: ticking is how you NARROW, so an untouched form must start
        // from "no value filter" rather than from a full set the user has to empty first.
        Values.Clear();
        foreach (var value in owner.DistinctValues(column))
        {
            var included = filter.Included?.Contains(DataGridColumnFilter.Text(value)) ?? false;
            var line = new DataGridFilterValue(value, included);
            line.PropertyChanged += OnValueToggled;
            Values.Add(line);
        }

        Fill(filter);

        // The flyout has ALREADY been laid out by the time this runs - the popup measures its content when it opens,
        // and only then can the editor be found and filled - so the pass is asked for here.
        InvalidateMeasure();
        (VisualParent as IMeasurableComponent)?.InvalidateMeasure();
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _selectAll = GetTemplateChild("PART_SelectAll") as CheckBox;
        _valueList = GetTemplateChild("PART_Values") as ListBox;
        _firstOperator = GetTemplateChild("PART_FirstOperator") as DropDown;
        _firstValue = GetTemplateChild("PART_FirstValue") as TextBox;
        _firstMatchCase = GetTemplateChild("PART_FirstMatchCase") as ToggleButton;
        _logic = GetTemplateChild("PART_Logic") as DropDown;
        _secondOperator = GetTemplateChild("PART_SecondOperator") as DropDown;
        _secondValue = GetTemplateChild("PART_SecondValue") as TextBox;
        _secondMatchCase = GetTemplateChild("PART_SecondMatchCase") as ToggleButton;
        _apply = GetTemplateChild("PART_Apply") as ButtonBase;
        _clear = GetTemplateChild("PART_Clear") as ButtonBase;

        if (_valueList != null) _valueList.ItemsSource = Values;
        if (_firstOperator != null) _firstOperator.ItemsSource = OperatorNames;
        if (_secondOperator != null) _secondOperator.ItemsSource = OperatorNames;
        if (_logic != null) _logic.ItemsSource = LogicNames;

        if (_selectAll != null) _selectAll.PropertyChanged += OnSelectAllToggled;
        if (_apply != null) _apply.Click += OnApply;
        if (_clear != null) _clear.Click += OnClear;

        if (Owner != null && Column != null) Fill(Owner.FilterFor(Column));
    }

    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();

        if (_selectAll != null) _selectAll.PropertyChanged -= OnSelectAllToggled;
        if (_apply != null) _apply.Click -= OnApply;
        if (_clear != null) _clear.Click -= OnClear;

        _selectAll = null;
        _valueList = null;
        _firstOperator = null;
        _firstValue = null;
        _firstMatchCase = null;
        _logic = null;
        _secondOperator = null;
        _secondValue = null;
        _secondMatchCase = null;
        _apply = null;
        _clear = null;
    }

    /// <summary>Writes what the form says into the column's filter and re-runs the rows.</summary>
    public void Apply()
    {
        if (Owner == null || Column == null) return;

        var filter = Owner.FilterFor(Column);

        // Ticking nothing is not "let nothing through" - it is "I did not filter by value", the same thing ticking
        // everything means. Only a partial tick narrows.
        var ticked = Ticked();
        filter.Included = ticked.Count == 0 || ticked.Count == Values.Count ? null : ticked;

        Read(_firstOperator, _firstValue, _firstMatchCase, filter.First);
        Read(_secondOperator, _secondValue, _secondMatchCase, filter.Second);
        filter.Logic = _logic is { SelectedIndex: 1 } ? DataGridFilterLogic.Or : DataGridFilterLogic.And;

        Owner.ApplyFilters();
        Closed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Drops this column's filter entirely and shows every row it was hiding.</summary>
    public void Clear()
    {
        if (Owner == null || Column == null) return;

        Owner.ClearColumnFilter(Column);
        Tick(false);
        Fill(Owner.FilterFor(Column));
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void Fill(DataGridColumnFilter filter)
    {
        _syncing = true;

        if (_firstOperator != null) _firstOperator.SelectedIndex = (int)filter.First.Operator;
        if (_firstValue != null) _firstValue.Text = filter.First.Value?.ToString() ?? string.Empty;
        if (_firstMatchCase != null) _firstMatchCase.IsChecked = filter.First.IsCaseSensitive;
        if (_secondOperator != null) _secondOperator.SelectedIndex = (int)filter.Second.Operator;
        if (_secondValue != null) _secondValue.Text = filter.Second.Value?.ToString() ?? string.Empty;
        if (_secondMatchCase != null) _secondMatchCase.IsChecked = filter.Second.IsCaseSensitive;
        if (_logic != null) _logic.SelectedIndex = (int)filter.Logic;
        if (_selectAll != null) _selectAll.IsChecked = AllTicked();

        _syncing = false;
    }

    private static void Read(DropDown source, TextBox text, ToggleButton matchCase, DataGridFilterCondition condition)
    {
        if (source is { SelectedIndex: >= 0 }) condition.Operator = (DataGridFilterOperator)source.SelectedIndex;
        condition.Value = text?.Text;
        condition.IsCaseSensitive = matchCase?.IsChecked == true;
    }

    private bool AllTicked()
    {
        if (Values.Count == 0) return false;

        foreach (var value in Values)
        {
            if (!value.IsChecked) return false;
        }

        return true;
    }

    private HashSet<string> Ticked()
    {
        var ticked = new HashSet<string>();
        foreach (var value in Values)
        {
            if (value.IsChecked) ticked.Add(value.Text);
        }

        return ticked;
    }

    private void Tick(bool ticked)
    {
        _syncing = true;
        foreach (var value in Values) value.IsChecked = ticked;
        _syncing = false;
    }

    private void OnSelectAllToggled(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (_syncing || e.Property != ToggleButton.IsCheckedProperty) return;
        Tick(_selectAll?.IsChecked == true);
    }

    private void OnValueToggled(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (_syncing || e.Property != DataGridFilterValue.IsCheckedProperty || _selectAll == null) return;

        _syncing = true;
        _selectAll.IsChecked = AllTicked();
        _syncing = false;
    }

    private void OnApply(object sender, RoutedEventArgs e) => Apply();

    private void OnClear(object sender, RoutedEventArgs e) => Clear();
}
