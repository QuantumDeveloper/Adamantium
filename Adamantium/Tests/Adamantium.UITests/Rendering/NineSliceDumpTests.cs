using System;
using System.Collections.Generic;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Fonts;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Rendering;
using Adamantium.Vulkan.Core;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

// TEMPORARY: renders the sandbox's own nine-slice demo cases with the sandbox's own texture and writes them out, so the
// result can be LOOKED at instead of described.
[TestFixture]
[Category("Gpu")]
[Explicit]
public class NineSliceDumpTests
{
    private sealed class Factory : IResourceFactory
    {
        private readonly IGraphicsDevice _device;
        public Factory(IGraphicsDevice device) => _device = device;
        public ITexture CreateTexture(TextureDescription description, byte[] pixelData) => _device.CreateTexture(description, pixelData);
        public ITexture CreateTextureArray(TextureDescription description, IReadOnlyList<byte[]> layers) => _device.CreateTextureArray(description, layers);
        public ITexture ImportSharedSurface(SharedSurfaceDescriptor descriptor) => throw new NotSupportedException();
        public IRenderTarget CreateRenderTarget(uint width, uint height, MSAALevel msaa, SurfaceFormat format, ImageLayout desiredLayout) => throw new NotSupportedException();
        public FontRenderer GetFontRenderer(IGraphicsDevice graphicsDevice) => throw new NotSupportedException();
    }

    [Test]
    public void Dump()
    {
        var device = GpuTestDevice.Device;
        var png = @"C:\AdamantiumEngine\Adamantium\Adamantium\Adamantium.Game.Sandbox\Textures\nine-slice-frame.png";
        var bitmap = new BitmapImage(new Uri(png));
        bitmap.EnsureLoadedAsync().GetAwaiter().GetResult();

        void Shot(string name, Brush brush, double w, double h, double offset)
        {
            using var renderer = new OffscreenTestRenderer(device, new RenderUnitFactory(device, new Factory(device)), (uint)(w + 40), (uint)(h + 40))
            {
                ClearColor = new Color(32, 32, 32, 255)
            };
            var control = new TestControl { RenderAction = s => s.DrawRectangle(brush, new Rect(0, 0, w, h)) };
            control.Bounds = new Rect(0, 0, w, h);
            control.RenderSize = new Size(w, h);
            control.RenderTransform = new Transform { TranslateX = offset, TranslateY = offset };
            var root = new VisualRoot(control, (uint)(w + 40), (uint)(h + 40));
            Assert.That(renderer.RenderFrame(root), Is.True);
            renderer.Save($@"C:\Users\admin\AppData\Local\Temp\claude\c--AdamantiumEngine\{name}.png", ImageFileType.Png);
        }

        Shot("ns-repeat-int", new NineSliceBrush(bitmap) { Slice = new Thickness(0.25), EdgeMode = NineSliceEdgeMode.Repeat }, 240, 90, 20);
        Shot("ns-repeat-frac", new NineSliceBrush(bitmap) { Slice = new Thickness(0.25), EdgeMode = NineSliceEdgeMode.Repeat }, 240, 90, 20.37);
        Shot("ns-stretch", new NineSliceBrush(bitmap) { Slice = new Thickness(0.25) }, 240, 90, 20.37);
        Shot("img-plain", new ImageBrush(bitmap), 240, 90, 20.37);
    }
}
