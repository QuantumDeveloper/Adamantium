using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.MVVM;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.DataGrid;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>One node of the grid's data. A tree AND a table: the first columns are flat facts, the hierarchy starts
/// wherever the expander column is put - which is the case this control was written for.
/// <para>It reports its OWN errors (<see cref="System.ComponentModel.INotifyDataErrorInfo"/>), which is the second of the two
/// ways the grid learns a cell is wrong - the first being a rule on the column. Both are shown on this page on purpose:
/// a rule is what a page without a validating model uses, and this is what a model that knows better says for itself.</para></summary>
public class GridNode : System.ComponentModel.INotifyDataErrorInfo
{
    public string Code { get; set; }

    /// <summary>The code of the record this one belongs to, or null at the top. What <c>ParentKeyPath</c> reads - the
    /// same parentage the nested <see cref="Children"/> already holds, said as a key instead.</summary>
    public string ParentCode { get; set; }

    public string Owner { get; set; }
    public string Name { get; set; }
    public int Size { get; set; }

    /// <summary>Setting it re-states the record's errors, so a status typed into the grid repaints the cell without
    /// anything else being touched - which is the half of INotifyDataErrorInfo that is easy to leave out.</summary>
    public string Status
    {
        get => _status;
        set
        {
            if (_status == value) return;
            _status = value;
            ErrorsChanged?.Invoke(this, new System.ComponentModel.DataErrorsChangedEventArgs(nameof(Status)));
        }
    }

    private string _status;

    public bool HasErrors => _status == "error";

    public event EventHandler<System.ComponentModel.DataErrorsChangedEventArgs> ErrorsChanged;

    public IEnumerable GetErrors(string propertyName) =>
        propertyName == nameof(Status) && HasErrors
            ? new[] { "The record is filed as an error" }
            : Array.Empty<string>();

    public bool Done { get; set; }
    public string Checksum { get; set; }
    public string Region { get; set; }
    public string Batch { get; set; }
    public string Revision { get; set; }
    public string Line { get; set; }
    public string Shift { get; set; }
    public string Updated { get; set; }
    public ObservableCollection<GridNode> Children { get; } = new();

    /// <summary>What this row's status MEANS - a meaning, never a colour: the theme decides what "error" looks like, and
    /// a model that handed out brushes would break the moment the theme changed. Read by the Status column's
    /// <see cref="DataGridColumn.StateBinding"/>, like any other value.</summary>
    public object StatusState => Status == "error" ? "Error" : null;

    /// <summary>A finished record keeps the code it was filed under. Read by the Code column's
    /// <see cref="DataGridColumn.IsReadOnlyBinding"/> - which is how ONE CELL refuses an edit its column allows.</summary>
    public bool IsLocked => Done;
}

/// <summary>Data grid tab: ten thousand rows with a subtree, and every feature reachable from the toolbar above it.</summary>
[ViewModel]
public partial class DataGridViewModel : TabPageViewModel
{
    private static readonly string[] Owners = ["Ada", "Grace", "Alan", "Edsger", "Barbara"];
    private static readonly string[] Statuses = ["ok", "review", "error"];
    private static readonly string[] Regions = ["Nordics", "Iberia", "Levant", "Pacific", "Andes"];
    private static readonly string[] Shifts = ["day", "night", "swing"];

    private static void Branch(GridNode parent, Random random, int seed, int depth, int deepest)
    {
        if (depth > deepest) return;

        var count = depth == 1 ? 4 : 2;
        for (var c = 1; c <= count; c++)
        {
            var child = new GridNode
            {
                Code = $"{parent.Code}.{c}",
                Owner = parent.Owner,
                Name = depth == 1 ? $"Part {c} of {parent.Code}" : $"Level {depth + 1} item {c} of {parent.Code}",
                Size = random.Next(1, 999),
                Status = Statuses[(seed + c + depth) % Statuses.Length],
                Done = (c + depth) % 2 == 0
            };

            // The SAME parentage, said the other way: held as nesting for ChildrenPath, and named by key for
            // KeyPath/ParentKeyPath. One generator feeds both, so the two ways of describing a tree can be compared on
            // the same data instead of on two demos that only look alike.
            child.ParentCode = parent.Code;

            Fill(child, seed + c + depth);
            Branch(child, random, seed + c, depth + 1, deepest);
            parent.Children.Add(child);
        }
    }

