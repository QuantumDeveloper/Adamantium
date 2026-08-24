using System;
using System.Runtime.InteropServices;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// The soft band under a shape - an aura or a shadow. GPU tests because the whole point is WHERE the colour lands and
/// how it fades, and nothing on the CPU side can see that: the band is the shape's own signed distance read further out,
/// so only the pixels say whether the falloff, the offset and the clip-to-outside are right.
/// </summary>
[TestFixture]
[Category("Gpu")]
public class HaloRenderTests
{
    private const int Dim = 220;
    private const int Size = 80;    // the shape
    private const int At = 70;      // its offset in the target, leaving room for the band on every side

    private static OffscreenTestRenderer _renderer;

    [OneTimeSetUp]
    public void CreateRenderer()
    {
        var device = GpuTestDevice.Device;
        _renderer = new OffscreenTestRenderer(device, new RenderUnitFactory(device, new DeviceResourceFactory(device)), Dim, Dim)
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

    private static byte[] Draw(Aura aura, Shadow shadow, Brush fill = null)
    {
        var brush = fill ?? new SolidColorBrush(Colors.White);
        var control = new TestControl
        {
            RenderAction = s => s.DrawRectangle(brush, new Rect(0, 0, Size, Size)),
            Aura = aura,
            Shadow = shadow
        };
        control.Bounds = new Rect(0, 0, Size, Size);
        control.RenderSize = new Size(Size, Size);
        control.RenderTransform = new Transform { TranslateX = At, TranslateY = At };

        var root = new VisualRoot(control, Dim, Dim);
        Assert.That(_renderer.RenderFrame(root), Is.True);

        using var img = _renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var pixels = new byte[(int)img.TotalSizeInBytes];
        Marshal.Copy(img.DataPointer, pixels, 0, pixels.Length);
        return pixels;
    }

    private static (byte R, byte G, byte B) At_(byte[] p, int x, int y)
    {
        var i = (y * Dim + x) * 4;
        return (p[i + 2], p[i + 1], p[i + 0]);
    }

    [Test]
    public void AnAuraPaintsOutsideTheShape()
    {
        var pixels = Draw(new Aura { Radius = 24, Color = Colors.Red, Opacity = 1.0 }, null);

        var justOutside = At_(pixels, At + Size / 2, At - 4);   // 4 px above the top edge
        Assert.That(justOutside.R, Is.GreaterThan(60), $"nothing was painted beside the shape: {justOutside}");
    }

    // ...and it FADES: the whole difference between a glow and a coloured border is that the far side is fainter.
    [Test]
    public void AnAuraFadesWithDistance()
    {
        var pixels = Draw(new Aura { Radius = 24, Color = Colors.Red, Opacity = 1.0 }, null);

        var near = At_(pixels, At + Size / 2, At - 3);
        var far = At_(pixels, At + Size / 2, At - 18);

        Assert.That(near.R, Is.GreaterThan(far.R + 20), $"the band does not fall off: near {near.R}, far {far.R}");
    }

    [Test]
    public void AnAuraStopsAtItsRadius()
    {
        var pixels = Draw(new Aura { Radius = 16, Color = Colors.Red, Opacity = 1.0 }, null);

        var beyond = At_(pixels, At + Size / 2, At - 30);   // well past the 16 px reach
        Assert.That(beyond.R, Is.LessThan(12), $"the band reaches past its radius: {beyond}");
    }

    // A shadow has a DIRECTION - the one thing that makes it not an aura. With a downward offset the band below the
    // shape must be stronger than the band above it, or the offset was dropped somewhere.
    [Test]
    public void AShadowIsThrownTowardsItsOffset()
    {
        var pixels = Draw(null, new Shadow { OffsetY = 14, BlurRadius = 14, Color = Colors.Red, Opacity = 1.0 });

        var below = At_(pixels, At + Size / 2, At + Size + 8);
        var above = At_(pixels, At + Size / 2, At - 8);

        Assert.That(below.R, Is.GreaterThan(above.R + 40), $"the shadow is not thrown: below {below.R}, above {above.R}");
    }

    // An outer band is NOT painted beneath the shape, as CSS clips a box-shadow. Otherwise a translucent card darkens
    // itself, and the fill it was given is no longer the colour on screen.
    [Test]
    public void AnOuterBandIsNotPaintedUnderTheShape()
    {
        var translucent = new SolidColorBrush(new Color(255, 255, 255, 40));
        var withBand = Draw(new Aura { Radius = 24, Color = Colors.Red, Opacity = 1.0 }, null, translucent);
        var without = Draw(null, null, translucent);

        var inside = At_(withBand, At + Size / 2, At + Size / 2);
        var plain = At_(without, At + Size / 2, At + Size / 2);

        Assert.That(inside.R - plain.R, Is.LessThan(12).And.GreaterThan(-12),
            $"the band shows through the shape: {inside} against {plain}");
    }

    // The band is the LOWEST layer, so it is flushed before every fill in its clip group - which on its own would put it
    // under a PARENT's background too, since that was batched earlier in the walk. Then the glow is drawn and instantly
    // painted over, and nothing is visible at all. The overlap test has to notice and flush the earlier fill first.
    [Test]
    public void ABandIsNotCoveredByItsParentsBackground()
    {
        var parent = new TestControl
        {
            RenderAction = s => s.DrawRectangle(new SolidColorBrush(new Color(16, 20, 24, 255)), new Rect(0, 0, Dim, Dim))
        };
        parent.Bounds = new Rect(0, 0, Dim, Dim);
        parent.RenderSize = new Size(Dim, Dim);

        var child = new TestControl
        {
            RenderAction = s => s.DrawRectangle(new SolidColorBrush(Colors.White), new Rect(0, 0, Size, Size)),
            Aura = new Aura { Radius = 24, Color = Colors.Red, Opacity = 1.0 }
        };
        child.Bounds = new Rect(0, 0, Size, Size);
        child.RenderSize = new Size(Size, Size);
        child.RenderTransform = new Transform { TranslateX = At, TranslateY = At };
        parent.Add(child);

        var root = new VisualRoot(parent, Dim, Dim);
        Assert.That(_renderer.RenderFrame(root), Is.True);

        using var img = _renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var pixels = new byte[(int)img.TotalSizeInBytes];
        Marshal.Copy(img.DataPointer, pixels, 0, pixels.Length);

        var beside = At_(pixels, At + Size / 2, At - 5);
        Assert.That(beside.R, Is.GreaterThan(60),
            $"the parent's background covered the glow: {beside}");
    }

    // The aura is drawn OVER the shadow, because a shadow falls on what is behind the element while an aura is light
    // coming off the element itself. Baked the other way round a dark shadow sits on the glow and eats it.
    [Test]
    public void TheAuraIsDrawnOverTheShadow()
    {
        var pixels = Draw(new Aura { Radius = 26, Color = Colors.Red, Opacity = 1.0 },
            new Shadow { OffsetX = 0, OffsetY = 0, BlurRadius = 26, Color = Colors.Black, Opacity = 1.0 });

        // Both bands are at full strength here, so whichever is drawn LAST is the colour on screen.
        var beside = At_(pixels, At + Size / 2, At - 4);
        Assert.That(beside.R, Is.GreaterThan(90), $"the shadow covered the aura: {beside}");
    }

    // An INNER band lies INSIDE the shape, so it has to be drawn OVER the fill. Under it - which is where every outer
    // band goes - the shape's own fill covers it completely and nothing reaches the screen at all.
    [Test]
    public void AnInnerAuraIsDrawnOverTheFill()
    {
        var pixels = Draw(new Aura { Radius = 20, Color = Colors.Red, Opacity = 1.0, Inner = true }, null);

        var justInside = At_(pixels, At + Size / 2, At + 4);       // a few px inside the top edge
        var middle = At_(pixels, At + Size / 2, At + Size / 2);    // the shape's centre, past the reach

        Assert.That(justInside.R - justInside.B, Is.GreaterThan(60),
            $"the fill covered the inner glow: {justInside}");
        Assert.That(middle.R - middle.B, Is.LessThan(20),
            $"the inner glow did not fade toward the middle: {middle}");
    }

    [Test]
    public void AnInnerShadowIsDrawnOverTheFill()
    {
        var pixels = Draw(null, new Shadow { OffsetX = 0, OffsetY = 0, BlurRadius = 18, Color = Colors.Black, Opacity = 1.0, Inner = true });

        var justInside = At_(pixels, At + Size / 2, At + 4);
        var middle = At_(pixels, At + Size / 2, At + Size / 2);

        Assert.That(justInside.R, Is.LessThan(140), $"the fill covered the inset shadow: {justInside}");
        Assert.That(middle.R, Is.GreaterThan(200), $"the inset shadow did not fade toward the middle: {middle}");
    }

    // ARBITRARY geometry has no closed-form distance, so the band reads one baked per shape. Widening the AA ring
    // instead cannot work: offsetting a contour that far is a Minkowski sum, and its result changes topology - a star's
    // notches close up - which a vertex-expanded ring cannot represent.
    private static byte[] DrawGeometry(Aura aura, Vector2[] points)
    {
        var geometry = new StreamGeometry();
        geometry.Open().BeginFigure(points[0], true, true).PolylineLineTo(points[1..], true);

        var control = new TestControl
        {
            RenderAction = s => s.DrawGeometry(new SolidColorBrush(Colors.White), geometry),
            Aura = aura
        };
        control.Bounds = new Rect(0, 0, Size, Size);
        control.RenderSize = new Size(Size, Size);
        control.RenderTransform = new Transform { TranslateX = At, TranslateY = At };

        var root = new VisualRoot(control, Dim, Dim);
        Assert.That(_renderer.RenderFrame(root), Is.True);

        using var img = _renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var pixels = new byte[(int)img.TotalSizeInBytes];
        Marshal.Copy(img.DataPointer, pixels, 0, pixels.Length);
        return pixels;
    }

    [Test]
    public void AnAuraFollowsATriangle()
    {
        var pixels = DrawGeometry(new Aura { Radius = 22, Color = Colors.Red, Opacity = 1.0 },
            [new Vector2(Size / 2.0, 0), new Vector2(Size, Size), new Vector2(0, Size)]);

        // Just BELOW the triangle's base, where its outline runs - the band has to be there...
        var underBase = At_(pixels, At + Size / 2, At + Size + 5);
        // ...and NOT out by the apex's own height on the left, which is far outside the sloping side.
        var offSlope = At_(pixels, At + 4, At + 6);

        Assert.That(underBase.R, Is.GreaterThan(60), $"no band under the triangle's base: {underBase}");
        Assert.That(offSlope.R, Is.LessThan(30), $"the band ignored the sloping side: {offSlope}");
    }

    // The concave case, and the reason a widened ring was the wrong idea: a star's notches must keep their own band
    // rather than being bridged over.
    [Test]
    public void AnAuraFollowsAStarsNotches()
    {
        const double innerRatio = 0.382;
        var c = Size / 2.0;
        var star = new Vector2[10];
        for (var i = 0; i < 10; i++)
        {
            var angle = -Math.PI / 2 + i * Math.PI / 5;
            var radius = i % 2 == 0 ? 1.0 : innerRatio;
            star[i] = new Vector2(c + Math.Cos(angle) * c * radius, c + Math.Sin(angle) * c * radius);
        }

        var pixels = DrawGeometry(new Aura { Radius = 14, Color = Colors.Red, Opacity = 1.0 }, star);

        // Just past an arm's TIP: the band follows it out.
        var pastTip = At_(pixels, At + (int)c, At - 5);
        // Deep in a notch direction, far from any edge: the band must have faded, or it bridged the concavity.
        var notchAngle = -Math.PI / 2 + Math.PI / 5;
        var notchX = (int)(At + c + Math.Cos(notchAngle) * c * 0.95);
        var notchY = (int)(At + c + Math.Sin(notchAngle) * c * 0.95);
        var inNotch = At_(pixels, notchX, notchY);

        Assert.That(pastTip.R, Is.GreaterThan(60), $"no band past the star's tip: {pastTip}");
        Assert.That(inNotch.R, Is.LessThan(pastTip.R), $"the band is as strong in the notch as at the tip: {inNotch}");
    }

    // Past the baked field's range the field encodes a CLAMP, not a distance. Read as "exactly range" that is a constant
    // everywhere beyond, i.e. a flat plateau of band filling the whole field box and ending in a hard square edge - which
    // is what showed up on a star at a big radius. A solid rim (Spread) is what pushes the plateau above zero, so the
    // test uses one.
    [Test]
    public void AFieldBandHasNoPlateauPastItsRange()
    {
        var pixels = DrawGeometry(new Aura { Radius = 30, Spread = 24, Color = Colors.Red, Opacity = 1.0 },
            [new Vector2(Size / 2.0, 0), new Vector2(Size, Size), new Vector2(0, Size)]);

        // Beyond the band's own reach (24 + 30 px from the outline) but still INSIDE the baked field's box - which is
        // exactly where a clamp-read-as-a-distance lights up, and where the hard square edge came from.
        var beyond = At_(pixels, At - 25, At - 25);
        Assert.That(beyond.R, Is.LessThan(30), $"the band plateaus past its range: {beyond}");
    }

    // A LIVING aura: the reach WANDERS along the outline instead of standing at one distance everywhere. That is the
    // whole difference from a still band, and it is measurable - walk the ring around the shape and the band's edge is
    // no longer at the same place on every side.
    [Test]
    public void ALivingAuraIsUneven()
    {
        var still = Draw(new Aura { Radius = 26, Color = Colors.Red, Opacity = 1.0 }, null);
        var alive = Draw(new Aura { Radius = 26, Turbulence = 1.2, Flow = 0, Detail = 4, Color = Colors.Red, Opacity = 1.0 }, null);

        Assert.That(Spread(alive), Is.GreaterThan(Spread(still) + 20),
            $"the living band is as even as the still one: {Spread(alive)} against {Spread(still)}");
    }

    // A living aura with a PALETTE travels its colours rather than painting one - so more than one hue reaches the band.
    [Test]
    public void ALivingAuraTravelsItsPalette()
    {
        var aura = new Aura
        {
            Radius = 26, Turbulence = 1.2, Flow = 0, Detail = 4, Opacity = 1.0, Color = Colors.Red,
            Palette =
            [
                new GradientStop(Colors.Red, 0.0),
                new GradientStop(Colors.Lime, 1.0)
            ]
        };

        var pixels = Draw(aura, null);

        var reds = 0;
        var greens = 0;
        for (var y = At - 24; y < At + Size + 24; y += 2)
        {
            for (var x = At - 24; x < At + Size + 24; x += 2)
            {
                var p = At_(pixels, x, y);
                if (p.R > 90 && p.G < 50) reds++;
                if (p.G > 90 && p.R < 50) greens++;
            }
        }

        Assert.That(reds, Is.GreaterThan(10), "the palette's first colour never reached the band");
        Assert.That(greens, Is.GreaterThan(10), "the palette's second colour never reached the band");
    }

    // How far apart the band's outer edge sits on different sides of the shape - a still band is the same everywhere, a
    // living one is not.
    private static int Spread(byte[] pixels)
    {
        var min = int.MaxValue;
        var max = 0;
        for (var y = At; y < At + Size; y += 4)
        {
            var reach = 0;
            for (var d = 1; d < 60; d++)
            {
                if (At_(pixels, At - d, y).R > 40) reach = d;
            }
            if (reach < min) min = reach;
            if (reach > max) max = reach;
        }
        return max - min;
    }

    // Switched off, a band keeps its settings and simply is not drawn. Zeroing the radius or the opacity would only fake
    // that, and leave the author somewhere to put the real values while it is off.
    [Test]
    public void ASwitchedOffAuraDrawsNothing()
    {
        var pixels = Draw(new Aura { Radius = 26, Color = Colors.Red, Opacity = 1.0, IsEnabled = false }, null);

        var beside = At_(pixels, At + Size / 2, At - 5);
        Assert.That(beside.R, Is.LessThan(12), $"a switched-off aura still painted: {beside}");
    }

    [Test]
    public void ASwitchedOffShadowDrawsNothing()
    {
        var pixels = Draw(null, new Shadow { OffsetY = 14, BlurRadius = 14, Color = Colors.Red, Opacity = 1.0, IsEnabled = false });

        var below = At_(pixels, At + Size / 2, At + Size + 8);
        Assert.That(below.R, Is.LessThan(12), $"a switched-off shadow still painted: {below}");
    }

    // ...and a LIVING one goes quiet too - it is a different pass, so it needs its own answer rather than inheriting one.
    [Test]
    public void ASwitchedOffLivingAuraDrawsNothing()
    {
        var pixels = Draw(new Aura
        {
            Radius = 26, Turbulence = 1.2, Flow = 0, Detail = 4, Color = Colors.Red, Opacity = 1.0, IsEnabled = false
        }, null);

        var beside = At_(pixels, At + Size / 2, At - 5);
        Assert.That(beside.R, Is.LessThan(12), $"a switched-off living aura still painted: {beside}");
    }

    // Aura AND shadow at once - the case that made them two properties rather than one.
    [Test]
    public void AnAuraAndAShadowBothDraw()
    {
        var pixels = Draw(new Aura { Radius = 20, Color = Colors.Lime, Opacity = 1.0 },
            new Shadow { OffsetY = 18, BlurRadius = 10, Color = Colors.Red, Opacity = 1.0 });

        var above = At_(pixels, At + Size / 2, At - 6);          // only the aura reaches here
        var below = At_(pixels, At + Size / 2, At + Size + 12);  // the shadow is thrown here

        Assert.That(above.G, Is.GreaterThan(40), $"the aura is missing: {above}");
        Assert.That(below.R, Is.GreaterThan(40), $"the shadow is missing: {below}");
    }

    // WHERE a band is drawn is answered by TWO things, exactly as it is for every fill family: the bake folded into the
    // instance, and the transform slot applied on top of it. The bake used to be dropped here and the slot was the
    // band's ONLY address - right while that slot holds the full world, wrong the moment it holds a motion NODE's,
    // because the shape's own place INSIDE the node then has nowhere to live and the band collapses onto the node's
    // origin. Measured as a sliding view's aura jumping to the top-left corner while its shape moved correctly.
    //
    // The claim is deliberately "identical", not "roughly there": a node above a shape is a statement about how the
    // frame is drawn, never about what it looks like.
    [Test]
    public void AnAuraInsideAMotionNode_StaysOnItsShape()
    {
        var plain = DrawNested(node: false);
        var underNode = DrawNested(node: true);

        Assert.That(DifferingPixels(plain, underNode), Is.Zero,
            "a motion node above the shape moved its band away from it");
    }

    // The same shape, at the same world place, twice: once directly under the stage and once under a panel that rides
    // its own slot. Only the shape's offset lives INSIDE the panel, which is the whole point - a node-relative bake of
    // zero would prove nothing.
    private static byte[] DrawNested(bool node)
    {
        var stage = new TestControl { Bounds = new Rect(0, 0, Dim, Dim), RenderSize = new Size(Dim, Dim) };

        var host = stage;
        if (node)
        {
            var panel = new TestControl { Bounds = new Rect(0, 0, Dim, Dim), RenderSize = new Size(Dim, Dim), IsRenderMotionNode = true };
            stage.Add(panel);
            host = panel;
        }

        var shape = new TestControl
        {
            Bounds = new Rect(At, At, Size, Size),
            RenderSize = new Size(Size, Size),
            RenderAction = s => s.DrawRectangle(new SolidColorBrush(Colors.White), new Rect(0, 0, Size, Size)),
            Aura = new Aura { Radius = 24, Color = Colors.Red, Opacity = 1.0 }
        };
        host.Add(shape);

        Assert.That(_renderer.RenderFrame(new VisualRoot(stage, Dim, Dim)), Is.True);

        using var img = _renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var pixels = new byte[(int)img.TotalSizeInBytes];
        Marshal.Copy(img.DataPointer, pixels, 0, pixels.Length);
        return pixels;
    }

    // A RECOLOUR, on the very next frame. The bands are their own records in their own arena, and only the recording
    // walk ever wrote them - so a repaint rewrote the shape's fill and left its aura on the old colour until some
    // unrelated frame happened to walk the scene. "It catches up eventually" is exactly the bug: the colour a hand just
    // chose has to be on screen now, and it must not cost the frame a walk to get there.
    // The same holds for a SHADOW: both sides ride this one collector, split only by which side of the fill they paint.
    [Test]
    public void RecolouringAnAura_ShowsOnTheNextFrame_WithoutAWalk()
    {
        var control = new TestControl
        {
            Bounds = new Rect(At, At, Size, Size),
            RenderSize = new Size(Size, Size),
            RenderAction = s => s.DrawRectangle(new SolidColorBrush(Colors.White), new Rect(0, 0, Size, Size)),
            Aura = new Aura { Radius = 24, Color = Colors.Red, Opacity = 1.0 }
        };

        var stage = new TestControl { Bounds = new Rect(0, 0, Dim, Dim), RenderSize = new Size(Dim, Dim) };
        stage.Add(control);
        var root = new VisualRoot(stage, Dim, Dim);

        Assert.That(_renderer.RenderFrame(root), Is.True);
        RenderDirty.Clear();

        control.Aura = new Aura { Radius = 24, Color = Colors.Lime, Opacity = 1.0 };
        control.Invalidate();
        Assert.That(_renderer.RenderFrame(root), Is.True);

        var patched = Snapshot();
        var beside = At_(patched, At + Size / 2, At - 5);   // in the band, just outside the shape
        Assert.That(_renderer.Cache.LastFrameReplayed, Is.True,
            "a recolour must not cost the frame a walk - the band is patched where it already sits");
        Assert.That(beside.G, Is.GreaterThan(beside.R + 20),
            $"the aura is still painting the old colour a frame later: {beside}");

        RenderDirty.MarkStructural();
        Assert.That(_renderer.RenderFrame(root), Is.True);
        Assert.That(DifferingPixels(patched, Snapshot()), Is.Zero,
            "the patched band must be exactly what a full walk paints");
    }

    private static byte[] Snapshot()
    {
        using var img = _renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var pixels = new byte[(int)img.TotalSizeInBytes];
        Marshal.Copy(img.DataPointer, pixels, 0, pixels.Length);
        return pixels;
    }

    private static int DifferingPixels(byte[] a, byte[] b)
    {
        var count = 0;
        for (var i = 0; i < a.Length; i += 4)
        {
            if (a[i] != b[i] || a[i + 1] != b[i + 1] || a[i + 2] != b[i + 2] || a[i + 3] != b[i + 3]) count++;
        }
        return count;
    }
}
