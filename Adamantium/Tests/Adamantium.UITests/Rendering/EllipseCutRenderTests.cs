using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Fonts;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry.Shapes;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering;
using Adamantium.UI.Rendering.Payloads;
using Adamantium.Vulkan.Core;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// A SECTOR and a SEGMENT are not shapes of their own: they are the ellipse this batch already draws, with a straight
/// boundary added, and the field intersects the two. So they batch - no wedge mesh, no second pass, no collector beside
/// the one that draws circles.
/// <para>What each test is for: the cut lands in the right QUADRANT (a sign slip in the angle survives any test that only
/// counts pixels); the two closings differ where they must (a sector keeps the centre, a segment does not); the batch and
/// the tessellated fallback cut at the same place; and what the batch cannot express is REFUSED rather than drawn wrong.
/// The angles are the tessellator's: degrees of the parametric angle, and UI space has y DOWN, so 0..90 sweeps from the
/// right edge to the BOTTOM.</para>
/// </summary>
[TestFixture]
[Category("Gpu")]
public class EllipseCutRenderTests
{
    private const int Dim = 64;
    private const int Half = Dim / 2;

    private static byte[] Render(double startAngle, double sweepAngle, EllipseType type, bool batched = true,
        Pen pen = null, Brush fill = null, double ringThickness = 0)
    {
        fill ??= Brushes.White;
        var wasEnabled = EllipseBatchCollector.Enabled;
        EllipseBatchCollector.Enabled = batched;
        try
        {
            var device = GpuTestDevice.Device;
            var factory = new RenderUnitFactory(device, new StubResourceFactory());
            using var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Black };

            var stage = new TestControl { Bounds = new Rect(0, 0, Dim, Dim), RenderSize = new Size(Dim, Dim) };
            stage.RenderAction = s => s.DrawEllipse(new Rect(0, 0, Dim, Dim), fill, startAngle, sweepAngle, type, pen, ringThickness);

            var root = new VisualRoot(stage, Dim, Dim);
            Assert.That(renderer.RenderFrame(root), Is.True, "off-screen frame must render");
            RenderDirty.Clear();

            using var img = renderer.RenderTarget.ResolveTexture.ReadbackToImage();
            var bytes = new byte[(int)img.TotalSizeInBytes];
            Marshal.Copy(img.DataPointer, bytes, 0, bytes.Length);
            return bytes;
        }
        finally
        {
            EllipseBatchCollector.Enabled = wasEnabled;
        }
    }

    private static bool IsLit(byte[] px, int x, int y)
    {
        var i = (y * Dim + x) * 4;
        return px[i] > 128 || px[i + 1] > 128 || px[i + 2] > 128;
    }

    // One probe per quadrant, halfway out from the centre - well inside the ellipse, well away from every boundary.
    private static (bool right_down, bool left_down, bool left_up, bool right_up) Quadrants(byte[] px)
    {
        const int off = Dim / 5;
        return (
            IsLit(px, Half + off, Half + off),
            IsLit(px, Half - off, Half + off),
            IsLit(px, Half - off, Half - off),
            IsLit(px, Half + off, Half - off));
    }

    [Test]
    public void AQuarterSector_FillsThatQuarterAndNoOther()
    {
        var q = Quadrants(Render(0, 90, EllipseType.Sector));

        Assert.Multiple(() =>
        {
            Assert.That(q.right_down, Is.True, "0..90 sweeps from the right edge to the bottom (y is DOWN)");
            Assert.That(q.left_down, Is.False, "the other three quarters are outside the wedge");
            Assert.That(q.left_up, Is.False);
            Assert.That(q.right_up, Is.False);
        });
    }

    // The start angle has to be honoured, not just the sweep: this is the same wedge moved a half-turn.
    [Test]
    public void TheStartAngleMovesTheWedge()
    {
        var q = Quadrants(Render(180, 90, EllipseType.Sector));

        Assert.Multiple(() =>
        {
            Assert.That(q.left_up, Is.True, "180..270 sweeps from the left edge to the top");
            Assert.That(q.right_down, Is.False, "and the quarter 0..90 fills must now be empty");
        });
    }

    // The two closings differ exactly at the CENTRE: a sector reaches it, a segment is cut off by its chord. Testing
    // anything else about them would pass for both.
    [Test]
    public void ASectorKeepsTheCentre_ASegmentDoesNot()
    {
        var sector = Render(0, 90, EllipseType.Sector);
        var segment = Render(0, 90, EllipseType.EdgeToEdge);

        Assert.Multiple(() =>
        {
            Assert.That(IsLit(sector, Half + 2, Half + 2), Is.True, "a sector is closed through the centre");
            Assert.That(IsLit(segment, Half + 2, Half + 2), Is.False, "a segment is closed by the chord, short of the centre");
            // ...and both still fill the rim of their quarter, or the test above would pass on an empty picture.
            Assert.That(IsLit(segment, Half + Dim / 4, Half + Dim / 4), Is.True, "the segment still fills out to the arc");
        });
    }

    // A cut that more than half-turns is the other side of the wedge arithmetic (a union of half-planes rather than an
    // intersection), which is where a naive angle test inverts.
    [Test]
    public void AThreeQuarterSector_LeavesExactlyOneQuarterEmpty()
    {
        var q = Quadrants(Render(0, 270, EllipseType.Sector));

        Assert.Multiple(() =>
        {
            Assert.That(q.right_down, Is.True);
            Assert.That(q.left_down, Is.True);
            Assert.That(q.left_up, Is.True);
            Assert.That(q.right_up, Is.False, "0..270 stops one quarter short");
        });
    }

    // The whole point of doing it here: a sector must not leave the batch. If it fell back the picture would still be
    // right and the cost would silently be a class higher.
    [Test]
    public void ASectorIsTakenByTheBatch()
    {
        var sector = new EllipsePayload(Brushes.White, new Rect(0, 0, Dim, Dim), 0, 90, EllipseType.Sector, null);
        var segment = new EllipsePayload(Brushes.White, new Rect(0, 0, Dim, Dim), 30, 200, EllipseType.EdgeToEdge, null);

        Assert.Multiple(() =>
        {
            Assert.That(EllipseBatchCollector.WantsBatch(sector), Is.True, "a wedge is the same field with a straight edge");
            Assert.That(EllipseBatchCollector.WantsBatch(segment), Is.True, "and so is a chord");
        });
    }

    // NEGATIVE: what the batch cannot express, it must decline - not approximate. A negative sweep is mirrored by the
    // tessellator in a way the instance does not describe, and dashes are placed by arc length along the ELLIPSE, which is
    // not the outline a cut shape has.
    [Test]
    public void WhatTheInstanceCannotDescribe_IsRefused()
    {
        var backwards = new EllipsePayload(Brushes.White, new Rect(0, 0, Dim, Dim), 0, -90, EllipseType.Sector, null);
        var dashed = new EllipsePayload(Brushes.White, new Rect(0, 0, Dim, Dim), 0, 90, EllipseType.Sector,
            new Pen(Brushes.Red, 2, dashStrokeArray: [4.0, 2.0]));

        Assert.Multiple(() =>
        {
            Assert.That(EllipseBatchCollector.WantsBatch(backwards), Is.False, "a negative sweep goes to the tessellator");
            Assert.That(EllipseBatchCollector.WantsBatch(dashed), Is.False, "a dashed cut shape goes to the tessellator");
        });
    }

    // Batch and fallback must cut the same shape. The angles are chosen so no probe sits ON a boundary ray: the probes are
    // the 45-degree diagonals, and 30..170 keeps every one of them well inside or well outside. A wedge whose edge runs
    // through a probe measures the two paths against a rounding decision, not against each other - which is exactly what
    // this test failed on first (45..225 put the start ray through the first probe).
    [Test]
    public void BatchedAndTessellatedCutTheSameWedge()
    {
        var batched = Quadrants(Render(30, 170, EllipseType.Sector));
        var perUnit = Quadrants(Render(30, 170, EllipseType.Sector, batched: false));

        Assert.That(batched, Is.EqualTo(perUnit), "the SDF cut and the tessellated wedge must agree on what is inside");
    }

    // A STROKED edge-to-edge arc is an OPEN contour: a ribbon along the arc with two ends. It must not be outlined across
    // its chord, which is what a ring gauge looked like when fill and stroke shared one distance - a wedge with a straight
    // line drawn across it. The tessellator states the same rule (`isClosed` only for a full ellipse or a Sector).
    [Test]
    public void AStrokedArc_IsOpen_NotOutlinedAcrossItsChord()
    {
        var pen = new Pen(Brushes.White, 4);
        // A ring gauge has NO fill - only the ribbon. With a fill the segment would legitimately cover its own chord, and
        // the test would be asking about the wrong shape.
        var px = Render(0, 90, EllipseType.EdgeToEdge, batched: true, pen: pen, fill: Brushes.Transparent);

        // Midway along the chord between the two ends of a quarter arc, and midway along the arc itself.
        const int chord = Half + Half / 2;
        var arcAt = (int)(Half + Half * 0.707);

        Assert.Multiple(() =>
        {
            Assert.That(IsLit(px, arcAt, arcAt), Is.True, "the ribbon has to be there at all");
            Assert.That(IsLit(px, chord, chord), Is.False, "nothing may be drawn along the chord - the contour is open");
            Assert.That(IsLit(px, Half + 2, Half + 2), Is.False, "and nothing at the centre either: no radii, no wedge");
        });
    }

    // ...while a SECTOR is closed, so its stroke DOES run along both radii - the case the split must not break in the
    // other direction.
    [Test]
    public void AStrokedSector_IsClosed_AndStrokesItsRadii()
    {
        var pen = new Pen(Brushes.White, 4);
        var px = Render(0, 90, EllipseType.Sector, batched: true, pen: pen, fill: Brushes.Transparent);

        Assert.Multiple(() =>
        {
            Assert.That(IsLit(px, Half + Half / 2, Half + 1), Is.True, "the radius along the start angle is stroked");
            Assert.That(IsLit(px, Half + 1, Half + Half / 2), Is.True, "and so is the one along the end angle");
        });
    }

    // A WHOLE sweep has no cut, whatever the start angle says. With a non-zero start the two bounding rays of the wedge
    // land on the SAME ray, and anti-aliasing an edge that is not there left a one-pixel seam running out from the centre -
    // visible, and a disagreement with the tessellated path, which closes the contour whenever |sweep| >= 360.
    [Test]
    public void AWholeSweepFromANonZeroStart_HasNoSeam()
    {
        var px = Render(45, 360, EllipseType.Sector);

        // Along the start ray, from just outside the centre to just inside the rim: every pixel FULLY painted. The
        // threshold is deliberately high - a seam shows up as a half-covered pixel, which a "lit or not" test lets past.
        // The ray is DIAGONAL, so it reaches the rim at Half/sqrt(2) steps, not at Half - walking further only measures
        // the rim's own anti-aliasing (which this test first mistook for a seam).
        var lastInside = (int)(Half / 1.4142) - 2;
        for (var step = 3; step < lastInside; step++)
        {
            var i = ((Half + step) * Dim + (Half + step)) * 4;
            Assert.That(px[i + 2], Is.GreaterThan(200),
                $"seam at {step} px along the 45-degree start ray - a whole sweep must not be cut at all");
        }
    }

    [Test]
    public void AWholeSweepFromANonZeroStart_MatchesTheTessellatedPath()
    {
        var batched = Render(45, 360, EllipseType.Sector);
        var perUnit = Render(45, 360, EllipseType.Sector, batched: false);

        Assert.That(Quadrants(batched), Is.EqualTo(Quadrants(perUnit)), "a full ellipse is a full ellipse in both paths");
    }

    // A RING is the same trick inward: the field minus its own inward offset. It makes a ring gauge a SHAPE - thickness in
    // geometry, pen free for an outline - instead of a thick stroke pretending to be one.
    [Test]
    public void ARing_IsHollow_AndAsThickAsAsked()
    {
        const double thickness = 8;
        var px = Render(0, 360, EllipseType.Sector, ringThickness: thickness);

        Assert.Multiple(() =>
        {
            Assert.That(IsLit(px, Half, 3), Is.True, "the band is there at the top of the rim");
            Assert.That(IsLit(px, Half, Half), Is.False, "and the middle is a HOLE - that is what makes it a ring");
            // The band's inner edge sits `thickness` in from the outline, so a probe just inside it is empty and one just
            // outside it is painted. Measured along the vertical, where the rim is exactly at y = 0.
            Assert.That(IsLit(px, Half, (int)thickness - 2), Is.True, "just inside the band");
            Assert.That(IsLit(px, Half, (int)thickness + 3), Is.False, "just past the band's inner edge");
        });
    }

    // A ring plus a sweep is an ANNULAR SECTOR - a donut slice, and what a ring gauge actually is. Its ends are RADIAL
    // whichever closing was asked for: a chord across a band is not a shape anybody means, and radial ends are the only
    // ones the tessellated fallback (outer shape minus inner shape) reproduces exactly.
    [Test]
    public void ARingWithASweep_IsADonutSlice()
    {
        var px = Render(0, 90, EllipseType.Sector, ringThickness: 8);

        Assert.Multiple(() =>
        {
            // On the 45-degree diagonal the band of a 32px radius sits between 24 and 32 from the centre, i.e. 17..23 px
            // along each axis - a probe at Dim/4 (16) would be in the HOLE, which is what this test first measured.
            Assert.That(IsLit(px, Half + 20, Half + 20), Is.True, "the slice fills its quarter of the band");
            Assert.That(IsLit(px, Half, Half), Is.False, "the hole survives the cut");
            Assert.That(IsLit(px, Half - 20, Half - 20), Is.False, "and the other quarters are still out");
        });
    }

    [Test]
    public void ARing_IsHollowInTheTessellatedPathToo()
    {
        var px = Render(0, 360, EllipseType.Sector, batched: false, ringThickness: 8);
        Assert.Multiple(() =>
        {
            Assert.That(IsLit(px, Half, 3), Is.True, "the tessellated band is there");
            Assert.That(IsLit(px, Half, Half), Is.False, "and it has the same hole - a fallback that fills it draws a different shape");
        });
    }

    [Test]
    public void ARing_AgreesWithTheTessellatedFallback()
    {
        var batched = Render(0, 90, EllipseType.Sector, ringThickness: 8);
        var perUnit = Render(0, 90, EllipseType.Sector, batched: false, ringThickness: 8);

        int[] probes = [3, Half - 20, Half, Half + 20, Dim - 4];
        Assert.Multiple(() =>
        {
            foreach (var x in probes)
            {
                foreach (var y in probes)
                {
                    Assert.That(IsLit(batched, x, y), Is.EqualTo(IsLit(perUnit, x, y)), $"at ({x},{y})");
                }
            }
        });
    }

    // A MIRRORED world (ScaleY = -1) is how the ring gauge reverses its winding, so it is the transform this cut has to
    // survive. The tessellated path mirrors the mesh; the batch bakes a box and an angle, and an angle does not mirror by
    // itself - so the two must be measured against each other, not assumed to agree.
    [Test]
    public void AMirroredWorld_SweepsTheSameWayInBothPaths()
    {
        var batched = MirroredQuadrants(batched: true);
        var perUnit = MirroredQuadrants(batched: false);

        Assert.That(batched, Is.EqualTo(perUnit),
            "a vertical flip must reverse the winding the same way whether the arc is batched or tessellated");
    }

    private static (bool right_down, bool left_down, bool left_up, bool right_up) MirroredQuadrants(bool batched)
    {
        var wasEnabled = EllipseBatchCollector.Enabled;
        EllipseBatchCollector.Enabled = batched;
        try
        {
            var device = GpuTestDevice.Device;
            var factory = new RenderUnitFactory(device, new StubResourceFactory());
            using var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Black };

            var stage = new TestControl { Bounds = new Rect(0, 0, Dim, Dim), RenderSize = new Size(Dim, Dim) };
            stage.RenderAction = s => s.DrawEllipse(new Rect(0, 0, Dim, Dim), Brushes.White, 0, 90, EllipseType.Sector);
            // The flip about the box's own centre, which is what the ring's transform amounts to.
            stage.RenderTransform = new Transform { ScaleY = -1.0, RotationCenterX = Half, RotationCenterY = Half };

            var root = new VisualRoot(stage, Dim, Dim);
            Assert.That(renderer.RenderFrame(root), Is.True, "off-screen frame must render");
            RenderDirty.Clear();

            using var img = renderer.RenderTarget.ResolveTexture.ReadbackToImage();
            var bytes = new byte[(int)img.TotalSizeInBytes];
            Marshal.Copy(img.DataPointer, bytes, 0, bytes.Length);
            return Quadrants(bytes);
        }
        finally
        {
            EllipseBatchCollector.Enabled = wasEnabled;
        }
    }

    // The unit factory needs one, but nothing here draws a texture or text.
    private sealed class StubResourceFactory : IResourceFactory
    {
        public ITexture CreateTexture(TextureDescription description, byte[] pixelData) => throw new NotSupportedException();
        public ITexture CreateTextureArray(TextureDescription description, IReadOnlyList<byte[]> layers) => throw new NotSupportedException();
        public ITexture ImportSharedSurface(SharedSurfaceDescriptor descriptor) => throw new NotSupportedException();
        public IRenderTarget CreateRenderTarget(uint width, uint height, MSAALevel msaa, SurfaceFormat format, ImageLayout desiredLayout) => throw new NotSupportedException();
        public FontRenderer GetFontRenderer(IGraphicsDevice graphicsDevice) => throw new NotSupportedException();
    }
}
