using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

// A ContextMenu authored INLINE (a named part of a template, so the control can open it) must occupy no space where it
// stands: its rows live in the popup overlay. It measures to zero already, but a stretching slot arranged it to the whole
// cell, and last-in-z-order it then ate every press meant for what it covered.
[TestFixture]
public class ContextMenuFootprintTests
{
    private static (Button button, ContextMenu menu, Window window) Row()
    {
        var button = new Button { Width = 100, Height = 26 };
        var menu = new ContextMenu();
        var grid = new Grid();
        grid.Children.Add(button);
        grid.Children.Add(menu);   // after the button, as a named overflow part is

        var window = new Window { Width = 300, Height = 100, Content = grid };
        for (var i = 0; i < 5; i++) WindowExtension.UpdateTree(window);

        return (button, menu, window);
    }

    [Test]
    public void AnInlineMenuTakesNoSpace()
    {
        var (_, menu, _) = Row();

        Assert.That(menu.Bounds.Size, Is.EqualTo(Size.Zero));
    }

    [Test]
    public void AnInlineMenuDoesNotSwallowThePressBeneathIt()
    {
        var (button, _, window) = Row();
        var middle = new Vector2(button.Bounds.X + button.Bounds.Width / 2, button.Bounds.Y + button.Bounds.Height / 2);

        var hit = InputExtensions.HitTest(window, middle);

        Assert.That(hit, Is.Not.Null, "the press must reach the row under the menu");
        Assert.That(IsSelfOrAncestor(button, hit), Is.True, $"expected the button, routed to {hit.GetType().Name}");
    }

    private static bool IsSelfOrAncestor(IUIComponent expected, IUIComponent hit)
    {
        for (var node = hit; node != null; node = node.VisualParent)
        {
            if (ReferenceEquals(node, expected)) return true;
        }

        return false;
    }
}
