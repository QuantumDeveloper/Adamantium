using Adamantium.Core.DependencyInjection;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// The grid's chrome under each theme. A template that THROWS while it is being built is not an error anybody sees: the
/// control keeps whatever look it had, which for a fresh one is none - the header strip simply vanishes, separators and
/// all. That is exactly how a single unparsable attribute took every column header out, so every part the control drives
/// by name is asked for here, under both themes.
/// </summary>
[TestFixture]
public class DataGridThemePartsTests
{
    private FakeApp _app;
    private ThemeManager _themes;

    [OneTimeSetUp]
    public void EnsureAppContext()
    {
        _app = new FakeApp(new AdamantiumDependencyContainer()) { ResourceManager = new ResourceManager() };
        UIAppContext.Initialize(_app, null);
    }

    private void Use(Theme theme)
    {
        _app.ResourceManager = new ResourceManager();
        typeof(UIAppContext).GetProperty(nameof(UIAppContext.Current)).SetValue(null, _app);
        _themes = new ThemeManager(new AdamantiumDependencyContainer());
        _app.ThemeManager = _themes;
        ((FakeContext)_app.UIContext).ThemeEngine = _themes;

        _themes.AddTheme(theme.Name, theme);
        _themes.SetTheme(theme);
    }

    private static T Built<T>(T control) where T : Adamantium.UI.Controls.Base.Control
    {
        control.ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(control);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        control.Measure(new Size(400, 300));
        control.Arrange(new Rect(0, 0, 400, 300));
        return control;
    }

    private static Theme MacOs() => new Adamantium.UI.Themes.MacOsTheme.MacOs();

    private static Theme Fluent() => new Adamantium.UI.Themes.FluentTheme.Fluent();

    [Test]
    public void TheHeaderKeepsItsPartsUnderMacOs() => HeaderContract(MacOs());

    [Test]
    public void TheHeaderKeepsItsPartsUnderFluent() => HeaderContract(Fluent());

    private void HeaderContract(Theme theme)
    {
        Use(theme);
        var header = Built(new DataGridColumnHeader { Content = "Owner" });

        Assert.Multiple(() =>
        {
            Assert.That(header.Template, Is.Not.Null, "a template that threw leaves the header with none at all");
            Assert.That(header.GetTemplateChild("PART_ContentPresenter"), Is.Not.Null, "the caption");
            Assert.That(header.GetTemplateChild("SortGlyph"), Is.Not.Null, "the sort arrow");
            Assert.That(header.GetTemplateChild("PART_FilterButton"), Is.Not.Null, "the funnel");
            Assert.That(header.GetTemplateChild("PART_FilterPopup"), Is.Not.Null, "and its flyout");
        });
    }

    [Test]
    public void TheFunnelStaysInsideANarrowColumnUnderMacOs() => NarrowHeader(MacOs());

    [Test]
    public void TheFunnelStaysInsideANarrowColumnUnderFluent() => NarrowHeader(Fluent());

    // Squeeze the column and the caption gives way first, but the arrow and the funnel keep their size - so they have to
    // be CUT at the separator rather than left hanging over the next column.
    private void NarrowHeader(Theme theme)
    {
        Use(theme);
        var header = Built(new DataGridColumnHeader { Content = "Unit Price", CanFilter = true });

        header.Measure(new Size(36, 26), force: true);
        header.Arrange(new Rect(0, 0, 36, 26));

        var funnel = (IUIComponent)header.GetTemplateChild("PART_FilterButton");
        var left = funnel.WorldTransform.TranslationVector.X;

        Assert.Multiple(() =>
        {
            Assert.That(header.ClipToBounds, Is.True, "the header cuts what does not fit");
            Assert.That(left + funnel.RenderSize.Width, Is.LessThanOrEqualTo(36 + 0.5),
                $"the funnel starts at {left} and must end inside the column");
        });
    }

    [Test]
    public void TheCellKeepsItsPartsUnderMacOs() => CellContract(MacOs());

    [Test]
    public void TheCellKeepsItsPartsUnderFluent() => CellContract(Fluent());

