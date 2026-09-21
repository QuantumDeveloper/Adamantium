using System;
using System.Runtime.InteropServices;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>THE GRID A LONG WAY FROM HOME, in pixels. The lattice repeats every coarse cell, so a camera moved by a
/// whole number of cells must draw the SAME picture - that is the property the phase exists to keep, and the only one
/// that says the dots did not smear.
/// <para>A GPU test because the defect lives in what a float can still resolve once it is on the card: nothing on the
/// CPU can tell a dot from the line it ran into.</para></summary>
[TestFixture]
[Category("Gpu")]
public class CanvasGridFarRenderTests
{
    private const int Dim = 256;
    private const double Spacing = 32;
    private const double Coarsening = 5;
    private const double Scale = 1.0;
    private const double Period = Spacing * Coarsening * Scale;

    private static OffscreenTestRenderer _renderer;

    [OneTimeSetUp]
    public void CreateRenderer()
    {
        var device = GpuTestDevice.Device;
        _renderer = new OffscreenTestRenderer(device, new RenderUnitFactory(device, new DeviceResourceFactory(device)),
            Dim, Dim)
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

    private static byte[] Drawn(Vector2 offset, string save = null)
    {
        var brush = new CanvasGridBrush
        {
            Offset = offset,
            Scale = Scale,
            Spacing = Spacing,
            Coarsening = Coarsening,
            MinPitch = 8,
            MarkSize = 2,
            Marks = CanvasGridMarks.Dots,
            Background = Colors.Black,
            Color = Colors.White,
            AxisColor = Colors.Transparent   // the axes are a separate question; this one is about the lattice
        };

        var control = new TestControl
        {
            RenderAction = s => _ = s.DrawRectangle(brush, new Rect(0, 0, Dim, Dim))
        };

        control.Bounds = new Rect(0, 0, Dim, Dim);
        control.RenderSize = new Size(Dim, Dim);

        var root = new VisualRoot(control, Dim, Dim);

        Assert.That(_renderer.RenderFrame(root), Is.True);

        if (save != null) _renderer.RenderTarget.ResolveTexture.Save(save, ImageFileType.Png);

        using var img = _renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var pixels = new byte[(int)img.TotalSizeInBytes];

        Marshal.Copy(img.DataPointer, pixels, 0, pixels.Length);
        return pixels;
    }

    private static int Marks(byte[] pixels)
    {
        var lit = 0;

        for (var i = 0; i < pixels.Length; i += 4)
        {
            if (pixels[i] > 128) lit++;
        }

        return lit;
    }

    // THE SAME PICTURE. Moved by whole cells the lattice lands exactly where it was, so every pixel must agree - and
    // "every pixel" is the strongest thing that can be said about a grid that used to smear.
    [Test]
    [TestCase(1_000.0)]
    [TestCase(40_000.0)]
    [TestCase(200_000.0)]
    // ...and far enough that the old handover did not merely drift: at this distance the dots had run into blocks
    // covering eighteen times what they should (measured, 2304 pixels against 128).
    [TestCase(2_000_000.0)]
    public void AWholeNumberOfCellsAwayDrawsTheSameGrid(double cells)
    {
        var home = Drawn(new Vector2(13.5, 7.25));
        var far = Drawn(new Vector2(13.5 + Period * cells, 7.25 + Period * cells));

        var different = 0;
        for (var i = 0; i < home.Length; i += 4)
        {
            if (Math.Abs(home[i] - far[i]) > 8) different++;
        }

        Assert.That(different, Is.Zero,
            $"{cells} cells out, {different} pixels of {home.Length / 4} disagree with the same grid at home");
    }

    // ...AND THE MARKS ARE STILL MARKS. A smeared grid does not merely differ - it covers far MORE of the surface,
    // because each dot has run into its neighbours along a row. Counting what is lit says so without an eye.
    [Test]
    public void TheMarksDoNotSmearIntoLines()
    {
        var folder = Environment.GetEnvironmentVariable("ADAM_GRID_DUMP");
        var home = Drawn(new Vector2(13.5, 7.25), folder == null ? null : $"{folder}/grid-home.png");
        var far = Drawn(new Vector2(13.5 + Period * 2_000_000, 7.25 + Period * 2_000_000),
            folder == null ? null : $"{folder}/grid-far.png");

        var atHome = Marks(home);
        var away = Marks(far);

        Assert.That(atHome, Is.GreaterThan(0), "no grid was drawn at all");
        Assert.That(away, Is.EqualTo(atHome).Within(atHome * 0.02),
            $"far from home the marks cover {away} pixels against {atHome} at home - they have run together");
    }
}
