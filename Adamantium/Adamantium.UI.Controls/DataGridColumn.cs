using System;
using System.Collections;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>One column of a <see cref="TreeDataGrid"/>: what its header says, how wide it is, and how its cells are
/// drawn and edited. A column DESCRIBES cells - declared once, consulted by every row - so it holds no per-row state.
/// <para>A <see cref="FundamentalUIComponent"/> that JOINS the grid's logical tree, so <c>{Binding}</c> and
/// <c>{Ancestor}</c> resolve on it as on any element. Everything it reads off a ROW is a real
/// <see cref="BindingBase"/>, never a member name: a name carries no converter, format, mode or validation.</para></summary>
public abstract class DataGridColumn : FundamentalUIComponent
{
    public static readonly AdamantiumProperty HeaderProperty = AdamantiumProperty.Register(nameof(Header),
        typeof(object), typeof(DataGridColumn), new PropertyMetadata(null));

    /// <summary>What this column's cells show and write - <c>{Binding Name}</c>, with everything a binding can carry.
    /// A template column leaves it empty: its cell holds the ROW, and the template binds whatever it likes.</summary>
    public static readonly AdamantiumProperty BindingProperty = AdamantiumProperty.Register(nameof(Binding),
        typeof(BindingBase), typeof(DataGridColumn), new PropertyMetadata(null, OnRowBindingChanged));

    /// <summary>What a cell of this column MEANS for a given row - an error, a warning, whatever the application
    /// distinguishes. A meaning, never a colour: the theme decides what "error" looks like, and a model that handed out
    /// brushes would break the moment the theme changed.</summary>
    public static readonly AdamantiumProperty StateBindingProperty = AdamantiumProperty.Register(nameof(StateBinding),
        typeof(BindingBase), typeof(DataGridColumn), new PropertyMetadata(null, OnRowBindingChanged));

    /// <summary>Whether ONE cell refuses editing - a locked record, a field this user may not touch. Asked per row;
    /// <see cref="IsReadOnly"/> refuses the whole column and is asked first.</summary>
    public static readonly AdamantiumProperty IsReadOnlyBindingProperty = AdamantiumProperty.Register(
        nameof(IsReadOnlyBinding), typeof(BindingBase), typeof(DataGridColumn),
        new PropertyMetadata(null, OnRowBindingChanged));

    public static readonly AdamantiumProperty HeaderTemplateProperty = AdamantiumProperty.Register(nameof(HeaderTemplate),
        typeof(DataTemplate), typeof(DataGridColumn), new PropertyMetadata(null));

    /// <summary>How wide: a fixed number, <c>Auto</c> (the widest REALIZED cell - see TreeDataGrid's width pass) or a
    /// star share of what is left over.</summary>
    public static readonly AdamantiumProperty WidthProperty = AdamantiumProperty.Register(nameof(Width),
        typeof(GridLength), typeof(DataGridColumn), new PropertyMetadata(GridLength.Auto));

    public static readonly AdamantiumProperty MinWidthProperty = AdamantiumProperty.Register(nameof(MinWidth),
        typeof(Double), typeof(DataGridColumn), new PropertyMetadata(20.0));

    public static readonly AdamantiumProperty MaxWidthProperty = AdamantiumProperty.Register(nameof(MaxWidth),
        typeof(Double), typeof(DataGridColumn), new PropertyMetadata(Double.PositiveInfinity));

    public static readonly AdamantiumProperty CellTemplateProperty = AdamantiumProperty.Register(nameof(CellTemplate),
        typeof(DataTemplate), typeof(DataGridColumn), new PropertyMetadata(null));

    public static readonly AdamantiumProperty CellEditingTemplateProperty = AdamantiumProperty.Register(
        nameof(CellEditingTemplate), typeof(DataTemplate), typeof(DataGridColumn), new PropertyMetadata(null));