    private static void Fill(GridNode node, int seed)
    {
        node.Checksum = $"{seed * 2654435761u & 0xFFFFFF:X6}";
        node.Region = Regions[seed % Regions.Length];
        node.Batch = $"B-{seed % 97:D2}";
        node.Revision = $"r{seed % 40 + 1}";
        node.Line = $"L{seed % 8 + 1}";
        node.Shift = Shifts[seed % Shifts.Length];
        node.Updated = $"2026-{seed % 12 + 1:D2}-{seed % 27 + 1:D2}";
    }

    public DataGridViewModel() : base("Data grid")
    {
        var random = new Random(7);
        var roots = new List<GridNode>();

        // 10 000 rows before a single branch is opened, so the strip is virtualized from the first frame.
        for (var i = 1; i <= 10_000; i++)
        {
            var node = new GridNode
            {
                Code = $"P-{i:D5}",
                Owner = Owners[i % Owners.Length],
                Name = $"Assembly {i} - a name long enough to need trimming",
                Size = random.Next(1, 9999),
                Status = Statuses[i % Statuses.Length],
                // Every FOURTH, not every third: stepping in time with the status meant "done" only ever landed on an
                // "ok" record, so the one thing a row rule is here to show - a record that is done AND filed as an
                // error - could not occur at all until somebody ticked a box by hand. Out of step, it turns up on one
                // row in twelve, starting at the eighth.
                Done = i % 4 == 0
            };
            Fill(node, i);

            // Every fifth row carries a branch, and the deeper the row, the FEWER of them - so the set stays a table
            // with trees in it rather than a tree. The depth is not a limit of the control: it flattens whatever it is
            // given, and the indent walks with it.
            if (i % 5 == 0) Branch(node, random, i, depth: 1, deepest: i % 20 == 0 ? 4 : 2);

            roots.Add(node);
        }

        Nodes = new ObservableCollection<GridNode>(roots);

        // EVERY record at the top level - which is what a flat source IS. The same set the nested one holds, said the
        // way a database hands it over: no record contains another, and who belongs to whom is in the keys.
        var flat = new List<GridNode>(roots.Count);
        foreach (var root in roots) Flatten(root, flat);
        FlatNodes = new ObservableCollection<GridNode>(flat);
    }

    private static void Flatten(GridNode node, List<GridNode> into)
    {
        into.Add(node);
        foreach (var child in node.Children) Flatten(child, into);
    }

    public ObservableCollection<GridNode> Nodes { get; }

    /// <summary>The same records, flat. Bound instead of <see cref="Nodes"/> when the tree is worked out from keys.
    /// </summary>
    public ObservableCollection<GridNode> FlatNodes { get; }

    /// <summary>Which of the two ways the table is told about the tree. The rows on screen must come out IDENTICAL -
    /// that is the whole point of having both on one set of data, and the one way to see that the key relation really
    /// produces the same tree rather than something that merely looks like one.</summary>
    [Bindable] private bool _treeFromKeys;

    /// <summary>What the table reads: the nested records, or all of them flat.</summary>
    public IEnumerable TreeSource => TreeFromKeys ? FlatNodes : Nodes;

    /// <summary>Set only in the nested mode - the two ways must not both be on, or the table would be told the same
    /// parentage twice and the key relation would be the one quietly ignored.</summary>
    public string TreeChildrenPath => TreeFromKeys ? null : "Children";

    public string TreeKeyPath => TreeFromKeys ? "Code" : null;

    public string TreeParentKeyPath => TreeFromKeys ? "ParentCode" : null;

    /// <summary>What the table was GIVEN, which is the only visible difference between the two modes - and the point.
    /// The rows come out identical either way; what changes is the shape of the source. Said as a COUNT because that
    /// is what proves it: hand the table 12 448 flat records and it shows 10 000 rows with branches, so the relation
    /// really did build the tree. Were KeyPath quietly ignored, all 12 448 would be sitting in the table.</summary>
    public string TreeSourceStatus => TreeFromKeys
        ? $"{FlatNodes.Count:N0} records, all at the top level - the tree is worked out from Code / ParentCode"
        : $"{Nodes.Count:N0} records, each holding its own children";