    private void CellContract(Theme theme)
    {
        Use(theme);
        var cell = Built(new DataGridCell { Content = "value" });

        Assert.Multiple(() =>
        {
            Assert.That(cell.Template, Is.Not.Null);
            Assert.That(cell.GetTemplateChild("PART_ContentPresenter"), Is.Not.Null);
            Assert.That(cell.GetTemplateChild("PART_Expander"), Is.Not.Null, "the strip that opens a branch");
        });
    }

    private sealed class Item
    {
        public string Name { get; set; }
    }

    [Test]
    public void TheFilterFlyoutComesUpFilledUnderMacOs() => FilledFlyout(MacOs());

    [Test]
    public void TheFilterFlyoutComesUpFilledUnderFluent() => FilledFlyout(Fluent());

    // The editor lives in the popup's DEFERRED content, which does not exist until the popup is first opened. Looked up
    // in the header's own template it is simply not there, and the flyout comes up blank: no values to tick and no
    // operator chosen. Opening has to be what finds it.
    private void FilledFlyout(Theme theme)
    {
        Use(theme);

        var column = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(120) };
        var grid = new TreeDataGrid();
        grid.Columns.Add(column);
        grid.ItemsSource = new[] { new Item { Name = "one" }, new Item { Name = "two" }, new Item { Name = "one" } };
        Built(grid);

        var header = FirstHeader(grid);
        Assert.That(header, Is.Not.Null, "the strip built its headers");

        var view = header.OpenFilter();

