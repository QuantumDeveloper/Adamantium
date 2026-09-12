using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Templates;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>TreeDataGrid, phase 1: columns, the shared width pass, and flat rows.</summary>
[TestFixture]
public class DataGridTests
{
    private sealed class Row
    {
        public string Name { get; set; }
        public string Note { get; init; }
        public bool Locked { get; init; }
        public string Region { get; init; }
        public int Size { get; init; }
        public ObservableCollection<Row> Children { get; } = new();
    }

    private static TreeDataGrid Grid(params DataGridColumn[] columns)
    {
        var grid = new TreeDataGrid();
        foreach (var column in columns) grid.Columns.Add(column);
        grid.Template = new ControlTemplate(() =>
        {
            var root = new Adamantium.UI.Controls.Panels.Grid();
            var presenter = new ItemsPresenter();
            var indicator = new Border { Width = 2, Visibility = Visibility.Collapsed };
            root.Children.Add(presenter);
            root.Children.Add(indicator);

            var result = new TemplateResult { RootComponent = root };
            result.RegisterName("PART_ItemsPresenter", presenter);
            result.RegisterName("PART_DropIndicator", indicator);
            return result;
        });
        return grid;
    }

    // A layout pass driven the way the running app's LayoutManager drives one. Invalidating the grid alone is not enough:
    // the items panel is a measure BOUNDARY and the presenter above it stays valid, so the pass would stop there and the
    // rows would never re-measure. In the app the manager measures a dirty boundary directly; here nothing does.
    private static void Relayout(TreeDataGrid grid, double width = 400, double height = 200)
    {
        for (IUIComponent node = grid.ItemsHostPanel; node != null; node = node.VisualParent)
            (node as IMeasurableComponent)?.InvalidateMeasure();

        grid.Measure(new Size(width, height));
        grid.Arrange(new Rect(0, 0, width, height));
    }

    // A cell that invalidates ITSELF (its template or its editing state changed) is drained by the LayoutManager in the
    // app; here the valid row above it short-circuits the walk, so the chain is dirtied by hand.
    private static void Relayout(TreeDataGrid grid, IUIComponent from)
    {
        for (var node = from; node != null; node = node.VisualParent)
            (node as IMeasurableComponent)?.InvalidateMeasure();

        Relayout(grid);
    }

    private static List<Row> Flat(int count) =>
        Enumerable.Range(1, count).Select(i => new Row
        {
            Name = $"Item {i}", Note = $"Note {i}", Locked = i % 2 == 0,
            Region = i % 2 == 0 ? "north" : "south", Size = i
        }).ToList();

    [Test]
    public void Widths_FixedTakesItsOwn_StarsShareWhatIsLeft()
    {
        var fixedColumn = new DataGridTextColumn { Width = new GridLength(100), Binding = new Binding("Name") };
        var one = new DataGridTextColumn { Width = GridLength.Star, Binding = new Binding("Note") };
        var two = new DataGridTextColumn { Width = GridLength.Star, Binding = new Binding("Note") };

        var total = DataGridColumnLayout.Arrange(new[] { fixedColumn, one, two }, 500, out _, out _);

        Assert.Multiple(() =>
        {
            Assert.That(fixedColumn.ActualWidth, Is.EqualTo(100).Within(0.5), "a named width is not squeezed");
            Assert.That(one.ActualWidth, Is.EqualTo(200).Within(0.5), "the stars split the remainder");
            Assert.That(two.ActualWidth, Is.EqualTo(200).Within(0.5));
            Assert.That(total, Is.EqualTo(500).Within(0.5), "and together they fill the area");
        });
    }

    [Test]
    public void Widths_Offsets_AreRunningSums()
    {
        var a = new DataGridTextColumn { Width = new GridLength(80) };
        var b = new DataGridTextColumn { Width = new GridLength(120) };
        var c = new DataGridTextColumn { Width = new GridLength(60) };

        DataGridColumnLayout.Arrange(new[] { a, b, c }, 400, out _, out _);

        Assert.Multiple(() =>
        {
            Assert.That(a.Offset, Is.EqualTo(0).Within(0.5));
            Assert.That(b.Offset, Is.EqualTo(80).Within(0.5));
            Assert.That(c.Offset, Is.EqualTo(200).Within(0.5));
        });
    }

    [Test]
    public void Widths_WithNoRoomLeft_StarsFallBackToTheirMinimum_AndTheGridOverflows()
    {
        var wide = new DataGridTextColumn { Width = new GridLength(400) };
        var star = new DataGridTextColumn { Width = GridLength.Star, MinWidth = 40 };

        var total = DataGridColumnLayout.Arrange(new[] { wide, star }, 300, out _, out _);

        Assert.Multiple(() =>
        {
            Assert.That(wide.ActualWidth, Is.EqualTo(400).Within(0.5), "a named width is never squeezed to fit");
            Assert.That(star.ActualWidth, Is.EqualTo(40).Within(0.5), "the star has nothing to share and takes its floor");
            Assert.That(total, Is.GreaterThan(300), "so the grid is wider than its area - it scrolls sideways");
        });
    }

    [Test]
    public void Widths_MinAndMax_AreHonoured()
    {
        var clampedUp = new DataGridTextColumn { Width = new GridLength(10), MinWidth = 50 };
        var clampedDown = new DataGridTextColumn { Width = new GridLength(900), MaxWidth = 200 };

        DataGridColumnLayout.Arrange(new[] { clampedUp, clampedDown }, 1000, out _, out _);

        Assert.Multiple(() =>
        {
            Assert.That(clampedUp.ActualWidth, Is.EqualTo(50).Within(0.5));
            Assert.That(clampedDown.ActualWidth, Is.EqualTo(200).Within(0.5));
        });
    }

    // Auto settles on the widest REALIZED cell and only ever grows, because recomputing it downwards every pass makes
    // the columns breathe under the pointer. ResetAutoWidths is the way back.
    [Test]
    public void AutoWidth_OnlyGrows_UntilItIsReset()
    {
        var column = new DataGridTextColumn { Width = GridLength.Auto };

        Assert.That(DataGridColumnLayout.RecordMeasured(column, 60), Is.True, "first measurement takes");
        Assert.That(DataGridColumnLayout.RecordMeasured(column, 90), Is.True, "a wider cell grows it");
        Assert.That(DataGridColumnLayout.RecordMeasured(column, 40), Is.False, "a narrower one does not shrink it");

        DataGridColumnLayout.Arrange(new[] { column }, 500, out _, out _);
        Assert.That(column.ActualWidth, Is.EqualTo(90).Within(0.5));

        column.ResetMeasuredWidth();
        Assert.That(DataGridColumnLayout.RecordMeasured(column, 40), Is.True, "after a reset it takes what is on screen now");
    }