    partial void OnTreeFromKeysChanged(bool value)
    {
        RaisePropertyChanged(nameof(TreeSource));
        RaisePropertyChanged(nameof(TreeChildrenPath));
        RaisePropertyChanged(nameof(TreeKeyPath));
        RaisePropertyChanged(nameof(TreeParentKeyPath));
        RaisePropertyChanged(nameof(TreeSourceStatus));
    }

    [Bindable] private int _alternationCount = 2;

    [Bindable, Affects(nameof(CodeSide))] private bool _pinCode;

    /// <summary>Pins a column from the MIDDLE of the table, to show that the pinned zone is a place in the layout
    /// rather than a prefix of what was declared.</summary>
    [Bindable, Affects(nameof(RegionSide))] private bool _pinRegion;

    /// <summary>...and one pinned to the OTHER edge, which is where a table's status or its actions belong.</summary>
    [Bindable, Affects(nameof(StatusSide))] private bool _pinStatus;

    /// <summary>The side each column is pinned to - what the columns actually bind to. The switches are booleans
    /// because a check box is a boolean; the COLUMN is told a side, which is the only thing it can act on.</summary>
    public DataGridFrozenSide CodeSide => PinCode ? DataGridFrozenSide.Left : DataGridFrozenSide.None;

    public DataGridFrozenSide RegionSide => PinRegion ? DataGridFrozenSide.Left : DataGridFrozenSide.None;

    public DataGridFrozenSide StatusSide => PinStatus ? DataGridFrozenSide.Right : DataGridFrozenSide.None;

    [Bindable] private bool _showRowNumbers = true;

    [Bindable] private string _filterText;

    [Bindable] private string _copied;


    /// <summary>The owners a cell may be set to - a closed set the drop-down column binds to like any other list.
    /// A column stands in the grid's logical tree, so it reaches this view-model with no provider in between.</summary>
    public IReadOnlyList<string> OwnerChoices { get; } = Owners;

    /// <summary>Which rules the table draws. All four states are here to be tried: rows only is what a long list of
    /// records usually wants, columns only what a wide one does.</summary>
    [Bindable] private DataGridGridLines _gridLines = DataGridGridLines.All;

    /// <summary>What a click takes. Both states are here to be tried: a sheet of values wants cells, a list of records
    /// wants rows.</summary>
    [Bindable] private DataGridSelectionUnit _selectionUnit = DataGridSelectionUnit.Cell;

    public IReadOnlyList<DataGridSelectionUnit> SelectionUnitChoices { get; } =
    [
        DataGridSelectionUnit.Cell, DataGridSelectionUnit.FullRow
    ];

    public IReadOnlyList<DataGridGridLines> GridLineChoices { get; } =
    [
        DataGridGridLines.All, DataGridGridLines.Horizontal, DataGridGridLines.Vertical, DataGridGridLines.None
    ];

    /// <summary>Whether the strip a header is dropped into to group by it is shown.</summary>
    [Bindable] private bool _showGroupPanel;

    [Bindable] private bool _showSortPanel;

    /// <summary>Whether the table offers a say in which columns it shows - the handle at the end of the header band.
    /// Only the OFFER: what the user then chooses is the columns' own state and outlives this switch.</summary>
    [Bindable] private bool _canChooseColumns;

    /// <summary>Whether the headers offer their funnels at all. On, because a table you cannot narrow is a report -
    /// turning it off here is for seeing what a table without filtering looks like, and that switching it off gives
    /// back the rows the funnels had hidden.</summary>
    [Bindable] private bool _canFilterColumns = true;

    /// <summary>Whether the table keeps a blank row at the bottom to type a new record into. Off to begin with, so the
    /// difference it makes to the shape of the table can be seen by switching it on.</summary>
    [Bindable] private bool _showNewItemRow;

