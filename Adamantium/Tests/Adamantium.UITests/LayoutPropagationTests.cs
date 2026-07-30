using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

// Does a child's new size reach its ANCESTORS? A re-measured child whose DesiredSize changed makes every cached size
// above it wrong, so the layout manager has to carry that change up. Without it a subtree heals and the tree around it
// keeps arranging by numbers that no longer exist.
[TestFixture]
public class LayoutPropagationTests
{
    private sealed class TestWindowRoot : Grid, IRootVisualComponent
    {
        public Vector2 PointToClient(Vector2 point) => point;
        public Vector2 PointToScreen(Vector2 point) => point;
        public void AttachContextAndInitialize(IUIContext context) { }
        public double Left { get; set; }
        public double Top { get; set; }
        public string Title { get; set; }
        public double ClientWidth { get; set; }
        public double ClientHeight { get; set; }
        public IUIContext UIContext => null;
    }

    // The plain case: a grandchild grows, and the wrapper between it and the root must grow with it.
    [Test]
    public void AGrandchildThatGrows_ResizesItsAncestors()
    {
        var child = new Canvas { Width = 50, Height = 20 };
        var middle = new Border { Child = child };
        var root = new TestWindowRoot { Width = 400, Height = 400, ClientWidth = 400, ClientHeight = 400 };
        root.Children.Add(middle);

        root.Measure(new Size(400, 400));
        root.Arrange(new Rect(0, 0, 400, 400));
        Assert.That(middle.DesiredSize.Width, Is.EqualTo(50).Within(0.5), "the wrapper starts at its child's size");

        child.Width = 200;                                 // AffectsMeasure -> invalidates the CHILD only
        LayoutManager.For(root).ExecuteLayoutPass();        // one frame, exactly as the running app does it

        Assert.Multiple(() =>
        {
            Assert.That(child.DesiredSize.Width, Is.EqualTo(200).Within(0.5), "the child re-measured");
            Assert.That(middle.DesiredSize.Width, Is.EqualTo(200).Within(0.5),
                "and its new size reached the wrapper - a cached ancestor size is wrong the moment a child's changes");
            // Bounds are NOT asserted here: the wrapper sits in a Grid cell and is stretched to it, which is the Grid's
            // business and says nothing about propagation. DesiredSize is the answer that travels.
        });
    }

    // The shape the folded tab strip is: the grandchild is inside a PANEL that sums its children, and the thing above it
    // is what the strip arranges. Same rule, one level deeper - this is the case that stayed broken on screen.
    [Test]
    public void AChildInsidePanel_ThatGrows_ResizesTheOuterWrapper()
    {
        var label = new Canvas { Width = 78, Height = 29 };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(label);
        var tab = new Border { Child = row };
        var strip = new StackPanel { Orientation = Orientation.Vertical };
        strip.Children.Add(tab);

        var root = new TestWindowRoot { Width = 400, Height = 400, ClientWidth = 400, ClientHeight = 400 };
        root.Children.Add(strip);
        root.Measure(new Size(400, 400));
        root.Arrange(new Rect(0, 0, 400, 400));
        Assert.That(tab.DesiredSize.Height, Is.EqualTo(29).Within(0.5));

        // What a turned label does: narrow and tall instead of wide and short.
        label.Width = 17;
        label.Height = 54;
        LayoutManager.For(root).ExecuteLayoutPass();

        Assert.Multiple(() =>
        {
            Assert.That(row.DesiredSize.Height, Is.EqualTo(54).Within(0.5), "the row followed its child");
            Assert.That(tab.DesiredSize.Height, Is.EqualTo(54).Within(0.5), "and the tab followed the row");
            Assert.That(tab.Bounds.Height, Is.EqualTo(54).Within(0.5), "and was arranged that tall");
        });
    }
}
