using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Templates;
using NUnit.Framework;

namespace Adamantium.UITests;

// The flattened-tree virtualization core: TreeFlattener projects a tree (roots + a children getter + expand state) into a
// flat, display-ordered row list a VirtualizingPanel consumes. These are pure-CPU tests of that projection - flatten,
// splice on expand, drop-subtree on collapse, live reconciliation of a node's children collection - plus the wiring that
// points TreeView.Items at the flat rows. End-to-end viewport realization is the shared ItemsControl pipeline, covered by
// VirtualizingStackPanelRealizesOnlyVisibleWindow.
[TestFixture]
public class TreeViewVirtualizationTests
{
    private sealed class Node
    {
        public Node(string name, params Node[] children)
        {
            Name = name;
            Children = new ObservableCollection<Node>(children);
        }

        public string Name { get; }
        public ObservableCollection<Node> Children { get; }
        public bool Expanded { get; set; }   // persisted expansion (like a view-model's IsExpanded)
        public bool Selected { get; set; }   // persisted selection (like a view-model's IsSelected)
        public override string ToString() => Name;
    }

    private static TreeFlattener Flatten(out ObservableCollection<Node> roots, params Node[] rootNodes)
    {
        roots = new ObservableCollection<Node>(rootNodes);
        var flattener = new TreeFlattener(o => (o as Node)?.Children);
        flattener.SetRoots(roots);
        return flattener;
    }

    private static string[] Names(TreeFlattener f) => f.Rows.Select(r => ((Node)r.Node).Name).ToArray();
    private static int[] Depths(TreeFlattener f) => f.Rows.Select(r => r.Depth).ToArray();
    private static TreeRow Row(TreeFlattener f, string name) => f.Rows.First(r => ((Node)r.Node).Name == name);

    [Test]
    public void RootsFlattenCollapsed()
    {
        var f = Flatten(out _, new Node("a", new Node("a1")), new Node("b"));
        Assert.Multiple(() =>
        {
            Assert.That(Names(f), Is.EqualTo(new[] { "a", "b" }));
            Assert.That(Depths(f), Is.EqualTo(new[] { 0, 0 }));
            Assert.That(Row(f, "a").HasChildren, Is.True, "a has a child -> expandable");
            Assert.That(Row(f, "b").HasChildren, Is.False, "b is a leaf");
        });
    }

    [Test]
    public void ExpandSplicesChildrenAtDepth()
    {
        var f = Flatten(out _, new Node("a", new Node("c"), new Node("d")), new Node("b"));
        f.Expand(Row(f, "a"));
        Assert.Multiple(() =>
        {
            Assert.That(Names(f), Is.EqualTo(new[] { "a", "c", "d", "b" }), "children spliced right after their parent");
            Assert.That(Depths(f), Is.EqualTo(new[] { 0, 1, 1, 0 }), "children one level deeper");
        });
    }

    [Test]
    public void CollapseRemovesWholeVisibleSubtree()
    {
        var f = Flatten(out _, new Node("a", new Node("c", new Node("e")), new Node("d")), new Node("b"));
        f.Expand(Row(f, "a"));
        f.Expand(Row(f, "c"));
        Assert.That(Names(f), Is.EqualTo(new[] { "a", "c", "e", "d", "b" }), "nested expand shows the grandchild");
        f.Collapse(Row(f, "a"));
        Assert.That(Names(f), Is.EqualTo(new[] { "a", "b" }), "collapsing a drops c, e and d - the whole subtree");
    }

