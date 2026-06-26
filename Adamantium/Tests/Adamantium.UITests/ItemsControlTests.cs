using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Templates;
using NUnit.Framework;

namespace Adamantium.UITests;

// Pure-CPU (no GPU) tests for the ItemsControl core: item -> container generation through the ItemContainerGenerator,
// and that the ItemTemplate's bindings resolve against each item. Non-virtualizing path (Phase 1).
[TestFixture]
public class ItemsControlTests
{
    private sealed class Person
    {
        public string Name { get; init; }
        public double Size { get; init; }
    }

    // A minimal control template whose root is the ItemsPresenter the control looks up by PART name.
    private static ItemsControl ArrangedItemsControl(System.Collections.IEnumerable source, DataTemplate itemTemplate = null)
    {
        var ic = new ItemsControl();
        if (itemTemplate != null) ic.ItemTemplate = itemTemplate;
        ic.ItemsSource = source;
        ic.Template = new ControlTemplate(() =>
        {
            var presenter = new ItemsPresenter();
            var result = new TemplateResult { RootComponent = presenter };
            result.RegisterName("PART_ItemsPresenter", presenter);
            return result;
        });
        ic.Measure(new Size(500, 500));
        ic.Arrange(new Rect(0, 0, 500, 500));
        return ic;
    }

    private static ControlTemplate ItemsPresenterTemplate() => new(() =>
    {
        var presenter = new ItemsPresenter();
        var result = new TemplateResult { RootComponent = presenter };
        result.RegisterName("PART_ItemsPresenter", presenter);
        return result;
    });

    [Test]
    public void VirtualizingStackPanelRealizesOnlyVisibleWindow()
    {
        var items = Enumerable.Range(0, 10000).Cast<object>().ToList();
        // Uniform fixed-height item template -> item extent 20px.
        var template = new DataTemplate(() => new TemplateResult { RootComponent = new Border { Height = 20 } });

        var ic = new ItemsControl { ItemTemplate = template, ItemsSource = items };
        ic.Template = ItemsPresenterTemplate();              // default ItemsPanel = vertical (virtualizing) StackPanel
        ic.Measure(new Size(100, 300));
        ic.Arrange(new Rect(0, 0, 100, 300));

        var gen = ic.ItemContainerGenerator;
        var panel = ic.ItemsHostPanel;
        var scroll = (IScrollableContent)panel;

        Assert.Multiple(() =>
        {
            Assert.That(gen.RealizedCount, Is.LessThan(60), "only the visible window (+buffer) is realized, not 10000");
            Assert.That(scroll.Extent.Height, Is.EqualTo(10000 * 20).Within(1), "extent spans all items");
            Assert.That(gen.ContainerFromIndex(0), Is.Not.Null, "top realized at offset 0");
            Assert.That(gen.ContainerFromIndex(9999), Is.Null, "far end not realized");
        });

        // Scroll to the middle (item ~250 = 5000/20): the window must move there and stay bounded.
        scroll.SetOffset(new Vector2(0, 5000));
        panel.Measure(new Size(100, 300), true);
        panel.Arrange(new Rect(0, 0, 100, 300), true);

        var realized = gen.RealizedIndices.ToList();
        Assert.Multiple(() =>
        {
            Assert.That(gen.RealizedCount, Is.LessThan(60), "window stays bounded after scrolling");
            Assert.That(realized, Has.Some.InRange(248, 252), "window now centered near item 250");
            Assert.That(gen.ContainerFromIndex(0), Is.Null, "the top is recycled away after scrolling");
        });
    }

    [Test]
    public void RealizesOneContainerPerItem()
    {
        var ic = ArrangedItemsControl(new[] { "a", "b", "c" });
        var gen = ic.ItemContainerGenerator;

        Assert.Multiple(() =>
        {
            Assert.That(gen.ContainerFromIndex(0), Is.InstanceOf<ContentPresenter>());
            Assert.That(gen.ContainerFromIndex(1), Is.InstanceOf<ContentPresenter>());
            Assert.That(gen.ContainerFromIndex(2), Is.InstanceOf<ContentPresenter>());
            Assert.That(gen.ContainerFromIndex(3), Is.Null, "only as many containers as items");
            // Each container projects its own item.
            Assert.That(((ContentPresenter)gen.ContainerFromIndex(0)).Content, Is.EqualTo("a"));
            Assert.That(((ContentPresenter)gen.ContainerFromIndex(2)).Content, Is.EqualTo("c"));
        });
    }

