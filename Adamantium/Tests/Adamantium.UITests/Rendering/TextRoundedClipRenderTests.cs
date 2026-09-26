using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Fonts;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering;
using Adamantium.Vulkan.Core;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>Text under a rounded clip is cut at the corner. FontEffect clips with its own code, not the UI's
/// ClipMath, so only the pixels can show it still cuts.</summary>
[TestFixture]
[Category("Gpu")]
public class TextRoundedClipRenderTests
{
    private const int Dim = 128;
    private const int BoxWidth = 110;
    private const int BoxHeight = 90;
    private const int Radius = 40;

    private static OffscreenTestRenderer _renderer;

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

    [OneTimeSetUp]
    public void CreateRenderer()
    {
        var device = GpuTestDevice.Device;
        _renderer = new OffscreenTestRenderer(device, new RenderUnitFactory(device, new FontResourceFactory()), Dim, Dim)
        {
            ClearColor = Colors.Black
        };
    }

    [OneTimeTearDown]
    public void DisposeRenderer()
    {
        _renderer?.Dispose();
        _renderer = null;
    }

    private static byte[] Drawn(double radius)
    {
        // Lifted so the tops of the glyphs, not the line's empty ascent, fill the corner.
        var text = new TextBlock
        {
            Text = "WWW",
            Foreground = Brushes.White,
            FontSize = 44,
            RenderTransform = new Transform { TranslateY = -24 }
        };
        var box = new Border
        {
            Width = BoxWidth,
            Height = BoxHeight,
            ClipToBounds = true,
            ClipCornerRadius = new CornerRadius(radius),
            Child = text
        };

        var root = new VisualRoot(box, Dim, Dim);
        ((IMeasurableComponent)root).Measure(new Size(Dim, Dim));
        ((IMeasurableComponent)root).Arrange(new Rect(0, 0, Dim, Dim));

        RenderDirty.MarkStructural();
        Assert.That(_renderer.RenderFrame(root), Is.True, "off-screen frame must render");
        RenderDirty.Clear();

        using var img = _renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var pixels = new byte[(int)img.TotalSizeInBytes];
        Marshal.Copy(img.DataPointer, pixels, 0, pixels.Length);
        return pixels;
    }

    private static int Brightness(byte[] pixels, int x, int y)
    {
        var i = (y * Dim + x) * 4;
        return pixels[i] + pixels[i + 1] + pixels[i + 2];
    }

    // Outside the top-left arc by more than the anti-aliased edge.
    private static bool BeyondTheCorner(int x, int y)
    {
        var dx = Radius - (x + 0.5);
        var dy = Radius - (y + 0.5);
        return x < Radius && y < Radius && Math.Sqrt(dx * dx + dy * dy) > Radius + 1.5;
    }

    [Test]
    public void TextIsCutByTheRoundedCorner()
    {
        var square = Drawn(0);
        var rounded = Drawn(Radius);

        var litWithoutRounding = 0;
        var brightestWithRounding = 0;
        for (var y = 0; y < Radius; y++)
        {
            for (var x = 0; x < Radius; x++)
            {
                if (!BeyondTheCorner(x, y))
                {
                    continue;
                }

                if (Brightness(square, x, y) > 150)
                {
                    litWithoutRounding++;
                }

                brightestWithRounding = Math.Max(brightestWithRounding, Brightness(rounded, x, y));
            }
        }

        int minX = Dim, minY = Dim, maxX = -1, maxY = -1;
        for (var y = 0; y < Dim; y++)
        {
            for (var x = 0; x < Dim; x++)
            {
                if (Brightness(square, x, y) > 150)
                {
                    minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                }
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(litWithoutRounding, Is.GreaterThan(0), $"the text never reaches the corner, so nothing is tested; lit box ({minX},{minY})-({maxX},{maxY})");
            Assert.That(brightestWithRounding, Is.LessThan(30), "text shows beyond the rounded corner");
        });
    }

    [Test]
    public void TextAwayFromTheCornersIsUntouched()
    {
        var square = Drawn(0);
        var rounded = Drawn(Radius);

        var lit = 0;
        var differing = 0;
        for (var y = 0; y < BoxHeight; y++)
        {
            for (var x = Radius + 2; x < BoxWidth - Radius - 2; x++)
            {
                if (Brightness(square, x, y) > 150)
                {
                    lit++;
                }

                if (Brightness(square, x, y) != Brightness(rounded, x, y))
                {
                    differing++;
                }
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(lit, Is.GreaterThan(0), "no text between the corners, so nothing is compared");
            Assert.That(differing, Is.Zero, "the rounded clip changed text it does not reach");
        });
    }
}
