using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering;
using Adamantium.UI.Rendering.Payloads;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>A PLANE WITH NO EDGES, far from where it started. The camera never moves - it stands at the origin and the
/// scene is offset past it - and that offset is a double, so travelling is exact however far it goes.
/// <para>What is NOT exact is handing that distance to the GPU. A float32 a million pixels out has a quarter of a pixel
/// between one value and the next, sixteen million out it has two: neighbouring fragments resolve to the same world
/// point, so the dots run into lines and a pan steps the lattice instead of sliding it.</para>
/// <para>The grid repeats every cell, so it never needed the distance - only the PHASE within a cell. These pin that
/// the record carries a phase and nothing that grows.</para></summary>
[TestFixture]
public class CanvasGridFarFromHomeTests
{
    private const double Spacing = 32;
    private const double Coarsening = 5;
    private const double Scale = 1.0;

    private static double Period => Spacing * Coarsening * Scale;

    private static CanvasGridItem Baked(Vector2 offset)
    {
        var brush = new CanvasGridBrush
        {
            Offset = offset,
            Scale = Scale,
            Spacing = Spacing,
            Coarsening = Coarsening,
            MinPitch = 8,
            Marks = CanvasGridMarks.Dots
        };

        var payload = new RectanglePayload(brush, new Rect(0, 0, 1200, 800), default, null);

        Assert.That(CanvasGridCollector.BakeItem(payload, Matrix4x4F.Identity, 1.0, 0, -1, out var item), Is.True,
            "the grid refused to bake at all");

        return item;
    }

    // HOWEVER FAR. The record must never carry the distance travelled - that is the whole defect - so whatever the
    // camera has been through, what reaches the shader stays inside one cell.
    [Test]
    [TestCase(0)]
    [TestCase(1_000)]
    [TestCase(1_000_000)]
    [TestCase(16_000_000)]
    [TestCase(-4_500_000)]
    public void WhatReachesTheShaderNeverGrows(double far)
    {
        var item = Baked(new Vector2(far, far * 0.5));

        Assert.Multiple(() =>
        {
            Assert.That(item.Camera.X, Is.InRange(0, (float)Period),
                $"the distance travelled went to the shader: {item.Camera.X}");
            Assert.That(item.Camera.Y, Is.InRange(0, (float)Period),
                $"the distance travelled went to the shader: {item.Camera.Y}");
        });
    }

    // ...AND IT IS THE SAME GRID. A phase is only allowed to be smaller if it draws what the true offset would: the
    // lattice repeats every cell, so a camera moved by whole cells must hand over exactly what it had before.
    [Test]
    public void MovingByWholeCellsChangesNothing()
    {
        var home = Baked(new Vector2(17.5, -4.25));
        var away = Baked(new Vector2(17.5 + Period * 90_000, -4.25 - Period * 12_345));

        Assert.Multiple(() =>
        {
            Assert.That(away.Camera.X, Is.EqualTo(home.Camera.X).Within(0.001),
                "a whole number of cells moved the lattice");
            Assert.That(away.Camera.Y, Is.EqualTo(home.Camera.Y).Within(0.001));
        });
    }

    // A QUARTER PIXEL still reads as a quarter pixel. This is the symptom itself: at the old magnitudes two camera
    // positions a fraction of a pixel apart baked to the SAME float, which is a pan that stands still and then jumps.
    [Test]
    public void ASubPixelPanStillMovesTheGrid()
    {
        var far = Period * 40_000 + 13.0;   // a long way out, and not on a cell boundary
        var still = Baked(new Vector2(far, far));
        var nudged = Baked(new Vector2(far + 0.25, far));

        Assert.That(nudged.Camera.X, Is.Not.EqualTo(still.Camera.X),
            "a quarter-pixel pan came out as no movement at all - the lattice steps instead of sliding");
    }

    // THE AXES are the one thing that wants the origin, and they are penned in rather than dropped: past the element
    // there is no fragment for an axis to cover, so a value beyond it says all a larger one would and stays exact.
    [Test]
    public void TheAxesAreKeptButPennedIn()
    {
        var near = Baked(new Vector2(300, 200));
        var far = Baked(new Vector2(9_000_000, -9_000_000));

        Assert.Multiple(() =>
        {
            Assert.That(near.Clip.Y, Is.EqualTo(300).Within(0.001), "the axes moved while they were on screen");
            Assert.That(near.Clip.Z, Is.EqualTo(200).Within(0.001));

            Assert.That(far.Clip.Y, Is.LessThanOrEqualTo(1200 + 800 + 64),
                "the distance travelled reached the shader through the axes instead");
            Assert.That(far.Clip.Z, Is.GreaterThanOrEqualTo(-(1200 + 800 + 64)));

            // ...and still off the element, which is where the axes really are.
            Assert.That(far.Clip.Y, Is.GreaterThan(1200), "the axis was pulled back onto the element");
        });
    }
}
