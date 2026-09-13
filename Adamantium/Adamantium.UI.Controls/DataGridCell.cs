using System;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>One cell: the crossing of a realized row and a realized column. Holds no data of its own - it is told which
/// column and which item it stands for and reads both from there, so it survives being recycled onto another row.</summary>
public class DataGridCell : ContentControl
{
    // A cell CLIPS ONLY WHILE IT IS BEING EDITED (see OnIsEditingChanged): a clip is a GPU scissor and a scissor change
    // ends the batch, so a clipping cell cannot share a draw call - measured at 467 draws a frame against 191. The
    // editor is the one case it was ever for; ordinary content is kept inside the column by TextTrimming.
    // "Clip when the content overflows" is NOT AVAILABLE: Measure and Arrange both constrain what a child reports, so
    // neither pass can tell a cell that its content wanted more room.

    public static readonly AdamantiumProperty IsSelectedProperty = AdamantiumProperty.Register(nameof(IsSelected),
        typeof(bool), typeof(DataGridCell), new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty IsActiveProperty = AdamantiumProperty.Register(nameof(IsActive),
        typeof(bool), typeof(DataGridCell), new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    /// <summary>This cell holds what the search panel is looking for.</summary>
    public static readonly AdamantiumProperty IsSearchMatchProperty = AdamantiumProperty.Register(
        nameof(IsSearchMatch), typeof(bool), typeof(DataGridCell),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    /// <summary>...and it is the ONE the search is looking at. Set apart from the rest, the way a browser sets the
    /// current hit apart from the others it found.</summary>
    public static readonly AdamantiumProperty IsCurrentSearchMatchProperty = AdamantiumProperty.Register(
        nameof(IsCurrentSearchMatch), typeof(bool), typeof(DataGridCell),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    public bool IsSearchMatch
    {
        get => GetValue<bool>(IsSearchMatchProperty);
        set => SetValue(IsSearchMatchProperty, value);
    }

    public bool IsCurrentSearchMatch
    {
        get => GetValue<bool>(IsCurrentSearchMatchProperty);
        set => SetValue(IsCurrentSearchMatchProperty, value);
    }

    /// <summary>Selected as part of a range. Separate from <see cref="IsActive"/>, as in a spreadsheet: the active cell
    /// sits inside the selection and moves through it without clearing it.</summary>
    public bool IsSelected
    {
        get => GetValue<bool>(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>The one cell the keyboard is on.</summary>
    public bool IsActive
    {
        get => GetValue<bool>(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>The column this cell belongs to - the source of its width, its template and its content.</summary>
    public DataGridColumn Column { get; internal set; }

    /// <summary>Index of <see cref="Column"/> in the grid, so a cell can name itself without a search.</summary>
    public int ColumnIndex { get; internal set; }

    /// <summary>The item this cell's row stands for.</summary>
    public object Item { get; internal set; }

    /// <summary>How far the content is pushed right: the row's depth times the grid's indent, plus room for the expander.
    /// Non-zero ONLY on the column that shows the expander - every other column draws flat data at its own edge, which is
    /// what lets the hierarchy start at the third column instead of the first.</summary>
    public static readonly AdamantiumProperty IndentProperty = AdamantiumProperty.Register(nameof(Indent),
        typeof(Double), typeof(DataGridCell),
        new PropertyMetadata(0.0, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsArrange,
            OnIndentChanged));

    private static void OnIndentChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e) =>
        (d as DataGridCell)?.ApplyIndent();

    public static readonly AdamantiumProperty ShowsExpanderProperty = AdamantiumProperty.Register(nameof(ShowsExpander),
        typeof(bool), typeof(DataGridCell),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsMeasure | PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty IsExpandedProperty = AdamantiumProperty.Register(nameof(IsExpanded),
        typeof(bool), typeof(DataGridCell), new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    /// <summary>What the view-model says this cell MEANS - an error, a warning, whatever the application distinguishes.
    /// The theme turns it into a look; the model never names a colour.</summary>
    public static readonly AdamantiumProperty StateProperty = AdamantiumProperty.Register(nameof(State),
        typeof(object), typeof(DataGridCell), new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

    public static readonly AdamantiumProperty IsReadOnlyProperty = AdamantiumProperty.Register(nameof(IsReadOnly),
        typeof(bool), typeof(DataGridCell), new PropertyMetadata(false));

    /// <summary>Whether this cell draws the rule down its right edge - the grid's
    /// <see cref="TreeDataGrid.GridLinesVisibility"/>. On the CELL and not on the row, because a vertical rule belongs
    /// to a column: it has to stop where the column stops.</summary>
    public static readonly AdamantiumProperty ShowsVerticalLineProperty = AdamantiumProperty.Register(
        nameof(ShowsVerticalLine), typeof(bool), typeof(DataGridCell),
        new PropertyMetadata(true, PropertyMetadataOptions.AffectsRender, OnGridLineChanged));

    /// <summary>Whether this cell draws the rule under itself.</summary>
    public static readonly AdamantiumProperty ShowsHorizontalLineProperty = AdamantiumProperty.Register(
        nameof(ShowsHorizontalLine), typeof(bool), typeof(DataGridCell),
        new PropertyMetadata(true, PropertyMetadataOptions.AffectsRender, OnGridLineChanged));

    /// <summary>The two rules as ONE thickness for the cell's own border to carry - right for the column rule, bottom
    /// for the row rule. Elements of their own cost 147 draw segments a frame on a 90-cell table; the active cell's
    /// outline simply overrides the rule, and there is only ever one of it.</summary>
    /// <remarks>The default MATCHES the two flags' defaults: both are true, so a cell never CHANGES them and the
    /// callback never runs - a zero default would leave every cell ruleless.</remarks>
    public static readonly AdamantiumProperty GridLineThicknessProperty = AdamantiumProperty.Register(
        nameof(GridLineThickness), typeof(Thickness), typeof(DataGridCell),
        new PropertyMetadata(new Thickness(0, 0, 1, 1), PropertyMetadataOptions.AffectsRender));

    public Thickness GridLineThickness
    {
        get => GetValue<Thickness>(GridLineThicknessProperty);
        private set => SetValue(GridLineThicknessProperty, value);
    }

    private static void OnGridLineChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not DataGridCell cell) return;

        cell.GridLineThickness = new Thickness(0, 0,
            cell.ShowsVerticalLine ? 1 : 0,
            cell.ShowsHorizontalLine ? 1 : 0);
    }

    /// <summary>What the rules are painted with, or null to leave it to the theme.</summary>
    public static readonly AdamantiumProperty GridLineBrushProperty = AdamantiumProperty.Register(
        nameof(GridLineBrush), typeof(Brush), typeof(DataGridCell),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

    /// <summary>What a cell the search found is washed with. The theme sets it; a grid that names its own
    /// <see cref="TreeDataGrid.SearchMatchBrush"/> overrides it, exactly as it does the grid lines.</summary>
    public static readonly AdamantiumProperty SearchMatchBrushProperty = AdamantiumProperty.Register(
        nameof(SearchMatchBrush), typeof(Brush), typeof(DataGridCell),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

    /// <summary>What a cell holding a value the column will not accept is washed with. Set by the theme and overridden
    /// by <see cref="TreeDataGrid.ValidationErrorBrush"/>, exactly as the search washes are.</summary>
    public static readonly AdamantiumProperty ValidationErrorBrushProperty = AdamantiumProperty.Register(
        nameof(ValidationErrorBrush), typeof(Brush), typeof(DataGridCell),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

    /// <summary>What is wrong with this cell's value, or null when nothing is. The MESSAGE, so the cell can say it -
    /// a red box that does not tell you what it wants is a puzzle, not a validation.</summary>
    public static readonly AdamantiumProperty ValidationErrorProperty = AdamantiumProperty.Register(
        nameof(ValidationError), typeof(string), typeof(DataGridCell),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender, OnValidationErrorChanged));

    /// <summary>Whether <see cref="ValidationError"/> says anything - the flag a theme trigger reads, because a trigger
    /// tests a value and "any non-empty string" is not one.</summary>
    public static readonly AdamantiumProperty HasValidationErrorProperty = AdamantiumProperty.Register(
        nameof(HasValidationError), typeof(bool), typeof(DataGridCell),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    public Brush ValidationErrorBrush
    {
        get => GetValue<Brush>(ValidationErrorBrushProperty);
        set => SetValue(ValidationErrorBrushProperty, value);
    }

    public string ValidationError
    {
        get => GetValue<string>(ValidationErrorProperty);
        set => SetValue(ValidationErrorProperty, value);
    }

    public bool HasValidationError
    {
        get => GetValue<bool>(HasValidationErrorProperty);
        private set => SetValue(HasValidationErrorProperty, value);
    }

    // The message carries the tooltip with it: one write says what is wrong, whether it reaches the eye as a wash or
    // as words. A cell is recycled, so the empty case has to clear the tooltip rather than leave the last row's.
    private static void OnValidationErrorChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (d is not DataGridCell cell) return;

        var text = e.NewValue as string;
        cell.HasValidationError = !string.IsNullOrEmpty(text);
        cell.ToolTip = cell.HasValidationError ? text : null;
    }

    /// <summary>...and the cell the search is ON. A WASH, never a plate: a cell holds a check box and a meaning, and a
    /// solid colour swallows both.</summary>
    public static readonly AdamantiumProperty SearchCurrentBrushProperty = AdamantiumProperty.Register(
        nameof(SearchCurrentBrush), typeof(Brush), typeof(DataGridCell),
        new PropertyMetadata(null, PropertyMetadataOptions.AffectsRender));

    public Brush SearchMatchBrush
    {
        get => GetValue<Brush>(SearchMatchBrushProperty);
        set => SetValue(SearchMatchBrushProperty, value);
    }

    public Brush SearchCurrentBrush
    {
        get => GetValue<Brush>(SearchCurrentBrushProperty);
        set => SetValue(SearchCurrentBrushProperty, value);
    }

    public bool ShowsVerticalLine
    {
        get => GetValue<bool>(ShowsVerticalLineProperty);
        set => SetValue(ShowsVerticalLineProperty, value);
    }

    public bool ShowsHorizontalLine
    {
        get => GetValue<bool>(ShowsHorizontalLineProperty);
        set => SetValue(ShowsHorizontalLineProperty, value);
    }

    public Brush GridLineBrush
    {
        get => GetValue<Brush>(GridLineBrushProperty);
        set => SetValue(GridLineBrushProperty, value);
    }

    /// <summary>In edit: the cell shows its column's editing template instead of its ordinary one. Only this cell
    /// changes - editing one value must not rebuild the row around it.</summary>
    public static readonly AdamantiumProperty IsEditingProperty = AdamantiumProperty.Register(nameof(IsEditing),
        typeof(bool), typeof(DataGridCell),
        new PropertyMetadata(false, PropertyMetadataOptions.AffectsMeasure, OnIsEditingChanged));

    public bool IsEditing
    {
        get => GetValue<bool>(IsEditingProperty);
        set => SetValue(IsEditingProperty, value);
    }

    private static void OnIsEditingChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        if (d is not DataGridCell cell || cell.Column == null) return;

        cell.DetachEditor();
        cell.ContentTemplate = cell.IsEditing
            ? cell.Column.EditingTemplate ?? cell.Column.DisplayTemplate
            : cell.IsPlaceholder ? null
            : cell.Column.DisplayTemplate;
        cell._pendingEditorFocus = cell.IsEditing;
        cell.ClipToBounds = cell.IsEditing;
    }

    /// <summary>What the editor currently holds, for the commit to write. Null when nothing has been typed, in which case
    /// the commit falls back to the value the cell already showed.</summary>
    public object EditedValue { get; set; }

    internal object ValueForCommit() => EditedValue ?? Column?.ReadEditor(_editor) ?? Content;

    /// <summary>What the user typed to OPEN this editor. It is the edited VALUE from the moment it is typed - the
    /// editor is only the view of it, and it is built by the template swap this edit asks for and arrives a layout pass
    /// later. Written through here rather than into the editor so that the character is not lost in between: a commit
    /// in that gap would otherwise write back the value the typing was replacing.</summary>
    internal string PendingText
    {
        get => _pendingText;
        set
        {
            _pendingText = value;
            if (value is { Length: > 0 }) EditedValue = value;
        }
    }

    private string _pendingText;

    // A field of the strip for a record that does not exist yet, rather than a cell of a row. It stands for nothing
    // until someone types in it, and shows nothing until then.
    internal bool IsPlaceholder { get; set; }

    private void AttachEditor()
    {
        _editor = FindEditor(this);

        // Not found is NOT "there is none": the editing template is swapped in by this very edit, and its content can
        // be built a pass later than the arrange that first asks for it. Giving up here left an editor the user could
        // see and type into but that the cell did not know about - no Enter, no value on commit. The flag stays up and
        // the next arrange asks again; it comes down by itself when the edit ends.
        if (_editor == null) return;

        _pendingEditorFocus = false;

        Column?.PrepareEditor(_editor, Item, Content);

        // ...and what the user typed REPLACES what the editor was prepared with: typing over a cell is how a value is
        // replaced everywhere else, and an editor that kept the old value and appended would spell nonsense.
        if (PendingText is { Length: > 0 } seed && _editor is Text.TextBoxBase typed)
        {
            typed.Text = seed;

            // ...and the SELECTION has to be collapsed behind it. PrepareEditor selects the whole value so that typing
            // replaces it - which is right when the editor is opened with F2, and here would have the NEXT character
            // replace the one that opened it: five keys went in and "hello" came out "ello".
            typed.SelectionLength = 0;
            typed.SelectionStart = seed.Length;
            typed.CaretIndex = seed.Length;

            // ...and the seed stops being the answer the moment the editor holds it. EditedValue outranks the editor on
            // a commit - that is how a template column writes back - so leaving the first character there made it the
            // WHOLE value: five keys in, one character saved.
            EditedValue = null;
        }

        PendingText = null;
        if (_editor is TextBox box) box.EnterPressed += OnEditorEnter;

        // A drop-down COMMITS ON CHOICE. Its list is a popup: opening it takes the focus off the editor, so the
        // lost-focus rule alone ended the edit before anything was chosen and wrote back the value that was already
        // there - the cell looked as if the choice had been ignored.
        if (_editor is DropDown drop) drop.SelectionChanged += OnEditorChosen;

        _editor.LostFocus += OnEditorLostFocus;
        _editor.Focus();
    }

    private void DetachEditor()
    {
        EditedValue = null;
        if (_editor == null) return;

        if (_editor is TextBox box) box.EnterPressed -= OnEditorEnter;
        if (_editor is DropDown drop) drop.SelectionChanged -= OnEditorChosen;
        _editor.LostFocus -= OnEditorLostFocus;
        _editor = null;
    }

    private static IInputComponent FindEditor(IUIComponent root)
    {
        foreach (var child in root.VisualChildren)
        {
            if (child is IInputComponent { Focusable: true } editor) return editor;
            if (FindEditor(child) is { } nested) return nested;
        }

        return null;
    }

    private void OnEditorEnter(object sender, KeyEventArgs e)
    {
        var back = (e.Modifiers & (InputModifiers.LeftShift | InputModifiers.RightShift)) != 0;
        if (OwningGrid()?.FinishEditAndStep(back) == true) e.Handled = true;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        // Enter says "this cell is done", and the TUNNEL is the only place it can say so before the editor spends the
        // key on something else: a closed drop-down answers Enter by opening its list, so an edit made entirely of a
        // choice had no way to be finished at all - the key just re-opened what the user had already chosen from. An
        // OPEN list keeps the key, because there Enter is how a row is picked.
        if (e.Handled || e.Key != Key.Enter || !IsEditing) return;
        if (_editor is not DropDown { IsDropDownOpen: false }) return;

        var back = (e.Modifiers & (InputModifiers.LeftShift | InputModifiers.RightShift)) != 0;
        if (OwningGrid()?.FinishEditAndStep(back) == true) e.Handled = true;
    }

    private void OnEditorChosen(object sender, EventArgs e)
    {
        if (IsEditing) OwningGrid()?.FinishEditOnChoice();
    }

    private void OnEditorLostFocus(object sender, RoutedEventArgs e)
    {
        // ...and while that list is OPEN the focus is inside it, not gone from the cell: ending the edit here would
        // close the very list the user is choosing from.
        if (_editor is DropDown { IsDropDownOpen: true }) return;
        if (IsEditing) OwningGrid()?.FinishEditOnFocusLoss();
    }

    private TreeDataGrid OwningGrid()
    {
        // A cell lives in a row or in the strip for the record that does not exist yet. Both are ASKED rather than
        // guessed at: each holds the grid it belongs to, so a cell in a grid inside a cell cannot answer for the wrong
        // one.
        for (IUIComponent node = this; node != null; node = node.VisualParent)
        {
            if (node is DataGridRow row) return row.Owner;
            if (node is DataGridNewRowPresenter strip) return strip.Owner;
        }

        return null;
    }

    public object State
    {
        get => GetValue<object>(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public bool IsReadOnly
    {
        get => GetValue<bool>(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public Double Indent
    {
        get => GetValue<Double>(IndentProperty);
        set => SetValue(IndentProperty, value);
    }

    /// <summary>This cell carries the expander: it has room for one and, when its row has children, draws it.</summary>
    public bool ShowsExpander
    {
        get => GetValue<bool>(ShowsExpanderProperty);
        set => SetValue(ShowsExpanderProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue<bool>(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>Points the cell at a column and an item and pulls its content from them. Called on creation and again on
    /// every reuse - a recycled cell must forget everything about the row it came from.</summary>
    internal void Attach(DataGridColumn column, int columnIndex, object item, int row = -1)
    {
        Column = column;
        ColumnIndex = columnIndex;
        Item = item;

        // The EDITING template has to survive this. Attach runs on every measure pass, not only on a rebind, so handing
        // the cell its plain template back here wiped the editor before it was ever built - editing looked implemented
        // and did nothing at all.
        // A PLACEHOLDER shows none of it until it is opened: a display template over a record that does not exist draws
        // a live-looking control - the stand's tick box came out bright and half-set - that reads to the user as part
        // of the table and answers to nothing.
        ContentTemplate = IsEditing ? column?.EditingTemplate ?? column?.DisplayTemplate
            : IsPlaceholder ? null
            : column?.DisplayTemplate;

        Content = column?.CellContentFor(item);
        State = column?.StateFor(item);
        IsReadOnly = (column?.IsReadOnly ?? false)
                     || (column?.IsReadOnlyBinding != null && column.Read(column.IsReadOnlyBinding, item) as bool? == true);

        var grid = OwningGrid();
        ShowsVerticalLine = grid?.ShowsVerticalLines ?? false;
        ShowsHorizontalLine = grid?.ShowsHorizontalLines ?? false;

        // The selection is held as RANGES, not on the cells, so a cell built AFTER the selection was made has to ask
        // for its state - otherwise a row selected before a sideways scroll stops being highlighted at whichever column
        // happened to be realized at the time, and the band never reaches the row's edge.
        if (grid != null && row >= 0)
        {
            IsSelected = grid.SelectedCells.Contains(row, columnIndex) && !IsEditing;
            IsActive = row == grid.ActiveRow && columnIndex == grid.ActiveColumn;
        }

        // The search is held by ITEM, not by row number - the numbers move when a group opens - so a cell asks with
        // the item it is showing, and a cell built after the search was run is painted like the rest.
        IsSearchMatch = grid?.IsSearchMatch(item, columnIndex) ?? false;
        IsCurrentSearchMatch = grid?.IsCurrentSearchMatch(item, columnIndex) ?? false;

        // A brush the grid names wins over the theme's, and one it stops naming HANDS THE COLOUR BACK. Writing null
        // would not do that: null is a local value like any other, and a local value outranks the theme - the cell
        // would be left painted in nothing. Clearing the local slot is what lets the theme's own setter be seen again,
        // and it has to be done on the way out too: these carriers are recycled, so a cell that once took the grid's
        // colour would otherwise keep it for every row it is ever reused for.
        Adopt(GridLineBrushProperty, grid?.GridLinesBrush);
        Adopt(SearchMatchBrushProperty, grid?.SearchMatchBrush);
        Adopt(SearchCurrentBrushProperty, grid?.SearchCurrentMatchBrush);
        Adopt(ValidationErrorBrushProperty, grid?.ValidationErrorBrush);

        // Asked here, with everything else a cell learns on the way in, so a row scrolled back into view is marked the
        // same as one that never left. A cell being EDITED is left alone: the value under the editor is the old one,
        // and marking it red while the user is still typing the replacement reads as a complaint about the typing.
        ValidationError = IsEditing ? Refusal : column?.Validate(item);
    }

    /// <summary>Why the editor will not let go, while the table is BLOCKING - see
    /// <see cref="TreeDataGrid.ValidationMode"/>. The one thing an editing cell is marked for: the complaint is not
    /// about the typing, it is about the table refusing to take what was typed.</summary>
    internal string Refusal
    {
        get => _refusal;
        set
        {
            _refusal = value;
            if (IsEditing) ValidationError = value;
        }
    }

    private string _refusal;

    // Focus follows the pointer out of a refused editor before anything can object, so it is put back: an editor that
    // is still open and no longer has the caret is an editor the user cannot answer.
    internal void FocusEditor() => _editor?.Focus();

    private void Adopt(AdamantiumProperty property, Brush brush)
    {
        if (brush != null) SetValue(property, brush);
        else ClearValue(property);
    }

    /// <summary>Content is PUSHED RIGHT by <see cref="Indent"/> - that shift is the whole of what depth looks like in a
    /// tree table. The cell keeps its column's width whatever its content wants: the grid decided the widths once for
    /// everyone.</summary>
    protected override Size MeasureOverride(Size availableSize) => base.MeasureOverride(availableSize);

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (var visual in VisualChildren)
        {
            if (visual is IMeasurableComponent child) child.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
        }

        if (_pendingEditorFocus) AttachEditor();
        return finalSize;
    }

    // The depth shifts the CONTENT, and only the content. Shifting the whole of the template moved its ROOT with it -
    // and the root is where the frame lives, the grid lines and the selection wash - so the one column that carries the
    // expander began after an unpainted band: a selected cell there started past a hole, and under grouping, where every
    // record sits a level or two deep, the hole was the width of the nesting.
    private void ApplyIndent()
    {
        // MarginLeft, not Margin: it is an override of ONE side, so whatever margin the template authored for this part
        // stays its own and the depth never fights it.
        if (_indent != null) _indent.MarginLeft = Math.Max(0, Indent);
    }

    /// <summary>Where the expander sits, in this cell's own coordinates - the strip just left of the content. Empty when
    /// this cell carries no expander, so a hit test can ask without knowing which column it is on.</summary>
    /// <summary>Whether this cell's row can be opened - the expander is drawn only then, though the strip is reserved
    /// either way so the rows of one level line up.</summary>
    public static readonly AdamantiumProperty HasChildrenProperty = AdamantiumProperty.Register(nameof(HasChildren),
        typeof(bool), typeof(DataGridCell), new PropertyMetadata(false, PropertyMetadataOptions.AffectsRender));

    public bool HasChildren
    {
        get => GetValue<bool>(HasChildrenProperty);
        set => SetValue(HasChildrenProperty, value);
    }

    /// <summary>The expander is clicked on the element that DRAWS it - PART_Expander in the cell's template - not on a
    /// rectangle the grid works out for itself. Two descriptions of one strip are two chances to disagree.</summary>
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_expander != null) _expander.MouseLeftButtonDown -= OnExpanderPressed;
        _expander = GetTemplateChild("PART_Expander") as IInputComponent;
        if (_expander != null) _expander.MouseLeftButtonDown += OnExpanderPressed;

        _indent = GetTemplateChild("PART_Indent") as MeasurableUIComponent;
        ApplyIndent();   // a fresh template starts flat, and the depth it is standing at is already known
    }

    private MeasurableUIComponent _indent;
    private IInputComponent _expander;
    private IInputComponent _editor;
    private bool _pendingEditorFocus;

    private void OnExpanderPressed(object sender, MouseButtonEventArgs e)
    {
        if (!ShowsExpander || !HasChildren) return;

        for (IUIComponent node = this; node != null; node = node.VisualParent)
        {
            if (node is not DataGridRow row) continue;
            row.ToggleFromExpander();
            e.Handled = true;
            return;
        }
    }
}
