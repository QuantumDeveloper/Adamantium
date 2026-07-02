using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Templates;
using NUnit.Framework;

namespace Adamantium.UITests;

// Repro for "nothing renders inside panels" on a gallery tab: a fixed-size Border nested in a Grid inside a StackPanel
// must be laid out (non-zero RenderSize), Visible and attached - both when the tree is hosted directly AND when it is
// hosted through a DataTemplate/ContentPresenter (the tab-body path), which is the only structural difference from the
// old (working) directly-nested views.
[TestFixture]
public class PanelRenderTests
{
    private static T FindDescendant<T>(IUIComponent root) where T : class
    {
        foreach (var child in root.VisualChildren)
        {
            if (child is T match) return match;
            var deeper = FindDescendant<T>(child);
            if (deeper != null) return deeper;
        }
        return null;
    }

    private static StackPanel BuildTree(out Border innerBorder)
    {
        innerBorder = new Border { Width = 56, Height = 34, Background = new SolidColorBrush(Colors.Blue) };
        var grid = new Grid { Width = 300, Height = 60 };
        ((IContainer)grid).AddOrSetChildComponent(innerBorder);

        var stack = new StackPanel { Orientation = Orientation.Vertical };
        ((IContainer)stack).AddOrSetChildComponent(new TextBlock { Text = "Grid (3 columns)" });
        ((IContainer)stack).AddOrSetChildComponent(grid);
        return stack;
    }

    [Test]
    public void DirectlyNested_PanelChild_IsLaidOutAndVisible()
    {
        var stack = BuildTree(out var innerBorder);
        var label = (TextBlock)stack.Children[0];
        var grid = (Grid)stack.Children[1];
        var host = new Border { Width = 400, Height = 400, Child = stack };

        Adamantium.UI.Extensions.WindowExtension.UpdateTree(host);

        Assert.Multiple(() =>
        {
            // The render walk + attachment + WorldTransform all key off VisualParent; a Panel adds children via
            // VisualChildrenCollection.InsertRange - if that raises Reset (not per-item Add) SetVisualParent never runs,
            // so a panel child has NO VisualParent -> reports detached -> its render units get freed each frame -> blank.
            Assert.That(grid.VisualParent, Is.SameAs(stack), "grid's VisualParent is the StackPanel");
            Assert.That(innerBorder.VisualParent, Is.SameAs(grid), "innerBorder's VisualParent is the Grid");
            Assert.That(innerBorder.RenderSize, Is.EqualTo(new Size(56, 34)), "innerBorder arranged to its own size");
        });
    }

    [Test]
    public void HostedInDataTemplate_PanelChild_IsLaidOutAndVisible()
    {
        // Mirrors a tab body: PART_SelectedContentHost (a ContentPresenter) renders a data item through a DataTemplate
        // whose root is a ContentControl (a <View>) whose content is the panel tree.
        var template = new DataTemplate(() =>
        {
            var stack = BuildTree(out _);
            var view = new ContentControl { Content = stack };
            return new TemplateResult { RootComponent = view };
        });

        var presenter = new ContentPresenter { Content = new object(), ContentTemplate = template };
        var host = new Border { Width = 400, Height = 400, Child = presenter };

        Adamantium.UI.Extensions.WindowExtension.UpdateTree(host);

        var innerBorder = FindDescendant<Border>(presenter);
        Assert.Multiple(() =>
        {
            Assert.That(innerBorder, Is.Not.Null, "the panel's Border exists in the hosted tree");
            Assert.That(innerBorder?.Visibility, Is.EqualTo(Visibility.Visible), "border visible");
            Assert.That(innerBorder?.RenderSize, Is.EqualTo(new Size(56, 34)), "border arranged to its own size");
        });
    }
}
