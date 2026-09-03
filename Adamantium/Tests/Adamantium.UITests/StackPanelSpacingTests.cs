using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>The gap a StackPanel puts BETWEEN its children. Three things have to hold, and each of them is a way the
/// same feature is usually got wrong: the gap goes between and not after, a collapsed child takes neither slot nor gap,
/// and the panel REPORTS the space it used - a stack that lays its children out with gaps and then measures itself
/// without them is clipped by exactly the gaps it added.</summary>
[TestFixture]
public class StackPanelSpacingTests
{
    private static StackPanel Stack(double spacing, params double[] heights)
    {
        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = spacing };
        foreach (var h in heights)
        {
            ((IContainer)stack).AddOrSetChildComponent(new Border { Width = 40, Height = h });
        }

        return stack;
    }

    [Test]
    public void GapGoesBetweenChildren_NotAfterTheLast()
    {
        var stack = Stack(10, 20, 20, 20);
        var host = new Border { Width = 200, Height = 400, Child = stack };

        Adamantium.UI.Extensions.WindowExtension.UpdateTree(host);

        var children = stack.Children;
        Assert.That(children[0].Bounds.Y, Is.EqualTo(0).Within(0.01), "first child starts at the top");
        Assert.That(children[1].Bounds.Y, Is.EqualTo(30).Within(0.01), "20 tall + one 10 gap");
        Assert.That(children[2].Bounds.Y, Is.EqualTo(60).Within(0.01), "two items + two gaps");

        // 3 x 20 + 2 x 10 = 80. Eighty, not ninety: there is no gap after the last child.
        Assert.That(stack.DesiredSize.Height, Is.EqualTo(80).Within(0.01));
    }

    [Test]
    public void ACollapsedChildTakesNeitherSlotNorGap()
    {
        var stack = Stack(10, 20, 20, 20);
        stack.Children[1].Visibility = Visibility.Collapsed;
        var host = new Border { Width = 200, Height = 400, Child = stack };

        Adamantium.UI.Extensions.WindowExtension.UpdateTree(host);

        // The survivors sit as if the middle one had never been declared - one gap between them, not two.
        Assert.That(stack.Children[2].Bounds.Y, Is.EqualTo(30).Within(0.01));
        Assert.That(stack.DesiredSize.Height, Is.EqualTo(50).Within(0.01), "2 x 20 + one gap");
    }

    [Test]
    public void NoSpacing_LeavesTheStackExactlyAsItWas()
    {
        var stack = Stack(0, 20, 20, 20);
        var host = new Border { Width = 200, Height = 400, Child = stack };

        Adamantium.UI.Extensions.WindowExtension.UpdateTree(host);

        Assert.That(stack.Children[1].Bounds.Y, Is.EqualTo(20).Within(0.01));
        Assert.That(stack.DesiredSize.Height, Is.EqualTo(60).Within(0.01));
    }
}
