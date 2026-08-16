using System;
using System.Linq;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Diagnostics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Extensions;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// What one step of the tile-size slider actually costs, headless: the same drag the sandbox reproduces by hand, run
/// here so it can be measured as often as needed without a person at the keyboard. Not an assertion - it prints the
/// counters and passes; read the output.
/// </summary>
[TestFixture]
[Explicit("Measurement probe - run it deliberately and read the numbers")]
public class TileResizeCostProbe
{
    private const double ViewportW = 1200;
    private const double ViewportH = 800;

    private sealed class TestWindowRoot : Grid, IRootVisualComponent
    {
        public Vector2 PointToClient(PixelPoint point) => new((float)point.X, (float)point.Y);
        public PixelPoint PointToScreen(Vector2 point) => new(point.X, point.Y);
        public PixelPoint Position { get; set; }
        public void AttachContextAndInitialize(IUIContext context) { }
        public double Left { get; set; }
        public double Top { get; set; }
        public string Title { get; set; }
        public double ClientWidth { get; set; }
        public double ClientHeight { get; set; }
        public IUIContext UIContext => null;
    }

    [Test]
    public void OneSliderStep()
    {
        var items = Enumerable.Range(0, 6000).Cast<object>().ToList();
        WrapPanel panel = null;
        var ic = new ItemsControl
        {
            ItemsSource = items,
            ItemTemplate = new DataTemplate(() => new TemplateResult
            {
                RootComponent = new Border { Margin = new Thickness(3), Background = Brushes.Red }
            }),
            ItemsPanel = new ItemsPanelTemplate(() => new TemplateResult
            {
                RootComponent = panel = new WrapPanel { Orientation = Orientation.Horizontal, ItemWidth = 24, ItemHeight = 24 }
            }),
            Template = new ControlTemplate(() =>
            {
                var presenter = new ItemsPresenter();
                var result = new TemplateResult { RootComponent = presenter };
                result.RegisterName("PART_ItemsPresenter", presenter);
                return result;
            })
        };

        var root = new TestWindowRoot { ClientWidth = ViewportW, ClientHeight = ViewportH };
        root.Children.Add(ic);

        // Settle the initial fill first - its cost is not what a drag step costs.
        for (var i = 0; i < 60; i++) { WindowExtension.UpdateTree(root); RenderDirty.Clear(); }

        var cell = 24.0;
        LayoutTrace.ResetCounts();
        LayoutTrace.Counting = true;
        LayoutTrace.CountCallers = true;
        var measureBefore = MeasurableUIComponent.TotalMeasureCalls;
        var arrangeBefore = MeasurableUIComponent.TotalArrangeCalls;
        try
        {
            // FIVE steps of the slider, each followed by the passes it takes to settle - exactly what a hand does.
            for (var step = 0; step < 5; step++)
            {
                cell += step % 2 == 0 ? 6 : -3;
                panel.ItemWidth = cell;
                panel.ItemHeight = cell;
                for (var p = 0; p < 6; p++) { WindowExtension.UpdateTree(root); RenderDirty.Clear(); }
            }
        }
        finally
        {
            LayoutTrace.Counting = false;
            LayoutTrace.CountCallers = false;
        }

        TestContext.Out.WriteLine($"5 slider steps on {items.Count} items, viewport {ViewportW}x{ViewportH}");
        TestContext.Out.WriteLine($"measure {MeasurableUIComponent.TotalMeasureCalls - measureBefore}   " +
                                  $"arrange {MeasurableUIComponent.TotalArrangeCalls - arrangeBefore}");
        TestContext.Out.WriteLine(LayoutTrace.DumpCounts());
        LayoutTrace.ResetCounts();
    }
}