    [Test]
    public void ContainerIndexRoundTrips()
    {
        var ic = ArrangedItemsControl(new[] { "x", "y" });
        var gen = ic.ItemContainerGenerator;
        var c1 = gen.ContainerFromIndex(1);
        Assert.That(gen.IndexFromContainer(c1), Is.EqualTo(1));
    }

    [Test]
    public void ItemTemplateBindingResolvesAgainstItem()
    {
        var people = new List<Person>
        {
            new() { Name = "A", Size = 40 },
            new() { Name = "B", Size = 120 },
            new() { Name = "C", Size = 250 }
        };

        var template = new DataTemplate(() =>
        {
            var border = new Border();
            var result = new TemplateResult { RootComponent = border };
            result.AddBinding(border, "Width", new Binding("Size"));
            return result;
        });

        var ic = ArrangedItemsControl(people, template);
        var gen = ic.ItemContainerGenerator;

        Assert.Multiple(() =>
        {
            for (var i = 0; i < people.Count; i++)
            {
                var container = (ContentPresenter)gen.ContainerFromIndex(i);
                var border = container.VisualChildren.OfType<Border>().First();
                Assert.That(border.Width, Is.EqualTo(people[i].Size).Within(0.5),
                    $"item {i}: ItemTemplate {{Binding Size}} must resolve against the item");
            }
        });
    }

    [Test]
    public void ObservableCollectionChangesUpdateContainers()
    {
        var data = new ObservableCollection<string> { "a", "b", "c" };
        var ic = ArrangedItemsControl(data);
        var gen = ic.ItemContainerGenerator;

        // Containers are (re)realized on layout - the live framework triggers this from the collection change's
        // InvalidateMeasure; the headless test drives the layout pass explicitly.
        void Relayout()
        {
            ic.Measure(new Size(500, 500), true);
            ic.Arrange(new Rect(0, 0, 500, 500), true);
        }

        string ContentAt(int i) => (gen.ContainerFromIndex(i) as ContentPresenter)?.Content as string;

        Assert.That(ContentAt(2), Is.EqualTo("c"), "initial");

        data.Add("d");
        Relayout();
        Assert.That(ContentAt(3), Is.EqualTo("d"), "append");

        data.Insert(1, "B2");                       // a, B2, b, c, d
        Relayout();
        Assert.Multiple(() =>
        {
            Assert.That(ContentAt(1), Is.EqualTo("B2"), "inserted in the middle");
            Assert.That(ContentAt(2), Is.EqualTo("b"), "item after the insert shifted up");
        });

        data[0] = "A2";                             // replace head
        Relayout();
        Assert.That(ContentAt(0), Is.EqualTo("A2"), "replace");

        data.RemoveAt(0);                           // A2, B2, b, c, d -> B2, b, c, d
        Relayout();
        Assert.Multiple(() =>
        {
            Assert.That(ContentAt(0), Is.EqualTo("B2"), "remove shifts the rest down");
            Assert.That(gen.ContainerFromIndex(4), Is.Null, "count dropped to 4");
        });
    }

    [Test]
    public void DirectMarkupItemsAreRealized()
    {
        // Items authored directly (no ItemsSource) — the [Content] / IContainer path.
        var ic = new ItemsControl();
        ((IContainer)ic).AddOrSetChildComponent("one");
        ((IContainer)ic).AddOrSetChildComponent("two");
        ic.Template = new ControlTemplate(() =>
        {
            var presenter = new ItemsPresenter();
            var result = new TemplateResult { RootComponent = presenter };
            result.RegisterName("PART_ItemsPresenter", presenter);
            return result;
        });
        ic.Measure(new Size(500, 500));
        ic.Arrange(new Rect(0, 0, 500, 500));

        var gen = ic.ItemContainerGenerator;
        Assert.Multiple(() =>
        {
            Assert.That(((ContentPresenter)gen.ContainerFromIndex(0)).Content, Is.EqualTo("one"));
            Assert.That(((ContentPresenter)gen.ContainerFromIndex(1)).Content, Is.EqualTo("two"));
            Assert.That(gen.ContainerFromIndex(2), Is.Null);
        });
    }
}
