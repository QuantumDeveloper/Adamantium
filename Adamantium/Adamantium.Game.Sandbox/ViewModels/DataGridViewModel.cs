using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Adamantium.MVVM;
using Adamantium.UI.Controls;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>One node of the grid's data. A tree AND a table: the first columns are flat facts, the hierarchy starts
/// wherever the expander column is put - which is the case this control was written for.</summary>
public class GridNode
{
    public string Code { get; set; }
    public string Owner { get; set; }
    public string Name { get; set; }
    public int Size { get; set; }
    public string Status { get; set; }
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
                Done = i % 3 == 0
            };
            Fill(node, i);

            // Every fifth row carries a branch, and the deeper the row, the FEWER of them - so the set stays a table
            // with trees in it rather than a tree. The depth is not a limit of the control: it flattens whatever it is
            // given, and the indent walks with it.
            if (i % 5 == 0) Branch(node, random, i, depth: 1, deepest: i % 20 == 0 ? 4 : 2);

            roots.Add(node);
        }

        Nodes = new ObservableCollection<GridNode>(roots);
    }

    public ObservableCollection<GridNode> Nodes { get; }

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
}
