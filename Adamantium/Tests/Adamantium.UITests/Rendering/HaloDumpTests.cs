using System;
using System.Runtime.InteropServices;
using System.Text;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>Prints the band around a star as a coarse map, for LOOKING at a reported artefact instead of guessing where
/// it is. Explicit: a diagnostic, not a check.</summary>
[TestFixture]
[Category("Gpu")]
[Explicit]
public class HaloDumpTests
{
    [Test]
    public void DumpStarAura()
    {
        const int dim = 420;
        const int size = 180;
        const int at = 120;

        var c = size / 2.0;
        var star = new Vector2[10];
        for (var i = 0; i < 10; i++)
        {
            var angle = -Math.PI / 2 + i * Math.PI / 5;
            var radius = i % 2 == 0 ? 1.0 : 0.382;
            star[i] = new Vector2(c + Math.Cos(angle) * c * radius, c + Math.Sin(angle) * c * 0.667 * radius);
        }

        var geometry = new StreamGeometry();
        geometry.Open().BeginFigure(star[0], true, true).PolylineLineTo(star[1..], true);

        var control = new TestControl
        {
            RenderAction = s => s.DrawGeometry(new SolidColorBrush(Colors.White), geometry),
            Aura = new Aura { Radius = 60, Spread = 30, Color = Colors.Red, Opacity = 1.0 }
        };
        control.Bounds = new Rect(0, 0, size, size);
        control.RenderSize = new Size(size, size);
        control.RenderTransform = new Transform { TranslateX = at, TranslateY = at };

        var device = GpuTestDevice.Device;
        using var renderer = new OffscreenTestRenderer(device, new RenderUnitFactory(device, new DeviceResourceFactory(device)), dim, dim)
        {
            ClearColor = Colors.Black
        };

        var root = new VisualRoot(control, dim, dim);
        Assert.That(renderer.RenderFrame(root), Is.True);

        using var img = renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var pixels = new byte[(int)img.TotalSizeInBytes];
        Marshal.Copy(img.DataPointer, pixels, 0, pixels.Length);

        var map = new StringBuilder();
        for (var y = 0; y < dim; y += 6)
        {
            for (var x = 0; x < dim; x += 4)
            {
                var i = (y * dim + x) * 4;
                var r = pixels[i + 2];
                var g = pixels[i + 1];
                map.Append(g > 200 ? '#' : r > 200 ? '8' : r > 140 ? '6' : r > 80 ? '4' : r > 30 ? '2' : r > 8 ? '.' : ' ');
            }
            map.Append('\n');
        }
        TestContext.Out.WriteLine(map.ToString());
    }
}
