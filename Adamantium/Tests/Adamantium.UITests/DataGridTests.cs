using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            Name = $"Item {i}", Note = $"Note {i}", Locked = i % 2 == 0
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
}
