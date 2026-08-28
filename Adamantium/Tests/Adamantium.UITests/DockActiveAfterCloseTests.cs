using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Docking;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// Which panel is the one being worked in is remembered as a pane ID, because the id outlives the group holding it. A
/// group counts as active while it CONTAINS that id - so closing the active pane leaves the id naming something that no
/// longer exists, no group contains it, and the accent goes out everywhere while the panel underneath is plainly still
/// the one in use. Its own tab strip has already picked the next tab, so the tab looks selected inside a panel that
/// looks inactive; clicking the panel is the only way to get the frame back.
/// </summary>
[TestFixture]
public class DockActiveAfterCloseTests
{
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

    private static TestWindowRoot Rooted(DockingArea area)
    {
        var root = new TestWindowRoot { Width = 1000, Height = 700, ClientWidth = 1000, ClientHeight = 700 };
        root.Children.Add(area);
        WindowExtension.UpdateTree(root);
        WindowExtension.UpdateTree(root);
        return root;
    }

    private static bool AnyGroupActive(DockingArea area)
        => area.GetVisualDescendants().OfType<PaneGroup>().Any(g => g.IsActive);

    [Test]
    public void ClosingTheActivePane_LeavesItsPanelActive()
    {
        var area = new DockingArea();
        var root = Rooted(area);
        area.AddPane(new Pane { Id = "scene", Header = "scene" });
        area.AddPane(new Pane { Id = "game", Header = "game" });
        WindowExtension.UpdateTree(root);

        area.Activate("game");
        WindowExtension.UpdateTree(root);
        Assert.That(AnyGroupActive(area), Is.True, "a panel is active to begin with");

        area.RemovePane("game");
        WindowExtension.UpdateTree(root);
        WindowExtension.UpdateTree(root);

        Assert.That(AnyGroupActive(area), Is.True,
            "the panel is still the one being worked in - the pane that closed took the accent with it");
    }

    /// <summary>...and through the door a USER actually uses. A pane can leave two ways - RemovePane above, and the
    /// close path, which removes from the layout itself - and a rule about what closing means has to hold for both.
    /// Fixing only the first left the tab's own close button behaving exactly as before.</summary>
    [Test]
    public async System.Threading.Tasks.Task ClosingTheActivePaneThroughTheCloseCommand_LeavesItsPanelActive()
    {
        var area = new DockingArea();
        var root = Rooted(area);
        area.AddPane(new Pane { Id = "scene", Header = "scene" });
        area.AddPane(new Pane { Id = "game", Header = "game" });
        WindowExtension.UpdateTree(root);

        area.Activate("game");
        WindowExtension.UpdateTree(root);
        Assert.That(AnyGroupActive(area), Is.True, "a panel is active to begin with");

        await area.ClosePaneAsync("game");
        WindowExtension.UpdateTree(root);
        WindowExtension.UpdateTree(root);

        Assert.That(AnyGroupActive(area), Is.True, "closing a tab must not put the whole panel out");
    }
}
