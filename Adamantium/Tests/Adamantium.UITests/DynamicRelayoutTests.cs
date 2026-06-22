using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Diagnostics;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

// Reproduces the "resizing one control moves/stretches an unrelated element" bug. Drives TWO update frames (the
// dynamic path that static rendering never exercises): lay out, change a control's Width, lay out again, and check
// nothing unrelated jumped. LayoutTrace dumps every measure/arrange/update call so the mechanism is visible at once.
public class DynamicRelayoutTests
{
    // A layout root: a Grid (hosts children directly, no template/theme needed) that is also the visual root so its
    // descendants attach. Mirrors the window's role in WindowExtension's per-component update walk.
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

    [Test]
    public void SiblingResize_DoesNotMoveOrResizeUnrelatedElements()
    {
        var log = new List<string>();
        LayoutTrace.Sink = log.Add;
        LayoutTrace.Enabled = true;
        try
        {
            var root = new TestWindowRoot { Name = "root", Width = 1280, Height = 720, ClientWidth = 1280, ClientHeight = 720 };
            var panel = new Grid { Name = "panel" };                       // the "render target panel" / game host
            var image = new Border { Name = "image", Width = 200, Height = 200, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var button = new Border { Name = "button", Width = 150, Height = 150, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom };
            panel.Children.Add(image);
            panel.Children.Add(button);
            root.Children.Add(panel);

            log.Add("===================== FRAME 1 (initial layout) =====================");
            WindowExtension.UpdateTree(root);
            var imageBounds1 = image.Bounds;
            var panelBounds1 = panel.Bounds;
            log.Add($"=== AFTER FRAME 1: image={image.Bounds} panel={panel.Bounds} button={button.Bounds} ===");

            log.Add("===================== RESIZE button.Width 150 -> 800 =====================");
            button.Width = 800;

            log.Add("===================== FRAME 2 (relayout after resize) =====================");
            WindowExtension.UpdateTree(root);
            log.Add($"=== AFTER FRAME 2: image={image.Bounds} panel={panel.Bounds} button={button.Bounds} ===");

            foreach (var line in log) TestContext.WriteLine(line);
            System.IO.File.WriteAllLines(@"C:\Temp\layout-trace.txt", log);

            Assert.Multiple(() =>
            {
                Assert.That(image.Bounds, Is.EqualTo(imageBounds1), "image moved/resized when the unrelated sibling button was resized");
                Assert.That(panel.Bounds, Is.EqualTo(panelBounds1), "panel (game host) moved/resized when its child button was resized");
            });
        }
        finally
        {
            LayoutTrace.Enabled = false;
            LayoutTrace.Sink = null;
        }
    }
}
