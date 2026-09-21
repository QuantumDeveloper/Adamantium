using System.Collections.ObjectModel;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using NUnit.Framework;

namespace Adamantium.UITests;

// ListBox single-selection + the recycling-correctness that matters for a virtualized list: selection lives on the
// ListBox (by item), and is reflected onto whichever container currently hosts that item - even a recycled one rebound
// to it on scroll. Containers are realized directly via the generator (no GPU/layout needed for these).
public class ListBoxTests
{
    private static ListBox MakeListBox(params string[] items) => new() { ItemsSource = items };

    [Test]
    public void GeneratedContainer_IsListBoxItem()
    {
        var lb = MakeListBox("a");
        Assert.That(lb.ItemContainerGenerator.Realize(0), Is.InstanceOf<ListBoxItem>());
    }

    [Test]
    public void SelectedIndex_SetsItem_AndReflectsOntoContainers()
    {
        var lb = MakeListBox("a", "b", "c");
        var g = lb.ItemContainerGenerator;
        var c0 = (ListBoxItem)g.Realize(0);
        var c1 = (ListBoxItem)g.Realize(1);
        var c2 = (ListBoxItem)g.Realize(2);

        lb.SelectedIndex = 1;

        Assert.Multiple(() =>
        {
            Assert.That(lb.SelectedItem, Is.EqualTo("b"));
            Assert.That(c0.IsSelected, Is.False);
            Assert.That(c1.IsSelected, Is.True);
            Assert.That(c2.IsSelected, Is.False);
        });
    }

    [Test]
    public void SelectedItem_SetsIndex()
    {
        var lb = MakeListBox("a", "b", "c");
        lb.SelectedItem = "c";
        Assert.That(lb.SelectedIndex, Is.EqualTo(2));
    }

    [Test]
    public void SelectFromContainer_SelectsThatItem()
    {
        var lb = MakeListBox("a", "b", "c");
        var c1 = (ListBoxItem)lb.ItemContainerGenerator.Realize(1);

        lb.SelectFromContainer(c1);   // simulates a press on the container

        Assert.Multiple(() =>
        {
            Assert.That(lb.SelectedIndex, Is.EqualTo(1));
            Assert.That(lb.SelectedItem, Is.EqualTo("b"));
            Assert.That(c1.IsSelected, Is.True);
        });
    }

    [Test]
    public void Selection_FollowsItem_WhenContainerRecycledAndRebound()
    {
        var lb = MakeListBox("a", "b", "c", "d", "e");
        var g = lb.ItemContainerGenerator;
        g.SetWindow(0, 1);            // realize items 0,1
        lb.SelectedIndex = 0;        // select "a"
        g.SetWindow(3, 4);           // scroll away: item 0's container is recycled (becomes a donor)
        g.SetWindow(0, 1);           // scroll back: a recycled container is rebound to item 0

        Assert.That(((ListBoxItem)g.ContainerFromIndex(0)).IsSelected, Is.True,
            "a recycled container rebound to the selected item shows selected - selection lives on the item, not the container");
    }

    [Test]
    public void Multiple_Click_TogglesItems_Cumulatively()
    {
        var lb = MakeListBox("a", "b", "c");
        lb.SelectionMode = SelectionMode.Multiple;
        var g = lb.ItemContainerGenerator;
        var c0 = (ListBoxItem)g.Realize(0);
        var c2 = (ListBoxItem)g.Realize(2);

        lb.SelectFromContainer(c0);   // no modifier -> add (not replace)
        lb.SelectFromContainer(c2);

        Assert.Multiple(() =>
        {
            Assert.That(c0.IsSelected, Is.True);
            Assert.That(c2.IsSelected, Is.True);
            Assert.That(lb.SelectedItems, Is.EquivalentTo(new[] { "a", "c" }));
        });

        lb.SelectFromContainer(c0);   // click again toggles it off
        Assert.That(c0.IsSelected, Is.False);
        Assert.That(lb.SelectedItems, Is.EquivalentTo(new[] { "c" }));
    }

    [Test]
    public void Extended_PlainClickReplaces_CtrlClickToggles()
    {
        var lb = MakeListBox("a", "b", "c", "d");
        lb.SelectionMode = SelectionMode.Extended;
        var g = lb.ItemContainerGenerator;
        var c0 = (ListBoxItem)g.Realize(0);
        var c1 = (ListBoxItem)g.Realize(1);
        var c3 = (ListBoxItem)g.Realize(3);

        lb.SelectFromContainer(c0);                               // plain -> only "a"
        lb.SelectFromContainer(c3, InputModifiers.LeftControl);   // ctrl  -> add "d"
        Assert.That(lb.SelectedItems, Is.EquivalentTo(new[] { "a", "d" }));

        lb.SelectFromContainer(c1);                               // plain -> replace with only "b"
        Assert.Multiple(() =>
        {
            Assert.That(lb.SelectedItems, Is.EquivalentTo(new[] { "b" }));
            Assert.That(c0.IsSelected, Is.False);
            Assert.That(c3.IsSelected, Is.False);
            Assert.That(c1.IsSelected, Is.True);
        });
    }