    /// <summary>Where this page keeps the saved arrangement. A FILE, not a field: the whole point of saving the
    /// columns is that the choice outlives the run, and a demo that only remembered it in memory would demonstrate
    /// nothing - including whether the state object can actually be written at all.</summary>
    private static string LayoutFile =>
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "adamantium-datagrid-layout.json");

    [Bindable] private string _layoutStatus = "Nothing saved yet";

    [Command]
    private void SaveLayout(object target)
    {
        if (target is not TreeDataGrid grid) return;

        var json = System.Text.Json.JsonSerializer.Serialize(grid.CaptureColumnState());
        System.IO.File.WriteAllText(LayoutFile, json);
        LayoutStatus = $"Saved {json.Length} bytes";
    }

    [Command]
    private void RestoreLayout(object target)
    {
        if (target is not TreeDataGrid grid || !System.IO.File.Exists(LayoutFile))
        {
            LayoutStatus = "Nothing saved yet";
            return;
        }

        var state = System.Text.Json.JsonSerializer.Deserialize<DataGridColumnsState>(
            System.IO.File.ReadAllText(LayoutFile));
        grid.RestoreColumnState(state);
        LayoutStatus = $"Restored {state?.Columns.Count ?? 0} columns";
    }

    /// <summary>What the history buttons report. Shown as a COUNT of what is reachable in each direction, because
    /// "undo did something" is not visible on a table of ten thousand rows unless you were looking at the right one.
    /// </summary>
    [Bindable] private string _historyStatus = "Nothing to take back";

    [Command]
    private void UndoEdit(object target)
    {
        if (target is not TreeDataGrid grid) return;

        HistoryStatus = grid.Undo()
            ? $"Took one back; {(grid.CanUndo ? "more behind" : "nothing behind")}, {(grid.CanRedo ? "one ahead" : "none ahead")}"
            : "Nothing to take back";
    }

    [Command]
    private void RedoEdit(object target)
    {
        if (target is not TreeDataGrid grid) return;

        HistoryStatus = grid.Redo()
            ? $"Did one again; {(grid.CanUndo ? "more behind" : "nothing behind")}, {(grid.CanRedo ? "one ahead" : "none ahead")}"
            : "Nothing to do again";
    }

    /// <summary>What the export buttons report - the path, because a demo whose export cannot be OPENED has shown
    /// nothing.</summary>
    [Bindable] private string _exportStatus = "Nothing exported yet";

    [Command]
    private void ExportCsv(object target)
    {
        if (target is not TreeDataGrid grid) return;

        var path = Where("table.csv", "csv", new FileType("Comma-separated values", "csv"));
        if (path == null) return;

        // A BOM, and only because this is a file Excel opens: without one it reads a UTF-8 file as the machine's ANSI
        // code page and every non-Latin name comes up mangled. The grid writes to a TextWriter precisely so that the
        // encoding is the application's call and not the control's.
        using (var writer = new System.IO.StreamWriter(path, false, new System.Text.UTF8Encoding(true)))
            grid.ExportCsv(writer);

        ExportStatus = path;
    }

    [Command]
    private void ExportXlsx(object target)
    {
        if (target is not TreeDataGrid grid) return;

        var path = Where("table.xlsx", "xlsx", new FileType("Excel workbook", "xlsx"));
        if (path == null) return;

        using (var stream = System.IO.File.Create(path))
            grid.ExportXlsx(stream);

        ExportStatus = path;
    }

    private string Where(string name, string extension, FileType type)
    {
        if (!FileDialog.IsAvailable)
        {
            ExportStatus = "No file dialog on this platform";
            return null;
        }

        var path = FileDialog.Save(new SaveFileRequest
        {
            Title = "Export the table",
            FileName = name,
            DefaultExtension = extension,
            FileTypes = new[] { type }
        });

        if (path == null) ExportStatus = "Cancelled";
        return path;
    }

    /// <summary>Whether the strip that searches the table is shown.</summary>
    [Bindable] private bool _showSearchPanel;

    /// <summary>Whether this page names the search colours itself instead of leaving them to the theme. BOTH states are
    /// the point: saying nothing is what a table does by default, and saying something overrides it - and going back is
    /// the half that is easy to get wrong, on the control's side and on the binding's.</summary>
    [Bindable, Affects(nameof(SearchMatchBrush), nameof(SearchCurrentMatchBrush))]
    private bool _ownSearchColours;

    // Red, green, blue, ALPHA - and the alpha is the whole point: a wash lets the check box and the text under it
    // through, a plate swallows them. Starting away from the theme's yellow so that what this page names, and what it
    // would have got by saying nothing, cannot be mistaken for each other.
    [Bindable, Affects(nameof(SearchMatchBrush))]
    private Color _searchMatchColour = new(0x21, 0xC8, 0x6E, 0x59);

    [Bindable, Affects(nameof(SearchCurrentMatchBrush))]
    private Color _searchCurrentColour = new(0x21, 0xC8, 0x6E, 0xA6);

    /// <summary>What a found cell is washed with - null while the theme owns it, which is exactly what the grid's own
    /// property means by null.</summary>
    public Brush SearchMatchBrush => OwnSearchColours ? new SolidColorBrush(SearchMatchColour) : null;

    /// <summary>...and the cell the search is standing on.</summary>
    public Brush SearchCurrentMatchBrush => OwnSearchColours ? new SolidColorBrush(SearchCurrentColour) : null;

    /// <summary>Whether this page names the colour a rejected value is washed with, instead of leaving it to the theme -
    /// the same two states as the search colours, and the same reason for showing both.</summary>
    [Bindable, Affects(nameof(ValidationErrorBrush))]
    private bool _ownValidationColour;

    [Bindable, Affects(nameof(ValidationErrorBrush))]
    private Color _validationErrorColour = new(0xC8, 0x46, 0x21, 0x66);

    /// <summary>What a cell the column will not accept is washed with - null while the theme owns it.</summary>
    public Brush ValidationErrorBrush => OwnValidationColour ? new SolidColorBrush(ValidationErrorColour) : null;

    /// <summary>The range the Size column will accept. Bound INTO the rule from here, which is the point of a rule
    /// being a component: the limits are a page's business, not a compiled-in constant.</summary>
    [Bindable] private int _sizeFloor = 500;

    [Bindable] private int _sizeCeiling = 9000;

    /// <summary>Past this a Size still ACCEPTABLE means something - conditional formatting, which is a different
    /// question from whether the value is allowed at all. Bound, so dragging it repaints the column live.</summary>
    [Bindable] private int _sizeWarnAbove = 6000;

    /// <summary>Whether a refused value keeps the editor open. Both answers are on this page because the right one is
    /// the application's: a form being filled in has to let a half-finished record stand, a ledger must not take a
    /// figure that cannot be true.</summary>
    [Bindable, Affects(nameof(ValidationMode))] private bool _blockOnInvalid;

    public DataGridValidationMode ValidationMode =>
        BlockOnInvalid ? DataGridValidationMode.Block : DataGridValidationMode.Mark;

    /// <summary>What the Size column adds up to. Every aggregate is here to be tried: a sum is what a quantity wants,
    /// an average what a rate does.</summary>
    [Bindable, Affects(nameof(SizeAggregateFormat))] private DataGridAggregate _sizeAggregate = DataGridAggregate.Sum;

    /// <summary>How that total is written. It FOLLOWS the function, because the sign in front of a number says what
    /// the number is: a fixed "Σ" went on claiming a sum over an average and over a count.</summary>
    public string SizeAggregateFormat => SizeAggregate switch
    {
        DataGridAggregate.Sum => "Σ {0:N0}",
        DataGridAggregate.Average => "avg {0:N1}",
        DataGridAggregate.Min => "min {0:N0}",
        DataGridAggregate.Max => "max {0:N0}",
        DataGridAggregate.Count => "{0:N0} rows",
        _ => null
    };

    [Bindable, Affects(nameof(CodeAggregate))] private bool _countRows = true;

    /// <summary>Counting a text column is the one aggregate that means the same thing whatever is in it - how many
    /// rows there are - which is why it is the one offered on Code.</summary>
    public DataGridAggregate CodeAggregate => CountRows ? DataGridAggregate.Count : DataGridAggregate.None;

    /// <summary>A total has to SAY what it is. A bare "10000" under a column of codes reads as nothing at all - the
    /// format is where that is said, and it is bound rather than written in the markup because a literal starting with
    /// a brace is read as a markup extension.</summary>
    public string CodeAggregateFormat => "{0:N0} rows";

    public IReadOnlyList<DataGridAggregate> AggregateChoices { get; } =
    [
        DataGridAggregate.Sum, DataGridAggregate.Average, DataGridAggregate.Min, DataGridAggregate.Max,
        DataGridAggregate.Count, DataGridAggregate.None
    ];
}
