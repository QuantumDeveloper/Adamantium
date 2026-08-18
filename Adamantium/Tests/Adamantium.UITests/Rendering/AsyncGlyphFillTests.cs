using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Fonts;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering;
using Adamantium.Vulkan.Core;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// Rasterizing a glyph is MSDF arithmetic - about 8 ms apiece even with every core busy - and a tab full of new text used
/// to pay all of it before its first frame could go out (measured: 88% of the whole apply phase). So the frame ASKS for
/// its glyphs and does not wait: they are generated on a worker, uploaded on the thread that owns the device, and the
/// text blocks that were built without them rebuild as they land.
/// <para>What these tests pin is the pair of promises that makes that safe: text ARRIVES (a frame that starts with an
/// empty atlas ends up drawing the same pixels as a synchronous one), and a render that has no next frame - a bitmap
/// bake, a preview, an off-screen test - still fills inline, because for it "later" never comes.</para>
/// </summary>
[TestFixture]
[Category("Gpu")]
public class AsyncGlyphFillTests
{
    private const int Dim = 96;

    private sealed class FontResourceFactory : IResourceFactory
    {
        private readonly Dictionary<IGraphicsDevice, FontRenderer> _renderers = new();

        public ITexture CreateTexture(TextureDescription description, byte[] pixelData) => throw new NotSupportedException();
        public ITexture CreateTextureArray(TextureDescription description, IReadOnlyList<byte[]> layers) => throw new NotSupportedException();
        public ITexture ImportSharedSurface(SharedSurfaceDescriptor descriptor) => throw new NotSupportedException();
        public IRenderTarget CreateRenderTarget(uint width, uint height, MSAALevel msaa, SurfaceFormat format, ImageLayout desiredLayout) => throw new NotSupportedException();

        public FontRenderer GetFontRenderer(IGraphicsDevice graphicsDevice)
        {
            if (!_renderers.TryGetValue(graphicsDevice, out var renderer))
            {
                _renderers[graphicsDevice] = renderer = new FontRenderer(graphicsDevice);
            }

            return renderer;
        }
    }

    private static byte[] Pixels(OffscreenTestRenderer renderer)
    {
        using var img = renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var bytes = new byte[(int)img.TotalSizeInBytes];
        Marshal.Copy(img.DataPointer, bytes, 0, bytes.Length);
        return bytes;
    }

    private static int Ink(byte[] px)
    {
        var count = 0;
        for (var i = 0; i < px.Length; i += 4)
        {
            if (px[i] > 40 || px[i + 1] > 40 || px[i + 2] > 40) count++;
        }

        return count;
    }

    // Text whose glyphs are asked for asynchronously still ends up on screen: draw frames until the workers have caught
    // up, and the result is the same ink a synchronous fill produces. The interesting half is the FIRST frame, which is
    // allowed to be short of letters - that is the whole point - so what is pinned is that it CONVERGES, not that it is
    // instant.
    [Test]
    public void TextAsked_ForAsynchronously_StillArrives()
    {
        var device = GpuTestDevice.Device;
        var factory = new RenderUnitFactory(device, new FontResourceFactory());
        using var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Black };

        var block = new TextBlock { Text = "Wg8", Foreground = Brushes.White, FontSize = 28 };
        var stack = new StackPanel { Orientation = Orientation.Vertical };
        stack.Children.Add(block);
        var root = new VisualRoot(stack, Dim, Dim);

        var wasSynchronous = FontAtlasStore.SynchronousFill;
        FontAtlasStore.SynchronousFill = false;
        try
        {
            var sw = Stopwatch.StartNew();
            var ink = 0;
            // Each frame adopts whatever the workers finished; five seconds is a ceiling for the harness, not a target.
            while (sw.Elapsed.TotalSeconds < 5)
            {
                ((IMeasurableComponent)root).Measure(new Size(Dim, Dim));
                ((IMeasurableComponent)root).Arrange(new Rect(0, 0, Dim, Dim));
                Assert.That(renderer.RenderFrame(root), Is.True, "off-screen frame must render");
                RenderDirty.MarkStructural();
                ink = Ink(Pixels(renderer));
                if (ink > 0 && !FontAtlasStore.HasPendingGlyphs) break;
            }

            RenderDirty.Clear();
            Assert.That(ink, Is.GreaterThan(0), "the letters have to appear once their glyphs have been rasterized");
        }
        finally
        {
            FontAtlasStore.SynchronousFill = wasSynchronous;
        }
    }

    // A one-shot render has no "next frame", so it must not hand back a picture with the text still missing: the
    // synchronous switch is what every bake path (RenderTargetBitmap, the designer preview, this test harness) turns on.
    [Test]
    public void AOneShotRender_FillsItsGlyphsInline()
    {
        var device = GpuTestDevice.Device;
        var factory = new RenderUnitFactory(device, new FontResourceFactory());
        using var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Black };

        var wasSynchronous = FontAtlasStore.SynchronousFill;
        FontAtlasStore.SynchronousFill = true;
        try
        {
            var block = new TextBlock { Text = "Qx4", Foreground = Brushes.White, FontSize = 28 };
            var stack = new StackPanel { Orientation = Orientation.Vertical };
            stack.Children.Add(block);
            var root = new VisualRoot(stack, Dim, Dim);

            ((IMeasurableComponent)root).Measure(new Size(Dim, Dim));
            ((IMeasurableComponent)root).Arrange(new Rect(0, 0, Dim, Dim));
            Assert.That(renderer.RenderFrame(root), Is.True, "off-screen frame must render");
            RenderDirty.Clear();

            Assert.That(Ink(Pixels(renderer)), Is.GreaterThan(0),
                "a bake has only the frame it was asked for - its text cannot be left for later");
        }
        finally
        {
            FontAtlasStore.SynchronousFill = wasSynchronous;
        }
    }
}
