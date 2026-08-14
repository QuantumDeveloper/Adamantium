using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Shapes;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering;
using NUnit.Framework;
using Color = Adamantium.Mathematics.Color;
using Colors = Adamantium.Mathematics.Colors;

namespace Adamantium.UITests;

/// <summary>A brush is not IN the visual tree, so a binding written on one has no place to search FROM. That makes
/// <c>{Binding ElementName=X}</c> - the only way to name a VisualBrush's source in markup - resolve to nothing unless
/// the brush borrows the element that holds it. These pin both halves of that: WHERE the search starts, and WHEN it
/// happens.</summary>
[TestFixture]
public class BrushElementNameBindingTests
{
    // ROOTED on purpose: an element only announces that it joined the TREE when there is a tree to join, and that
    // announcement is what gives a brush its second chance to resolve.
    private static (StackPanel Panel, Border Source) Tree()
    {
        var source = new Border { Name = "Src" };
        var panel = new StackPanel();
        panel.Children.Add(source);
        _ = new VisualRoot(panel, 200, 200);
        return (panel, source);
    }

    private static VisualBrush BoundBrush()
    {
        var brush = new VisualBrush();
        BindingEngine.SetBinding(brush, VisualBrush.VisualProperty, new Binding { ElementName = "Src" });
        return brush;
    }

    // The order MARKUP uses: the brush is handed to its element before that element is added to anything, so the walk
    // at assignment time starts from an element with no ancestors and finds nothing. It has to run again on attach.
    [Test]
    public void ItResolvesOnceTheElementHoldingItJoinsTheTree()
    {
        var (panel, source) = Tree();
        var target = new Rectangle();
        var brush = BoundBrush();

        target.Fill = brush;
        Assert.That(brush.Visual, Is.Null, "the element holding it has no ancestors yet - there is nothing to find");

        panel.Children.Add(target);

        Assert.That(brush.Visual, Is.SameAs(source));
    }

    // A PATH binding needs a DataContext, and an INHERITED one is pushed down without announcing itself per descendant -
    // it also lands AFTER attach. So a brush that only refreshed on attach still read nothing, and every knob written on
    // a brush in markup silently kept its default.
    [Test]
    public void APathBindingOnABrushResolvesWhenTheDataContextArrives()
    {
        var (panel, _) = Tree();
        var target = new Rectangle();
        var brush = new SolidColorBrush();
        BindingEngine.SetBinding(brush, SolidColorBrush.ColorProperty, new Binding { Path = new PropertyPath("Chosen") });

        target.Fill = brush;
        panel.Children.Add(target);
        Assert.That(brush.Color, Is.EqualTo(default(Color)), "no data yet - there is nothing to read");

        panel.DataContext = new Palette { Chosen = Colors.Lime };

        Assert.That(brush.Color, Is.EqualTo(Colors.Lime));
    }

    private sealed class Palette
    {
        public Color Chosen { get; set; }
    }

    // And when the element is already in the tree, the very first resolve finds it - the half that needs the search to
    // start from the OWNER rather than from the brush, which is in no tree at all.
    [Test]
    public void ItResolvesImmediatelyWhenTheElementIsAlreadyInTheTree()
    {
        var (panel, source) = Tree();
        var target = new Rectangle();
        panel.Children.Add(target);

        var brush = BoundBrush();
        target.Fill = brush;

        Assert.That(brush.Visual, Is.SameAs(source));
    }
}
