using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Templates;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>"Drag the cell to its minimum and every tile disappears" (Layout tab, reported from the stand). Two very
/// different mechanisms could produce that and they are fixed in different places, so this measures which:
/// <list type="number">
/// <item>the PANEL's arithmetic collapses at a small uniform cell - containers realized with no size, or none at all;</item>
/// <item>the panel is fine and the tiles are all DEFERRED - a small cell means thousands of slots per pass, the bind
/// budget cannot keep up, and what is on screen is skeleton cards.</item>
/// </list>
/// </summary>
[TestFixture]
public class MinimumCellTilesTests
{
    private const int Items = 2000;
    private const double Cell = 24;          // the Layout tab's slider minimum
    private const double ViewW = 800, ViewH = 600;

    private static ControlTemplate ItemsPresenterTemplate() => new(() =>
    {
        var presenter = new ItemsPresenter();
        var result = new TemplateResult { RootComponent = presenter };
        result.RegisterName("PART_ItemsPresenter", presenter);
        return result;
    });

    private static ItemsControl TiledList(double budgetMs, bool withSkeletons = true)
    {
        var ic = new ItemsControl
        {
            // The theme supplies this in the running application, and WITHOUT it the panel makes no skeleton cards at
            // all - so IsLoadingItems reads False and a themeless test would "prove" a defect that is only its own
            // missing template. Stand-in card, same shape as the theme's: a plain Border.
            ItemSkeletonTemplate = withSkeletons
                ? new DataTemplate(() => new TemplateResult { RootComponent = new Border() })
                : null,
            ItemsSource = Enumerable.Range(0, Items).Cast<object>().ToList(),
            ItemsPanel = new ItemsPanelTemplate(() => new TemplateResult
            {
                RootComponent = new WrapPanel
                {
                    Orientation = Orientation.Horizontal,
                    ItemWidth = Cell,
                    ItemHeight = Cell,
                    ScrollBindBudget = budgetMs
                }
            }),
            Template = ItemsPresenterTemplate()
        };

        ic.Measure(new Size(ViewW, ViewH));
        ic.Arrange(new Rect(0, 0, ViewW, ViewH));
        return ic;
    }

    /// <summary>With no bind budget the panel must realize a full window of tiles, each one cell big. If this fails the
    /// arithmetic is at fault; if it passes, the panel is not what makes them vanish.</summary>
    [Test]
    public void AtTheMinimumCell_ThePanelStillGivesEveryTileItsCell()
    {
        var ic = TiledList(budgetMs: 0);
        var panel = (VirtualizingPanel)ic.ItemsHostPanel;
        var gen = ic.ItemContainerGenerator;

        var realized = 0;
        var sized = 0;
        Rect firstRect = default;
        for (var i = 0; i < Items; i++)
        {
            if (gen.ContainerFromIndex(i) is not IUIComponent c) continue;
            realized++;
            var bounds = c.Bounds;
            if (realized == 1) firstRect = bounds;
            if (bounds.Width > 0 && bounds.Height > 0) sized++;
        }

        TestContext.WriteLine($"cell={Cell}, viewport={ViewW}x{ViewH}");
        TestContext.WriteLine($"realized containers = {realized}, of them with a non-empty rect = {sized}");
        TestContext.WriteLine($"first container rect = {firstRect}");
        TestContext.WriteLine($"panel extent = {panel.DesiredSize}");

        Assert.Multiple(() =>
        {
            Assert.That(realized, Is.GreaterThan(0), "the panel realizes SOMETHING at the minimum cell");
            Assert.That(sized, Is.EqualTo(realized), "and every realized container gets a real rect, not a collapsed one");
            Assert.That(firstRect.Width, Is.EqualTo(Cell).Within(0.5), "a tile is exactly one cell wide");
            Assert.That(firstRect.Height, Is.EqualTo(Cell).Within(0.5));
        });
    }

    /// <summary>The same list under the tab's own default budget. This one only REPORTS - how many of the window's
    /// slots the budget manages to bind in a pass - because what counts as acceptable here is a judgement about the
    /// dial, not an invariant.</summary>
    [Test]
    public void AtTheMinimumCell_HowMuchTheBindBudgetKeepsUpWith()
    {
        var ic = TiledList(budgetMs: 6);
        var gen = ic.ItemContainerGenerator;

        var realized = 0;
        for (var i = 0; i < Items; i++)
            if (gen.ContainerFromIndex(i) != null)
                realized++;

        TestContext.WriteLine($"cell={Cell}, budget=6ms -> bound containers after pass 1 = {realized} of {Items}");
        // IsLoadingItems is exactly "there are skeleton cards on screen" (VirtualizingPanel.SyncLoadingState), which is
        // also the flag the theme's pulse trigger keys off - so it answers both questions at once.
        TestContext.WriteLine($"IsLoadingItems (= skeletons are showing) = {ic.IsLoadingItems}");

        // ...and how fast it catches up. Through the LAYOUT MANAGER, not by calling Measure again: a panel with slots
        // left over asks for the next slice with InvalidateMeasureNextPass, and a direct Measure never runs that queue -
        // so a hand-driven loop reports "stuck" no matter how well the drain works.
        var manager = LayoutManager.GetOrCreate(ic);
        for (var pass = 2; pass <= 8; pass++)
        {
            manager.ExecuteLayoutPass();

            var bound = 0;
            for (var i = 0; i < Items; i++)
                if (gen.ContainerFromIndex(i) != null)
                    bound++;

            TestContext.WriteLine($"  after pass {pass}: {bound}   (settled={manager.IsSettled})");
        }

        Assert.Pass("reporting only");
    }
}
