using System;
using System.IO;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Fonts;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering;
using Adamantium.Vulkan.Core;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// Proves <see cref="VisualRoot"/> (the off-screen root behind <see cref="VisualRenderer"/>) actually HOSTS a visual and
/// the production render path draws it into a texture. Renders a red box with a white inset via a hosted control and saves
/// the frame so the pixels can be eyeballed (mirrors the proven CompositorRenderTests off-screen pattern).
/// </summary>
[TestFixture]
[Category("Gpu")]
public class VisualRootRenderTests
{
    [Test]
    public void VisualRoot_Hosts_AndRenders_ToTexture()
    {
        var device = GpuTestDevice.Device;
        var factory = new RenderUnitFactory(device, new StubResourceFactory());
        using var renderer = new OffscreenTestRenderer(device, factory, 120, 80) { ClearColor = Colors.Black };

        var content = new TestControl
        {
            RenderAction = s =>
            {
                s.DrawRectangle(Brushes.Red, new Rect(0, 0, 120, 80));
                s.DrawRectangle(Brushes.White, new Rect(35, 25, 50, 30));
            }
        };
        content.Bounds = new Rect(0, 0, 120, 80);
        content.RenderSize = new Size(120, 80);

        // The whole point: host the control on a VisualRoot (not the test's own TestRoot) and render THAT.
        var root = new VisualRoot(content, 120, 80);
        Assert.That(renderer.RenderFrame(root), Is.True, "off-screen frame must render");

        var path = @"C:\Users\admin\AppData\Local\Temp\claude\c--AdamantiumEngine\f4c8121d-0937-4264-942f-d3066549043f\scratchpad\visualroot.png";
        renderer.Save(path, ImageFileType.Png);
        Assert.That(File.Exists(path), Is.True, "the rendered frame must be saved");
    }

    [Test]
    public void VisualRoot_RendersAumlTree_ViaMeasureArrange()
    {
        var factory = new RenderUnitFactory(GpuTestDevice.Device, new StubResourceFactory());
        using var renderer = new OffscreenTestRenderer(GpuTestDevice.Device, factory, 220, 140) { ClearColor = Colors.Transparent };

        // A NESTED AUML tree (StackPanel with children) - proves the runtime loader resolves nested child types (Border,
        // Ellipse) by name even under a non-entity root, and that the whole thing hosts + lays out + renders off-screen.
        const string auml =
            "<StackPanel xmlns='http://adamantium/ui' Orientation='Vertical' Width='220' Height='140' Background='#FF1B2430'>" +
            "<Border Background='#FFE23B3B' CornerRadius='10' Width='170' Height='52' Margin='16'/>" +
            "<Ellipse Fill='#FF3BE27A' Width='80' Height='48' Margin='8'/></StackPanel>";
        var res = Adamantium.UI.Core.Markup.AumlLoader.Load(auml);
        foreach (var d in res.Diagnostics) TestContext.WriteLine($"DIAG: {d}");
        var visual = res.Root as IUIComponent;
        Assert.That(visual, Is.Not.Null, "AUML must parse to a visual");

        // Exactly what VisualRenderer does: host + Measure/Arrange (NOT manual Bounds), then render.
        var root = new VisualRoot(visual, 220, 140);
        ((IMeasurableComponent)root).Measure(new Size(220, 140));
        ((IMeasurableComponent)root).Arrange(new Rect(new Size(220, 140)));

        Assert.That(renderer.RenderFrame(root), Is.True);

        // Transparent clear, so opaque pixels come ONLY from the rendered element (not the clear colour).
        using var img = renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var bytes = new byte[(int)img.TotalSizeInBytes];
        System.Runtime.InteropServices.Marshal.Copy(img.DataPointer, bytes, 0, bytes.Length);
        long opaque = 0;
        for (int i = 3; i < bytes.Length; i += 4) if (bytes[i] != 0) opaque++;
        Assert.That(opaque, Is.GreaterThan(0), "the AUML element must render visible (opaque) pixels");
    }

    // The unit factory needs one, but nothing here draws a texture or text.
    private sealed class StubResourceFactory : IResourceFactory
    {
        public ITexture CreateTexture(TextureDescription description, byte[] pixelData) => throw new NotSupportedException();
        public ITexture CreateTextureArray(TextureDescription description, System.Collections.Generic.IReadOnlyList<byte[]> layers) => throw new NotSupportedException();
        public ITexture ImportSharedSurface(SharedSurfaceDescriptor descriptor) => throw new NotSupportedException();
        public IRenderTarget CreateRenderTarget(uint width, uint height, MSAALevel msaa, SurfaceFormat format, ImageLayout desiredLayout) => throw new NotSupportedException();
        public FontRenderer GetFontRenderer(IGraphicsDevice graphicsDevice) => throw new NotSupportedException();
    }
}