        Assert.Multiple(() =>
        {
            Assert.That(view, Is.Not.Null, "the flyout's editor was found once the content was built");
            Assert.That(view.Column, Is.SameAs(column), "and it knows which column it is for");
            Assert.That(view.Values.Count, Is.EqualTo(2), "two distinct values, the duplicate counted once");
            Assert.That(((DropDown)view.GetTemplateChild("PART_FirstOperator")).SelectedIndex, Is.EqualTo(0),
                "the operator comes up chosen, not blank");
            Assert.That(((ListBox)view.GetTemplateChild("PART_Values")).ItemsSource, Is.SameAs(view.Values));
            // The flyout was measured EMPTY - the editor can only be found once the popup built it - so filling it has
            // to ask for another pass, or the first open shows the form it was measured with and only the second works.
            Assert.That(view.IsMeasureValid, Is.False, "filling the form asks for a fresh layout");
        });
    }

    private static DataGridColumnHeader FirstHeader(TreeDataGrid grid)
    {
        if (grid.GetTemplateChild("PART_Headers") is not IUIComponent strip) return null;
        foreach (var child in strip.VisualChildren)
        {
            if (child is DataGridColumnHeader header) return header;
        }

        return null;
    }

    // A long value list virtualizes: it builds for its VIEWPORT, not for the twelve thousand values a column can hold.
    // It also arranges a couple of buffer rows outside that viewport - normal, and trimmed by the scroll area's clip -
    // so the list's own box has to clip, which is what keeps a buffer row off the form around it.
    [Test]
    public void TheValueListBuildsForItsViewport()
    {
        Use(MacOs());

        var column = new DataGridTextColumn { Binding = new Binding("Name"), Width = new GridLength(120) };
        var grid = new TreeDataGrid();
        grid.Columns.Add(column);
        var items = new Item[200];
        for (var i = 0; i < items.Length; i++) items[i] = new Item { Name = $"P-{i:D5}" };
        grid.ItemsSource = items;
        Built(grid);

        var view = FirstHeader(grid).OpenFilter();
        view.Measure(new Size(260, 460));
        view.Arrange(new Rect(0, 0, 260, 460));

        var list = (ListBox)view.GetTemplateChild("PART_Values");
        var listTop = ((IUIComponent)list).WorldTransform.TranslationVector.Y;
        var listBottom = listTop + list.ActualHeight;

        var realized = 0;
        double lowest = 0;
        foreach (var index in list.ItemContainerGenerator.RealizedIndices)
        {
            realized++;
            if (list.ItemContainerGenerator.ContainerFromIndex(index) is IUIComponent container)
                lowest = System.Math.Max(lowest, container.WorldTransform.TranslationVector.Y);
        }

        TestContext.WriteLine($"list {listTop}..{listBottom} height={list.ActualHeight} realized={realized} lowest={lowest}");

        Assert.Multiple(() =>
        {
            Assert.That(realized, Is.LessThan(40), "the list virtualizes - the viewport is what it builds for");
            Assert.That(lowest, Is.GreaterThan(listBottom),
                "there ARE rows outside the viewport; the clip below is what keeps them off the form");
            Assert.That(list.ClipToBounds, Is.True, "so the list clips");
        });
    }

    [Test]
    public void ASelectedCellIsWashedNotFilledUnderMacOs() => SelectionWash(MacOs());

    [Test]
    public void ASelectedCellIsWashedNotFilledUnderFluent() => SelectionWash(Fluent());

    // A grid cell holds controls, not just text: a check box painted in the accent disappears into a selection painted
    // in the same accent. So the selection is TRANSLUCENT - and an invented palette key would resolve to nothing at all,
    // silently, which is the other half of what this guards.
    private void SelectionWash(Theme theme)
    {
        Use(theme);
        var cell = Built(new DataGridCell { Content = "value" });
        cell.IsSelected = true;

        var border = (Adamantium.UI.Controls.Decorators.Border)cell.GetTemplateChild("CellBorder");

        Assert.That(border.Background, Is.InstanceOf<Adamantium.UI.Core.Media.SolidColorBrush>(),
            "the selection brush resolved - a missing key paints nothing and says nothing");
        Assert.That(((Adamantium.UI.Core.Media.SolidColorBrush)border.Background).Color.A, Is.LessThan(255),
            "and it is a wash, so what is in the cell stays visible through it");
    }

    // Columns wider than the viewport have to BE scrollable: the extent the scroller sees must be the columns' width,
    // not the width it handed the rows - otherwise there is nothing to scroll and no bar to scroll it with.
    [Test]
    public void ColumnsWiderThanTheViewportScrollSideways()
    {
        Use(MacOs());

        var grid = new TreeDataGrid();
        for (var i = 0; i < 8; i++)
            grid.Columns.Add(new DataGridTextColumn { Header = $"C{i}", Binding = new Binding("Name"), Width = new GridLength(150) });
        grid.ItemsSource = new[] { new Item { Name = "one" }, new Item { Name = "two" } };

        grid.ApplyCurrentTheme();
        Adamantium.UI.Extensions.WindowExtension.UpdateTree(grid);
        Adamantium.UI.Core.Data.BindingUpdateQueue.Flush();
        grid.Measure(new Size(600, 300));
        grid.Arrange(new Rect(0, 0, 600, 300));

        var scroll = (ScrollViewer)grid.GetTemplateChild("PART_ScrollHost");
        var row = (IMeasurableComponent)grid.ItemContainerGenerator.ContainerFromIndex(0);
        var panel = (IMeasurableComponent)grid.ItemsHostPanel;
        TestContext.WriteLine($"columns={grid.ColumnsWidth} extent={scroll.ExtentSize} viewport={scroll.ViewportSize} " +
                              $"row={row?.DesiredSize} panel={panel?.DesiredSize}");

        Assert.Multiple(() =>
        {
            Assert.That(grid.ColumnsWidth, Is.EqualTo(1200).Within(1), "eight fixed columns keep their width");
            Assert.That(scroll.ExtentSize.Width, Is.GreaterThan(scroll.ViewportSize.Width),
                "the scroller knows the content is wider than the window onto it");
        });

        // ...and once it IS scrolled, a header has to stand over its own column. The rows are moved by the scroller
        // translating their whole panel; the header strip is outside it and moves itself - two mechanisms for one
        // number, and applying that number twice on one side is exactly how they came apart.
        scroll.SetScrollOffset(new Vector2(300, 0));

        // The strip's own invalidation is drained by the LayoutManager in the app; here the valid Border above it would
        // short-circuit the walk, so the chain is dirtied by hand - as everywhere else these tests re-lay a subtree.
        var header = FirstHeader(grid);
        for (IUIComponent node = header; node != null; node = node.VisualParent)
            (node as IMeasurableComponent)?.InvalidateMeasure();

        grid.Measure(new Size(600, 300), force: true);
        grid.Arrange(new Rect(0, 0, 600, 300));
        var scrolledRow = (IUIComponent)grid.ItemContainerGenerator.ContainerFromIndex(0);
        Assert.That(scrolledRow.RenderSize.Width, Is.EqualTo(1200).Within(1),
            "a row is as wide as the COLUMNS - it paints the stripe and the selection, and a row cut at the window "
            + "leaves both short of the last column");

        IUIComponent cell = null;
        foreach (var child in scrolledRow.VisualChildren)
        {
            if (child is DataGridCell c && c.ColumnIndex == header.ColumnIndex) cell = c;
        }

        var headerX = ((IUIComponent)header).WorldTransform.TranslationVector.X;
        var cellX = cell.WorldTransform.TranslationVector.X;

        TestContext.WriteLine($"scrolled: offset={grid.HorizontalOffset} scroll={scroll.ScrollOffset.X} " +
                              $"headerWorld={headerX} cellWorld={cellX} " +
                              $"headerLocal={((IUIComponent)header).Bounds.X} cellLocal={cell.Bounds.X}");

        Assert.Multiple(() =>
        {
            Assert.That(grid.HorizontalOffset, Is.EqualTo(300).Within(1), "the grid follows its scroller");
            Assert.That(((IUIComponent)header).Bounds.X, Is.EqualTo(-300).Within(1),
                "the header strip is outside the scroller, so it carries the offset itself");
            Assert.That(cell.Bounds.X, Is.EqualTo(0).Within(1),
                "and a cell does NOT - the scroller already moved the whole rows panel, and shifting here too moved the "
                + "cells at twice their headers' speed");
        });
    }

    [Test]
    public void AReadOnlyCellLooksReadOnlyUnderMacOs() => ReadOnlyLooksIt(MacOs());

    [Test]
    public void AReadOnlyCellLooksReadOnlyUnderFluent() => ReadOnlyLooksIt(Fluent());

    // A cell nobody can edit has to SAY so - otherwise the only way to find out is to double-click every column and see
    // which ones do nothing.
    private void ReadOnlyLooksIt(Theme theme)
    {
        Use(theme);

        var editable = Built(new DataGridCell { Content = "value" });
        var ordinary = ((ContentPresenter)editable.GetTemplateChild("PART_ContentPresenter")).Foreground;

        var locked = Built(new DataGridCell { Content = "value", IsReadOnly = true });
        var dimmed = ((ContentPresenter)locked.GetTemplateChild("PART_ContentPresenter")).Foreground;

        Assert.That(dimmed, Is.Not.Null);
        Assert.That(dimmed, Is.Not.EqualTo(ordinary), "read-only reads differently from the rest");
    }

    [Test]
    public void TheFilterViewKeepsItsPartsUnderMacOs() => FilterContract(MacOs());

    [Test]
    public void TheFilterViewKeepsItsPartsUnderFluent() => FilterContract(Fluent());

    // Every one of these is driven BY NAME from code: a missing part is a button that silently does nothing.
    private void FilterContract(Theme theme)
    {
        Use(theme);
        var view = Built(new DataGridFilterView());

        Assert.Multiple(() =>
        {
            Assert.That(view.Template, Is.Not.Null);
            Assert.That(view.GetTemplateChild("PART_SelectAll"), Is.Not.Null);
            Assert.That(view.GetTemplateChild("PART_Values"), Is.Not.Null);
            Assert.That(view.GetTemplateChild("PART_FirstOperator"), Is.Not.Null);
            Assert.That(view.GetTemplateChild("PART_FirstValue"), Is.Not.Null);
            Assert.That(view.GetTemplateChild("PART_FirstMatchCase"), Is.Not.Null, "aA on the first condition");
            Assert.That(view.GetTemplateChild("PART_Logic"), Is.Not.Null);
            Assert.That(view.GetTemplateChild("PART_SecondOperator"), Is.Not.Null);
            Assert.That(view.GetTemplateChild("PART_SecondValue"), Is.Not.Null);
            Assert.That(view.GetTemplateChild("PART_SecondMatchCase"), Is.Not.Null, "and on the second");
            Assert.That(view.GetTemplateChild("PART_Apply"), Is.Not.Null);
            Assert.That(view.GetTemplateChild("PART_Clear"), Is.Not.Null);
        });
    }
}