    public static readonly AdamantiumProperty SortMemberPathProperty = AdamantiumProperty.Register(nameof(SortMemberPath),
        typeof(String), typeof(DataGridColumn), new PropertyMetadata(null));

    public static readonly AdamantiumProperty CanSortProperty = AdamantiumProperty.Register(nameof(CanSort),
        typeof(bool), typeof(DataGridColumn), new PropertyMetadata(true));

    /// <summary>Stays put while the rest scroll sideways. Frozen columns are laid out in their own zone at the left.</summary>
    /// <summary>Which edge this column is pinned to, if any. A pinned column LEAVES its neighbours and joins the zone
    /// at that edge, in declaration order - the zone is a place in the layout, not a prefix of what was declared.</summary>
    public static readonly AdamantiumProperty FrozenSideProperty = AdamantiumProperty.Register(nameof(FrozenSide),
        typeof(DataGridFrozenSide), typeof(DataGridColumn),
        new PropertyMetadata(DataGridFrozenSide.None, OnLayoutChanged));

    // Pinning CHANGES THE LAYOUT, and a column is not a visual child of the grid - nothing invalidates on its behalf,
    // so it has to say so itself.
    private static void OnLayoutChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (d is DataGridColumn column) column.Owner?.InvalidateColumns();
    }

    public static readonly AdamantiumProperty CanUserResizeProperty = AdamantiumProperty.Register(nameof(CanUserResize),
        typeof(bool), typeof(DataGridColumn), new PropertyMetadata(true));

    /// <summary>Whether this column offers the header funnel. On by default: a table you cannot narrow is a report.</summary>
    public static readonly AdamantiumProperty CanUserFilterProperty = AdamantiumProperty.Register(nameof(CanUserFilter),
        typeof(bool), typeof(DataGridColumn), new PropertyMetadata(true));

    public static readonly AdamantiumProperty CanUserReorderProperty = AdamantiumProperty.Register(nameof(CanUserReorder),
        typeof(bool), typeof(DataGridColumn), new PropertyMetadata(true));

    public static readonly AdamantiumProperty IsReadOnlyProperty = AdamantiumProperty.Register(nameof(IsReadOnly),
        typeof(bool), typeof(DataGridColumn), new PropertyMetadata(false));

    private BoundValue _reader;

    public object Header
    {
        get => GetValue<object>(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public BindingBase Binding
    {
        get => GetValue<BindingBase>(BindingProperty);
        set => SetValue(BindingProperty, value);
    }

    public BindingBase StateBinding
    {
        get => GetValue<BindingBase>(StateBindingProperty);
        set => SetValue(StateBindingProperty, value);
    }

    public BindingBase IsReadOnlyBinding
    {
        get => GetValue<BindingBase>(IsReadOnlyBindingProperty);
        set => SetValue(IsReadOnlyBindingProperty, value);
    }

    public DataTemplate HeaderTemplate
    {
        get => GetValue<DataTemplate>(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }

    public GridLength Width
    {
        get => GetValue<GridLength>(WidthProperty);
        set => SetValue(WidthProperty, value);
    }

    public Double MinWidth
    {
        get => GetValue<Double>(MinWidthProperty);
        set => SetValue(MinWidthProperty, value);
    }

    public Double MaxWidth
    {
        get => GetValue<Double>(MaxWidthProperty);
        set => SetValue(MaxWidthProperty, value);
    }

    public DataTemplate CellTemplate
    {
        get => GetValue<DataTemplate>(CellTemplateProperty);
        set => SetValue(CellTemplateProperty, value);
    }

    public DataTemplate CellEditingTemplate
    {
        get => GetValue<DataTemplate>(CellEditingTemplateProperty);
        set => SetValue(CellEditingTemplateProperty, value);
    }

    public String SortMemberPath
    {
        get => GetValue<String>(SortMemberPathProperty);
        set => SetValue(SortMemberPathProperty, value);
    }

    public bool CanSort
    {
        get => GetValue<bool>(CanSortProperty);
        set => SetValue(CanSortProperty, value);
    }

    public DataGridFrozenSide FrozenSide
    {
        get => GetValue<DataGridFrozenSide>(FrozenSideProperty);
        set => SetValue(FrozenSideProperty, value);
    }

    /// <summary>Whether this column stands still while the table scrolls, on either edge.</summary>
    public bool IsFrozen => FrozenSide != DataGridFrozenSide.None;

    internal bool IsFrozenLeft => FrozenSide == DataGridFrozenSide.Left;

    internal bool IsFrozenRight => FrozenSide == DataGridFrozenSide.Right;

    public bool CanUserResize
    {
        get => GetValue<bool>(CanUserResizeProperty);
        set => SetValue(CanUserResizeProperty, value);
    }

    public bool CanUserFilter
    {
        get => GetValue<bool>(CanUserFilterProperty);
        set => SetValue(CanUserFilterProperty, value);
    }

    public bool CanUserReorder
    {
        get => GetValue<bool>(CanUserReorderProperty);
        set => SetValue(CanUserReorderProperty, value);
    }

    public bool IsReadOnly
    {
        get => GetValue<bool>(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>The width the last layout pass gave this column, in pixels. Written by the grid, read by the headers and
    /// by every row - one number from one place, or the header and the body drift apart, which is the classic table
    /// defect.</summary>
    public Double ActualWidth { get; internal set; }

    /// <summary>Where this column starts along the row, in the grid's own coordinates.</summary>
    public Double Offset { get; internal set; }

    /// <summary>The widest realized cell seen since the last reset, which is what an Auto column settles on. Only ever
    /// grows: recomputing it downwards every pass makes the columns breathe under the pointer.</summary>
    internal Double MeasuredWidth { get; set; }

    /// <summary>Forgets the measured width so the next pass takes it from the rows on screen NOW. The grid calls this on
    /// a source change, a sort, a filter and a double-click on the separator.</summary>
    internal void ResetMeasuredWidth() => MeasuredWidth = 0;

    /// <summary>What a cell wears while it is being edited: <see cref="CellEditingTemplate"/>, or the column's own
    /// default editor where it has one. A column with neither cannot be edited through the UI at all.</summary>
    protected internal virtual DataTemplate EditingTemplate => CellEditingTemplate;

    /// <summary>What a cell wears the rest of the time: <see cref="CellTemplate"/>, or the column's own default where it
    /// has one - a check box column draws a box, not the word "True".</summary>
    protected internal virtual DataTemplate DisplayTemplate => CellTemplate;

    /// <summary>Fills the editor built from <see cref="EditingTemplate"/> with the value being edited. Called once, when
    /// the editor appears; a column that declared no editor of its own has nothing to do here.</summary>
    protected internal virtual void PrepareEditor(IUIComponent editor, object item, object value) { }

    /// <summary>Reads back what the editor holds. Null means "nothing to say", and the value the cell already had
    /// stands - which is also what a template column does until it sets <see cref="DataGridCell.EditedValue"/>.</summary>
    protected internal virtual object ReadEditor(IUIComponent editor) => null;

    /// <summary>Whether ONE click on a cell flips its value. True for a check box, where the display and the editor are
    /// the same box: asking for a double-click first would be asking twice for the same thing.</summary>
    protected internal virtual bool TogglesOnClick => false;

    /// <summary>The content a cell shows for one row - the value the column's binding produces, or the ROW itself when
    /// the column names no binding, so a template column's cell binds against the item.</summary>
    protected internal object CellContentFor(object row) => Binding == null ? row : Read(Binding, row);

    // What a cell reads off its row changed, so every cell of this column has to read again. A binding is not layout:
    // nothing about it dirties a measure, and without this the new declaration would only take on the next thing that
    // happened to rebuild the rows.
    private static void OnRowBindingChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e) =>
        (component as DataGridColumn)?.Owner?.RefreshRealizedRows();

    /// <summary>The grid this column was added to, or null while it stands alone.</summary>
    protected internal TreeDataGrid Owner => LogicalParent as TreeDataGrid;

    /// <summary>Writes an edited value into one item through this column's binding, converting on the way. False when
    /// the binding refuses it - a one-way column is a column to read.</summary>
    protected internal bool Write(object item, object value)
    {
        if (Binding == null || item == null) return false;

        _reader ??= new BoundValue();
        _reader.PointAt(item, Binding);
        return _reader.Write(value);
    }

    /// <summary>Reads one of this column's row-bindings against one item. One reusable carrier per column: a bulk read -
    /// a sort, a filter's distinct values, a copy of the selection - walks every item in turn, and an object per item
    /// would be a garbage storm on a table that exists to hold a lot of rows.</summary>
    protected internal object Read(BindingBase binding, object item)
    {
        if (binding == null || item == null) return null;

        _reader ??= new BoundValue();
        _reader.PointAt(item, binding);
        return _reader.Value;
    }
}

/// <summary>A column of plain text, read off <see cref="Binding"/>. Editable out of the box: with no
/// <see cref="DataGridColumn.CellEditingTemplate"/> of its own it puts a text field in the cell, which is what makes a
/// double-click or F2 do something without any markup at all.</summary>
public class DataGridTextColumn : DataGridColumn
{
    private DataTemplate _defaultEditor;

    protected internal override DataTemplate EditingTemplate =>
        CellEditingTemplate ?? (_defaultEditor ??= new DataTemplate(() => new TemplateResult
        {
            RootComponent = DataGridEditors.Field()
        }));

    protected internal override void PrepareEditor(IUIComponent editor, object item, object value)
    {
        if (editor is not TextBox box) return;
        box.Text = value?.ToString() ?? String.Empty;
        box.SelectAll();
    }

    protected internal override object ReadEditor(IUIComponent editor) => (editor as TextBox)?.Text;
}

/// <summary>A column of check boxes. Draws a box in view and a live one in edit - the display box is there to be READ,
/// so it does not take the pointer; toggling is what editing the cell means.</summary>
public class DataGridCheckBoxColumn : DataGridColumn
{
    private DataTemplate _display;
    private DataTemplate _defaultEditor;

    protected internal override DataTemplate DisplayTemplate =>
        CellTemplate ?? (_display ??= DataGridEditors.Box(live: false));

    protected internal override DataTemplate EditingTemplate =>
        CellEditingTemplate ?? (_defaultEditor ??= DataGridEditors.Box(live: true));

    protected internal override void PrepareEditor(IUIComponent editor, object item, object value)
    {
        if (editor is ToggleButton toggle) toggle.IsChecked = value as bool? ?? false;
    }

    protected internal override object ReadEditor(IUIComponent editor) => (editor as ToggleButton)?.IsChecked;

    protected internal override bool TogglesOnClick => true;
}

/// <summary>A column whose cells are whatever <see cref="DataGridColumn.CellTemplate"/> says.</summary>
public class DataGridTemplateColumn : DataGridColumn
{
}

/// <summary>A column of choices. In VIEW it draws text, not a live DropDown: a hundred visible cells would otherwise be
/// a hundred controls with popups and subscriptions for a static string. The real DropDown appears only while the cell
/// is being edited - which is exactly the CellTemplate / CellEditingTemplate split.</summary>
public class DataGridDropDownColumn : DataGridColumn
{
    public static readonly AdamantiumProperty ItemsSourceProperty = AdamantiumProperty.Register(nameof(ItemsSource),
        typeof(IEnumerable), typeof(DataGridDropDownColumn), new PropertyMetadata(null));

    public static readonly AdamantiumProperty DisplayMemberPathProperty = AdamantiumProperty.Register(
        nameof(DisplayMemberPath), typeof(String), typeof(DataGridDropDownColumn), new PropertyMetadata(null));

    public static readonly AdamantiumProperty SelectedValuePathProperty = AdamantiumProperty.Register(
        nameof(SelectedValuePath), typeof(String), typeof(DataGridDropDownColumn), new PropertyMetadata(null));

    public static readonly AdamantiumProperty IsEditableTextProperty = AdamantiumProperty.Register(nameof(IsEditableText),
        typeof(bool), typeof(DataGridDropDownColumn), new PropertyMetadata(false));

    private DataTemplate _defaultEditor;

    public IEnumerable ItemsSource
    {
        get => GetValue<IEnumerable>(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public String DisplayMemberPath
    {
        get => GetValue<String>(DisplayMemberPathProperty);
        set => SetValue(DisplayMemberPathProperty, value);
    }

    public String SelectedValuePath
    {
        get => GetValue<String>(SelectedValuePathProperty);
        set => SetValue(SelectedValuePathProperty, value);
    }

    public bool IsEditableText
    {
        get => GetValue<bool>(IsEditableTextProperty);
        set => SetValue(IsEditableTextProperty, value);
    }

    /// <summary>Choices for one row when they differ per row. Asked LAZILY, when editing starts - resolving it per cell
    /// would have every visible row drag in a list of choices, and only the edited one needs it.</summary>
    public Func<object, IEnumerable> ItemsSourceSelector { get; set; }

    /// <summary>The choices for <paramref name="row"/>: the per-row selector if there is one, otherwise the shared list -
    /// which is an ordinary bindable property, so a view-model's list reaches it like anything else. Called when editing
    /// begins, not when a cell is built.</summary>
    public IEnumerable ChoicesFor(object row) => ItemsSourceSelector?.Invoke(row) ?? ItemsSource;

    protected internal override DataTemplate EditingTemplate =>
        CellEditingTemplate ?? (_defaultEditor ??= new DataTemplate(() => new TemplateResult
        {
            RootComponent = new DropDown { MinWidth = 0, MinHeight = 0, BorderThickness = new Thickness(0) }
        }));

    protected internal override void PrepareEditor(IUIComponent editor, object item, object value)
    {
        if (editor is not DropDown drop) return;

        drop.ItemTemplate = DisplayMemberPath is { Length: > 0 } ? DataGridEditors.Label(DisplayMemberPath) : null;
        drop.ItemsSource = ChoicesFor(item);
        drop.SelectedItem = Match(drop.ItemsSource, value);
    }

    protected internal override object ReadEditor(IUIComponent editor) =>
        editor is DropDown { SelectedItem: { } chosen } ? Project(chosen) : null;

    private object Match(IEnumerable choices, object value)
    {
        if (choices == null) return null;

        foreach (var choice in choices)
        {
            if (Equals(Project(choice), value)) return choice;
        }

        return null;
    }

    private object Project(object choice) =>
        SelectedValuePath is { Length: > 0 } path ? TreeChildResolver.ForValuePath(path)(choice) : choice;
}

internal static class DataGridEditors
{
    public static TextBox Field() => new()
    {
        BorderThickness = new Thickness(0),
        Padding = new Thickness(0),
        MinWidth = 0,
        MinHeight = 0,
        VerticalAlignment = VerticalAlignment.Stretch
    };

    public static DataTemplate Box(bool live) => new(() =>
    {
        var check = new CheckBox
        {
            IsHitTestVisible = live,
            MinWidth = 0,
            MinHeight = 0,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var result = new TemplateResult { RootComponent = check };
        result.AddBinding(check, "IsChecked", new Binding());
        return result;
    });

    public static DataTemplate Label(string path) => new(() =>
    {
        var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        var result = new TemplateResult { RootComponent = text };
        result.AddBinding(text, "Text", new Binding(path));
        return result;
    });
}