    [Test]
    public void AFlatSource_ProducesOneRowPerItem_AtDepthZero()
    {
        var grid = Grid(new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) });
        grid.ItemsSource = Flat(5);

        Assert.Multiple(() =>
        {
            Assert.That(grid.Rows.Count, Is.EqualTo(5));
            Assert.That(grid.Rows.All(r => r.Depth == 0), Is.True, "a flat table is the tree with no children");
        });
    }

    // The same control, given a child path, is a tree - which is the whole argument for one control instead of two.
    [Test]
    public void TheSameControl_WithAChildPath_IsATree()
    {
        var parent = new Row { Name = "Parent" };
        parent.Children.Add(new Row { Name = "Child" });

        var grid = Grid(new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) });
        grid.ChildrenPath = "Children";
        grid.ItemsSource = new List<Row> { parent };

        Assert.That(grid.Rows.Count, Is.EqualTo(1), "collapsed, only the root is a row");
        Assert.That(grid.Rows[0].HasChildren, Is.True, "and it knows it can be opened");
    }

    [Test]
    public void ARow_PlacesItsCellsAtTheColumnOffsets()
    {
        var a = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(80) };
        var b = new DataGridTextColumn { Binding = new Binding("Note"), Width = new GridLength(120) };
        var grid = Grid(a, b);
        grid.ItemsSource = Flat(3);

        grid.Measure(new Size(400, 200));
        grid.Arrange(new Rect(0, 0, 400, 200));

        var row = grid.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow;
        Assert.That(row, Is.Not.Null, "the first row is realized");

        Assert.Multiple(() =>
        {
            Assert.That(row.CellAt(0).Bounds.X, Is.EqualTo(0).Within(0.5));
            Assert.That(row.CellAt(0).Bounds.Width, Is.EqualTo(80).Within(0.5));
            Assert.That(row.CellAt(1).Bounds.X, Is.EqualTo(80).Within(0.5), "the second cell starts where the first ends");
            Assert.That(row.CellAt(1).Bounds.Width, Is.EqualTo(120).Within(0.5));
        });
    }

    [Test]
    public void ACell_TakesItsContentFromItsColumn()
    {
        var grid = Grid(new DataGridTextColumn { Binding = new Binding("Note"), Width = new GridLength(100) });
        grid.ItemsSource = Flat(2);

        grid.Measure(new Size(400, 200));
        grid.Arrange(new Rect(0, 0, 400, 200));

        var row = grid.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow;
        Assert.That(row.CellAt(0).Content, Is.EqualTo("Note 1"), "the column names the member, the cell reads it");
    }

    // The band comes from the row's place in the DATA, not from the container's place in the panel: containers are
    // recycled, so their order stops matching the data's as soon as you scroll.
    [Test]
    public void AlternationIndex_ComesFromTheRowsPositionInTheData()
    {
        var grid = Grid(new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) });
        grid.AlternationCount = 2;
        grid.ItemsSource = Flat(4);

        grid.Measure(new Size(400, 200));
        grid.Arrange(new Rect(0, 0, 400, 200));

        var bands = Enumerable.Range(0, 4)
            .Select(i => (grid.ItemContainerGenerator.ContainerFromIndex(i) as DataGridRow)?.AlternationIndex)
            .ToArray();

        Assert.That(bands, Is.EqualTo(new int?[] { 0, 1, 0, 1 }));
    }

    // The band is worked out when a row is BOUND, so a changed count reaches nothing that already exists unless the
    // control says so - the setting looked inert until the rows happened to be recycled.
    [Test]
    public void ChangingAlternationCount_RestripesTheRowsAlreadyOnScreen()
    {
        var grid = Grid(new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) });
        grid.AlternationCount = 2;
        grid.ItemsSource = Flat(6);
        Relayout(grid);

        grid.AlternationCount = 3;

        var bands = Enumerable.Range(0, 6)
            .Select(i => (grid.ItemContainerGenerator.ContainerFromIndex(i) as DataGridRow)?.AlternationIndex)
            .ToArray();

        Assert.That(bands, Is.EqualTo(new int?[] { 0, 1, 2, 0, 1, 2 }), "no relayout, and the stripes already changed");

        grid.AlternationCount = 0;
        bands = Enumerable.Range(0, 6)
            .Select(i => (grid.ItemContainerGenerator.ContainerFromIndex(i) as DataGridRow)?.AlternationIndex)
            .ToArray();

        Assert.That(bands, Is.All.EqualTo(0), "zero turns striping off rather than leaving the last pattern");
    }

    // ---- Phase 2: the expander, and the fact that it can live in ANY column -------------------------------------

    private static (TreeDataGrid grid, Row root) Tree(params DataGridColumn[] columns)
    {
        var root = new Row { Name = "Root", Note = "n" };
        root.Children.Add(new Row { Name = "A", Note = "n" });
        root.Children.Add(new Row { Name = "B", Note = "n" });

        var grid = Grid(columns);
        grid.ChildrenPath = "Children";
        grid.ItemsSource = new List<Row> { root };
        return (grid, root);
    }

    [Test]
    public void Expanding_SplicesTheChildrenIntoTheFlatList()
    {
        var (grid, root) = Tree(new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) });

        Assert.That(grid.Rows.Count, Is.EqualTo(1));

        grid.Expand(root);
        Assert.That(grid.Rows.Count, Is.EqualTo(3), "the branch spliced in");
        Assert.That(grid.Rows[1].Depth, Is.EqualTo(1), "and its children came in one level down");

        grid.Collapse(root);
        Assert.That(grid.Rows.Count, Is.EqualTo(1), "and out again");
    }

    // Addressed by ITEM, because the row being opened usually has no container - that is the case that has to work.
    [Test]
    public void ExpandingIsAddressedByItem_NotByContainer()
    {
        var (grid, root) = Tree(new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) });

        Assert.That(grid.IsExpanded(root), Is.False);
        grid.Toggle(root);
        Assert.That(grid.IsExpanded(root), Is.True, "no layout pass, no container, and it still opened");
    }

    [Test]
    public void TheExpanderGoesInWhicheverColumnTheGridNames()
    {
        var first = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) };
        var second = new DataGridTextColumn { Binding = new Binding("Note"), Width = new GridLength(100) };
        var third = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(120) };

        var (grid, root) = Tree(first, second, third);
        grid.ExpanderColumnIndex = 2;
        grid.Expand(root);
        grid.Measure(new Size(400, 200));
        grid.Arrange(new Rect(0, 0, 400, 200));

        var child = grid.ItemContainerGenerator.ContainerFromIndex(1) as DataGridRow;
        Assert.That(child, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(child.CellAt(2).ShowsExpander, Is.True, "the third column carries it");
            Assert.That(child.CellAt(0).ShowsExpander, Is.False, "the flat columns know nothing about the hierarchy");
            Assert.That(child.CellAt(1).ShowsExpander, Is.False);
            Assert.That(child.CellAt(0).Indent, Is.EqualTo(0).Within(0.5), "and are not indented by depth");
            Assert.That(child.CellAt(2).Indent, Is.GreaterThan(0), "only the carrier is");
        });
    }

    // One property, one answer: there is no way to ask for two carriers, and an index past the end is clamped rather
    // than left to some arbitrary "first wins" rule the author never sees.
    [Test]
    public void TheCarrierDefaultsToTheFirstColumn_AndAnOutOfRangeIndexIsClamped()
    {
        var (grid, _) = Tree(
            new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) },
            new DataGridTextColumn { Binding = new Binding("Note"), Width = new GridLength(100) });

        Assert.That(grid.ExpanderColumn, Is.SameAs(grid.Columns[0]), "unset means the first");

        grid.ExpanderColumnIndex = 99;
        Assert.That(grid.ExpanderColumn, Is.SameAs(grid.Columns[1]), "past the end is the last, not nothing");

        grid.ExpanderColumnIndex = -5;
        Assert.That(grid.ExpanderColumn, Is.SameAs(grid.Columns[0]));
    }

    [Test]
    public void Indent_GrowsWithDepth_OnTheCarrierOnly()
    {
        var carrier = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(200) };
        var (grid, root) = Tree(carrier);
        grid.Indent = 20;
        grid.ExpanderSize = 16;
        grid.Expand(root);
        grid.Measure(new Size(400, 200));
        grid.Arrange(new Rect(0, 0, 400, 200));

        var rootRow = grid.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow;
        var childRow = grid.ItemContainerGenerator.ContainerFromIndex(1) as DataGridRow;

        Assert.Multiple(() =>
        {
            // DEPTH only. The expander's own strip is a column of the cell template, not part of the indent - folding it
            // in put the drawn triangle to the right of the strip being hit-tested, and clicking it did nothing.
            Assert.That(rootRow.CellAt(0).Indent, Is.EqualTo(0).Within(0.5), "depth 0 is not indented at all");
            Assert.That(childRow.CellAt(0).Indent, Is.EqualTo(20).Within(0.5), "depth 1: one indent");
            Assert.That(rootRow.CellAt(0).HasChildren, Is.True, "the carrier knows the row can be opened");
            Assert.That(childRow.CellAt(0).ShowsExpander, Is.True, "and carries the strip at every depth");
        });
    }

    // ---- Phase 3: column virtualization and frozen columns ------------------------------------------------------

    private static TreeDataGrid WideGrid(int columnCount, double columnWidth = 100)
    {
        var columns = Enumerable.Range(0, columnCount)
            .Select(i => (DataGridColumn)new DataGridTextColumn
            {
                Binding = new Binding(i % 2 == 0 ? "Name" : "Note"),
                Width = new GridLength(columnWidth)
            })
            .ToArray();

        var grid = Grid(columns);
        grid.ItemsSource = Flat(3);
        return grid;
    }

    // Unlike the tab strip, this window needs no estimate: the widths are all known from the width pass, so the running
    // sums are exact and the window is arithmetic.
    [Test]
    public void OnlyTheColumnsInTheViewport_GetCells()
    {
        var grid = WideGrid(200);
        Relayout(grid, width: 400);

        var row = grid.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow;
        var realized = Enumerable.Range(0, 200).Count(i => row.CellAt(i) != null);

        Assert.Multiple(() =>
        {
            Assert.That(realized, Is.LessThan(200), "a wide table does not build every cell of every row");
            Assert.That(row.CellAt(0), Is.Not.Null, "the columns on screen are built");
            Assert.That(row.CellAt(199), Is.Null, "the ones far off to the right are not");
        });
    }

    [Test]
    public void ScrollingSideways_MovesTheWindow()
    {
        var grid = WideGrid(200);
        Relayout(grid, width: 400);

        grid.HorizontalOffset = 10000;   // column 100 and its neighbours
        Relayout(grid, width: 400);

        var row = grid.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow;
        Assert.Multiple(() =>
        {
            Assert.That(row.CellAt(100), Is.Not.Null, "what is on screen now is built");
            Assert.That(row.CellAt(0), Is.Null, "and what left the window is not");
        });
    }

    [Test]
    public void AFrozenColumn_IsBuiltWhereverTheGridIsScrolled()
    {
        var grid = WideGrid(200);
        grid.Columns[0].FrozenSide = DataGridFrozenSide.Left;
        Relayout(grid, width: 400);

        grid.HorizontalOffset = 10000;
        Relayout(grid, width: 400);

        var row = grid.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow;
        Assert.Multiple(() =>
        {
            Assert.That(row.CellAt(0), Is.Not.Null, "frozen means it stays - that is the whole of it");
            Assert.That(row.CellAt(1), Is.Null, "its unfrozen neighbour left with the rest");
        });
    }

    // ---- Phase 4: selection as a spreadsheet does it ------------------------------------------------------------

    private static TreeDataGrid SelectableGrid(int rows = 6)
    {
        var grid = Grid(
            new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) },
            new DataGridTextColumn { Binding = new Binding("Note"), Width = new GridLength(100) });
        grid.ItemsSource = Flat(rows);
        return grid;
    }

    // Held as RECTANGLES, not as cells: a column of a million rows is one range, and storing it per cell would spend
    // memory on exactly the case a data grid is written for.
    [Test]
    public void SelectingAColumn_IsOneRange_HoweverManyRows()
    {
        var grid = Grid(new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) });
        grid.ItemsSource = Flat(100_000);

        grid.SelectColumns(0, 0);

        Assert.Multiple(() =>
        {
            Assert.That(grid.SelectedCells.Ranges.Count, Is.EqualTo(1), "one rectangle, not a hundred thousand cells");
            Assert.That(grid.SelectedCells.Contains(99_999, 0), Is.True, "and it really does cover the last row");
        });
    }

    [Test]
    public void ShiftExtendsFromTheAnchor_CtrlAddsASecondBlock()
    {
        var grid = SelectableGrid();

        grid.SelectCell(1, 0);
        grid.SelectCell(3, 1, extend: true);

        Assert.Multiple(() =>
        {
            Assert.That(grid.SelectedCells.Ranges.Count, Is.EqualTo(1), "extending grows the block, it does not add one");
            Assert.That(grid.SelectedCells.Contains(2, 0), Is.True, "everything between the anchor and here is in");
            Assert.That(grid.SelectedCells.Contains(0, 0), Is.False);
        });

        grid.SelectCell(5, 0, add: true);

        Assert.Multiple(() =>
        {
            Assert.That(grid.SelectedCells.Ranges.Count, Is.EqualTo(2), "Ctrl adds a block beside the first");
            Assert.That(grid.SelectedCells.Contains(2, 0), Is.True, "and keeps it");
            Assert.That(grid.SelectedCells.Contains(5, 0), Is.True);
        });
    }

    // The active cell sits INSIDE the selection and moves through it without clearing it - as in a spreadsheet.
    [Test]
    public void TheActiveCellIsSeparateFromTheSelection()
    {
        var grid = SelectableGrid();
        grid.SelectCell(1, 0);
        grid.SelectCell(3, 1, extend: true);

        Assert.Multiple(() =>
        {
            Assert.That(grid.ActiveRow, Is.EqualTo(3));
            Assert.That(grid.ActiveColumn, Is.EqualTo(1));
            Assert.That(grid.SelectedCells.Contains(1, 0), Is.True, "the block the active cell moved through is intact");
        });
    }

    [Test]
    public void Copy_LaysTheSelectionOutAsTabsAndNewlines()
    {
        var grid = SelectableGrid();
        grid.SelectCell(0, 0);
        grid.SelectCell(1, 1, extend: true);

        Assert.That(grid.GetSelectionAsText(), Is.EqualTo("Item 1\tNote 1\nItem 2\tNote 2"));
    }

    // Disjoint blocks are laid out inside their BOUNDING rectangle, so what lands in a spreadsheet is a grid rather than
    // rows sliding under each other. A cell inside the bounds but outside every block comes out empty.
    [Test]
    public void Copy_KeepsDisjointBlocksInLine()
    {
        var grid = SelectableGrid();
        grid.SelectCell(0, 0);
        grid.SelectCell(2, 1, add: true);

        Assert.That(grid.GetSelectionAsText(), Is.EqualTo("Item 1\t\n\t\n\tNote 3"));
    }

    [Test]
    public void Arrows_MoveTheActiveCell_AndShiftDragsTheSelectionAlong()
    {
        var grid = SelectableGrid(5);
        grid.SelectCell(2, 0);

        grid.MoveActive(1, 0);
        Assert.That(grid.ActiveRow, Is.EqualTo(3), "down one");

        grid.MoveActive(1, 1, extend: true);
        Assert.Multiple(() =>
        {
            Assert.That(grid.ActiveRow, Is.EqualTo(4));
            Assert.That(grid.ActiveColumn, Is.EqualTo(1));
            Assert.That(grid.SelectedCells.Contains(3, 0), Is.True, "Shift dragged the block from the anchor");
        });

        grid.MoveActive(10, 0);
        Assert.That(grid.ActiveRow, Is.EqualTo(4), "the edge clamps, it does not wrap");
    }

    [Test]
    public void SelectAll_CoversEveryCell()
    {
        var grid = SelectableGrid(4);
        grid.SelectAllCells();

        Assert.Multiple(() =>
        {
            Assert.That(grid.SelectedCells.Contains(0, 0), Is.True);
            Assert.That(grid.SelectedCells.Contains(3, 1), Is.True);
            Assert.That(grid.SelectedCells.Ranges.Count, Is.EqualTo(1));
        });
    }

    // Selection is addressed by INDEX, so it survives what containers do not: a cell scrolled out of the window is still
    // selected, and says so again when it is built.
    [Test]
    public void SelectionSurvivesRecycling()
    {
        var grid = SelectableGrid(500);
        Relayout(grid);

        grid.SelectCell(400, 1);
        Assert.That(grid.SelectedCells.Contains(400, 1), Is.True, "a row with no container can still be selected");

        Relayout(grid);
        Assert.That(grid.SelectedCells.Contains(400, 1), Is.True, "and a layout pass does not forget it");
    }

    // ---- Everything a column reads off a row is a BINDING -------------------------------------------------------

    // Value, meaning and refusal are three bindings against the row's item - no provider, no member names, and every
    // one of them free to carry a converter or a format the way any binding in the framework can.
    [Test]
    public void ACellsValueStateAndRefusalAllComeFromBindings()
    {
        var grid = SelectableGrid(3);
        grid.Columns[1].StateBinding = new Binding("Note");
        grid.Columns[1].IsReadOnlyBinding = new Binding("Locked");
        Relayout(grid);

        var row = grid.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow;
        var item = (Row)grid.Rows[0].Node;

        Assert.Multiple(() =>
        {
            Assert.That(row.CellAt(0).Content, Is.EqualTo(item.Name), "the value the column's binding produces");
            Assert.That(row.CellAt(1).State, Is.EqualTo(item.Note), "a MEANING, not a colour");
            Assert.That(row.CellAt(1).IsReadOnly, Is.EqualTo(item.Locked), "and this one cell's own refusal");
        });
    }

    // ---- Frozen columns ------------------------------------------------------------------------------------------

    // A frozen column is laid out in a zone of its own at the LEFT, whatever its place among the columns, and the
    // scrolling ones start after that zone. One pass decides it, so the row, the header strip, the column window and
    // the hit test cannot disagree about where a column is.
    [Test]
    public void FrozenColumns_TakeTheLeftZoneAndTheRestFollow()
    {
        var first = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) };
        var pinned = new DataGridTextColumn { Binding = new Binding("Note"), Width = new GridLength(60), FrozenSide = DataGridFrozenSide.Left };
        var last = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(80) };

        var total = DataGridColumnLayout.Arrange(new[] { first, pinned, last }, 500, out var frozenWidth, out _);

        Assert.Multiple(() =>
        {
            Assert.That(pinned.Offset, Is.EqualTo(0), "the pinned one starts the row, though it was declared second");
            Assert.That(frozenWidth, Is.EqualTo(60));
            Assert.That(first.Offset, Is.EqualTo(60), "and the scrolling ones follow the zone");
            Assert.That(last.Offset, Is.EqualTo(160));
            Assert.That(total, Is.EqualTo(240));
        });
    }

    // The OTHER edge: totals, a status, the buttons that act on the row. Laid out at the END of the content - which is
    // where the zone stands when the table is scrolled fully across - and slid back to the viewport's edge everywhere
    // else by ONE number, so the row, the header strip and the hit test cannot disagree about where it is.
    [Test]
    public void AColumnPinnedRight_TakesTheTailOfTheRow()
    {
        var first = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) };
        var pinned = new DataGridTextColumn
        {
            Binding = new Binding("Note"), Width = new GridLength(60), FrozenSide = DataGridFrozenSide.Right
        };
        var last = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(80) };

        var total = DataGridColumnLayout.Arrange(new[] { first, pinned, last }, 500, out var left, out var right);

        Assert.Multiple(() =>
        {
            Assert.That(first.Offset, Is.EqualTo(0), "the scrolling ones keep the head of the row");
            Assert.That(last.Offset, Is.EqualTo(100));
            Assert.That(pinned.Offset, Is.EqualTo(180), "and the pinned one goes to the very end, though declared second");
            Assert.That(left, Is.EqualTo(0), "nothing is pinned on the left");
            Assert.That(right, Is.EqualTo(60));
            Assert.That(total, Is.EqualTo(240));
        });
    }

    // The right zone STANDS STILL: laid out at the content's end, it is slid back by exactly what is off-screen, so it
    // sits on the viewport's edge - and that slide reaches nought precisely when the table is scrolled fully across.
    [Test]
    public void TheRightZone_StandsOnTheViewportsEdgeAtEveryScroll()
    {
        var wide = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(600) };
        var pinned = new DataGridTextColumn
        {
            Binding = new Binding("Note"), Width = new GridLength(60), FrozenSide = DataGridFrozenSide.Right
        };
        var grid = Grid(wide, pinned);
        grid.ItemsSource = Flat(3);
        Relayout(grid);

        var row = grid.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow;
        var atRest = row.CellAt(1).Bounds.X;

        grid.HorizontalOffset = 120;
        Relayout(grid);

        Assert.Multiple(() =>
        {
            Assert.That(row.CellAt(1).Bounds.X - atRest, Is.EqualTo(120).Within(0.01),
                "in the row's own coordinates it moved WITH the scroll - which is what keeps it still on screen");
            Assert.That(grid.RightFrozenWidth, Is.EqualTo(60));
        });

        // ...INCLUDING at the far end, which is the one place two definitions of "fully scrolled" can disagree: the
        // zone used to be measured against the columns' width while the scroller travels by its own extent, and the
        // couple of pixels between them showed as the zone stepping sideways on arrival and again on leaving.
        // 260 is the whole travel here (660 of columns against a 400 viewport), so the last step lands exactly on it.
        var anchor = row.CellAt(1).Bounds.X - grid.HorizontalOffset;
        foreach (var offset in new[] { 200.0, 259.0, 260.0 })
        {
            grid.HorizontalOffset = offset;
            Relayout(grid);
            Assert.That(row.CellAt(1).Bounds.X - grid.HorizontalOffset, Is.EqualTo(anchor).Within(0.01),
                $"the zone stands still at offset {offset}");
        }
    }

    // The strip and its headers MUST cut what slides past them, and the renderer reads a hot FIELD behind ClipToBounds,
    // not the property. A type default (OverrideMetadata) raises no change, so the callback that writes that field never
    // runs and the clip silently does not happen - which is why these two set it in their constructors, where it is a
    // real write. Asserted here so nobody has to find that out a third time.
    [Test]
    public void TheHeaderStripAndItsHeaders_ClipWhatSlidesPastThem()
    {
        var grid = SelectableGrid(2);
        Relayout(grid);
        var headers = Headers(grid);

        Assert.Multiple(() =>
        {
            Assert.That(headers.ClipToBounds, Is.True, "the strip cuts a header that slid past the left edge");
            Assert.That(HeaderOf(headers, 0).ClipToBounds, Is.True, "and a header cuts its own funnel at the separator");
        });
    }

    // Pinning changes WHICH CELLS a row has - a pinned column is realized on every row at once - and a row only builds
    // its cells in its measure. Invalidating the rows was not enough: the ones that already existed came up without a
    // cell for the newcomer, and the column stayed blank until a scroll happened to rebuild them.
    [Test]
    public void PinningAColumn_GivesEveryRealizedRowItsCell()
    {
        var wide = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(600) };
        var alsoWide = new DataGridTextColumn { Binding = new Binding("Note"), Width = new GridLength(600) };
        var pinned = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) };
        var grid = Grid(wide, alsoWide, pinned);
        grid.ItemsSource = Flat(3);
        Relayout(grid);

        var row = grid.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow;
        Assert.That(row.CellAt(2), Is.Null, "outside the window to begin with, so no cell on this row");

        pinned.FrozenSide = DataGridFrozenSide.Right;
        grid.Measure(new Size(400, 200));
        grid.Arrange(new Rect(0, 0, 400, 200));

        Assert.That(row.CellAt(2), Is.Not.Null, "pinned means realized on every row, and at once - not after a scroll");
    }

    // Pinning is a LAYOUT change and the column has to say so itself - it is not a visual child of the grid, so nothing
    // invalidates on its behalf. Measured WITHOUT invalidating by hand, which is the whole question: the switch used to
    // do nothing until something else happened to re-measure. Pinning the FIRST column hid it - its place never moved.
    [Test]
    public void PinningAColumn_RelaysTheTableOutByItself()
    {
        var grid = SelectableGrid(3);
        Relayout(grid);
        Assert.That(grid.Columns[1].Offset, Is.EqualTo(100), "second in line to begin with");

        grid.Columns[1].FrozenSide = DataGridFrozenSide.Left;
        grid.Measure(new Size(400, 200));
        grid.Arrange(new Rect(0, 0, 400, 200));

        Assert.Multiple(() =>
        {
            Assert.That(grid.Columns[1].Offset, Is.EqualTo(0), "the pinned one takes the head of the row");
            Assert.That(grid.Columns[0].Offset, Is.EqualTo(100), "and the one it left goes behind the zone");
        });
    }

    // The pinned zone paints in TWO layers - the table's surface and the row's OWN band brush over it - and the band
    // must be that very brush, not a colour mixed from it. A colour picker changes one brush object in place, and a
    // mixed copy would leave the zone in the shade it was mixed at while every other column followed.
    [Test]
    public void ThePinnedZone_PaintsWithTheRowsOwnBrush_NotACopyOfIt()
    {
        var pinned = new DataGridTextColumn
        {
            Binding = new Binding("Name"), Width = new GridLength(80), FrozenSide = DataGridFrozenSide.Left
        };
        var grid = Grid(pinned, new DataGridTextColumn { Binding = new Binding("Note"), Width = new GridLength(120) });
        grid.ItemsSource = Flat(4);
        grid.AlternationCount = 2;
        grid.AlternationBrush = new SolidColorBrush(Colors.Olive);
        Relayout(grid);

        var banded = grid.ItemContainerGenerator.ContainerFromIndex(1) as DataGridRow;
        var backdrop = banded.FrozenBackdrop;

        Assert.Multiple(() =>
        {
            Assert.That(backdrop, Is.Not.Null, "something has to paint the zone - a cell is transparent");
            Assert.That(((Border)backdrop.Child).Background, Is.SameAs(banded.Background),
                "the band is the row's OWN brush, so a colour changed in place is followed");
            Assert.That(backdrop.Background, Is.SameAs(grid.Background), "over the table's surface, which is opaque");
        });
    }

    // The zone does not scroll: the row lives inside the sideways scroller, so a pinned cell is pushed back by exactly
    // what the scroller moved and stands still while the rest slide under it.
    [Test]
    public void AFrozenCell_StandsStillWhileTheRowScrollsSideways()
    {
        var pinned = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(80), FrozenSide = DataGridFrozenSide.Left };
        var scrolling = new DataGridTextColumn { Binding = new Binding("Note"), Width = new GridLength(120) };
        var grid = Grid(pinned, scrolling);
        grid.ItemsSource = Flat(3);
        Relayout(grid);

        var row = grid.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow;
        var before = row.CellAt(0).Bounds.X;

        grid.HorizontalOffset = 50;
        Relayout(grid);

        Assert.Multiple(() =>
        {
            Assert.That(row.CellAt(0).Bounds.X - before, Is.EqualTo(50).Within(0.01),
                "in the row's own coordinates it moved WITH the scroll - which is what keeps it still on screen");
            Assert.That(row.CellAt(0).ZIndex, Is.GreaterThan(row.CellAt(1).ZIndex),
                "and it is drawn over what passes under it");
        });
    }

    // Rules between cells: horizontal, vertical, both or neither - the four states every table this one is measured
    // against offers, and the header strip follows the same setting so the two can never disagree.
    [TestCase(DataGridGridLines.All, true, true)]
    [TestCase(DataGridGridLines.Horizontal, false, true)]
    [TestCase(DataGridGridLines.Vertical, true, false)]
    [TestCase(DataGridGridLines.None, false, false)]
    public void GridLines_AreDrawnInTheDirectionsAskedFor(DataGridGridLines lines, bool vertical, bool horizontal)
    {
        var grid = SelectableGrid(3);
        grid.GridLinesVisibility = lines;
        Relayout(grid);

        var cell = grid.CellFor(0, 0);
        Assert.Multiple(() =>
        {
            Assert.That(cell.ShowsVerticalLine, Is.EqualTo(vertical));
            Assert.That(cell.ShowsHorizontalLine, Is.EqualTo(horizontal));
        });
    }

    // Both by default: a grid that draws nothing between its cells is not what anyone coming from another framework
    // expects to find.
    [Test]
    public void GridLines_AreBothByDefault()
    {
        var grid = SelectableGrid(2);
        Relayout(grid);

        Assert.That(grid.GridLinesVisibility, Is.EqualTo(DataGridGridLines.All));
        Assert.That(grid.CellFor(0, 0).ShowsVerticalLine, Is.True);
        Assert.That(grid.CellFor(0, 0).ShowsHorizontalLine, Is.True);
    }

    // A clip is a SCISSOR on the GPU and a scissor change ends the batch, so a cell that clips cannot share a draw call
    // with its neighbours: measured on 90 cells, 467 draws a frame against 191 and 262 fps against 408. Only the
    // EDITOR ever needed it - a themed field carries a standalone control's MinWidth and would hang over the next
    // column - so only an editing cell asks for one.
    [Test]
    public void OnlyAnEditingCellTakesAScissor()
    {
        var grid = SelectableGrid(3);
        Relayout(grid);

        Assert.That(grid.CellFor(0, 0).ClipToBounds, Is.False, "an ordinary cell shares its neighbours' draw call");

        grid.BeginEdit(0, 0);
        Relayout(grid, grid.CellFor(0, 0));

        Assert.That(grid.CellFor(0, 0).ClipToBounds, Is.True, "an editor is cut at the column edge");

        grid.CancelEdit();
        Relayout(grid, grid.CellFor(0, 0));

        Assert.That(grid.CellFor(0, 0).ClipToBounds, Is.False, "and gives the scissor back when the edit ends");
    }

    // The rule colour is the grid's to name, and it reaches the cells that draw it. ONE brush for both directions:
    // the rules ride on the cell's own border, and a border has one.
    [Test]
    public void GridLines_TakeTheBrushTheGridNames()
    {
        var grid = SelectableGrid(2);
        grid.GridLinesBrush = Brushes.Green;
        Relayout(grid);

        Assert.That(grid.CellFor(0, 0).GridLineBrush, Is.EqualTo(Brushes.Green));
    }

    // ---- Group order ---------------------------------------------------------------------------------------------

    // Captions run in KEY order, not in the order the values happened to turn up. Built by first encounter, a table
    // sorted by anything else gave a list of captions in no order at all - and a fold nobody can scan is a shuffle.
    [Test]
    public void GroupCaptions_RunInKeyOrder_WhateverTheRowsAreSortedBy()
    {
        var size = new DataGridTextColumn { Binding = new Binding("Size"), SortMemberPath = "Size" };
        var name = new DataGridTextColumn { Binding = new Binding("Name"), SortMemberPath = "Name" };
        var grid = Grid(size, name);
        grid.ItemsSource = new List<Row>
        {
            new() { Name = "d", Size = 30 }, new() { Name = "c", Size = 10 },
            new() { Name = "b", Size = 20 }, new() { Name = "a", Size = 10 }
        };

        grid.SortBy(name);              // sorted by something that is NOT the grouping column
        grid.GroupBy(size);

        var captions = grid.Rows.Where(r => r.Node is DataGridGroup)
            .Select(r => ((DataGridGroup)r.Node).Key).ToArray();

        Assert.That(captions, Is.EqualTo(new object[] { 10, 20, 30 }),
            "and as NUMBERS - the same comparison the columns sort by, not their text");
    }

    // ...and they follow the table when the table is sorted by the very column it is grouped by: captions and the rows
    // under them running opposite ways is the one arrangement nobody asked for.
    [Test]
    public void GroupCaptions_TurnAround_WhenTheTableIsSortedByThatColumnDescending()
    {
        var region = new DataGridTextColumn { Binding = new Binding("Region"), SortMemberPath = "Region" };
        var grid = Grid(region);
        grid.ItemsSource = new List<Row>
        {
            new() { Region = "north" }, new() { Region = "south" }, new() { Region = "east" }
        };

        grid.GroupBy(region);
        grid.SortBy(region, descending: true);

        var captions = grid.Rows.Where(r => r.Node is DataGridGroup)
            .Select(r => ((DataGridGroup)r.Node).Key.ToString()).ToArray();

        Assert.That(captions, Is.EqualTo(new[] { "south", "north", "east" }));
    }

    // ---- The chooser itself ---------------------------------------------------------------------------------------

    private static DataGridColumnChooser Chooser(TreeDataGrid grid)
    {
        var chooser = new DataGridColumnChooser { Owner = grid };
        chooser.Template = new ControlTemplate(() =>
        {
            var items = new StackPanel { Orientation = Orientation.Vertical };
            var result = new TemplateResult { RootComponent = items };
            result.RegisterName("PART_Items", items);
            return result;
        });

        // The first pass applies the template, and only then is there a panel to build the switches into.
        ((IMeasurableComponent)chooser).InvalidateMeasure();
        chooser.Measure(new Size(200, 400));
        chooser.Arrange(new Rect(0, 0, 200, 400));
        return chooser;
    }

    private static List<CheckBox> SwitchesOf(DataGridColumnChooser chooser) =>
        (chooser.GetTemplateChild("PART_Items") as Panel)?.Children.OfType<CheckBox>().ToList() ?? new List<CheckBox>();

    // EVERY column gets a switch, including the one that may not be hidden - that one comes ticked and DISABLED. Left
    // out, it simply is not in the list, and from the outside that reads as a list with something missing rather than
    // as a rule; the first question it got was "where is Code?".
    [Test]
    public void TheChooser_OffersEveryColumn_AndTheOneThatIsNotYoursComesLocked()
    {
        var locked = new DataGridTextColumn { Header = "Code", Binding = new Binding("Name"), CanUserHide = false };
        var free = new DataGridTextColumn { Header = "Note", Binding = new Binding("Note") };
        var grid = Grid(locked, free);
        grid.ItemsSource = Flat(2);

        var switches = SwitchesOf(Chooser(grid));

        Assert.Multiple(() =>
        {
            Assert.That(switches.Count, Is.EqualTo(2), "both columns are offered");
            Assert.That(switches[0].IsChecked, Is.True, "the locked one is shown as shown");
            Assert.That(switches[0].IsEnabled, Is.False, "...and as not yours to change");
            Assert.That(switches[1].IsEnabled, Is.True);
        });
    }

    // A column the table is GROUPED BY is a different matter and really is absent: it is not hidden, it has moved into
    // the group captions, and offering to show it would be offering something that cannot happen.
    [Test]
    public void TheChooser_LeavesOutAColumnTheTableIsGroupedBy()
    {
        var region = new DataGridTextColumn { Header = "Region", Binding = new Binding("Region") };
        var grid = Grid(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") }, region);
        grid.ItemsSource = Flat(4);
        grid.GroupBy(region);

        var switches = SwitchesOf(Chooser(grid));

        Assert.That(switches.Count, Is.EqualTo(1), "only the column that is still a column of the table");
        Assert.That(switches[0].Content, Is.EqualTo("Name"));
    }

    // The switch is the column: turning it off hides the column, turning it back on brings it back.
    [Test]
    public void TurningASwitchOff_HidesThatColumn()
    {
        var note = new DataGridTextColumn { Header = "Note", Binding = new Binding("Note") };
        var grid = Grid(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name") }, note);
        grid.ItemsSource = Flat(2);
        var switches = SwitchesOf(Chooser(grid));

        switches[1].IsChecked = false;
        Assert.That(note.IsVisible, Is.False);

        switches[1].IsChecked = true;
        Assert.That(note.IsVisible, Is.True);
    }

    // ...and a switch that is not the user's changes nothing even when something writes to it: the rule lives in the
    // control, not only in whether the theme let the pointer reach it.
    [Test]
    public void ASwitchThatIsNotYours_ChangesNothing_EvenWrittenTo()
    {
        var locked = new DataGridTextColumn { Header = "Code", Binding = new Binding("Name"), CanUserHide = false };
        var grid = Grid(locked);
        grid.ItemsSource = Flat(2);
        var switches = SwitchesOf(Chooser(grid));

        switches[0].IsChecked = false;

        Assert.That(locked.IsVisible, Is.True, "the column a table cannot be read without stays");
    }

    // ---- The saved arrangement ----------------------------------------------------------------------------------

    // What the user did to the columns comes back whole. Saving it is what makes choosing them worth anything: without
    // it the same columns are hidden again on every run.
    [Test]
    public void AnArrangement_ComesBackAsItWasSaved()
    {
        var name = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) };
        var note = new DataGridTextColumn { Binding = new Binding("Note"), Width = new GridLength(100) };
        var region = new DataGridTextColumn { Binding = new Binding("Region"), Width = new GridLength(100) };
        var grid = Grid(name, note, region);
        grid.ItemsSource = Flat(4);

        note.IsVisible = false;
        region.Width = new GridLength(250);
        region.FrozenSide = DataGridFrozenSide.Left;
        grid.MoveColumn(2, 0);              // Region first
        grid.SortBy(name, descending: true);

        var saved = grid.CaptureColumnState();

        // Everything back the way it was NOT: a restore that happens to agree with the current state proves nothing.
        note.IsVisible = true;
        region.Width = new GridLength(60);
        region.FrozenSide = DataGridFrozenSide.None;
        grid.MoveColumn(0, 2);
        grid.SortBy(null);

        grid.RestoreColumnState(saved);

        Assert.Multiple(() =>
        {
            Assert.That(note.IsVisible, Is.False, "what was hidden is hidden again");
            Assert.That(region.Width.Value, Is.EqualTo(250), "and what was widened keeps its width");
            Assert.That(region.FrozenSide, Is.EqualTo(DataGridFrozenSide.Left), "...and its pin");
            Assert.That(grid.Columns.IndexOf(region), Is.Zero, "...and its place");
            Assert.That(grid.SortColumn, Is.SameAs(name), "the sort is part of the arrangement");
            Assert.That(grid.SortDescending, Is.True);
        });
    }

    // A saved arrangement outlives the table changing under it. Thrown away whole at the first added column, it would
    // be worthless: a release that adds one field would cost every user their layout.
    [Test]
    public void AnArrangement_SurvivesAColumnAddedOrDroppedSinceItWasSaved()
    {
        var name = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) };
        var gone = new DataGridTextColumn { Binding = new Binding("Note"), Width = new GridLength(100) };
        var grid = Grid(name, gone);
        grid.ItemsSource = Flat(4);

        gone.IsVisible = false;
        grid.MoveColumn(1, 0);
        var saved = grid.CaptureColumnState();

        // The release the layout was saved before: one column is gone, one is new.
        grid.Columns.Remove(gone);
        var added = new DataGridTextColumn { Binding = new Binding("Region"), Width = new GridLength(80) };
        grid.Columns.Add(added);

        grid.RestoreColumnState(saved);

        Assert.Multiple(() =>
        {
            Assert.That(grid.Columns.Count, Is.EqualTo(2), "a name the table no longer has is passed over");
            Assert.That(grid.Columns.IndexOf(name), Is.Zero, "what it does know is put where it was");
            Assert.That(added.IsVisible, Is.True, "a column it never heard of is left as it is");
            Assert.That(grid.Columns.IndexOf(added), Is.EqualTo(1), "...and follows the ones it does know");
        });
    }

    // The whole point of the state object is that it can be WRITTEN. "Plain values any serializer can take" is a claim,
    // and a claim about a type is a test: a round trip through a real serializer, not through the object itself.
    [Test]
    public void AnArrangement_SurvivesBeingWrittenOutAndReadBack()
    {
        var name = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(140) };
        var note = new DataGridTextColumn { Binding = new Binding("Note"), Width = GridLength.Star };
        var grid = Grid(name, note);
        grid.ItemsSource = Flat(2);

        note.IsVisible = false;
        name.FrozenSide = DataGridFrozenSide.Left;
        grid.SortBy(name, descending: true);

        var json = System.Text.Json.JsonSerializer.Serialize(grid.CaptureColumnState());
        var read = System.Text.Json.JsonSerializer.Deserialize<DataGridColumnsState>(json);

        note.IsVisible = true;
        name.FrozenSide = DataGridFrozenSide.None;
        grid.SortBy(null);

        grid.RestoreColumnState(read);

        Assert.Multiple(() =>
        {
            Assert.That(note.IsVisible, Is.False);
            Assert.That(note.Width.IsStar, Is.True, "a star width is a star again, not the number 1");
            Assert.That(name.Width.Value, Is.EqualTo(140));
            Assert.That(name.FrozenSide, Is.EqualTo(DataGridFrozenSide.Left));
            Assert.That(grid.SortColumn, Is.SameAs(name));
            Assert.That(grid.SortDescending, Is.True);
        });
    }

    // No key, no memory - and said out loud rather than guessed at. A column with nothing to name it is passed over on
    // the way out, so nothing claims to remember it on the way back in.
    [Test]
    public void AColumnWithNothingToNameIt_IsNotSaved()
    {
        var named = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) };
        var anonymous = new DataGridTemplateColumn { Width = new GridLength(50) };
        var grid = Grid(named, anonymous);
        grid.ItemsSource = Flat(2);

        var saved = grid.CaptureColumnState();

        Assert.That(saved.Columns.Count, Is.EqualTo(1), "only the one that can be found again");
        Assert.That(saved.Columns[0].Key, Is.EqualTo("Name"));
    }

    // ---- Choosing columns: hiding is not removing --------------------------------------------------------------

    // A hidden column takes no width and no cell, and everything holding its INDEX still finds it where it was: the
    // column stays in the collection, so a selection, a sort and a filter all survive it being turned off and on.
    [Test]
    public void AHiddenColumn_TakesNoRoomAndNoCell_ButKeepsItsPlace()
    {
        var first = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) };
        var middle = new DataGridTextColumn { Binding = new Binding("Note"), Width = new GridLength(100) };
        var last = new DataGridTextColumn { Binding = new Binding("Region"), Width = new GridLength(100) };
        var grid = Grid(first, middle, last);
        grid.ItemsSource = Flat(3);
        Relayout(grid);

        middle.IsVisible = false;
        Relayout(grid);

        Assert.Multiple(() =>
        {
            Assert.That(middle.ActualWidth, Is.Zero, "a column nobody sees takes no width");
            Assert.That(last.Offset, Is.EqualTo(100).Within(0.5), "and the one after it closes the gap");
            Assert.That(grid.CellFor(0, 1), Is.Null, "no cell is built for it");
            Assert.That(grid.Columns.IndexOf(last), Is.EqualTo(2), "hiding is not removing - the indices stand");
        });

        middle.IsVisible = true;
        Relayout(grid);

        Assert.Multiple(() =>
        {
            Assert.That(middle.ActualWidth, Is.EqualTo(100).Within(0.5), "and it comes back at the width it had");
            Assert.That(grid.CellFor(0, 1), Is.Not.Null);
        });
    }

    // The two reasons a column is not on screen are NOT the same, and IsShown is the one answer everything asks.
    [Test]
    public void GroupingByAColumn_HidesIt_WithoutTouchingWhatTheUserChose()
    {
        var column = new DataGridTextColumn { Binding = new Binding("Region"), Width = new GridLength(100) };
        var grid = Grid(new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) }, column);
        grid.ItemsSource = Flat(4);
        Relayout(grid);

        grid.GroupBy(column);
        Relayout(grid);

        Assert.Multiple(() =>
        {
            Assert.That(column.IsShown, Is.False, "a column the table is grouped by has moved into the captions");
            Assert.That(column.IsVisible, Is.True, "...but the user never asked for it to be hidden");
        });

        grid.ClearGrouping();
        Relayout(grid);
        Assert.That(column.IsShown, Is.True, "and ungrouping brings it back without anyone re-ticking anything");
    }

    // ---- Validation: a value the column will not accept, and a record that complains about itself ----------------

    private sealed class NoBlanks : DataGridValidationRule
    {
        public override string Validate(object value, object item) =>
            string.IsNullOrWhiteSpace(value as string) ? "a name is required" : null;
    }

    private sealed class Complaining : System.ComponentModel.INotifyDataErrorInfo
    {
        public string Name { get; set; }
        public string Note { get; set; }
        private string _fault;

        public string Fault
        {
            get => _fault;
            set
            {
                _fault = value;
                ErrorsChanged?.Invoke(this, new System.ComponentModel.DataErrorsChangedEventArgs(nameof(Name)));
            }
        }

        public bool HasErrors => _fault != null;
        public event EventHandler<System.ComponentModel.DataErrorsChangedEventArgs> ErrorsChanged;

        public System.Collections.IEnumerable GetErrors(string propertyName) =>
            propertyName == nameof(Name) && _fault != null ? new[] { _fault } : Array.Empty<string>();
    }

    // The rule is asked of EVERY row, not only of an edit: data arrives wrong as readily as it is typed wrong, and a
    // table that only marked what was typed in front of it would leave a loaded page looking clean.
    [Test]
    public void ACellTheColumnWillNotAccept_IsMarked_WithoutAnyoneEditingIt()
    {
        var column = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) };
        var grid = Grid(column);
        grid.ItemsSource = new List<Row> { new() { Name = "kept" }, new() { Name = "  " } };
        column.ValidationRule = new NoBlanks();

        Relayout(grid);

        Assert.Multiple(() =>
        {
            Assert.That(grid.CellFor(0, 0).HasValidationError, Is.False, "a value the rule accepts is left alone");
            Assert.That(grid.CellFor(1, 0).HasValidationError, Is.True, "and one it does not is marked");
            Assert.That(grid.CellFor(1, 0).ValidationError, Is.EqualTo("a name is required"),
                "the MESSAGE travels with the mark - a red box that says nothing is a puzzle");
            Assert.That(grid.CellFor(1, 0).ToolTip, Is.EqualTo("a name is required"), "...and the cell can say it");
        });
    }

    // The OTHER source, asked in the same breath: a record that knows it is wrong says so itself, and the grid takes
    // that as readily as its own rule - with no rule on the column at all.
    [Test]
    public void ARecordThatReportsItsOwnError_MarksTheCell()
    {
        var item = new Complaining { Name = "Ada", Fault = "this one is filed as an error" };
        var grid = Grid(new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) });
        grid.ItemsSource = new List<Complaining> { item };

        Relayout(grid);
        Assert.That(grid.CellFor(0, 0).ValidationError, Is.EqualTo("this one is filed as an error"));

        // ...and it can stop complaining without anything else being touched: an error arrives and leaves on its own
        // event, which is the half of INotifyDataErrorInfo that is easy to leave out.
        item.Fault = null;
        Relayout(grid);
        Assert.That(grid.CellFor(0, 0).HasValidationError, Is.False, "the mark goes when the complaint does");
    }

    // The colour is the PAGE's to name, exactly as the search washes are - and handing it back has to restore the
    // theme's, which is the half a plain null assignment gets wrong.
    [Test]
    public void TheErrorWash_TakesTheBrushTheGridNames_AndGivesItBack()
    {
        var column = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) };
        var grid = Grid(column);
        grid.ItemsSource = new List<Row> { new() { Name = "  " } };
        column.ValidationRule = new NoBlanks();
        Relayout(grid);

        grid.CellFor(0, 0).SetValue(DataGridCell.ValidationErrorBrushProperty, Brushes.Blue, ValuePriority.Style);

        grid.ValidationErrorBrush = Brushes.Red;
        Relayout(grid);
        Assert.That(grid.CellFor(0, 0).ValidationErrorBrush, Is.EqualTo(Brushes.Red), "the page's colour wins");

        grid.ValidationErrorBrush = null;
        Relayout(grid);
        Assert.That(grid.CellFor(0, 0).ValidationErrorBrush, Is.EqualTo(Brushes.Blue),
            "and saying nothing hands the theme's colour back");
    }

    private sealed class Washes : System.ComponentModel.INotifyPropertyChanged
    {
        private Brush _match;

        public Brush Match
        {
            get => _match;
            set { _match = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Match))); }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }

    // The search washes are the grid's to name the same way - and giving the name up has to HAND THE COLOUR BACK. The
    // cells are recycled carriers: a colour written into one and never taken out again would follow it onto every row
    // it is later reused for, and no theme setter could be seen through it.
    // Driven through a BINDING, because that is how a page drives it and assigning the property in the test does not go
    // where the real value goes. The version of this test that assigned it directly passed while the stand could not
    // hand the colour back at all - the binding was dropping the null on the way, and nothing here could see that.
    [Test]
    public void SearchWashes_TakeTheBrushTheGridNames_AndGiveItBack()
    {
        var washes = new Washes { Match = Brushes.Green };
        var grid = SelectableGrid(2);
        grid.DataContext = washes;
        grid.SetBinding(nameof(TreeDataGrid.SearchMatchBrush), new Binding(nameof(Washes.Match)));
        Relayout(grid);
        // At Style priority, which is where a theme's setter actually lands - a plain assignment here would write the
        // very slot the grid writes, and the test would be measuring itself.
        grid.CellFor(0, 0).SetValue(DataGridCell.SearchMatchBrushProperty, Brushes.Blue, ValuePriority.Style);

        grid.SearchCurrentMatchBrush = Brushes.Red;
        Relayout(grid);

        Assert.Multiple(() =>
        {
            Assert.That(grid.CellFor(0, 0).SearchMatchBrush, Is.EqualTo(Brushes.Green));
            Assert.That(grid.CellFor(0, 0).SearchCurrentBrush, Is.EqualTo(Brushes.Red));
        });

        // A SECOND colour, still through the binding: naming one and then naming another has to land too, not only the
        // first one to arrive.
        washes.Match = Brushes.Yellow;
        BindingUpdateQueue.Flush();
        Relayout(grid);
        Assert.That(grid.CellFor(0, 0).SearchMatchBrush, Is.EqualTo(Brushes.Yellow));

        washes.Match = null;
        BindingUpdateQueue.Flush();
        grid.SearchCurrentMatchBrush = null;
        Relayout(grid);

        Assert.That(grid.CellFor(0, 0).SearchMatchBrush, Is.EqualTo(Brushes.Blue),
            "the colour from below is visible again, not the one the grid stopped naming");
    }

    // The rules are a THICKNESS on the cell's own border, so the four visibility states have to come out as the four
    // thicknesses - a right edge for the column rule, a bottom edge for the row rule.
    [TestCase(DataGridGridLines.All, 1.0, 1.0)]
    [TestCase(DataGridGridLines.Horizontal, 0.0, 1.0)]
    [TestCase(DataGridGridLines.Vertical, 1.0, 0.0)]
    [TestCase(DataGridGridLines.None, 0.0, 0.0)]
    public void GridLines_ReachTheCellsOwnBorder(DataGridGridLines lines, double right, double bottom)
    {
        var grid = SelectableGrid(3);
        grid.GridLinesVisibility = lines;
        Relayout(grid);

        var thickness = grid.CellFor(0, 0).GridLineThickness;
        Assert.Multiple(() =>
        {
            Assert.That(thickness.Right, Is.EqualTo(right));
            Assert.That(thickness.Bottom, Is.EqualTo(bottom));
            Assert.That(thickness.Left, Is.EqualTo(0.0), "a rule is drawn once, by the cell on its near side");
            Assert.That(thickness.Top, Is.EqualTo(0.0));
        });
    }

    // Whether a ROW is still listening to this item - read off the delegate itself, not guessed from the code. Rows
    // only: the binding system leaves one SourceEntry of its own on any object it ever observed, which holds its
    // subscribers weakly and dies with the object - that is not what this asks about.
    private static bool RowsListeningTo(LiveRow item) =>
        typeof(LiveRow)
            .GetField(nameof(LiveRow.PropertyChanged), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.GetValue(item) is Delegate handler
        && handler.GetInvocationList().Any(d => d.Target is DataGridRow);

    private sealed class LiveRow : System.ComponentModel.INotifyPropertyChanged
    {
        private string _name;

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Name)));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }

    // The model moved on its own - a service answered and the value was rewritten. Nothing about that is a layout
    // event, so a table that only re-reads its cells when something happens to measure them shows a stale number until
    // the user scrolls. The row follows its item while it is realized.
    [Test]
    public void ACellFollowsItsItemWhenTheModelRewritesTheValue()
    {
        var items = new List<LiveRow> { new() { Name = "before" } };
        var grid = Grid(new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) });
        grid.ItemsSource = items;
        Relayout(grid);

        Assert.That(grid.CellFor(0, 0).Content, Is.EqualTo("before"));

        items[0].Name = "after the service answered";

        Assert.That(grid.CellFor(0, 0).Content, Is.EqualTo("after the service answered"),
            "the cell followed the model without waiting for a layout pass");
    }

    // A view-model asks whether the table is in edit - to hold a toolbar, to refuse leaving the page, to postpone a
    // refresh. So it is a real property the binding engine can see, not a computed one it would silently never find.
    [Test]
    public void WhetherTheTableIsInEdit_IsSomethingAViewModelCanBindTo()
    {
        var (grid, _) = EditableGrid();
        var seen = new List<bool>();
        grid.PropertyChanged += (_, e) =>
        {
            if (e.Property == TreeDataGrid.IsEditingProperty) seen.Add((bool)e.NewValue);
        };

        Assert.That(grid.IsEditing, Is.False);

        grid.BeginEdit(0, 0);
        Assert.That(grid.IsEditing, Is.True);

        grid.CancelEdit();

        Assert.Multiple(() =>
        {
            Assert.That(grid.IsEditing, Is.False);
            Assert.That(seen, Is.EqualTo(new[] { true, false }), "and it ANNOUNCED both, which is what a binding needs");
        });
    }

    // The subscription points at the LONG-LIVED end: the item belongs to the application and outlives the table. A row
    // that is dropped without letting go is not a stale row - it is a page that never goes away, held by the item that
    // still calls it. Asked of the COLLECTOR, because "the code plainly unsubscribes" is what everyone says about every
    // leak that ever shipped.
    [Test]
    public void ADroppedRow_LetsGoOfItsItem()
    {
        var items = new List<LiveRow> { new() { Name = "one" }, new() { Name = "two" } };
        var grid = Grid(new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) });
        grid.ItemsSource = items;
        Relayout(grid);

        Assert.That(items.Count(RowsListeningTo), Is.EqualTo(2), "the realized rows are listening to their items");

        // What a Reset does, and what closing a page does: the containers are dropped wholesale.
        grid.ItemContainerGenerator.Clear();

        Assert.That(items.Count(RowsListeningTo), Is.Zero, "and every one of them let go");
    }

    // A state is read PER ROW: the rows that mean nothing must come back with nothing. A state that leaked to every
    // cell of the column would paint the whole column as an error, which is worse than not marking it at all.
    [Test]
    public void ACellsStateIsReadForItsOwnRowOnly()
    {
        var grid = Grid(new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) });
        grid.ItemsSource = new List<Row>
        {
            new() { Name = "one", Note = null },
            new() { Name = "two", Note = "Error" },
            new() { Name = "three", Note = null }
        };
        grid.Columns[0].StateBinding = new Binding("Note");
        Relayout(grid);

        Assert.Multiple(() =>
        {
            Assert.That(grid.CellFor(0, 0).State, Is.Null, "a row that means nothing says nothing");
            Assert.That(grid.CellFor(1, 0).State, Is.EqualTo("Error"));
            Assert.That(grid.CellFor(2, 0).State, Is.Null, "and the one after it is not tarred with it either");
        });
    }

    // A converter on a column's value binding - the plainest proof that the value is a real binding and not a member
    // name, which could never carry one.
    [Test]
    public void AColumnsValueBindingCarriesItsConverter()
    {
        var grid = SelectableGrid(2);
        grid.Columns[0].Binding = new Binding("Name") { Converter = new Shout() };
        Relayout(grid);

        var row = grid.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow;
        Assert.That(row.CellAt(0).Content, Is.EqualTo(((Row)grid.Rows[0].Node).Name.ToUpperInvariant()));
    }

    private sealed class Shout : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            (value as string)?.ToUpperInvariant();

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) =>
            (value as string)?.ToLowerInvariant();
    }

    // Selection driven FROM the model - highlighting search hits, marking bad values, restoring a selection on the way
    // back to a tab. A WPF DataGrid cannot be asked for this at all.
    [Test]
    public void AViewModelCanSetTheSelection()
    {
        var grid = SelectableGrid(10);

        grid.SelectedCells.Set(new CellRange(2, 0, 4, 1));

        Assert.Multiple(() =>
        {
            Assert.That(grid.SelectedCells.Contains(3, 1), Is.True);
            Assert.That(grid.SelectedCells.Contains(9, 0), Is.False);
        });
    }

    // ---- Phase 5: sorting within siblings, filtering that keeps ancestors ---------------------------------------

    private static TreeDataGrid SortableTree()
    {
        var b = new Row { Name = "B" };
        b.Children.Add(new Row { Name = "B-2" });
        b.Children.Add(new Row { Name = "B-1" });

        var a = new Row { Name = "A" };
        a.Children.Add(new Row { Name = "A-1" });

        var grid = Grid(new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100), SortMemberPath = "Name" });
        grid.ChildrenPath = "Children";
        grid.ItemsSource = new List<Row> { b, a };
        return grid;
    }

    [Test]
    public void Sorting_OrdersTheRoots()
    {
        var grid = SortableTree();
        grid.SortBy(grid.Columns[0]);

        Assert.That(Names(grid), Is.EqualTo(new[] { "A", "B" }));
    }

    // WITHIN siblings, never across the whole list: a global reordering puts children beside strangers and the hierarchy
    // stops meaning anything.
    [Test]
    public void Sorting_StaysWithinSiblings_SoTheHierarchyHolds()
    {
        var grid = SortableTree();
        grid.SortBy(grid.Columns[0]);

        var b = grid.Rows.Select(r => r.Node).Cast<Row>().First(r => r.Name == "B");
        grid.Expand(b);

        Assert.That(Names(grid), Is.EqualTo(new[] { "A", "B", "B-1", "B-2" }),
            "B's children are sorted among THEMSELVES and stay under B");
    }

    [Test]
    public void SortingDescending_TurnsItRound()
    {
        var grid = SortableTree();
        grid.SortBy(grid.Columns[0], descending: true);

        Assert.That(Names(grid), Is.EqualTo(new[] { "B", "A" }));
    }

    // A match nobody can reach is not a match: an ancestor that fails the filter is kept as a signpost to one that passes.
    [Test]
    public void Filtering_KeepsTheAncestorsOfAMatch()
    {
        var grid = SortableTree();
        grid.SetFilter(node => node is Row { Name: "B-1" });

        Assert.That(Names(grid), Is.EqualTo(new[] { "B" }), "B does not match, but it is the only way to B-1");

        var b = grid.Rows.Select(r => r.Node).Cast<Row>().First();
        grid.Expand(b);

        Assert.That(Names(grid), Is.EqualTo(new[] { "B", "B-1" }), "and under it, only what matched");
    }

    [Test]
    public void ClearingTheFilter_BringsEverythingBack()
    {
        var grid = SortableTree();
        grid.SetFilter(node => node is Row { Name: "B-1" });
        grid.SetFilter(null);

        Assert.That(Names(grid), Is.EqualTo(new[] { "B", "A" }));
    }

    // The doc's rule: a sort or a filter is one of the moments an Auto column is measured afresh, because the rows on
    // screen are not the rows it settled on.
    [Test]
    public void SortingResetsTheAutoWidths()
    {
        var column = new DataGridTextColumn { Binding = new Binding("Name"), Width = GridLength.Auto, SortMemberPath = "Name" };
        var grid = Grid(column);
        grid.ItemsSource = Flat(3);
        DataGridColumnLayout.RecordMeasured(column, 300);

        grid.SortBy(column);

        Assert.That(column.MeasuredWidth, Is.EqualTo(0), "the width it had described the rows it no longer shows");
    }

    private static string[] Names(TreeDataGrid grid) =>
        grid.Rows.Select(r => ((Row)r.Node).Name).ToArray();

    // ---- Phase 6: editing ---------------------------------------------------------------------------------------

    private sealed class Editable
    {
        public string Name { get; set; }
        public int Count { get; set; }
        public bool Done { get; set; }
        public bool Locked { get; set; }
        public ObservableCollection<Editable> Children { get; } = new();
    }

    private static (TreeDataGrid grid, List<Editable> items) EditableGrid()
    {
        var items = new List<Editable>
        {
            new() { Name = "one", Count = 1 },
            new() { Name = "two", Count = 2 }
        };

        var grid = Grid(
            new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) },
            new DataGridTextColumn { Binding = new Binding("Count"), Width = new GridLength(100) });
        grid.ItemsSource = items;
        Relayout(grid);
        return (grid, items);
    }

    [Test]
    public void Editing_WritesThroughToTheItem()
    {
        var (grid, items) = EditableGrid();

        Assert.That(grid.BeginEdit(0, 0), Is.True);
        grid.CellFor(0, 0).EditedValue = "renamed";
        Assert.That(grid.CommitEdit(), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(items[0].Name, Is.EqualTo("renamed"));
            Assert.That(grid.IsEditing, Is.False, "and the cell left edit mode");
        });
    }

    // A text editor's string has to land in an int column - the write converts, and refuses when it cannot.
    [Test]
    public void Editing_ConvertsToTheMembersType()
    {
        var (grid, items) = EditableGrid();

        grid.BeginEdit(0, 1);
        grid.CellFor(0, 1).EditedValue = "42";
        Assert.That(grid.CommitEdit(), Is.True);
        Assert.That(items[0].Count, Is.EqualTo(42));

        grid.BeginEdit(1, 1);
        grid.CellFor(1, 1).EditedValue = "not a number";
        Assert.Multiple(() =>
        {
            Assert.That(grid.CommitEdit(), Is.False, "a value the member will not take is a refusal, not a crash");
            Assert.That(grid.IsEditing, Is.True, "and the cell stays in edit with what was typed");
            Assert.That(items[1].Count, Is.EqualTo(2), "nothing was written");
        });
    }

    [Test]
    public void Cancelling_WritesNothing()
    {
        var (grid, items) = EditableGrid();

        grid.BeginEdit(0, 0);
        grid.CellFor(0, 0).EditedValue = "discarded";
        grid.CancelEdit();

        Assert.Multiple(() =>
        {
            Assert.That(items[0].Name, Is.EqualTo("one"));
            Assert.That(grid.IsEditing, Is.False);
        });
    }

    [Test]
    public void AHandlerCanRefuseAnEdit_BeforeAndAfter()
    {
        var (grid, items) = EditableGrid();

        grid.CellEditBeginning += (_, e) => e.Cancel = e.Column == grid.Columns[1];
        Assert.That(grid.BeginEdit(0, 1), Is.False, "refused before it started");

        grid.CellEditEnding += (_, e) => e.Cancel = Equals(e.Value, "no");
        grid.BeginEdit(0, 0);
        grid.CellFor(0, 0).EditedValue = "no";
        Assert.Multiple(() =>
        {
            Assert.That(grid.CommitEdit(), Is.False, "and refused on the way out");
            Assert.That(items[0].Name, Is.EqualTo("one"));
        });
    }

    [Test]
    public void AReadOnlyColumn_DoesNotEdit()
    {
        var (grid, _) = EditableGrid();
        grid.Columns[0].IsReadOnly = true;

        Assert.That(grid.BeginEdit(0, 0), Is.False);
    }

    // An edit goes back the way the value came: through the column's binding, converter and all. A one-way column is a
    // column to READ, and says so instead of reporting a write that never landed.
    [Test]
    public void AOneWayColumnRefusesTheWrite()
    {
        var (grid, items) = EditableGrid();
        grid.Columns[0].Binding = new Binding("Name") { Mode = BindingMode.OneWay };
        Relayout(grid);

        grid.BeginEdit(0, 0);
        grid.CellFor(0, 0).EditedValue = "not landing";
        var committed = grid.CommitEdit();

        Assert.Multiple(() =>
        {
            Assert.That(committed, Is.False, "the write said it did not land");
            Assert.That(items[0].Name, Is.EqualTo("one"), "and the item is untouched");
        });
    }

    [Test]
    public void EditingSwapsOnlyThatCellsTemplate()
    {
        var (grid, _) = EditableGrid();
        var editing = new DataTemplate(() => new TemplateResult { RootComponent = new Adamantium.UI.Controls.Text.TextBox() });
        grid.Columns[0].CellEditingTemplate = editing;

        grid.BeginEdit(0, 0);

        Assert.Multiple(() =>
        {
            Assert.That(grid.CellFor(0, 0).ContentTemplate, Is.SameAs(editing));
            Assert.That(grid.CellFor(0, 1).ContentTemplate, Is.Not.SameAs(editing), "the neighbour was left alone");
        });
    }

    // The value the commit writes has to come from the EDITOR, not from what the cell was showing before it opened -
    // otherwise every edit writes the old value straight back and the feature looks like it does nothing at all.
    [Test]
    public void TheCommitTakesWhatTheEditorHolds()
    {
        var (grid, items) = EditableGrid();
        Chrome(grid);

        Assert.That(grid.BeginEdit(0, 0), Is.True);
        Relayout(grid, grid.CellFor(0, 0));

        var editor = Editor(grid.CellFor(0, 0));
        Assert.That(editor, Is.Not.Null, "a text column brings its own editor - no markup needed");
        Assert.That(editor.Text, Is.EqualTo("one"), "and it opens on the value that was there");

        editor.Text = "typed in";
        Assert.That(grid.CommitEdit(), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(items[0].Name, Is.EqualTo("typed in"));
            Assert.That(grid.IsEditing, Is.False);
        });
    }

    [Test]
    public void CancelKeepsWhatTheEditorNeverWrote()
    {
        var (grid, items) = EditableGrid();
        Chrome(grid);

        grid.BeginEdit(0, 0);
        Relayout(grid, grid.CellFor(0, 0));
        Editor(grid.CellFor(0, 0)).Text = "discarded";
        grid.CancelEdit();

        Assert.That(items[0].Name, Is.EqualTo("one"));
    }

    // Every built-in column type brings its own editor, and the commit reads THAT editor - a check box column that
    // wrote back the value it started with would look exactly like one that worked.
    [Test]
    public void ACheckBoxColumnEditsThroughItsBox()
    {
        var items = new List<Editable> { new() { Name = "one", Done = false } };
        var column = new DataGridCheckBoxColumn { Binding = new Binding("Done"), Width = new GridLength(60) };
        var grid = Grid(column);
        grid.ItemsSource = items;
        Relayout(grid);
        Chrome(grid);

        Assert.That(Descendant<CheckBox>(grid.CellFor(0, 0)), Is.Not.Null, "a box is drawn, not the word False");

        Assert.That(grid.BeginEdit(0, 0), Is.True);
        Relayout(grid, grid.CellFor(0, 0));

        var box = Descendant<CheckBox>(grid.CellFor(0, 0));
        Assert.That(box.IsChecked, Is.False, "the editor opens on the value that was there");

        box.IsChecked = true;
        Assert.That(grid.CommitEdit(), Is.True);
        Assert.That(items[0].Done, Is.True);
    }

    // Press on a cell and drag: the block follows the pointer and SHRINKS again when it comes back - it is extended from
    // the anchor every time, never grown by whatever it passes over.
    [Test]
    public void DraggingOutASelectionFollowsThePointerBothWays()
    {
        var grid = SelectableGrid(4);
        Relayout(grid);

        var rows = Enumerable.Range(0, 4)
            .Select(i => grid.ItemContainerGenerator.ContainerFromIndex(i) as DataGridRow)
            .ToArray();

        grid.SelectCell(0, 0, extend: false, add: false);
        grid.BeginDragSelect(adds: false);

        grid.DragSelectFromRow(rows[2], 1);
        Assert.That(Selected(grid), Is.EqualTo(6), "three rows by two columns");

        grid.DragSelectFromRow(rows[1], 0);
        Assert.That(Selected(grid), Is.EqualTo(2), "coming back shrinks the same block");

        grid.EndDragSelect();
        grid.DragSelectFromRow(rows[3], 1);
        Assert.That(Selected(grid), Is.EqualTo(2), "and a released pointer no longer draws anything");
    }

    // The CELL is what the theme dims, so it has to carry the answer - from its column and from the provider alike.
    [Test]
    public void ACellKnowsItIsReadOnly()
    {
        var items = new List<Editable> { new() { Name = "one" }, new() { Name = "two" } };
        var locked = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100), IsReadOnly = true };
        var open = new DataGridTextColumn { Binding = new Binding("Count"), Width = new GridLength(100) };
        var grid = Grid(locked, open);
        grid.ItemsSource = items;
        Relayout(grid);

        Assert.Multiple(() =>
        {
            Assert.That(grid.CellFor(0, 0).IsReadOnly, Is.True, "refused by the column");
            Assert.That(grid.CellFor(0, 1).IsReadOnly, Is.False);
        });

        // ...and ONE CELL refuses through a binding read off its own row, which the rest of the column knows nothing of.
        items[1].Locked = true;
        open.IsReadOnlyBinding = new Binding("Locked");
        Relayout(grid);

        Assert.That(grid.CellFor(1, 1).IsReadOnly, Is.True, "refused for one cell");
        Assert.That(grid.CellFor(0, 1).IsReadOnly, Is.False, "which is not the whole column");
    }

    private static int Selected(TreeDataGrid grid)
    {
        var count = 0;
        for (var row = 0; row < grid.Rows.Count; row++)
        {
            for (var column = 0; column < grid.Columns.Count; column++)
            {
                if (grid.SelectedCells.Contains(row, column)) count++;
            }
        }

        return count;
    }

    // A check box shows the same box whether or not it is being edited, so asking for a double-click first asks twice
    // for the same thing. One click flips it - through the ordinary edit, so a read-only cell still refuses.
    [Test]
    public void OneClickFlipsACheckBoxCell()
    {
        var items = new List<Editable> { new() { Name = "one", Done = false } };
        var column = new DataGridCheckBoxColumn { Binding = new Binding("Done"), Width = new GridLength(60) };
        var grid = Grid(column);
        grid.ItemsSource = items;
        Relayout(grid);

        Assert.That(grid.ToggleCell(0, 0), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(items[0].Done, Is.True, "flipped and written");
            Assert.That(grid.IsEditing, Is.False, "and it did not stay in edit");
        });

        grid.ToggleCell(0, 0);
        Assert.That(items[0].Done, Is.False, "and back again");

        column.IsReadOnly = true;
        Assert.That(grid.ToggleCell(0, 0), Is.False, "a read-only column refuses the click too");
        Assert.That(items[0].Done, Is.False);
    }

    [Test]
    public void ADropDownColumnTakesItsChoicesFromItsOwnList()
    {
        var items = new List<Editable> { new() { Name = "one" } };
        var column = new DataGridDropDownColumn
        {
            Binding = new Binding("Name"),
            Width = new GridLength(100),
            ItemsSource = new[] { "one", "two", "three" }
        };
        var grid = Grid(column);
        grid.ItemsSource = items;
        Relayout(grid);
        Chrome(grid);

        grid.BeginEdit(0, 0);
        Relayout(grid, grid.CellFor(0, 0));

        var drop = Descendant<DropDown>(grid.CellFor(0, 0));
        Assert.That(drop, Is.Not.Null);
        Assert.That(drop.SelectedItem, Is.EqualTo("one"), "opened on what the cell held");

        drop.SelectedItem = "three";
        grid.CommitEdit();
        Assert.That(items[0].Name, Is.EqualTo("three"), "and the choice went back through the column's binding");
    }

    private static T Descendant<T>(IUIComponent root) where T : class
    {
        foreach (var child in root.VisualChildren)
        {
            if (child is T match) return match;
            if (Descendant<T>(child) is { } nested) return nested;
        }

        return null;
    }

    // The cell shows its content through PART_ContentPresenter, so without a template there is nothing to build an
    // editing template INTO. This is the theme's chrome cut down to the one part that matters here.
    private static void Chrome(TreeDataGrid grid)
    {
        foreach (var index in grid.ItemContainerGenerator.RealizedIndices)
        {
            if (grid.ItemContainerGenerator.ContainerFromIndex(index) is not DataGridRow row) continue;
            for (var column = 0; column < grid.Columns.Count; column++)
            {
                if (row.CellAt(column) is not { } cell) continue;
                cell.Template = CellChrome();
                cell.InvalidateMeasure();
            }

            row.InvalidateMeasure();
        }

        Relayout(grid);
    }

    private static ControlTemplate CellChrome() => new(() =>
    {
        var presenter = new ContentPresenter();
        var result = new TemplateResult { RootComponent = presenter };
        result.RegisterName("PART_ContentPresenter", presenter);
        result.AddTemplateBinding(presenter, "Content", new TemplateBinding { Path = "Content" });
        result.AddTemplateBinding(presenter, "ContentTemplate", new TemplateBinding { Path = "ContentTemplate" });
        return result;
    });

    private static Adamantium.UI.Controls.Text.TextBox Editor(IUIComponent root)
    {
        foreach (var child in root.VisualChildren)
        {
            if (child is Adamantium.UI.Controls.Text.TextBox box) return box;
            if (Editor(child) is { } nested) return nested;
        }

        return null;
    }

    // Indent is not a width adjustment: the content has to MOVE, and the cell was shrinking itself without shifting
    // anything, so a child row drew flush against its parent and the hierarchy was invisible.
    [Test]
    public void Indent_ShiftsTheContentRight()
    {
        var carrier = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(200) };
        var (grid, root) = Tree(carrier);
        grid.Indent = 20;
        grid.Expand(root);
        Relayout(grid);

        var rootRow = grid.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow;
        var childRow = grid.ItemContainerGenerator.ContainerFromIndex(1) as DataGridRow;

        Assert.Multiple(() =>
        {
            Assert.That(ContentX(rootRow.CellAt(0)), Is.EqualTo(0).Within(0.5), "depth 0 sits at the cell's edge");
            Assert.That(ContentX(childRow.CellAt(0)), Is.EqualTo(20).Within(0.5), "depth 1 is drawn one indent in");
        });
    }

    private static double ContentX(DataGridCell cell)
    {
        foreach (var child in cell.VisualChildren) return ((IUIComponent)child).Bounds.X;
        return double.NaN;
    }

    // ---- Phase 7: the header strip's state, and column filters ---------------------------------------------------

    private static DataGridHeadersPresenter Headers(TreeDataGrid grid)
    {
        var headers = new DataGridHeadersPresenter { Owner = grid };
        headers.Measure(new Size(400, 26));
        headers.Arrange(new Rect(0, 0, 400, 26));
        return headers;
    }

    private static DataGridColumnHeader HeaderOf(DataGridHeadersPresenter headers, int index)
    {
        foreach (var child in headers.VisualChildren)
        {
            if (child is DataGridColumnHeader header && header.ColumnIndex == index) return header;
        }

        return null;
    }

    // A reorder has to be VISIBLE while it happens: a mark where the column would land, and the carried header saying
    // it was picked up. Without them the table sat still until the button came up and the column had already moved -
    // a gesture the user has to perform twice to learn.
    [Test]
    public void ADraggedColumn_ShowsWhereItWouldLandAndThatItWasPickedUp()
    {
        var grid = Grid(
            new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) },
            new DataGridTextColumn { Binding = new Binding("Note"), Width = new GridLength(100) });
        grid.ItemsSource = Flat(3);
        grid.DropIndicatorBrush = Brushes.Red;
        Relayout(grid);

        var headers = Headers(grid);
        headers.BeginReorder(0, 120);
        Relayout(grid);

        var mark = grid.GetTemplateChild("PART_DropIndicator") as Border;
        Assert.Multiple(() =>
        {
            // An OVERRIDE while the drag runs: the carried header spends it over something else, and a per-element
            // cursor is only ever applied for the element the pointer is actually on - set on the strip, it appeared
            // only once the button came back up.
            Assert.That(Mouse.OverrideCursor, Is.EqualTo(Cursors.SizeAll), "the pointer says a column is being carried");
            Assert.That(HeaderOf(headers, 0).IsDragging, Is.True, "the header says it is being carried");
            Assert.That(mark, Is.Not.Null, "and a mark stands where the column would land");
            Assert.That(mark.Visibility, Is.EqualTo(Visibility.Visible));
            Assert.That(mark.Margin.Left, Is.EqualTo(99).Within(2), "at the boundary the pointer has reached");

            // It lives in the GRID's template and runs the whole height, because the place being aimed at is a place
            // among the ROWS - a child of the header strip could not be drawn past the strip.
            Assert.That(mark.Bounds.Height, Is.GreaterThan(headers.Bounds.Height), "taller than the header band");
        });

        headers.EndReorder(120);

        Assert.Multiple(() =>
        {
            Assert.That(mark.Visibility, Is.EqualTo(Visibility.Collapsed), "and it goes when the drag does");
            Assert.That(HeaderOf(headers, 0).IsDragging, Is.False);
            Assert.That(Mouse.OverrideCursor, Is.Null, "and the pointer is given back");
        });
    }

    // The strip syncs itself in MEASURE, so a sort that changes no width never reached it and the arrow simply never
    // appeared - it showed up only when something else happened to re-measure the headers.
    [Test]
    public void SortingReachesTheHeaderStrip()
    {
        var grid = Grid(
            new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) },
            new DataGridTextColumn { Binding = new Binding("Note"), Width = new GridLength(100) });
        grid.ItemsSource = Flat(3);
        Relayout(grid);

        var headers = Headers(grid);
        grid.SortBy(grid.Columns[0]);
        headers.Measure(new Size(400, 26));

        Assert.Multiple(() =>
        {
            Assert.That(HeaderOf(headers, 0).SortDirection, Is.EqualTo(DataGridSortDirection.Ascending));
            Assert.That(HeaderOf(headers, 1).SortDirection, Is.EqualTo(DataGridSortDirection.None), "only the one sorted");
        });

        grid.SortBy(grid.Columns[0], descending: true);
        headers.Measure(new Size(400, 26));
        Assert.That(HeaderOf(headers, 0).SortDirection, Is.EqualTo(DataGridSortDirection.Descending));
    }

    [Test]
    public void AColumnFilterKeepsOnlyTheTickedValues()
    {
        var grid = Grid(new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) });
        grid.ItemsSource = Flat(4);
        Relayout(grid);

        var filter = grid.FilterFor(grid.Columns[0]);
        filter.Included = new HashSet<string> { "Item 2", "Item 4" };
        grid.ApplyFilters();

        Assert.That(Names(grid), Is.EqualTo(new[] { "Item 2", "Item 4" }));

        grid.ClearColumnFilter(grid.Columns[0]);
        Assert.That(Names(grid).Length, Is.EqualTo(4), "clearing gives every row back");
    }

    [Test]
    public void TwoConditionsJoinAsAskedAndTwoColumnsNarrowTogether()
    {
        var grid = Grid(
            new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) },
            new DataGridTextColumn { Binding = new Binding("Note"), Width = new GridLength(100) });
        grid.ItemsSource = Flat(6);
        Relayout(grid);

        var byName = grid.FilterFor(grid.Columns[0]);
        byName.First.Operator = DataGridFilterOperator.EndsWith;
        byName.First.Value = "2";
        byName.Logic = DataGridFilterLogic.Or;
        byName.Second.Operator = DataGridFilterOperator.EndsWith;
        byName.Second.Value = "5";
        grid.ApplyFilters();

        Assert.That(Names(grid), Is.EqualTo(new[] { "Item 2", "Item 5" }), "OR let both through");

        var byNote = grid.FilterFor(grid.Columns[1]);
        byNote.First.Operator = DataGridFilterOperator.Contains;
        byNote.First.Value = "5";
        grid.ApplyFilters();

        Assert.That(Names(grid), Is.EqualTo(new[] { "Item 5" }), "a second column narrows the first one's rows");
    }

    // A filter tests the value the CELL shows - the column's own binding, converter and all. Reading the item by some
    // other route filtered on a value the table never displayed: ticking "false" brought back rows shown as true.
    [Test]
    public void AFilterSeesWhatTheCellShows()
    {
        var items = new List<Editable> { new() { Name = "one" }, new() { Name = "two" } };
        var column = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100), SortMemberPath = "Name" };
        var grid = Grid(column);
        grid.ItemsSource = items;
        Relayout(grid);

        items[1].Name = "one";   // edited through the same binding the cell reads

        grid.FilterFor(column).Included = new HashSet<string> { "one" };
        grid.ApplyFilters();

        Assert.That(grid.Rows.Count, Is.EqualTo(2), "both rows now READ as 'one', whatever the items still say");
        Assert.That(grid.DistinctValues(column), Is.EquivalentTo(new object[] { "one" }),
            "and the value list offers what the cells show");
    }

    // Removing a row from the SOURCE takes it off the table - including while the table is sorted or filtered, where the
    // rows on screen are a shaped list of the control's own and a splice into it would never reach the application's.
    [Test]
    public void RemovingFromTheSourceRemovesTheRow()
    {
        var items = new ObservableCollection<Editable>
        {
            new() { Name = "one" }, new() { Name = "two" }, new() { Name = "three" }
        };

        var column = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100), SortMemberPath = "Name" };
        var grid = Grid(column);
        grid.ItemsSource = items;
        Relayout(grid);

        items.RemoveAt(1);
        Assert.That(grid.Rows.Count, Is.EqualTo(2), "plain: the flattener splices the row out");

        grid.SortBy(column);
        Assert.That(grid.Rows.Count, Is.EqualTo(2));

        items.RemoveAt(0);
        Assert.That(grid.Rows.Count, Is.EqualTo(1), "sorted: the shaped view is rebuilt from what is left");

        items.Add(new Editable { Name = "four" });
        Assert.That(grid.Rows.Count, Is.EqualTo(2), "and an added row appears in its sorted place");
    }

    // Dragging a header moves the COLUMN, and the expander goes with its own column rather than staying on an index
    // that now belongs to a different one.
    [Test]
    public void MovingAColumnTakesItsExpanderWithIt()
    {
        var first = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) };
        var second = new DataGridTextColumn { Binding = new Binding("Note"), Width = new GridLength(100) };
        var third = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) };

        var grid = Grid(first, second, third);
        grid.ItemsSource = Flat(2);
        grid.ExpanderColumnIndex = 1;
        Relayout(grid);

        Assert.That(grid.ExpanderColumn, Is.SameAs(second));

        grid.MoveColumn(1, 2);

        Assert.Multiple(() =>
        {
            Assert.That(grid.Columns[2], Is.SameAs(second), "the column moved");
            Assert.That(grid.ExpanderColumn, Is.SameAs(second), "and kept the expander");
            Assert.That(grid.ExpanderColumnIndex, Is.EqualTo(2));
        });

        grid.MoveColumn(2, 0);
        Assert.That(grid.Columns[0], Is.SameAs(second));
        Assert.That(grid.ExpanderColumn, Is.SameAs(second));
    }

    [Test]
    public void DeleteRemovesTheSelectedRowsFromTheirOwnCollections()
    {
        var root = new Editable { Name = "root" };
        root.Children.Add(new Editable { Name = "child one" });
        root.Children.Add(new Editable { Name = "child two" });
        var items = new ObservableCollection<Editable> { root, new() { Name = "loose" } };

        var grid = Grid(new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) });
        grid.ChildrenPath = "Children";
        grid.ItemsSource = items;
        Relayout(grid);
        grid.Expand(grid.Rows[0]);

        grid.SelectCell(1, 0, extend: false, add: false);
        Assert.That(grid.DeleteSelectedRows(), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(root.Children.Count, Is.EqualTo(1), "a child is removed from its PARENT's collection");
            Assert.That(items.Count, Is.EqualTo(2), "and the roots are untouched");
        });

        grid.SelectCell(0, 0, extend: false, add: false);
        grid.DeleteSelectedRows();
        Assert.That(items.Count, Is.EqualTo(1), "a root row goes from the source");
    }

    [Test]
    public void AHandlerCanRefuseADeleteAndSupplyANewRow()
    {
        var items = new ObservableCollection<Editable> { new() { Name = "one" } };
        var grid = Grid(new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) });
        grid.ItemsSource = items;
        Relayout(grid);

        grid.RowDeleting += (_, e) => e.Cancel = true;
        grid.SelectCell(0, 0, extend: false, add: false);

        Assert.That(grid.DeleteSelectedRows(), Is.False, "refused");
        Assert.That(items.Count, Is.EqualTo(1));

        grid.RowAdding += (_, e) => e.NewItem = new Editable { Name = "made by the application" };
        var added = grid.AddRow();

        Assert.Multiple(() =>
        {
            Assert.That(added, Is.Not.Null);
            Assert.That(items.Count, Is.EqualTo(2));
            Assert.That(items[1].Name, Is.EqualTo("made by the application"), "added right after the active row");
        });
    }

    [Test]
    public void CopyingPutsTheSelectionOnTheClipboardAsTabbedText()
    {
        var grid = SelectableGrid(3);
        Relayout(grid);

        grid.SelectCell(0, 0, extend: false, add: false);
        grid.SelectCell(1, 1, extend: true, add: false);

        Adamantium.UI.Core.Input.Clipboard.SetText(string.Empty);
        Assert.That(grid.CopySelection(), Is.True);

        var text = Adamantium.UI.Core.Input.Clipboard.GetText();
        Assert.That(text, Is.EqualTo(grid.GetSelectionAsText()));
        Assert.That(text, Does.Contain("\t").And.Contain("\n"), "two columns and two rows");
    }

    // A member path is the markup-friendly way to say where children live, not the only one: data that arrives FLAT -
    // a table with a parent id, the shape a database hands you - has no such member, and a selector answers for it.
    [Test]
    public void ChildrenCanComeFromASelectorInsteadOfAPath()
    {
        var rows = new[]
        {
            new Editable { Name = "root" },
            new Editable { Name = "child of root" },
            new Editable { Name = "loose" }
        };

        var grid = Grid(new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) });
        grid.ChildrenSelector = item => ReferenceEquals(item, rows[0]) ? new[] { rows[1] } : null;
        grid.ItemsSource = new[] { rows[0], rows[2] };
        Relayout(grid);

        Assert.That(grid.Rows.Count, Is.EqualTo(2), "the child is under a closed branch");
        Assert.That(grid.Rows[0].HasChildren, Is.True, "which the selector is what says");

        grid.Expand(grid.Rows[0]);
        Assert.That(grid.Rows.Select(r => ((Editable)r.Node).Name),
            Is.EqualTo(new[] { "root", "child of root", "loose" }));
    }

    // The list is what gets a hidden row BACK, so it has to come from the whole source rather than from what survived.
    [Test]
    public void TheValueListCoversTheWholeSourceIncludingChildren()
    {
        var column = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) };
        var (grid, root) = Tree(column);

        var values = grid.DistinctValues(column);
        Assert.That(values, Does.Contain(root.Name));
        Assert.That(values.Count, Is.GreaterThan(1), "children count even while the branch is shut");

        grid.FilterFor(column).Included = new HashSet<string>();
        grid.ApplyFilters();

        Assert.That(grid.Rows.Count, Is.EqualTo(0), "an empty tick set hides everything - it is not the same as 'all'");
        Assert.That(grid.DistinctValues(column).Count, Is.EqualTo(values.Count), "and the list still offers them back");
    }

    // The form opens EMPTY, the way a spreadsheet's does: ticking is how you narrow, so an untouched list must mean
    // "no value filter" - not a full set the user has to empty first.
    [Test]
    public void TheValueListOpensUntickedAndOnlyAPartialTickNarrows()
    {
        var column = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) };
        var grid = Grid(column);
        grid.ItemsSource = Flat(3);
        Relayout(grid);

        var view = new DataGridFilterView();
        view.Open(grid, column);

        Assert.That(view.Values.Count, Is.EqualTo(3));
        Assert.That(view.Values.Any(v => v.IsChecked), Is.False, "nothing is ticked to begin with");

        view.Apply();
        Assert.That(Names(grid).Length, Is.EqualTo(3), "and applying an untouched form hides nothing");

        view.Values[1].IsChecked = true;
        view.Apply();
        Assert.That(Names(grid), Is.EqualTo(new[] { "Item 2" }), "a partial tick is what narrows");

        view.Values[0].IsChecked = true;
        view.Values[2].IsChecked = true;
        view.Apply();
        Assert.That(Names(grid).Length, Is.EqualTo(3), "ticking everything says the same as ticking nothing");
    }

    [Test]
    public void MatchCaseIsHonoured()
    {
        var condition = new DataGridFilterCondition
        {
            Operator = DataGridFilterOperator.IsEqualTo,
            Value = "item"
        };

        Assert.That(condition.Passes("Item"), Is.True, "off by default, as a search box is");

        condition.IsCaseSensitive = true;
        Assert.Multiple(() =>
        {
            Assert.That(condition.Passes("Item"), Is.False);
            Assert.That(condition.Passes("item"), Is.True);
        });
    }

    [Test]
    public void AFilteredColumnSaysSoInItsHeader()
    {
        var grid = Grid(new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) });
        grid.ItemsSource = Flat(3);
        Relayout(grid);

        var headers = Headers(grid);
        Assert.That(HeaderOf(headers, 0).IsFiltered, Is.False);

        grid.FilterFor(grid.Columns[0]).First.Value = "Item 1";
        grid.ApplyFilters();
        headers.Measure(new Size(400, 26));

        Assert.That(HeaderOf(headers, 0).IsFiltered, Is.True);
    }

    [Test]
    public void ChangingColumns_ChangesTheCells()
    {
        var grid = Grid(new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(80) });
        grid.ItemsSource = Flat(2);
        grid.Measure(new Size(400, 200));
        grid.Arrange(new Rect(0, 0, 400, 200));

        grid.Columns.Add(new DataGridTextColumn { Binding = new Binding("Note"), Width = new GridLength(120) });
        Relayout(grid);

        var row = grid.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow;
        Assert.That(row.CellAt(1), Is.Not.Null, "the new column brought a cell with it");
        Assert.That(row.CellAt(1).Content, Is.EqualTo("Note 1"));
    }

    // ---- Row numbers -----------------------------------------------------------------------------------------------

    private static TreeDataGrid NumberedGrid(int rows = 4)
    {
        var grid = SelectableGrid(rows);
        grid.ShowRowNumbers = true;
        Relayout(grid);
        return grid;
    }

    private static DataGridRowHeader NumberOf(TreeDataGrid grid, int row) =>
        (grid.ItemContainerGenerator.ContainerFromIndex(row) as DataGridRow)?.NumberHeader;

    private static void Press(DataGridRowHeader header, InputModifiers modifiers = InputModifiers.None) =>
        ((IObservableComponent)header).RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, MouseButtons.Left,
            MouseButtonState.Pressed, modifiers | InputModifiers.LeftMouseButton, 0)
        { RoutedEvent = InputUIComponent.MouseLeftButtonDownEvent });

    // The number is the row's place among what is VISIBLE, not its place in the source: after a sort the strip still
    // reads 1, 2, 3 down the screen, because that is the number a person points at when they say "row 2".
    [Test]
    public void TheNumber_CountsWhatIsOnScreen_NotTheSource()
    {
        var grid = NumberedGrid(3);
        grid.SortBy(grid.Columns[0], descending: true);
        Relayout(grid);

        Assert.Multiple(() =>
        {
            Assert.That(grid.CellFor(0, 0).Content, Is.EqualTo("Item 3"), "the last source row is now the first");
            Assert.That(NumberOf(grid, 0).Number, Is.EqualTo(1), "and it is still numbered 1");
            Assert.That(NumberOf(grid, 1).Number, Is.EqualTo(2));
            Assert.That(NumberOf(grid, 2).Number, Is.EqualTo(3));
        });
    }

    // Pressing the number takes the ROW - a rectangle across every column, which is what row selection is here - and
    // the strip says so, or the gesture looks like it did nothing.
    [Test]
    public void PressingANumber_TakesTheWholeRow()
    {
        var grid = NumberedGrid();
        Press(NumberOf(grid, 1));

        Assert.Multiple(() =>
        {
            Assert.That(grid.SelectedCells.Contains(1, 0), Is.True);
            Assert.That(grid.SelectedCells.Contains(1, 1), Is.True, "every column, not the one under the pointer");
            Assert.That(grid.SelectedCells.Contains(0, 0), Is.False);
            Assert.That(NumberOf(grid, 1).IsSelected, Is.True, "and the strip shows the row it took");
            Assert.That(NumberOf(grid, 0).IsSelected, Is.False);
        });
    }

    // The same two modifiers the cells answer to, for the same reason: a person who has learnt them over the data does
    // not learn them again over the strip.
    [Test]
    public void ShiftStretchesTheStrip_CtrlAddsAnotherRow()
    {
        var grid = NumberedGrid(5);

        Press(NumberOf(grid, 1));
        Press(NumberOf(grid, 3), InputModifiers.LeftShift);

        Assert.Multiple(() =>
        {
            Assert.That(grid.SelectedCells.Contains(2, 1), Is.True, "everything between the anchor and here");
            Assert.That(grid.SelectedCells.Contains(0, 0), Is.False);
            Assert.That(NumberOf(grid, 2).IsSelected, Is.True, "and the strip carries the whole block");
        });

        Press(NumberOf(grid, 0), InputModifiers.LeftControl);

        Assert.Multiple(() =>
        {
            Assert.That(grid.SelectedCells.Contains(0, 0), Is.True, "Ctrl adds a row beside the block");
            Assert.That(grid.SelectedCells.Contains(2, 0), Is.True, "and keeps it");
        });
    }

    // FullRow: the click still lands on a cell, it just takes the row. Answered at the ONE funnel every gesture passes
    // through, so a press, a drag and an arrow key cannot disagree about what the mode means.
    [Test]
    public void InFullRowMode_AClickOnAnyCellTakesTheWholeRow()
    {
        var grid = SelectableGrid(4);
        grid.SelectionUnit = DataGridSelectionUnit.FullRow;
        Relayout(grid);

        grid.SelectCell(1, 1);

        Assert.Multiple(() =>
        {
            Assert.That(grid.SelectedCells.Contains(1, 0), Is.True, "the column that was NOT clicked comes too");
            Assert.That(grid.SelectedCells.Contains(1, 1), Is.True);
            Assert.That(grid.SelectedCells.Contains(0, 1), Is.False, "and no other row does");
            Assert.That(grid.ActiveColumn, Is.EqualTo(1), "the keyboard still stands where the click landed");
        });

        // Shift and Ctrl keep meaning what they mean over the cells - rows now, blocks of them.
        grid.SelectCell(3, 0, extend: true);

        Assert.That(grid.SelectedCells.Contains(2, 1), Is.True, "Shift stretches the block of ROWS from the anchor");
    }

    // A row taken over the CELLS is a row all the same: the strip reads the selection, it does not keep one of its own.
    [Test]
    public void SelectingEveryCellOfARow_LightsTheStripToo()
    {
        var grid = NumberedGrid();
        grid.SelectRows(2, 2);

        Assert.Multiple(() =>
        {
            Assert.That(NumberOf(grid, 2).IsSelected, Is.True);
            Assert.That(NumberOf(grid, 1).IsSelected, Is.False);
        });
    }

    // The strip is the head of the PINNED zone, so the columns start after it and a frozen column starts after it too -
    // otherwise the numbers and the first column stand in the same pixels.
    [Test]
    public void TheStrip_PushesTheColumnsAcross()
    {
        var grid = NumberedGrid();
        var withNumbers = grid.Columns[0].Offset;

        grid.ShowRowNumbers = false;
        Relayout(grid);

        Assert.Multiple(() =>
        {
            Assert.That(withNumbers, Is.EqualTo(grid.RowNumberWidth).Within(0.01).Or.GreaterThan(0),
                "the first column begins where the strip ends");
            Assert.That(grid.Columns[0].Offset, Is.EqualTo(0), "and takes the row back when the strip is off");
        });
    }

    // As wide as the widest number REALIZED, the way an Auto column is as wide as what it was given: five digits are not
    // clipped, and two digits do not reserve room for five. It only ever GROWS within a scroll - a strip recomputed
    // downwards as rows come and go makes the whole table breathe sideways under the pointer.
    [Test]
    public void TheStrip_GrowsToTheWidestNumberSeen_AndNeverShrinksBack()
    {
        var grid = NumberedGrid();
        var narrow = grid.RowNumberWidth;

        grid.ReportRowNumberWidth(narrow + 20);
        var grown = grid.RowNumberWidth;

        grid.ReportRowNumberWidth(narrow);

        Assert.Multiple(() =>
        {
            Assert.That(narrow, Is.GreaterThan(0), "the strip measures what it holds");
            Assert.That(grown, Is.EqualTo(narrow + 20), "a wider number widens it");
            Assert.That(grid.RowNumberWidth, Is.EqualTo(grown), "a narrower one does not take the width back");
        });
    }

    // ARRANGE MUST NOT MEASURE. The strip was asked for its width unbounded from ArrangeOverride and then measured
    // again at the settled width: two different available sizes, so neither call could ever hit the measure cache (it
    // compares the size the last measure ran with). Two full measures of a templated control per row per frame, each
    // invalidating its geometry - the table re-laid itself out forever standing still, 30 000 measures a second.
    [Test]
    public void ArrangingARow_MeasuresNothing()
    {
        var grid = NumberedGrid();
        var row = grid.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow;

        var before = MeasurableUIComponent.TotalMeasureCores;
        row.Arrange(new Rect(0, 0, 260, 28));

        Assert.That(MeasurableUIComponent.TotalMeasureCores - before, Is.Zero,
            "a width asked for during arrange is a width the next measure cannot cache");
    }

    // Turning the strip off resets its width, and the numbers it comes back to are the same numbers - so a strip that
    // only measured itself when the DIGITS changed never measured again, and its numbers were drawn over the first
    // column, trimmed to an ellipsis.
    [Test]
    public void TheStrip_TakesItsWidthBackWhenItIsTurnedOnAgain()
    {
        var grid = NumberedGrid();
        var width = grid.RowNumberWidth;

        grid.ShowRowNumbers = false;
        Relayout(grid);
        Assert.That(grid.Columns[0].Offset, Is.EqualTo(0), "off means the columns take the row back");

        grid.ShowRowNumbers = true;
        Relayout(grid);

        Assert.Multiple(() =>
        {
            Assert.That(grid.RowNumberWidth, Is.EqualTo(width), "and on gives the strip the same room it had");
            Assert.That(grid.Columns[0].Offset, Is.EqualTo(width), "so the first column starts after it, not under it");
        });
    }

    // The corner is the one gesture every spreadsheet has: it stands for no row and no column, so it takes both.
    [Test]
    public void TheCorner_TakesTheWholeTable()
    {
        var grid = NumberedGrid(3);
        var headers = Headers(grid);

        Press(headers.Corner);

        Assert.Multiple(() =>
        {
            Assert.That(grid.SelectedCells.Contains(0, 0), Is.True);
            Assert.That(grid.SelectedCells.Contains(2, 1), Is.True, "every row and every column");
            Assert.That(grid.SelectedCells.Ranges.Count, Is.EqualTo(1), "as ONE rectangle, not a cell each");
        });
    }

    // Nothing of the strip exists while the table shows no numbers - not a collapsed element holding width, and not a
    // corner over the first header.
    [Test]
    public void WithoutNumbers_ThereIsNoStripAtAll()
    {
        var grid = SelectableGrid(3);
        Relayout(grid);
        var headers = Headers(grid);

        Assert.Multiple(() =>
        {
            Assert.That(grid.RowNumberWidth, Is.EqualTo(0));
            Assert.That(NumberOf(grid, 0), Is.Null);
            Assert.That(headers.Corner, Is.Null);
        });
    }

    private sealed class CountingRow
    {
        public static int Reads;
        private readonly string _name;

        public CountingRow(string name) => _name = name;

        public string Name
        {
            get
            {
                Reads++;
                return _name;
            }
        }
    }

    // A sort must read each row's column ONCE, not twice per comparison. Reading through the binding is not cheap - it
    // re-points a live binding at the row - and n log n of them is what made sorting ten thousand rows stall visibly.
    [Test]
    public void Sorting_ReadsEachRowOnce_NotTwicePerComparison()
    {
        var grid = Grid(new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) });
        var rows = Enumerable.Range(1, 256).Select(i => new CountingRow($"Item {i:0000}")).ToList();
        grid.ItemsSource = rows;

        CountingRow.Reads = 0;
        grid.SortBy(grid.Columns[0], descending: true);

        Assert.Multiple(() =>
        {
            Assert.That(grid.Rows[0].Node, Is.SameAs(rows[^1]), "it really did sort");
            Assert.That(CountingRow.Reads, Is.EqualTo(rows.Count), "one reading of the column per row, and no more");
        });
    }

    // A sort reorders and nothing else, so it does not re-total - but the numbers must still be the ones the table
    // holds, and a filter after it must still move them.
    [Test]
    public void SortingKeepsTheTotals_AndAFilterStillMovesThem()
    {
        var grid = GroupableGrid(4);
        grid.Columns[1].Aggregate = DataGridAggregate.Sum;

        grid.SortBy(grid.Columns[1], descending: true);
        Assert.That(grid.TotalFor(grid.Columns[1]), Is.EqualTo(10.0), "the same rows, in another order");

        grid.SetFilter(item => ((Row)item).Size > 2);
        Assert.That(grid.TotalFor(grid.Columns[1]), Is.EqualTo(7.0), "and a filter is not a reorder");
    }

    // The grip is found where the header actually STANDS, not where the column's own numbers put it. A pinned header
    // does not move with the scroll, so working its place out by subtracting the offset pointed the pointer at whatever
    // column happened to be that far along the content - and the press there moved a column instead of resizing one.
    [Test]
    public void ThePinnedSeparator_StaysUnderThePointerWhileTheRestScroll()
    {
        var grid = Grid(
            new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) },
            new DataGridTextColumn { Binding = new Binding("Note"), Width = new GridLength(100) },
            new DataGridTextColumn { Binding = new Binding("Region"), Width = new GridLength(100) });
        grid.Columns[0].FrozenSide = DataGridFrozenSide.Left;
        grid.ItemsSource = Flat(3);
        Relayout(grid, width: 200);

        var headers = Headers(grid);
        Assert.That(headers.SeparatorAt(100), Is.EqualTo(0), "unscrolled, the pinned edge is where it is drawn");

        grid.HorizontalOffset = 40;
        headers.Measure(new Size(200, 26));
        headers.Arrange(new Rect(0, 0, 200, 26));

        Assert.Multiple(() =>
        {
            Assert.That(headers.SeparatorAt(100), Is.EqualTo(0), "and it has not moved: a pinned column never does");
            Assert.That(headers.SeparatorAt(160), Is.EqualTo(1), "while the columns that scroll came 40 closer");
        });
    }

    // A row builds its cells from the column WINDOW, and the window is moved by the GRID's measure. A row that was
    // measured before that ran holds the window from before the scroll, and nothing else ever tells it otherwise: on
    // the stand, scrolling sideways left the rows holding the columns that had gone off the left and none of the ones
    // that had come in from the right - and resizing any column put it right, which is what said they were stale.
    [Test]
    public void AColumnWindowThatMoved_TellsTheRowsItDid()
    {
        var columns = Enumerable.Range(0, 6)
            .Select(_ => (DataGridColumn)new DataGridTextColumn
            {
                Binding = new Binding("Name"), Width = new GridLength(100)
            }).ToArray();

        var grid = Grid(columns);
        grid.ItemsSource = Flat(3);
        Relayout(grid, width: 220);

        var row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(0);
        Assert.That(row.CellAt(5), Is.Null, "the far column is outside the window to begin with");

        // Measured CLEAN against the window as it stands, and only then does the scroll happen.
        grid.HorizontalOffset = 300;
        row.Measure(new Size(220, 28));
        Assert.That(((IMeasurableComponent)row).IsMeasureValid, Is.True);

        grid.Measure(new Size(220, 200));

        Assert.That(((IMeasurableComponent)row).IsMeasureValid, Is.False,
            "the grid's measure moved the window, so the rows that build from it are stale");
    }

    // A search counts what the TABLE holds, not what the screen happens to show: every shown column of every row
    // behind the current shape. The count is of cells, because that is what gets painted and stepped through.
    [Test]
    public void Searching_FindsEveryCellThatHoldsIt()
    {
        var grid = GroupableGrid(6);
        grid.SearchText = "north";

        grid.Search();

        Assert.Multiple(() =>
        {
            Assert.That(grid.MatchCount, Is.EqualTo(3), "three rows carry that region, one cell each");
            Assert.That(grid.CurrentMatch, Is.EqualTo(1), "and the first is the one being looked at");
        });
    }

    // A search looks at the VALUE, so a column that does not show its value as words must be left out of it: a check
    // box reads as "False", and searching for "al" lit up every unticked box in the table - pointing at text nobody
    // can see anywhere on screen.
    [Test]
    public void Searching_SkipsAColumnThatShowsNoWords()
    {
        var grid = Grid(
            new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) },
            new DataGridCheckBoxColumn { Binding = new Binding("Locked"), Width = new GridLength(60) });
        grid.ItemsSource = Flat(4);

        grid.SearchText = "al";
        grid.Search();

        Assert.That(grid.MatchCount, Is.EqualTo(0), "\"False\" holds \"al\", and that is not a match anyone can see");
    }

    private sealed class Shouting : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value?.ToString()?.ToUpperInvariant();

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => value;
    }

    // A search must find what the cell SHOWS. A column whose binding transforms its value cannot be read the cheap way
    // - the cheap way reads the property, and the property is not what is on screen - so it is read through the
    // binding however much that costs. Measured: a reading through a binding is some 3 µs against 0.3 µs without it,
    // which is exactly the temptation this guards against.
    [Test]
    public void Searching_ReadsAConvertedColumnThroughItsBinding()
    {
        var shouting = new DataGridTextColumn
        {
            Binding = new Binding("Name") { Converter = new Shouting() }, Width = new GridLength(100)
        };

        var grid = Grid(shouting);
        grid.ItemsSource = Flat(4);

        Assert.That(shouting.ReadsWithoutTheUI, Is.False, "a converter is exactly what the cheap reading cannot do");

        grid.SearchText = "ITEM 2";
        grid.Search();
        Assert.That(grid.MatchCount, Is.EqualTo(1), "what the cell shows is found");
    }

    // Case is not what a reader is searching for.
    [Test]
    public void Searching_IgnoresCase()
    {
        var grid = GroupableGrid(6);
        grid.SearchText = "NORTH";

        grid.Search();

        Assert.That(grid.MatchCount, Is.EqualTo(3));
    }

    // A column the table is GROUPED BY is not in the table, so it is not searched either - its value is in the
    // caption, and counting it would count cells nobody can see.
    [Test]
    public void Searching_SkipsAColumnThatLeftTheTable()
    {
        var grid = GroupableGrid(6);
        grid.SearchText = "north";
        grid.Search();
        Assert.That(grid.MatchCount, Is.EqualTo(3));

        grid.GroupBy(grid.Columns[0]);
        grid.Search();

        Assert.That(grid.MatchCount, Is.EqualTo(0), "the Region column is the grouping now");
    }

    // Next and previous WRAP: a Find Next that stops at the last match leaves the reader scrolling back by hand.
    [Test]
    public void SteppingThroughMatches_WrapsRound()
    {
        var grid = GroupableGrid(6);
        grid.SearchText = "north";
        grid.Search();

        grid.FindNext();
        grid.FindNext();
        Assert.That(grid.CurrentMatch, Is.EqualTo(3), "stepped to the last");

        grid.FindNext();
        Assert.That(grid.CurrentMatch, Is.EqualTo(1), "and round to the first");

        grid.FindPrevious();
        Assert.That(grid.CurrentMatch, Is.EqualTo(3), "and back round the other way");
    }

    // A match inside a SHUT group is still a match, and stepping onto it opens the group that holds it - a count that
    // covered only what happens to be unfolded would be a count of the screen, not of the table.
    [Test]
    public void SteppingOntoAMatchInsideAShutGroup_OpensIt()
    {
        var grid = GroupableGrid(6);

        // Grouped by REGION, searched in Name: grouping by the column being searched would hide it, and the search
        // would honestly find nothing.
        grid.GroupBy(grid.Columns[0]);
        Assert.That(grid.Rows.All(r => r.Node is DataGridGroup), Is.True, "everything is folded away");

        grid.SearchText = "Item 4";
        grid.Search();

        Assert.Multiple(() =>
        {
            Assert.That(grid.MatchCount, Is.EqualTo(1), "the row is found though nothing shows it");
            Assert.That(grid.Rows.Any(r => r.Node is Row { Name: "Item 4" }), Is.True, "and its group was opened");
        });
    }

    // The paint is held by ITEM, so a cell built after the search - scrolled into view, or realized when a group was
    // opened - is painted like the rest. Held by row number it would have moved the moment a group opened.
    [Test]
    public void AMatchedCell_PaintsItselfWhenItIsBuilt()
    {
        var grid = GroupableGrid(6);
        grid.SearchText = "north";
        grid.Search();
        Relayout(grid);

        var matched = Enumerable.Range(0, 6)
            .Select(i => grid.ItemContainerGenerator.ContainerFromIndex(i) as DataGridRow)
            .Select(r => r?.CellAt(0))
            .Where(c => c is { IsSearchMatch: true })
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(matched.Count, Is.EqualTo(3), "every matched cell on screen says so");
            Assert.That(matched.Count(c => c.IsCurrentSearchMatch), Is.EqualTo(1), "and exactly one is the current");
        });
    }

    // Taking the strip away calls the search off, for the same reason taking the grouping strip away ungroups: what it
    // painted over the table would otherwise stay with nothing left to explain or undo it.
    [Test]
    public void HidingTheSearchStrip_CallsTheSearchOff()
    {
        var grid = GroupableGrid(6);
        grid.ShowSearchPanel = true;
        grid.SearchText = "north";
        grid.Search();
        Assert.That(grid.MatchCount, Is.EqualTo(3));

        grid.ShowSearchPanel = false;

        Assert.Multiple(() =>
        {
            Assert.That(grid.MatchCount, Is.EqualTo(0));
            Assert.That(grid.CurrentMatch, Is.EqualTo(0));
        });
    }

    private sealed class PanelSwitch : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _shown;

        public bool Shown
        {
            get => _shown;
            set { _shown = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Shown))); }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }

    // The strip closes ITSELF, from its own button, so the switch that opened it has to hear about it. A page binds the
    // switch with a plain {Binding} and never says Mode - that resolves to what the PROPERTY declares, which is why the
    // property declares TwoWay. Left one-way, the strip went away and the checkbox stayed on.
    // And the close has to write the way PART_Close writes: SetCurrentValue, at the binding's own slot. The plain CLR
    // setter writes Local, which outranks Binding for good - the strip then closed once and the switch could never open
    // it again, because every later push landed in a slot the local write masks.
    [Test]
    public void TheSearchStripClosingItself_ReachesTheSourceItWasOpenedFrom()
    {
        var switcher = new PanelSwitch { Shown = true };
        var grid = GroupableGrid(6);
        grid.DataContext = switcher;
        grid.SetBinding(nameof(TreeDataGrid.ShowSearchPanel), new Binding(nameof(PanelSwitch.Shown)));
        Assert.That(grid.ShowSearchPanel, Is.True, "the binding pushes the source on connect");

        grid.SetCurrentValue(TreeDataGrid.ShowSearchPanelProperty, false);   // what PART_Close does

        Assert.That(switcher.Shown, Is.False, "the source learns the strip closed itself");

        switcher.Shown = true;          // and the switch has to be able to open it again
        BindingUpdateQueue.Flush();
        Assert.That(grid.ShowSearchPanel, Is.True, "nothing was left masking the binding");
    }

    private static void PressKey(TreeDataGrid grid, Key key) =>
        grid.RaiseEvent(new KeyEventArgs(KeyboardDevice.CurrentDevice, key, InputModifiers.None, 0)
        {
            RoutedEvent = Keyboard.KeyDownEvent
        });

    // Escape takes the strip away, from the table or from the strip's own field - the key travels up through every
    // element and the field never claims it, so the grid is the one place that has to answer for both.
    [Test]
    public void Escape_TakesTheSearchStripAway()
    {
        var grid = GroupableGrid(6);
        grid.ShowSearchPanel = true;

        PressKey(grid, Key.Escape);

        Assert.That(grid.ShowSearchPanel, Is.False);
    }

    // And it leaves the INNERMOST thing first: with a cell open, Escape belongs to the editor and the strip stays.
    [Test]
    public void Escape_LeavesTheEditorBeforeTheStrip()
    {
        var grid = SelectableGrid();
        grid.ShowSearchPanel = true;
        Assert.That(grid.BeginEdit(1, 0), Is.True, "a cell has to be open for the order to mean anything");

        PressKey(grid, Key.Escape);

        Assert.Multiple(() =>
        {
            Assert.That(grid.IsEditing, Is.False, "the editor is what Escape left");
            Assert.That(grid.ShowSearchPanel, Is.True, "and the strip is still there");
        });

        PressKey(grid, Key.Escape);
        Assert.That(grid.ShowSearchPanel, Is.False, "the next one takes the strip");
    }

    private static DataTemplate ADetailsPanel(double height = 120) =>
        new(() => new TemplateResult { RootComponent = new Border { Height = height } });

    // The panel is as tall as what is IN it, not as tall as the table guessed. The guess is only the first answer - a
    // panel is placed before it is built - and the stack is told the real one as soon as there is one, in BOTH
    // directions: a tab switched to a shorter one makes the panel shorter, and a height that only grew would leave a
    // band of nothing under it.
    [Test]
    public void APanelIsAsTallAsItsContent_AndTheStackIsToldWhenThatChanges()
    {
        var grid = SelectableGrid(4);
        grid.RowHeight = 24;
        grid.RowDetailsHeight = 120;   // the guess, until the panel has measured
        grid.RowDetailsTemplate = ADetailsPanel(260);
        var item = grid.Rows[1].Node;
        grid.ToggleRowDetails(item);

        Assert.That(grid.HeightOfRowDetails(item), Is.EqualTo(120), "the guess stands until the panel is built");

        Relayout(grid, 400, 600);

        Assert.Multiple(() =>
        {
            Assert.That(grid.HeightOfRowDetails(item), Is.EqualTo(260).Within(1), "and the content's own height replaces it");
            Assert.That(grid.RowExtentExceptions[0].Extra, Is.EqualTo(236).Within(1), "the stack is told the same number");
        });

        // What a tab switch does: the same panel, now shorter.
        grid.ReportRowDetailsHeight(item, 80);

        Assert.Multiple(() =>
        {
            Assert.That(grid.HeightOfRowDetails(item), Is.EqualTo(80), "shorter is taken as readily as taller");
            Assert.That(grid.RowExtentExceptions[0].Extra, Is.EqualTo(56).Within(1));
        });
    }

    // A table with panels open has to go QUIET when nobody is touching it. It did not: the panel row built a part on
    // every measure that it dropped again a few lines later, and a child added and removed invalidates the row that is
    // measuring - so the row never went valid and the whole realized set re-measured on every pass. Asserted on whether
    // the layout SETTLES rather than on a measure count, because a count only says "a lot", and a lot is also what a
    // table someone is actually using looks like.
    [Test]
    public void APanelOpen_LetsTheTableGoQuiet()
    {
        var grid = SelectableGrid(40);
        grid.RowHeight = 24;
        grid.RowDetailsTemplate = ADetailsPanel(140);

        var first = grid.Rows[1].Node;
        var second = grid.Rows[5].Node;
        grid.ToggleRowDetails(first);
        grid.ToggleRowDetails(second);

        var manager = LayoutManager.For(grid);
        for (var i = 0; i < 12; i++)
        {
            Relayout(grid, 400, 600);
            manager.ExecuteLayoutPass();
        }

        var rows = Enumerable.Range(0, 40)
            .Select(i => grid.ItemContainerGenerator.ContainerFromIndex(i) as DataGridRow)
            .Where(row => row is { IsRowDetails: true })
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(rows, Is.Not.Empty, "no panel was realized, so this proves nothing");
            Assert.That(rows.All(row => row.IsMeasureValid), Is.True, "a panel row came out of the pass still dirty");
            Assert.That(manager.IsSettled, Is.True, "the table keeps measuring itself with nobody touching it");
        });
    }

    // The panel is a ROW of the same flat list, spliced right after the record it belongs to - not a second list and
    // not a child of the row. That is what makes it cost nothing new: the virtualizer realizes it as it realizes any
    // row, and ten thousand records with one panel open build one panel.
    [Test]
    public void ARecordsPanel_IsARowOfItsOwn_RightAfterTheRecord()
    {
        var grid = SelectableGrid(4);
        grid.RowDetailsTemplate = ADetailsPanel();
        var item = grid.Rows[1].Node;

        grid.ToggleRowDetails(item);

        Assert.Multiple(() =>
        {
            Assert.That(grid.Rows.Count, Is.EqualTo(5), "one row more, not one list more");
            Assert.That(grid.Rows[2].IsDetails, Is.True, "and it stands right after the record it belongs to");
            Assert.That(((DataGridRowDetails)grid.Rows[2].Node).Item, Is.SameAs(item));
            Assert.That(grid.Rows[3].Node, Is.SameAs(grid.Rows[3].Node), "the records after it are still records");
            Assert.That(grid.Rows[3].IsDetails, Is.False);
        });

        grid.ToggleRowDetails(item);
        Assert.That(grid.Rows.Count, Is.EqualTo(4), "and shutting it takes the row away again");
    }

    // A panel takes no ORDINAL. It is the record above it said at length, so a number spent on one makes the record
    // after it read as though a row had gone missing - on the stand, opening the first record's panel renumbered the
    // second one 3.
    [Test]
    public void APanel_TakesNoNumberOfItsOwn()
    {
        var grid = SelectableGrid(4);
        grid.ShowRowNumbers = true;
        grid.RowDetailsTemplate = ADetailsPanel();
        grid.ToggleRowDetails(grid.Rows[0].Node);
        Relayout(grid);

        var numbers = grid.ItemContainerGenerator.RealizedIndices
            .Select(i => grid.ItemContainerGenerator.ContainerFromIndex(i) as DataGridRow)
            .Where(r => r is { IsRowDetails: false })
            .Select(r => r.Number)
            .OrderBy(n => n)
            .ToArray();

        Assert.That(numbers, Is.EqualTo(new[] { 1, 2, 3, 4 }), "the records count 1..4 with a panel standing among them");
    }

    // A record's panel and a record's BRANCH are two different questions: a row can show its long form with its
    // children shut, and open its children with the panel shut. The panel stands at the owner's depth, so every walk
    // that reads the tree's shape by depth has to know it is not part of that shape - the collapse below took nothing
    // away at all while it did not.
    [Test]
    public void APanelAndABranch_AreOpenedAndShutIndependently()
    {
        var (grid, root) = Tree(new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) });
        grid.RowDetailsTemplate = ADetailsPanel();

        grid.ToggleRowDetails(root);
        grid.Expand(root);

        Assert.Multiple(() =>
        {
            Assert.That(grid.Rows[1].IsDetails, Is.True, "the panel stays put when the branch opens");
            Assert.That(grid.Rows.Count, Is.EqualTo(1 + 1 + 2), "the root, its panel, its two children");
        });

        grid.Collapse(root);

        Assert.Multiple(() =>
        {
            Assert.That(grid.Rows.Count, Is.EqualTo(1 + 1), "the children went and the panel did not");
            Assert.That(grid.Rows[1].IsDetails, Is.True);
        });
    }

    // The rows are virtualized against a UNIFORM pitch, so a row that stands taller has to be named as an exception to
    // it - otherwise the scrollbar measures a table that is not there and every row below the panel is drawn where the
    // hit-test is not.
    [Test]
    public void APanelIsNamedToTheStack_AsAnExceptionToTheUniformPitch()
    {
        var grid = SelectableGrid(4);
        grid.RowHeight = 24;
        grid.RowDetailsHeight = 120;
        grid.RowDetailsTemplate = ADetailsPanel();

        Assert.That(grid.RowExtentExceptions, Is.Empty, "nothing is out of the ordinary while nothing is open");

        grid.ToggleRowDetails(grid.Rows[1].Node);

        Assert.Multiple(() =>
        {
            Assert.That(grid.RowExtentExceptions.Count, Is.EqualTo(1));
            Assert.That(grid.RowExtentExceptions[0].Index, Is.EqualTo(2), "at the panel's place in the flat list");
            Assert.That(grid.RowExtentExceptions[0].Extra, Is.EqualTo(96), "and by what it stands taller than a row");
        });
    }

    // The group whose caption says this - what a test that is about a group's CONTENT should ask for. Its place among
    // the captions is a different question, with its own tests.
    private static DataGridGroup GroupNamed(TreeDataGrid grid, string key) =>
        grid.Rows.Select(r => r.Node).OfType<DataGridGroup>()
            .First(g => string.Equals(g.Key?.ToString(), key, StringComparison.Ordinal));

    private static TreeDataGrid GroupableGrid(int rows = 6)
    {
        var grid = Grid(
            new DataGridTextColumn { Binding = new Binding("Region"), Width = new GridLength(100) },
            new DataGridTextColumn { Binding = new Binding("Size"), Width = new GridLength(100) },
            new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) });
        grid.ItemsSource = Flat(rows);
        return grid;
    }

    // A group is a NODE with children, so everything the tree already does applies to it: it is one row, the rows under
    // it are its children, and the flattener splices them the way it splices a branch. Nothing about grouping needed a
    // second mechanism.
    [Test]
    public void Grouping_PutsAHeaderOverEachSetOfRowsAndKeepsThemUnderIt()
    {
        var grid = GroupableGrid();

        grid.GroupBy(grid.Columns[0]);
        grid.ExpandRow(grid.Rows[0]);

        Assert.Multiple(() =>
        {
            Assert.That(grid.Rows[0].Node, Is.InstanceOf<DataGridGroup>(), "a group leads its rows");
            Assert.That(((DataGridGroup)grid.Rows[0].Node).Count, Is.EqualTo(3), "and knows how many it has");
            Assert.That(grid.Rows[1].Node, Is.InstanceOf<Row>(), "opened, its rows are spliced in under it");
            Assert.That(grid.Rows[1].Depth, Is.EqualTo(1), "one level in, as a branch's children are");
        });
    }

    // A table is grouped in order to FOLD it, so grouping shows the CAPTIONS and nothing else until one is opened.
    // Groups that came up open were the table back again with captions in it: closing one only uncovered the next open
    // one, and over ten thousand rows there were hundreds of them.
    [Test]
    public void Grouping_ShowsTheCaptionsAndNothingUnderThem()
    {
        var grid = GroupableGrid(6);

        grid.GroupBy(grid.Columns[0]);

        Assert.Multiple(() =>
        {
            Assert.That(grid.Rows.Count, Is.EqualTo(2), "two groups over six rows, and six rows folded away");
            Assert.That(grid.Rows.All(r => r.Node is DataGridGroup), Is.True);
            Assert.That(grid.Rows.All(r => !r.IsExpanded), Is.True);
        });

        grid.ExpandRow(grid.Rows[0]);
        Assert.That(grid.Rows.Count, Is.EqualTo(5), "opening one brings back its three");
    }

    // Closing a group takes its rows off the table, exactly as closing a branch does - and the same ToggleRow does it,
    // whether the press came from the caption, the number or the expander.
    [Test]
    public void ClosingAGroup_TakesItsRowsAway()
    {
        var grid = GroupableGrid(4);
        grid.GroupBy(grid.Columns[0]);
        grid.ExpandRow(grid.Rows[0]);
        Assert.That(grid.Rows.Count, Is.EqualTo(4));

        grid.ToggleRow(grid.Rows[0]);

        Assert.Multiple(() =>
        {
            Assert.That(grid.Rows.Count, Is.EqualTo(2), "one group shut takes its two rows with it");
            Assert.That(grid.Rows[0].Node, Is.InstanceOf<DataGridGroup>());
            Assert.That(grid.Rows[1].Node, Is.InstanceOf<DataGridGroup>(), "the other group is now next");
        });
    }

    // Grouping by a second column nests: the outer group's children are groups of their own, and only the innermost
    // hold rows. The order of GroupDescriptions is the order of the nesting, and each level opens on its own.
    [Test]
    public void GroupingByTwoColumns_Nests()
    {
        var grid = GroupableGrid(4);

        grid.GroupBy(grid.Columns[0]);
        grid.GroupBy(grid.Columns[2]);
        grid.ExpandRow(grid.Rows[0]);

        var outer = (DataGridGroup)grid.Rows[0].Node;

        Assert.Multiple(() =>
        {
            Assert.That(outer.Level, Is.EqualTo(0));
            Assert.That(grid.Rows[1].Node, Is.InstanceOf<DataGridGroup>(), "the outer group holds groups");
            Assert.That(((DataGridGroup)grid.Rows[1].Node).Level, Is.EqualTo(1));
            Assert.That(grid.Rows[1].IsExpanded, Is.False, "each level opens on its own, one press at a time");
        });

        grid.ExpandRow(grid.Rows[1]);
        Assert.That(grid.Rows[2].Node, Is.InstanceOf<Row>(), "and the inner one holds the rows");
    }

    // A group the user opened comes back open after the table is re-shaped. Every group is built afresh by a sort or a
    // filter, so remembering the OBJECTS forgot the user's work every time: open a group, sort a column, and it shut.
    [Test]
    public void AnOpenedGroup_IsStillOpenAfterASort()
    {
        var grid = GroupableGrid(6);
        grid.GroupBy(grid.Columns[0]);
        var opened = (DataGridGroup)grid.Rows[0].Node;
        grid.ExpandRow(grid.Rows[0]);
        var open = grid.Rows.Count;

        grid.SortBy(grid.Columns[1], descending: true);

        // BY KEY, not by place: what order the groups themselves come in is a separate question (plan item 2a), and
        // this one is only about whether the group the user opened is still open.
        var again = grid.Rows.First(r => r.Node is DataGridGroup group && Equals(group.Key, opened.Key));

        Assert.Multiple(() =>
        {
            Assert.That(again.IsExpanded, Is.True, "the group the user opened is open again");
            Assert.That(grid.Rows.Count, Is.EqualTo(open), "and only that one");
        });
    }

    // ...and a group the user SHUT stays shut, or the memory would only ever grow one way.
    [Test]
    public void AClosedGroup_IsStillClosedAfterASort()
    {
        var grid = GroupableGrid(6);
        grid.GroupBy(grid.Columns[0]);
        grid.ExpandRow(grid.Rows[0]);
        grid.CollapseRow(grid.Rows[0]);

        grid.SortBy(grid.Columns[1], descending: true);

        Assert.That(grid.Rows.Count, Is.EqualTo(2), "two captions and nothing under them");
    }

    // Two columns deep there are hundreds of captions, so folding the table is a command of its own - and it takes a
    // DEPTH, which is what "leave only the first level open" means.
    [Test]
    public void FoldingTheWholeTable_TakesADepth()
    {
        var grid = GroupableGrid(4);
        grid.GroupBy(grid.Columns[0]);
        grid.GroupBy(grid.Columns[2]);

        grid.ExpandAllGroups();
        var all = grid.Rows.Count;

        grid.ExpandGroupsTo(1);
        var outerOnly = grid.Rows.Count;

        grid.CollapseAllGroups();

        Assert.Multiple(() =>
        {
            Assert.That(all, Is.EqualTo(10), "two groups, four inner groups, four rows");
            Assert.That(outerOnly, Is.EqualTo(6), "the outer two open, the inner four shut");
            Assert.That(grid.Rows.Count, Is.EqualTo(2), "and shut, only the outermost captions are left");
        });
    }

    // Grouping by a column that is already grouped by takes it back OUT - one call, so the chip in the panel and the
    // header dropped into it are the same gesture in both directions.
    [Test]
    public void GroupingByTheSameColumnTwice_Ungroups()
    {
        var grid = GroupableGrid(4);

        grid.GroupBy(grid.Columns[0]);
        grid.GroupBy(grid.Columns[0]);

        Assert.Multiple(() =>
        {
            Assert.That(grid.GroupDescriptions.Count, Is.EqualTo(0));
            Assert.That(grid.Rows.Count, Is.EqualTo(4), "and the rows are a flat table again");
            Assert.That(grid.Rows[0].Node, Is.InstanceOf<Row>());
        });
    }

    // The order of the chips IS the nesting, so carrying one along the strip is how a column changes how deep it
    // groups - and the rows have to come out re-nested, not merely re-labelled.
    [Test]
    public void CarryingAChip_ChangesHowDeepItsColumnGroups()
    {
        var grid = GroupableGrid(4);
        var outer = grid.Columns[0];
        var inner = grid.Columns[2];
        grid.GroupBy(outer);
        grid.GroupBy(inner);

        grid.MoveGrouping(1, 0);

        grid.ExpandRow(grid.Rows[0]);
        var top = (DataGridGroup)grid.Rows[0].Node;
        var under = (DataGridGroup)grid.Rows[1].Node;

        Assert.Multiple(() =>
        {
            Assert.That(grid.GroupDescriptions[0], Is.SameAs(inner), "the carried column is outermost now");
            Assert.That(top.Column, Is.SameAs(inner), "and the groups really are built that way round");
            Assert.That(under.Column, Is.SameAs(outer));
        });
    }

    // The × is drawn as the way out of the grouping, so it has to be the only thing that takes a column out. A chip
    // that ungrouped wherever it was pressed made its own × decoration, and surprised anyone who meant to move it.
    [Test]
    public void PressingAChip_UngroupsOnlyThroughItsCross()
    {
        var grid = GroupableGrid(4);
        grid.GroupBy(grid.Columns[0]);
        grid.GroupBy(grid.Columns[2]);

        var panel = GroupPanel(grid);
        var chip = ChipsOf(panel)[0];
        var cross = CrossOf(chip);

        Assert.Multiple(() =>
        {
            Assert.That(cross, Is.Not.Null, "the chip has a × of its own");
            Assert.That(chip.PressedRemove(cross), Is.True, "a press that landed on it takes the column out");
            Assert.That(chip.PressedRemove(chip), Is.False, "a press anywhere else on the chip takes nothing out");
        });
    }

    private static IUIComponent CrossOf(DataGridGroupChip chip)
    {
        var pending = new Stack<IUIComponent>();
        pending.Push(chip);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node is Adamantium.UI.Controls.Base.UIComponent { Name: "PART_Remove" }) return node;
            foreach (var child in node.VisualChildren) pending.Push(child);
        }

        return null;
    }

    // Carrying a chip and pressing one are the SAME press: the strip owns both, so a chip cannot ungroup itself on the
    // way to another place in the order.
    [Test]
    public void AChipCarriedAcross_DoesNotAlsoUngroupItself()
    {
        var grid = GroupableGrid(4);
        grid.GroupBy(grid.Columns[0]);
        grid.GroupBy(grid.Columns[2]);

        var panel = GroupPanel(grid);
        Assert.That(panel.DropTargetAt(0), Is.EqualTo(0), "dropped at the left edge, it lands first");

        panel.BeginCarry(1, 200);
        panel.EndCarry(0);

        Assert.Multiple(() =>
        {
            Assert.That(grid.GroupDescriptions.Count, Is.EqualTo(2), "both columns are still grouped by");
            Assert.That(grid.GroupDescriptions[0], Is.SameAs(grid.Columns[2]), "and the carried one moved");
        });
    }

    private static DataGridGroupPanel GroupPanel(TreeDataGrid grid)
    {
        var panel = new DataGridGroupPanel { Owner = grid };
        panel.Template = new ControlTemplate(() =>
        {
            var root = new Adamantium.UI.Controls.Panels.Grid();
            var chips = new StackPanel { Orientation = Orientation.Horizontal };
            var mark = new Border { Width = 2, Visibility = Visibility.Collapsed };
            root.Children.Add(chips);
            root.Children.Add(mark);

            var result = new TemplateResult { RootComponent = root };
            result.RegisterName("PART_Chips", chips);
            result.RegisterName("PART_DropMark", mark);
            return result;
        });

        // The first pass applies the template, and only then does the strip have a chips panel to build into. A chip
        // has no size of its own without a theme, so it is given one and laid out again: the gesture is answered
        // against WHERE the chips are.
        ((IMeasurableComponent)panel).InvalidateMeasure();
        panel.Measure(new Size(400, 30));
        panel.Arrange(new Rect(0, 0, 400, 30));

        foreach (var chip in ChipsOf(panel))
        {
            chip.Width = 100;
            chip.Height = 20;
            chip.Template = ChipTemplate();
            for (IUIComponent node = chip; node != null; node = node.VisualParent)
                (node as IMeasurableComponent)?.InvalidateMeasure();
        }

        panel.Measure(new Size(400, 30));
        panel.Arrange(new Rect(0, 0, 400, 30));
        return panel;
    }

    // A chip has no template without a theme, and the rule under test is about one of its PARTS.
    private static ControlTemplate ChipTemplate() => new(() =>
    {
        var root = new Adamantium.UI.Controls.Panels.Grid();
        var cross = new Border { Name = "PART_Remove", Width = 10 };
        root.Children.Add(cross);

        var result = new TemplateResult { RootComponent = root };
        result.RegisterName("PART_Remove", cross);
        return result;
    });

    private static List<DataGridGroupChip> ChipsOf(IUIComponent root)
    {
        var found = new List<DataGridGroupChip>();
        var pending = new Stack<IUIComponent>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node is DataGridGroupChip chip) found.Add(chip);
            foreach (var child in node.VisualChildren) pending.Push(child);
        }

        return found;
    }

    // A header carried into the grouping strip GROUPS instead of moving: the same drag has two endings, and which one
    // it gets is decided by where it was let go.
    [Test]
    public void AHeaderDroppedIntoTheGroupingStrip_GroupsInsteadOfMoving()
    {
        var grid = GroupableGrid(4);
        var headers = Headers(grid);
        var order = grid.Columns[0];

        headers.BeginReorder(0, 10);
        headers.EndReorder(250, intoGroupPanel: true);

        Assert.Multiple(() =>
        {
            Assert.That(grid.GroupDescriptions.Count, Is.EqualTo(1), "it grouped");
            Assert.That(grid.GroupDescriptions[0], Is.SameAs(order));
            Assert.That(grid.Columns[0], Is.SameAs(order), "and it did NOT also move");
        });
    }

    // A total counts the rows of its own level: shut or open changes nothing, and a BRANCH's children are counted by
    // that branch, not by the table. Counting them here made the table's total disagree with the sum of its groups' -
    // on the stand, 58000 against five groups of 2000.
    [Test]
    public void ATotal_CountsItsOwnLevel_WhateverIsOpen()
    {
        var grid = GroupableGrid(4);
        var parent = (Row)grid.Rows[0].Node;
        parent.Children.Add(new Row { Name = "Child", Region = "north", Size = 100 });
        grid.ChildrenPath = "Children";
        grid.Refresh();

        grid.Columns[1].Aggregate = DataGridAggregate.Sum;

        Assert.Multiple(() =>
        {
            Assert.That(grid.HasTotals, Is.True, "the footer band exists exactly while a column asks for a total");
            Assert.That(grid.TotalFor(grid.Columns[1]), Is.EqualTo(10.0), "1+2+3+4, and NOT the branch's own 100");
            Assert.That(grid.TotalFor(grid.Columns[0]), Is.Null, "a column that asks for none has none");
        });

        grid.ExpandRow(grid.Rows[0]);
        Assert.That(grid.TotalFor(grid.Columns[1]), Is.EqualTo(10.0), "and opening the branch does not change it");
    }

    // What the table totals is what its groups total between them - one population, counted once, however it is cut up.
    [Test]
    public void TheTableTotal_IsWhatItsGroupsTotalBetweenThem()
    {
        var grid = GroupableGrid(6);
        grid.Columns[1].Aggregate = DataGridAggregate.Count;
        grid.GroupBy(grid.Columns[0]);

        var groups = grid.Rows.Select(r => r.Node).OfType<DataGridGroup>().ToList();
        var counted = groups.Sum(g => (int)grid.TotalFor(grid.Columns[1], g));

        Assert.That(counted, Is.EqualTo(grid.TotalFor(grid.Columns[1])), "the parts add up to the whole");
    }

    // A filtered-out row is not part of the total: the shape is what the table HOLDS, and a filter changes it.
    [Test]
    public void ATotal_LeavesOutWhatTheFilterRemoved()
    {
        var grid = GroupableGrid(4);
        grid.Columns[1].Aggregate = DataGridAggregate.Sum;
        Assert.That(grid.TotalFor(grid.Columns[1]), Is.EqualTo(10.0));

        grid.SetFilter(item => ((Row)item).Size > 2);

        Assert.That(grid.TotalFor(grid.Columns[1]), Is.EqualTo(7.0), "3+4, the two rows the filter left");
    }

    // Every aggregate over one column, so each one is pinned to a number rather than to "it computed something".
    [Test]
    public void TheAggregates_EachAnswerForTheirOwnQuestion()
    {
        var grid = GroupableGrid(4);
        var column = grid.Columns[1];

        column.Aggregate = DataGridAggregate.Count;
        Assert.That(grid.TotalFor(column), Is.EqualTo(4));

        column.Aggregate = DataGridAggregate.Min;
        Assert.That(grid.TotalFor(column), Is.EqualTo(1.0));

        column.Aggregate = DataGridAggregate.Max;
        Assert.That(grid.TotalFor(column), Is.EqualTo(4.0));

        column.Aggregate = DataGridAggregate.Average;
        Assert.That(grid.TotalFor(column), Is.EqualTo(2.5));
    }

    // A group's total counts ITS rows only - that is the whole point of a total in a group's header.
    [Test]
    public void AGroupsTotal_CountsOnlyItsOwnRows()
    {
        var grid = GroupableGrid(4);
        grid.Columns[1].Aggregate = DataGridAggregate.Sum;
        grid.GroupBy(grid.Columns[0]);

        // BY ITS KEY, not by its place. This test is about what a total counts; taking the group at row 0 tied it to
        // the order groups happen to be built in, and it broke the day that order became the KEY's.
        var south = GroupNamed(grid, "south");

        Assert.Multiple(() =>
        {
            Assert.That(grid.TotalFor(grid.Columns[1], south), Is.EqualTo(4.0), "rows 1 and 3");
            Assert.That(grid.TotalFor(grid.Columns[1]), Is.EqualTo(10.0), "while the table's own total is all four");
        });
    }

    // A total is worked out ONCE per shape and remembered: a walk of the data per frame is what a footer must never
    // cost. Changing the shape is what forgets it.
    [Test]
    public void AGroupsTotal_IsWorkedOutOnceAndForgottenWhenTheShapeChanges()
    {
        var grid = GroupableGrid(4);
        grid.Columns[1].Aggregate = DataGridAggregate.Sum;
        grid.GroupBy(grid.Columns[0]);

        var group = GroupNamed(grid, "south");
        var first = grid.TotalFor(grid.Columns[1], group);

        Assert.That(grid.TotalFor(grid.Columns[1], group), Is.SameAs(first), "asked twice, worked out once");

        grid.SetFilter(item => ((Row)item).Size > 2);

        var regrouped = GroupNamed(grid, "south");
        Assert.That(grid.TotalFor(grid.Columns[1], regrouped), Is.EqualTo(3.0), "and the new shape has its own");
    }

    // The strip of totals is placed by the SAME numbers the rows and the header are - the grid's one width pass - so a
    // total can never stand under another column.
    [Test]
    public void TheFooterStrip_StandsOnTheColumnsItTotals()
    {
        var grid = GroupableGrid(4);
        grid.Columns[1].Aggregate = DataGridAggregate.Sum;
        Relayout(grid);

        var footer = new DataGridFooterPresenter { Owner = grid };
        footer.Measure(new Size(400, 24));
        footer.Arrange(new Rect(0, 0, 400, 24));

        var cells = footer.VisualChildren.OfType<DataGridFooterCell>().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(cells.Count, Is.EqualTo(3), "one place per column, total or not");
            Assert.That(cells[1].Bounds.X, Is.EqualTo(grid.Columns[1].Offset), "and each stands on its own column");
            Assert.That(cells[1].Content, Is.EqualTo("10"), "with the number written out");
            Assert.That(cells[0].Content, Is.Null, "a column with no total leaves its place blank");
        });
    }

    // A column being resized has to carry the strip of totals with it. The strip measures its cells at the columns'
    // widths, but a measure that is not invalidated never runs: widening a column left its total trimmed to an ellipsis
    // in the width it used to have.
    [Test]
    public void TheFooterStrip_FollowsAColumnBeingResized()
    {
        var grid = GroupableGrid(4);
        grid.Columns[1].Aggregate = DataGridAggregate.Sum;
        Relayout(grid);

        var footer = new DataGridFooterPresenter { Owner = grid };
        footer.Measure(new Size(400, 24));
        footer.Arrange(new Rect(0, 0, 400, 24));

        var cell = footer.VisualChildren.OfType<DataGridFooterCell>().ElementAt(1);
        Assert.That(cell.Bounds.Width, Is.EqualTo(100), "it starts at its column's width");

        grid.Columns[1].Width = new GridLength(180);
        grid.InvalidateColumns();
        Relayout(grid);

        footer.Measure(new Size(400, 24));
        footer.Arrange(new Rect(0, 0, 400, 24));

        Assert.That(cell.Bounds.Width, Is.EqualTo(180), "and follows the column that was widened");
    }

    // The strip of totals sits OUTSIDE the rows' scroller and carries the sideways offset itself, so it has to be told
    // when that offset moves. Told only the header band, the totals stayed where they were last measured and stood
    // under the wrong columns the moment the table was scrolled.
    [Test]
    public void TheFooterStrip_FollowsTheColumnsSideways()
    {
        var grid = GroupableGrid(4);
        grid.Columns[1].Aggregate = DataGridAggregate.Sum;
        Relayout(grid, width: 200);

        var footer = new DataGridFooterPresenter { Owner = grid };
        footer.Measure(new Size(200, 24));
        footer.Arrange(new Rect(0, 0, 200, 24));

        var cell = footer.VisualChildren.OfType<DataGridFooterCell>().ElementAt(1);
        Assert.That(cell.Bounds.X, Is.EqualTo(100), "unscrolled, it stands on its column");

        grid.HorizontalOffset = 60;

        // TOLD, before anything measures it again: nothing else in a scroll invalidates this strip, and a strip that
        // is never invalidated is never re-placed - that is the whole defect, not the arithmetic below.
        Assert.That(((IMeasurableComponent)footer).IsMeasureValid, Is.False, "the strip was told the columns moved");

        footer.Measure(new Size(200, 24));
        footer.Arrange(new Rect(0, 0, 200, 24));

        Assert.That(cell.Bounds.X, Is.EqualTo(40), "and it moves with them");
    }

    // The format belongs to the COLUMN, so a sum is written the way that column's values are.
    [Test]
    public void ATotal_IsWrittenTheWayItsColumnAsksFor()
    {
        var grid = GroupableGrid(4);
        var column = grid.Columns[1];
        column.Aggregate = DataGridAggregate.Sum;
        column.AggregateFormat = "Σ {0:N0}";

        var footer = new DataGridFooterPresenter { Owner = grid };
        footer.Measure(new Size(400, 24));

        var cell = footer.VisualChildren.OfType<DataGridFooterCell>().ElementAt(1);
        Assert.That(cell.Content, Is.EqualTo("Σ 10"));
    }

    // The caption and a total drawn in the same place are two things and one of them is unreadable - measured on the
    // stand, "Region: Iberia (2000)" with the Code column's count of 2000 written over its first word. The caption
    // gives way: it starts after a total standing where it would begin, and stops where the next one starts.
    [Test]
    public void TheGroupsCaption_GivesWayToTheTotalsOnItsRow()
    {
        var grid = Grid(
            new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(100) },
            new DataGridTextColumn { Binding = new Binding("Note"), Width = new GridLength(100) },
            new DataGridTextColumn { Binding = new Binding("Size"), Width = new GridLength(100) },
            new DataGridTextColumn { Binding = new Binding("Region"), Width = new GridLength(100) });
        grid.ItemsSource = Flat(4);
        grid.Columns[0].Aggregate = DataGridAggregate.Count;
        grid.Columns[2].Aggregate = DataGridAggregate.Count;

        // Grouped by the LAST column, so the two totals keep a column between them for the caption to stand in - the
        // column the table is grouped by leaves the table entirely.
        grid.GroupBy(grid.Columns[3]);
        Relayout(grid, width: 400);

        var caption = (grid.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow)?.GroupCaption;

        Assert.Multiple(() =>
        {
            Assert.That(caption, Is.Not.Null);
            Assert.That(caption.Bounds.X, Is.EqualTo(100), "it begins after the total that stood where it would");
            Assert.That(caption.Bounds.Width, Is.EqualTo(100), "and stops where the next total begins");
        });
    }

    // The zebra counts the rows of the DATA. A caption stands in the same flat list, so it used to take whichever
    // stripe its place happened to fall on and the group headers came out half light, half dark - a table that looks
    // like it made a mistake. It takes no stripe at all now, and its own colour from the theme.
    [Test]
    public void AGroupRow_TakesNoStripeOfTheZebra()
    {
        var grid = GroupableGrid(4);
        grid.AlternationCount = 2;
        grid.GroupBy(grid.Columns[0]);
        grid.ExpandRow(grid.Rows[0]);
        grid.ExpandRow(grid.Rows[3]);
        Relayout(grid);

        var rows = Enumerable.Range(0, 6)
            .Select(i => grid.ItemContainerGenerator.ContainerFromIndex(i) as DataGridRow).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(rows[0].IsGroup, Is.True, "the first row stands for a group");
            Assert.That(rows[3].IsGroup, Is.True, "and so does the one that starts the next");
            Assert.That(rows[0].AlternationIndex, Is.EqualTo(0), "a caption takes no stripe");
            Assert.That(rows[3].AlternationIndex, Is.EqualTo(0), "whatever place it fell on");
            Assert.That(rows[1].AlternationIndex, Is.Not.EqualTo(rows[2].AlternationIndex),
                "while the rows under it still alternate");
        });
    }

    // A part that comes and goes has to COME AND GO. A group's totals were hidden instead of removed when the container
    // went back to standing for a record, and the drawn set went on holding children that had left it: after scrolling
    // away and back, twenty-seven rows of twenty-nine were painted blank on the stand.
    [Test]
    public void AGroupsTotals_LeaveTheRowWhenItStopsStandingForAGroup()
    {
        var grid = GroupableGrid(4);
        grid.Columns[1].Aggregate = DataGridAggregate.Sum;
        grid.GroupBy(grid.Columns[0]);
        grid.ExpandRow(grid.Rows[0]);
        Relayout(grid);

        var groupRow = grid.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow;
        Assert.That(TotalsIn(groupRow), Is.EqualTo(1), "the group row carries the one column's total");

        grid.ClearGrouping();
        Relayout(grid);

        var dataRow = grid.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow;

        Assert.Multiple(() =>
        {
            Assert.That(dataRow?.Group, Is.Null, "the same container stands for a record now");
            Assert.That(TotalsIn(dataRow), Is.EqualTo(0), "and no total is left inside it, hidden or otherwise");
        });
    }

    private static int TotalsIn(DataGridRow row) =>
        row == null ? -1 : ((IUIComponent)row).VisualChildren.OfType<DataGridFooterCell>().Count();

    // The strip is where a grouping is shown and where it is undone, so taking it away ungroups. Hidden over a grouped
    // table, it left the table standing in groups with nothing saying why and no way back.
    [Test]
    public void HidingTheGroupingStrip_Ungroups()
    {
        var grid = GroupableGrid(4);
        grid.ShowGroupPanel = true;
        grid.GroupBy(grid.Columns[0]);
        Assert.That(grid.Rows[0].Node, Is.InstanceOf<DataGridGroup>());

        grid.ShowGroupPanel = false;

        Assert.Multiple(() =>
        {
            Assert.That(grid.GroupDescriptions.Count, Is.EqualTo(0));
            Assert.That(grid.Rows.Count, Is.EqualTo(4), "the table is flat again");
            Assert.That(grid.Rows[0].Node, Is.InstanceOf<Row>());
        });
    }

    // ...but a page that never shows the strip and groups from code is left alone: nothing ever turns off.
    [Test]
    public void GroupingWithoutTheStrip_IsLeftAlone()
    {
        var grid = GroupableGrid(4);

        grid.GroupBy(grid.Columns[0]);

        Assert.Multiple(() =>
        {
            Assert.That(grid.ShowGroupPanel, Is.False, "no strip was ever asked for");
            Assert.That(grid.GroupDescriptions.Count, Is.EqualTo(1), "and the grouping stands");
            Assert.That(grid.Rows[0].Node, Is.InstanceOf<DataGridGroup>());
        });
    }

    // A column the table is GROUPED BY leaves the table: its value is the same on every row of a group and already
    // stands in that group's caption, so keeping it repeated one value down the whole table - and, worse, it went on
    // scrolling under the caption and its own total landed on top of it.
    [Test]
    public void TheGroupedColumn_LeavesTheTable()
    {
        var grid = GroupableGrid(4);
        var grouped = grid.Columns[0];
        Relayout(grid, width: 400);
        var full = grid.ColumnsWidth;

        grid.GroupBy(grouped);
        Relayout(grid, width: 400);

        var row = grid.ItemContainerGenerator.ContainerFromIndex(1) as DataGridRow;

        Assert.Multiple(() =>
        {
            Assert.That(grouped.IsShown, Is.False);
            Assert.That(grouped.ActualWidth, Is.EqualTo(0), "it takes no width");
            Assert.That(grid.ColumnsWidth, Is.EqualTo(full - 100), "so the table is one column narrower");
            Assert.That(row?.CellAt(0), Is.Null, "and no row builds a cell for it");
        });

        grid.ClearGrouping();
        Relayout(grid, width: 400);

        Assert.Multiple(() =>
        {
            Assert.That(grouped.IsShown, Is.True, "ungrouping gives it back");
            Assert.That(grid.ColumnsWidth, Is.EqualTo(full));
        });
    }

    // The expander cannot live on a column that left the table, or the tree could not be opened at all.
    [Test]
    public void TheExpander_MovesOffAColumnThatWasGroupedBy()
    {
        var grid = GroupableGrid(4);
        grid.ExpanderColumnIndex = 0;
        Assert.That(grid.ExpanderColumn, Is.SameAs(grid.Columns[0]));

        grid.GroupBy(grid.Columns[0]);

        Assert.That(grid.ExpanderColumn, Is.SameAs(grid.Columns[1]), "it moves to the first column still shown");
    }

    // The pinned zone is a COLUMN's lane, and a caption drawn across it reads as that column's text. So the caption
    // begins where that zone ends, whatever else is going on.
    [Test]
    public void TheGroupsCaption_BeginsWhereThePinnedZoneEnds()
    {
        var grid = GroupableGrid(4);
        grid.Columns[0].FrozenSide = DataGridFrozenSide.Left;
        grid.GroupBy(grid.Columns[2]);
        Relayout(grid, width: 400);

        var row = grid.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow;
        var caption = row?.GroupCaption;

        Assert.Multiple(() =>
        {
            Assert.That(caption, Is.Not.Null);
            Assert.That(grid.FrozenWidth, Is.EqualTo(100), "one pinned column and no number strip");
            Assert.That(caption.Bounds.X, Is.EqualTo(100), "the caption starts after it, not across it");
        });
    }

    // A group row belongs to no column, so it holds no cells at all - and the container it is recycled from has to give
    // them back to the pool, or the next data row it becomes comes up empty.
    [Test]
    public void AGroupRow_HoldsNoCells_AndGivesThemBackWhenItStopsBeingOne()
    {
        var grid = GroupableGrid(4);
        grid.GroupBy(grid.Columns[0]);
        Relayout(grid);

        var groupRow = grid.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow;

        Assert.Multiple(() =>
        {
            Assert.That(groupRow?.Group, Is.Not.Null, "the first row stands for a group");
            Assert.That(groupRow.CellAt(0), Is.Null, "and holds no cell of any column");
        });

        grid.ClearGrouping();
        Relayout(grid);

        var dataRow = grid.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow;

        Assert.Multiple(() =>
        {
            Assert.That(dataRow?.Group, Is.Null);
            Assert.That(dataRow.CellAt(0), Is.Not.Null, "the container is a data row again");
            Assert.That(dataRow.CellAt(0).Visibility, Is.EqualTo(Visibility.Visible), "with its cells shown");
        });
    }

    private sealed class Weighed
    {
        public double Weight { get; init; }
    }

    private static string Csv(TreeDataGrid grid, char separator = ',')
    {
        var writer = new StringWriter();
        grid.ExportCsv(writer, separator);
        return writer.ToString();
    }

    // The file ends with a line break, so the split leaves a trailing empty piece that is not a line.
    private static string[] Lines(string csv)
    {
        var lines = csv.Split(new[] { "\r\n" }, StringSplitOptions.None);
        return lines.Take(lines.Length - 1).ToArray();
    }

    private static string Sheet(TreeDataGrid grid)
    {
        using var stream = new MemoryStream();
        grid.ExportXlsx(stream);
        stream.Position = 0;

        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml").Open());
        return reader.ReadToEnd();
    }

    private static TreeDataGrid ExportableGrid(int rows = 2)
    {
        var grid = Grid(
            new DataGridTextColumn { Header = "Name", Binding = new Binding("Name"), Width = new GridLength(100), SortMemberPath = "Name" },
            new DataGridTextColumn { Header = "Note", Binding = new Binding("Note"), Width = new GridLength(100) },
            new DataGridTextColumn { Header = "Size", Binding = new Binding("Size"), Width = new GridLength(100) });
        grid.ItemsSource = Flat(rows);
        return grid;
    }

    // An export nobody trusts is one that does not match what is on screen.
    [Test]
    public void TheFile_CarriesTheColumnsTheUserCanSee_InTheOrderTheyStandIn()
    {
        var grid = ExportableGrid();
        grid.Columns[1].IsVisible = false;

        var lines = Lines(Csv(grid));

        Assert.Multiple(() =>
        {
            Assert.That(lines[0], Is.EqualTo("Name,Size"), "the hidden one is not in the file either");
            Assert.That(lines[1], Is.EqualTo("Item 1,1"));
            Assert.That(lines.Length, Is.EqualTo(3), "a header and the two records");
        });
    }

    // The three things that end a field early. A reader that gets these wrong silently shifts every column after them.
    [Test]
    public void AFieldThatWouldBreakTheFormat_ComesBackWhole()
    {
        var grid = Grid(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name"), Width = new GridLength(100) });
        grid.ItemsSource = new List<Row>
        {
            new() { Name = "Smith, John" },
            new() { Name = "say \"yes\"" },
            new() { Name = "two\r\nlines" }
        };

        Assert.That(Csv(grid), Is.EqualTo("Name\r\n\"Smith, John\"\r\n\"say \"\"yes\"\"\"\r\n\"two\r\nlines\"\r\n"));
    }

    // A comma is what the format is named after; a spreadsheet whose locale lists with semicolons puts a
    // comma-separated file in one column. Quoting follows whichever was chosen - a comma in a field is only dangerous
    // when the comma is the separator.
    [Test]
    public void TheSeparator_IsTheCallersToChoose_AndTheQuotingFollowsIt()
    {
        var grid = Grid(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name"), Width = new GridLength(100) });
        grid.ItemsSource = new List<Row> { new() { Name = "Smith, John" } };

        Assert.That(Csv(grid, ';'), Is.EqualTo("Name\r\nSmith, John\r\n"));
    }

    // A collapsed branch is a fold of the VIEW. If the file followed it, the same table would export differently
    // depending on which arrows somebody had clicked.
    [Test]
    public void TheFile_CarriesTheWholeTree_WhateverIsFoldedAway()
    {
        var grid = SortableTree();

        var lines = Lines(Csv(grid));

        Assert.Multiple(() =>
        {
            Assert.That(grid.Rows.Count, Is.EqualTo(2), "on screen the two roots stand closed");
            Assert.That(lines.Skip(1), Is.EqualTo(new[] { "B", "B-2", "B-1", "A", "A-1" }), "the file has all five");
        });
    }

    [Test]
    public void TheFile_IsWhatTheTableIsShowing_SortedAndFiltered()
    {
        var grid = ExportableGrid(4);
        grid.SetFilter(item => ((Row)item).Size % 2 == 1);
        grid.SortBy(grid.Columns[0], descending: true);

        Assert.That(Lines(Csv(grid)).Skip(1).Select(l => l.Split(',')[0]),
            Is.EqualTo(new[] { "Item 3", "Item 1" }));
    }

    // Grouping moves a column's value into the captions and the table stops drawing it. A file has no captions, so
    // taking the column out of the export would drop the very field the table is organised by.
    [Test]
    public void AColumnTheTableIsGroupedBy_StillGoesOut_AndTheCaptionsDoNot()
    {
        var grid = GroupableGrid(4);
        grid.Columns[0].Header = "Region";
        grid.GroupBy(grid.Columns[0]);

        var lines = Lines(Csv(grid));

        Assert.Multiple(() =>
        {
            Assert.That(grid.Columns[0].IsShown, Is.False, "the table itself has stopped showing it");
            Assert.That(lines[0].Split(',')[0], Is.EqualTo("Region"), "the file keeps it");
            Assert.That(lines.Length, Is.EqualTo(5), "four records, and no line for either caption");
            Assert.That(lines.Skip(1).Count(l => l.StartsWith("south")), Is.EqualTo(2));
        });
    }

    // A file is written to be read elsewhere. A number whose decimal separator came from the machine that wrote it is a
    // number the next machine reads wrong - or, worse, reads as two fields.
    [Test]
    public void ANumber_IsWrittenTheSame_WhateverTheMachineIsSetTo()
    {
        var was = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
        try
        {
            var grid = Grid(new DataGridTextColumn { Header = "Weight", Binding = new Binding("Weight"), Width = new GridLength(100) });
            grid.ItemsSource = new List<Weighed> { new() { Weight = 1.5 } };

            Assert.That(Lines(Csv(grid))[1], Is.EqualTo("1.5"));
        }
        finally
        {
            CultureInfo.CurrentCulture = was;
        }
    }

    // An .xlsx is a zip of XML parts, so writing a real one needs no library at all - only the right parts, each named
    // by the one above it.
    [Test]
    public void TheWorkbook_IsAZipWithThePartsAWorkbookNeeds()
    {
        using var stream = new MemoryStream();
        ExportableGrid().ExportXlsx(stream);
        stream.Position = 0;

        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

        Assert.That(zip.Entries.Select(e => e.FullName), Is.EquivalentTo(new[]
        {
            "[Content_Types].xml", "_rels/.rels", "xl/workbook.xml",
            "xl/_rels/workbook.xml.rels", "xl/worksheets/sheet1.xml"
        }));
    }

    // The whole reason for writing a workbook rather than a CSV with a different extension: a column of numbers that
    // arrives as text cannot be summed until somebody converts it.
    [Test]
    public void InTheWorkbook_ANumberArrivesAsANumber_AndTextAsText()
    {
        var sheet = Sheet(ExportableGrid(1));

        Assert.Multiple(() =>
        {
            Assert.That(sheet, Does.Contain("<c r=\"C2\"><v>1</v></c>"), "no type at all is what a number looks like");
            Assert.That(sheet, Does.Contain("r=\"A2\" t=\"inlineStr\""), "and text says what it is");
        });
    }

    [Test]
    public void InTheWorkbook_TextThatWouldBreakTheXml_IsEscaped()
    {
        var grid = Grid(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name"), Width = new GridLength(100) });
        grid.ItemsSource = new List<Row> { new() { Name = "<a & b>" } };

        Assert.That(Sheet(grid), Does.Contain("&lt;a &amp; b&gt;"));
    }

    // A cell says where it is by letters and a number, and the letters do not stop at Z. Twenty-seven columns is not an
    // unusual table.
    [Test]
    public void InTheWorkbook_TheColumnAfterZ_IsAA()
    {
        var grid = WideGrid(28);
        for (var i = 0; i < grid.Columns.Count; i++) grid.Columns[i].Header = $"C{i}";

        var sheet = Sheet(grid);

        Assert.Multiple(() =>
        {
            Assert.That(sheet, Does.Contain("r=\"Z1\""));
            Assert.That(sheet, Does.Contain("r=\"AA1\""));
            Assert.That(sheet, Does.Contain("r=\"AB1\""));
        });
    }
}