    [Test]
    public void ExpandRaisesOneRangeAddNotNPerItem()
    {
        var kids = Enumerable.Range(0, 50).Select(i => new Node("n" + i)).ToArray();
        var f = Flatten(out _, new Node("a", kids));
        var adds = 0;
        NotifyCollectionChangedEventArgs last = null;
        ((INotifyCollectionChanged)f.Rows).CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Add) { adds++; last = e; }
        };
        f.Expand(Row(f, "a"));
        Assert.Multiple(() =>
        {
            Assert.That(adds, Is.EqualTo(1), "one range Add for the whole child run, not 50 - the O(viewport) path");
            Assert.That(last.NewItems.Count, Is.EqualTo(50));
            Assert.That(last.NewStartingIndex, Is.EqualTo(1), "inserted right after the parent row");
        });
    }

    [Test]
    public void LazilyAddedChildReconcilesIntoExpandedBranch()
    {
        var a = new Node("a", new Node("c"));
        var f = Flatten(out _, a);
        f.Expand(Row(f, "a"));
        Assert.That(Names(f), Is.EqualTo(new[] { "a", "c" }));

        a.Children.Add(new Node("x"));   // a directory loading a new child AFTER expand -> observed and spliced in
        Assert.That(Names(f), Is.EqualTo(new[] { "a", "c", "x" }));
    }

    [Test]
    public void RemovingChildDropsItsRowAndSubtree()
    {
        var c = new Node("c", new Node("e"));
        var a = new Node("a", c, new Node("d"));
        var f = Flatten(out _, a);
        f.Expand(Row(f, "a"));
        f.Expand(Row(f, "c"));
        Assert.That(Names(f), Is.EqualTo(new[] { "a", "c", "e", "d" }));

        a.Children.Remove(c);   // c (and its shown child e) leave the flat list; sibling d stays
        Assert.That(Names(f), Is.EqualTo(new[] { "a", "d" }));
    }

    [Test]
    public void CollapsedBranchIsNotObserved()
    {
        var a = new Node("a", new Node("c"));
        var f = Flatten(out _, a);
        // a is collapsed: we only observe expanded branches, so a change to its children must not touch the flat list.
        a.Children.Add(new Node("x"));
        Assert.That(Names(f), Is.EqualTo(new[] { "a" }));
    }

    [Test]
    public void RootsCollectionChangesAreReflected()
    {
        var f = Flatten(out var roots, new Node("a"), new Node("b"));
        roots.Add(new Node("c"));
        Assert.That(Names(f), Is.EqualTo(new[] { "a", "b", "c" }), "a new root appears");

        roots.Clear();
        roots.Add(new Node("z"));
        Assert.That(Names(f), Is.EqualTo(new[] { "z" }), "a reset + add re-roots the tree");
    }

    [Test]
    public void BuildRestoresExpandedNodesSubtree()
    {
        var a = new Node("a", new Node("c"), new Node("d")) { Expanded = true };   // a was left expanded
        var roots = new ObservableCollection<Node>([a, new Node("b")]);
        var f = new TreeFlattener(o => (o as Node)?.Children, o => (o as Node)?.Expanded ?? false);
        f.SetRoots(roots);   // a rebuild (e.g. a recreated view over a persisted VM) restores a's open subtree
        Assert.Multiple(() =>
        {
            Assert.That(Names(f), Is.EqualTo(new[] { "a", "c", "d", "b" }));
            Assert.That(Row(f, "a").IsExpanded, Is.True);
        });
    }

    [Test]
    public void BuildRestoresNestedExpansionRecursively()
    {
        var c = new Node("c", new Node("e")) { Expanded = true };
        var a = new Node("a", c) { Expanded = true };
        var f = new TreeFlattener(o => (o as Node)?.Children, o => (o as Node)?.Expanded ?? false);
        f.SetRoots(new ObservableCollection<Node>([a]));
        Assert.Multiple(() =>
        {
            Assert.That(Names(f), Is.EqualTo(new[] { "a", "c", "e" }), "both levels restored open");
            Assert.That(Depths(f), Is.EqualTo(new[] { 0, 1, 2 }));
        });
    }

    [Test]
    public void BuildRestoresSelectedRows()
    {
        var c = new Node("c") { Selected = true };
        var a = new Node("a", c, new Node("d")) { Expanded = true };
        var f = new TreeFlattener(o => (o as Node)?.Children, o => (o as Node)?.Expanded ?? false, o => (o as Node)?.Selected ?? false);
        f.SetRoots(new ObservableCollection<Node>([a]));   // rebuild restores c's selection from the node
        Assert.Multiple(() =>
        {
            Assert.That(Row(f, "c").IsSelected, Is.True);
            Assert.That(Row(f, "a").IsSelected, Is.False);
            Assert.That(Row(f, "d").IsSelected, Is.False);
        });
    }

    [Test]
    public void TreeViewProjectsFlatRowsIntoItems()
    {
        var roots = new ObservableCollection<Node>([new Node("a", new Node("c"), new Node("d"))]);
        var tree = new TreeView
        {
            ItemTemplate = new HierarchicalDataTemplate(() => new TemplateResult { RootComponent = new Border() })
            {
                ItemsSource = new Binding("Children")
            },
            ItemsSource = roots
        };
        // Items is pointed at the flat rows (not the raw roots): one collapsed root row to start.
        Assert.Multiple(() =>
        {
            Assert.That(tree.Items.Count, Is.EqualTo(1));
            Assert.That(tree.Items[0], Is.InstanceOf<TreeRow>());
            Assert.That(((TreeRow)tree.Items[0]).Node, Is.SameAs(roots[0]));
        });
    }
}
