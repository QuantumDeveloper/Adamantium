using Adamantium.Mathematics;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Extensions;
using Adamantium.UI.Controls.Docking;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// The ARRANGEMENT belongs to the workspace, not to whichever control happens to be showing it. A view rebuilt on
/// re-entry - leaving a tab and coming back, or a theme swap, which rebuilds the same way - hands the workspace a
/// brand-new area with an empty tree, and the outgoing one takes the zones with it.
/// <para>Measured on the stand before the fix: the incoming area reported roots=0, and the region adapter then
/// re-opened every pane in the DEFAULT zone - so two document areas came back as one holding all the tabs. Nothing was
/// pruned or collapsed on the way (no empty group dropped, no split merged); the arrangement was simply never carried
/// across the handover.</para>
/// </summary>
[TestFixture]
public class DockingWorkspaceHandoverTests
{
    /// <summary>A visual root with a client viewport, mirroring a window's role (the shape the other docking tests use).</summary>
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

    // Rooted and laid out, or nothing happens: an area outside a visual tree never builds its layout, so every pane
    // waits in the deferred queue and two different arrangements serialise identically - a test that cannot fail.
    private static DockingArea Rooted(DockingArea area)
    {
        var root = new TestWindowRoot { Width = 1000, Height = 700, ClientWidth = 1000, ClientHeight = 700 };
        root.Children.Add(area);
        WindowExtension.UpdateTree(root);
        WindowExtension.UpdateTree(root);
        return area;
    }

    // Two ZONES rather than one group of two tabs: the split is the whole point of the arrangement being state.
    private static DockingArea SplitArea()
    {
        var area = Rooted(new DockingArea());
        area.AddPane(new Pane { Id = "scene", Header = "scene" }, DockZone.Center);
        area.AddPane(new Pane { Id = "inspector", Header = "inspector" }, DockZone.Right);
        return area;
    }

    [Test]
    public void AViewRebuiltOnReEntry_KeepsTheArrangement()
    {
        var workspace = new DockingWorkspace();

        var first = SplitArea();
        workspace.Attach(first);
        var before = workspace.Save();
        Assert.That(before, Is.Not.Null.And.Not.Empty, "there is an arrangement to lose in the first place");

        // The view is rebuilt: a NEW area handed to the same workspace, with its panes re-opened the way a region
        // adapter re-opens them - all into the DEFAULT zone, knowing nothing about where they used to live.
        var second = Rooted(new DockingArea());
        second.AddPane(new Pane { Id = "scene", Header = "scene" });
        second.AddPane(new Pane { Id = "inspector", Header = "inspector" });
        Assert.That(second.SaveLayout(), Is.Not.EqualTo(before),
            "the rebuilt view really has lost the zones - otherwise there is nothing for the handover to restore");

        workspace.Attach(second);

        Assert.That(workspace.Save(), Is.EqualTo(before),
            "the workspace is where the arrangement lives - a new control must be given it, not start from scratch");
    }
}
