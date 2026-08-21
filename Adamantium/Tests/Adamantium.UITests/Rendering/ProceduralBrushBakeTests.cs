using System;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering;
using Adamantium.UI.Rendering.Payloads;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

// GPU-FREE unit tests for the procedural-brush bake: the conic gradient TYPE, and the pattern/noise CanBatch + BakeItem
// contract the shader relies on (type selector, cell/scale in device px, the FBM noise params). No device is touched -
// PackGeometry and BakeItem are pure CPU, and CanBatch only inspects the brush - so these run in the plain (non-Gpu) pass.
[TestFixture]
public class ProceduralBrushBakeTests
{
    private static RectanglePayload Payload(Brush brush)
        => new RectanglePayload(brush, new Rect(0, 0, 100, 50), new CornerRadius(6), null);

    [Test]
    public void ConicGradient_PacksAsGradientTypeThree()
    {
        var conic = new ConicGradientBrush { Center = new Vector2(0.3f, 0.7f), StartAngle = 90 };
        var type = GradientBake.PackGeometry(conic, out var geom0, out _);
        Assert.Multiple(() =>
        {
            Assert.That(type, Is.EqualTo(3f), "conic is gradient type 3 (not a new pass)");
            Assert.That(geom0.X, Is.EqualTo(0.3f).Within(1e-4f), "centre x -> geom0.x");
            Assert.That(geom0.Y, Is.EqualTo(0.7f).Within(1e-4f), "centre y -> geom0.y");
            Assert.That(geom0.Z, Is.EqualTo(0.25f).Within(1e-4f), "start angle 90deg -> 0.25 turns in geom0.z");
        });
    }

    [Test]
    public void PatternAndNoise_CanBatch_ButSolidGradientAndNullDoNot()
    {
        var collector = new PatternRectCollector();
        Assert.Multiple(() =>
        {
            Assert.That(collector.CanBatch(Payload(new PatternBrush())), Is.True, "a pattern fill batches into the pattern pass");
            Assert.That(collector.CanBatch(Payload(new NoiseBrush())), Is.True, "a noise fill batches into the SAME pass");
            Assert.That(collector.CanBatch(Payload(new SolidColorBrush(new Color(255, 0, 0, 255)))), Is.False, "a solid fill does not");
            Assert.That(collector.CanBatch(Payload(new LinearGradientBrush())), Is.False, "a gradient fill does not");
            Assert.That(collector.CanBatch(Payload(null)), Is.False, "no fill does not");
        });
    }

    // The textured batch takes the SAMPLED fills and nothing else - a procedural one has its own pass, and routing it
    // here would sample a texture that is not there.
    [Test]
    public void TexturedFills_CanBatch_ButProceduralOnesDoNot()
    {
        var collector = new TextureBatchCollector();
        Assert.Multiple(() =>
        {
            Assert.That(collector.CanBatch(Payload(new ImageBrush())), Is.True, "an image fill batches into the textured pass");
            Assert.That(collector.CanBatch(Payload(new NineSliceBrush())), Is.True, "and so does a nine-slice");
            Assert.That(collector.CanBatch(Payload(new PatternBrush())), Is.False, "a pattern fill has its own pass");
            Assert.That(collector.CanBatch(Payload(new SolidColorBrush(new Color(255, 0, 0, 255)))), Is.False);
            Assert.That(collector.CanBatch(Payload(null)), Is.False);
        });
    }

    [Test]
    public void PatternBrush_Bakes_TypeCellAndZeroNoise()
    {
        var brush = new PatternBrush { Pattern = PatternType.Dots, CellSize = 20, HatchAngle = 60, Color1 = new Color(200, 100, 50, 255), Color2 = new Color(10, 20, 30, 255) };
        var ok = PatternRectCollector.BakeItem(Payload(brush), Matrix4x4F.Identity, 1.0, 0, out var item);
        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(item.Params.Y, Is.EqualTo(2f), "type = the PatternType ordinal (Dots = 2)");
            Assert.That(item.Params.Z, Is.EqualTo(20f).Within(1e-4f), "cell = CellSize * sx (sx = 1)");
            Assert.That(item.Params.X, Is.EqualTo(6f).Within(1e-4f), "corner radius carried into params.x");
            // A pattern's noise record carries the HATCH LINE NORMAL, not FBM params: the trig is baked here because the
            // pattern pixel shader is already at the NVVM instruction limit. Only the two FBM slots are unused.
            Assert.That(item.Noise.X, Is.EqualTo((float)Math.Cos(Math.PI / 3)).Within(1e-4f), "cos(HatchAngle) -> Noise.x");
            Assert.That(item.Noise.Y, Is.EqualTo((float)Math.Sin(Math.PI / 3)).Within(1e-4f), "sin(HatchAngle) -> Noise.y");
            Assert.That(item.Noise.Z, Is.EqualTo(0f), "lacunarity is a noise-only slot");
            Assert.That(item.Noise.W, Is.EqualTo(0f), "and so is gain");
        });
    }

    [Test]
    public void NoiseBrush_Bakes_TypeFourAndFbmParams()
    {
        var brush = new NoiseBrush { Scale = 40, Octaves = 5, Seed = 12, Lacunarity = 2.5, Gain = 0.6 };
        var ok = PatternRectCollector.BakeItem(Payload(brush), Matrix4x4F.Identity, 1.0, 0, out var item);
        Assert.Multiple(() =>
        {
            Assert.That(ok, Is.True);
            Assert.That(item.Params.Y, Is.EqualTo(4f), "noise is pattern type 4");
            Assert.That(item.Params.Z, Is.EqualTo(40f).Within(1e-4f), "cell = Scale * sx");
            Assert.That(item.Noise.X, Is.EqualTo(5f), "octaves -> Noise.x");
            Assert.That(item.Noise.Y, Is.EqualTo(12f).Within(1e-4f), "seed -> Noise.y");
            Assert.That(item.Noise.Z, Is.EqualTo(2.5f).Within(1e-4f), "lacunarity -> Noise.z");
            Assert.That(item.Noise.W, Is.EqualTo(0.6f).Within(1e-4f), "gain -> Noise.w");
        });
    }

    [Test]
    public void CellSize_ScalesByWorldDeviceScale()
    {
        var world = Matrix4x4F.Identity;
        world.M11 = 2f;
        world.M22 = 2f;
        var brush = new PatternBrush { CellSize = 16 };
        PatternRectCollector.BakeItem(Payload(brush), world, 1.0, 0, out var item);
        Assert.That(item.Params.Z, Is.EqualTo(32f).Within(1e-4f), "cell bakes to device px (16 * sx = 2)");
    }

    [Test]
    public void RotatedWorld_IsRejected_ForPerUnitDraw()
    {
        var rotated = Matrix4x4F.Identity;
        rotated.M12 = 0.5f;   // shear/rotation term
        var ok = PatternRectCollector.BakeItem(Payload(new PatternBrush()), rotated, 1.0, 0, out _);
        Assert.That(ok, Is.False, "a rotated/sheared world can't hold an axis-aligned instance -> per-unit");
    }
}
