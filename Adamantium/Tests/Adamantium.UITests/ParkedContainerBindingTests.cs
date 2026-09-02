using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Templates;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>Parking an off-screen container CLOSES every binding in it, so the tile leaves any shared source's fan-out.
/// What re-opens them is the only question that matters, and the answer used to be "the DataContext change a rebind
/// makes" - which is no change at all when the container is handed back the item it already held. It then came back on
/// screen looking right and dead: whatever its bindings had last written stayed frozen there for the rest of its life.
/// </summary>
[TestFixture]
public class ParkedContainerBindingTests
{
    private sealed class Row : INotifyPropertyChanged
    {
        private double _size;

        public double Size
        {
            get => _size;
            set { _size = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Size))); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    private static ControlTemplate ItemsPresenterTemplate() => new(() =>
    {
        var presenter = new ItemsPresenter();
        var result = new TemplateResult { RootComponent = presenter };
        result.RegisterName("PART_ItemsPresenter", presenter);
        return result;
    });

    // The window's last index at a viewport of `height`, from the panel's own arithmetic: floor/ceil over a 20px item
    // plus the two-item buffer on each side. Stated here so the two viewports below are chosen, not guessed.
    private const double ItemExtent = 20;

    // The item template's Border, wherever the container's own template put it. Walked by hand: GetVisualDescendants()
    // returns only the IMMEDIATE children despite its name.
    private static Border TemplatedBorder(IUIComponent node)
    {
        if (node is Border border && !double.IsNaN(border.Width)) return border;

        foreach (var child in node.VisualChildren)
        {
            if (TemplatedBorder(child) is { } found) return found;
        }

        return null;
    }

    /// <summary>Shrink the viewport by one item and grow it back: exactly one container is parked and exactly one slot
    /// then needs filling, so the pool hands that container back to THE ITEM IT ALREADY HELD. Nothing about it changes,
    /// which is precisely why nothing used to wake it up.</summary>
    [Test]
    public void AContainerParkedAndGivenItsOwnItemBackStillTracksIt()
    {
        var rows = Enumerable.Range(0, 100).Select(i => new Row { Size = 10 + i }).ToList();

        var template = new DataTemplate(() =>
        {
            var border = new Border { Height = ItemExtent };
            var result = new TemplateResult { RootComponent = border };
            result.AddBinding(border, "Width", new Binding("Size"));
            return result;
        });

        var ic = new ItemsControl { ItemTemplate = template, ItemsSource = rows, Template = ItemsPresenterTemplate() };
        ic.Measure(new Size(100, 300));
        ic.Arrange(new Rect(0, 0, 100, 300));

        var gen = ic.ItemContainerGenerator;
        var panel = ic.ItemsHostPanel;
        var edge = gen.RealizedIndices.Max();          // the last index the window reaches
        var parked = gen.ContainerFromIndex(edge);
        Assert.That(parked, Is.Not.Null);

        // One item less of viewport: the edge index leaves the window with nothing entering, so it is surplus and the
        // panel parks it - Collapsed, and every binding in it closed.
        panel.Measure(new Size(100, 300 - ItemExtent), true);
        panel.Arrange(new Rect(0, 0, 100, 300 - ItemExtent), true);
        Assert.That(gen.ContainerFromIndex(edge), Is.Null, "the edge container must actually have been parked");

        // ...and back. The one slot that is missing is the one that container just held, so the pool returns it to its
        // own item and PrepareContainer writes a DataContext it already has.
        panel.Measure(new Size(100, 300), true);
        panel.Arrange(new Rect(0, 0, 100, 300), true);
        var revived = gen.ContainerFromIndex(edge);

        Assert.That(revived, Is.SameAs(parked), "the same container comes back - or this test is not testing the case");

        rows[edge].Size = 777;
        BindingUpdateQueue.Flush();

        Assert.That(TemplatedBorder(revived)?.Width, Is.EqualTo(777).Within(0.5),
            "a revived container has to follow its item again - its bindings were closed when it was parked");
    }
}
