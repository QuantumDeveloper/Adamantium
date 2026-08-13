using Adamantium.Mathematics;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>The off-screen renderer shares ONE GPU device with the window's render thread, so an off-screen draw has to
/// be handed to that thread rather than submitted by whoever asked. These pin the deferral: the raster fallback for a
/// DrawingImage was rendering synchronously from the loop thread, and its picture came back with one shape wearing
/// another's fill colour on roughly one launch in six.</summary>
[TestFixture]
public class VisualRendererQueueTests
{
    [Test]
    public void RequestRender_TouchesNoDevice_AndDeliversNothingOnTheCallersThread()
    {
        // No device service at all: anything that reached the GPU half here would throw rather than pass.
        var renderer = new VisualRenderer(null, null);
        ImageSource delivered = null;
        var called = false;

        renderer.RequestRender(new Border(), new Size(8, 8), 1.0, Colors.Transparent, image =>
        {
            called = true;
            delivered = image;
        });

        Assert.That(called, Is.False, "the bake must be queued for the render thread, not drawn on the caller's");
        Assert.That(delivered, Is.Null);
    }

    [Test]
    public void RequestRender_IgnoresAnAskItCouldNeverAnswer()
    {
        var renderer = new VisualRenderer(null, null);

        Assert.DoesNotThrow(() => renderer.RequestRender(null, new Size(8, 8), 1.0, null, _ => { }));
        Assert.DoesNotThrow(() => renderer.RequestRender(new Border(), new Size(8, 8), 1.0, null, null));
    }
}