    [Test]
    public void Extended_ShiftClick_SelectsRangeFromAnchor()
    {
        var lb = MakeListBox("a", "b", "c", "d", "e");
        lb.SelectionMode = SelectionMode.Extended;
        var g = lb.ItemContainerGenerator;
        for (var i = 0; i < 5; i++) g.Realize(i);

        lb.SelectFromContainer((ListBoxItem)g.ContainerFromIndex(1));                            // anchor = "b"
        lb.SelectFromContainer((ListBoxItem)g.ContainerFromIndex(3), InputModifiers.LeftShift);  // range "b".."d"

        Assert.That(lb.SelectedItems, Is.EquivalentTo(new[] { "b", "c", "d" }));
    }

    [Test]
    public void SelectedItems_BoundCollection_ControlMutatesIt_ForViewModel()
    {
        var chosen = new ObservableCollection<object>();   // the view-model's own collection
        var lb = MakeListBox("a", "b", "c");
        lb.SelectionMode = SelectionMode.Multiple;
        lb.SelectedItems = chosen;                          // hand it to the control (a two-way binding does this)
        var c1 = (ListBoxItem)lb.ItemContainerGenerator.Realize(1);

        lb.SelectFromContainer(c1);                         // user selects

        Assert.That(chosen, Does.Contain("b"),
            "the control mutates the SAME collection the view-model holds - no attached-behaviour workaround");
    }

    [Test]
    public void SelectedItems_TwoWayBinding_WinsOverDefault_AndControlUsesIt()
    {
        // Simulate a {Binding} (ValuePriority.Binding): it must take effect, so the control adopts the view-model's own
        // collection. Regression guard: a ctor-seeded default at Local priority would outrank Binding and silently
        // isolate the binding (SelectedItems would never reach the view-model -> the "Selected" list stays empty).
        var chosen = new ObservableCollection<object>();
        var lb = MakeListBox("a", "b", "c");
        lb.SelectionMode = SelectionMode.Multiple;
        lb.SetValue(ListBox.SelectedItemsProperty, chosen, ValuePriority.Binding);

        Assert.That(lb.SelectedItems, Is.SameAs(chosen), "the bound collection IS the control's SelectedItems");

        var c1 = (ListBoxItem)lb.ItemContainerGenerator.Realize(1);
        lb.SelectFromContainer(c1);
        Assert.That(chosen, Does.Contain("b"), "the control mutates the bound collection, not an internal default");
    }

    [Test]
    public void SelectedItems_BoundCollection_ViewModelMutatesIt_DrivesSelection()
    {
        var chosen = new ObservableCollection<object>();
        var lb = MakeListBox("a", "b", "c");
        lb.SelectionMode = SelectionMode.Multiple;
        lb.SelectedItems = chosen;
        var c2 = (ListBoxItem)lb.ItemContainerGenerator.Realize(2);

        chosen.Add("c");                                    // the view-model drives the selection

        Assert.Multiple(() =>
        {
            Assert.That(c2.IsSelected, Is.True, "mutating the bound collection selects the item in the control");
            Assert.That(lb.SelectedItem, Is.EqualTo("c"));
        });
    }

    [Test]
    public void MultiSelection_FollowsItems_AcrossRecycle()
    {
        var lb = MakeListBox("a", "b", "c", "d", "e");
        lb.SelectionMode = SelectionMode.Multiple;
        var g = lb.ItemContainerGenerator;
        g.SetWindow(0, 1);
        lb.SelectFromContainer((ListBoxItem)g.ContainerFromIndex(0));   // select "a"
        lb.SelectFromContainer((ListBoxItem)g.ContainerFromIndex(1));   // select "b"
        g.SetWindow(3, 4);                                              // scroll away (containers recycled)
        g.SetWindow(0, 1);                                              // scroll back (rebound)

        Assert.Multiple(() =>
        {
            Assert.That(((ListBoxItem)g.ContainerFromIndex(0)).IsSelected, Is.True);
            Assert.That(((ListBoxItem)g.ContainerFromIndex(1)).IsSelected, Is.True);
        });
    }
}
