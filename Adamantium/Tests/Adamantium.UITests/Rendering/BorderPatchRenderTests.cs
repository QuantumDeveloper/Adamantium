using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Fonts;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering;
using Adamantium.Vulkan.Core;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// A BORDER lives in a retained instance like any other rect, so it inherits that machinery's failure mode: a slot
/// patched, reused or renumbered by somebody else, after which the shape draws with a neighbour's data until something
/// forces a full walk. That reads as "the border vanished and stayed vanished, then came back when I touched anything
/// else" - the exact report this fixture exists for, and the same class as the skeleton pulse recolouring the wrong card.
/// <para>Each test drives one of the paths that rewrites a retained slot - a paint-only change, a change of the drawn
/// UNIT COUNT next door, and a resize - and asks the same question: does the picture still equal what a full walk draws?
/// A border missing from a patched frame is invisible to a pixel test that only looks at the border itself.</para>
/// </summary>
[TestFixture]
[Category("Gpu")]
public class BorderPatchRenderTests
{
    private const int Dim = 96;
    private const int Rows = 4;
    private const int RowHeight = 20;

    private sealed class Scene : IDisposable
    {
        public OffscreenTestRenderer Renderer;
        public VisualRoot Root;
        public TestControl[] Extras;      // draw nothing until asked - the unit-count change next door
        public TestControl[] Borders;     // one bordered card per row

        public void Draw()
        {
            Assert.That(Renderer.RenderFrame(Root), Is.True, "off-screen frame must render");
            RenderDirty.Clear();
        }

        public void Dispose() => Renderer.Dispose();
    }

    private static TestControl Placed(Rect bounds) =>
        new() { Bounds = bounds, RenderSize = new Size(bounds.Width, bounds.Height) };

    // Rows of bordered cards, each with an empty sibling in front of it. Unequal sides and unequal corners on purpose:
    // that is the case that used to leave the batch entirely, so it is the one whose retained slot is newest.
    private static Scene NewScene()
    {
        var device = GpuTestDevice.Device;
        var factory = new RenderUnitFactory(device, new StubResourceFactory());
        var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Black };

        var stage = Placed(new Rect(0, 0, Dim, Dim));
        var borders = new TestControl[Rows];
        var extras = new TestControl[Rows];
        for (var i = 0; i < Rows; i++)
        {
            var y = i * RowHeight;
            borders[i] = Placed(new Rect(0, y, Dim, RowHeight));
            borders[i].RenderAction = s => s.DrawBorder(Brushes.Blue, new Rect(4, 2, Dim - 8, RowHeight - 4),
                new CornerRadius(6, 0, 6, 0), Brushes.Red, new Thickness(2, 5, 2, 5));
            stage.Add(borders[i]);

            extras[i] = Placed(new Rect(0, y, Dim, RowHeight));
            stage.Add(extras[i]);
        }

        var scene = new Scene { Renderer = renderer, Root = new VisualRoot(stage, Dim, Dim), Extras = extras, Borders = borders };
        scene.Draw();
        return scene;
    }

    private static byte[] Pixels(OffscreenTestRenderer renderer)
    {
        using var img = renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var bytes = new byte[(int)img.TotalSizeInBytes];
        Marshal.Copy(img.DataPointer, bytes, 0, bytes.Length);
        return bytes;
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

    private static void AssertMatchesAFullWalk(Scene scene, byte[] patched, string because)
    {
        RenderDirty.MarkStructural();
        scene.Draw();

        Assert.That(scene.Renderer.Cache.LastFrameReplayed, Is.False, "the reference frame has to actually walk");
        var walked = Pixels(scene.Renderer);
        Assert.That(DifferingPixels(patched, walked), Is.Zero, because + " " + Where(patched, walked));
    }

    // Names the region that differs, and one pixel out of it. A count alone cannot tell "the newcomer landed wrong" from
    // "a card lost its ring" - and those are opposite bugs.
    private static string Where(byte[] a, byte[] b)
    {
        int minX = Dim, minY = Dim, maxX = -1, maxY = -1, sample = -1;
        for (var y = 0; y < Dim; y++)
        {
            for (var x = 0; x < Dim; x++)
            {
                var i = (y * Dim + x) * 4;
                if (a[i] == b[i] && a[i + 1] == b[i + 1] && a[i + 2] == b[i + 2]) continue;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
                if (sample < 0) sample = i;
            }
        }

        if (sample < 0) return "(identical)";
        return $"differs in x {minX}..{maxX}, y {minY}..{maxY}; patched rgb=({a[sample + 2]},{a[sample + 1]},{a[sample]}) walked rgb=({b[sample + 2]},{b[sample + 1]},{b[sample]})";
    }

    private static int BorderPixels(byte[] px)
    {
        var count = 0;
        for (var i = 0; i < px.Length; i += 4)
        {
            if (px[i + 2] > 128 && px[i] < 96) count++;   // BGRA: red ring, blue fill
        }

        return count;
    }

    // A paint-only change on the bordered card itself: its slot is rewritten in place. The ring has to survive being
    // re-baked through the patch path, not just through a walk.
    [Test]
    public void RecolouringABorderedCard_KeepsItsRing()
    {
        using var scene = NewScene();
        var ringBefore = BorderPixels(Pixels(scene.Renderer));
        Assert.That(ringBefore, Is.GreaterThan(0), "the setup must actually draw rings");

        scene.Borders[1].RenderAction = s => s.DrawBorder(Brushes.Blue, new Rect(4, 2, Dim - 8, RowHeight - 4),
            new CornerRadius(6, 0, 6, 0), Brushes.Red, new Thickness(2, 5, 2, 5));
        scene.Borders[1].Invalidate();
        scene.Draw();

        var patched = Pixels(scene.Renderer);
        Assert.That(BorderPixels(patched), Is.EqualTo(ringBefore),
            "a re-baked bordered card must still ring - a lost Inset draws the fill and nothing else");
        AssertMatchesAFullWalk(scene, patched, "the patched frame must be pixel-identical to a full walk");
    }

    // The neighbour's unit COUNT changes (0 -> 1), which is the splice path: runs are excised, appended and renumbered
    // around the borders. A slot that ends up belonging to somebody else is how a border silently loses its ring.
    //
    // This is the one that found the paint-order defect this fixture was written for, and it was never about borders - the
    // same scene with plain rects failed identically. A batch SEGMENT glues every control between two flushes while the op
    // drawing it carries one rank, so a newcomer whose rank lands INSIDE that span had nowhere correct to go: inserted
    // before the op it painted UNDER the card it must cover (measured: rgb 25,115,0 where a walk gives 0,128,0), and
    // inserted after it would cover cards that paint later. The segment is now cut at the newcomer's rank.
    // Until this test the defect was only ever seen as a 1-in-6 flake in ViewportResize_Splices - far too thin a thread
    // to fix a paint-order bug by.
    [Test]
    public void ANeighbourAppearing_LandsOnTopOfTheCard_NotUnderIt()
    {
        using var scene = NewScene();
        Assert.That(BorderPixels(Pixels(scene.Renderer)), Is.GreaterThan(0), "the setup must actually draw rings");

        scene.Extras[2].RenderAction = s => s.DrawRectangle(Brushes.Green, new Rect(0, 0, 8, RowHeight));
        scene.Extras[2].Invalidate();
        scene.Draw();

        var patched = Pixels(scene.Renderer);
        Assert.That(scene.Renderer.Cache.LastFrameReplayed, Is.True,
            "and it must still be a patch - a correct picture bought with a full walk is the other half of this bug");
        // The newcomer paints AFTER its row's card, so it legitimately covers part of that card's ring. What it may not do
        // is differ from what the walk draws, which is what the count alone cannot tell.
        AssertMatchesAFullWalk(scene, patched, "the newcomer must land exactly where a full walk puts it");
    }

    // TWO patches in ONE frame, with a SPLIT between them. The newcomer's placement cuts a recorded segment in two, and
    // the OTHER patch had already been resolved against a segment before that cut (the frame resolves every patch first,
    // then mutates - by design, so a refusal costs nothing). While a segment was named by its POSITION in the draw order,
    // that resolved name meant a different segment after the insert, and the frame re-issued somebody else's layer: the
    // shift-by-one that used to be held off by three synchronised fix-up loops. Segments carry stable ids now, so there is
    // nothing to keep in sync - and this is the test that would have caught a loop nobody remembered to write.
    [Test]
    public void TwoPatchesInOneFrame_AroundASplit_EachReissuesItsOwnLayer()
    {
        using var scene = NewScene();
        Assert.That(BorderPixels(Pixels(scene.Renderer)), Is.GreaterThan(0), "the setup must actually draw rings");

        // The newcomer: rank inside a recorded segment's span, so placing it cuts that segment.
        scene.Extras[1].RenderAction = s => s.DrawRectangle(Brushes.Green, new Rect(0, 0, 8, RowHeight));
        scene.Extras[1].Invalidate();

        // ...and a LATER card whose drawn unit count changes in the same frame, so it goes through the layer re-issue.
        scene.Borders[3].RenderAction = s =>
        {
            s.DrawRectangle(Brushes.Blue, new Rect(4, 2, Dim - 8, RowHeight - 4));
            s.DrawRectangle(Brushes.Yellow, new Rect(6, 4, 10, RowHeight - 8));
        };
        scene.Borders[3].Invalidate();

        scene.Draw();

        var patched = Pixels(scene.Renderer);
        AssertMatchesAFullWalk(scene, patched,
            "each patch must land in its OWN layer, however the split moved the segments around it");
    }

    // ...and again, with the newcomer VANISHING - the other half of the splice, where a run is excised and what follows
    // is renumbered down. Repeated on purpose: the arena reuses freed blocks, so the second lap hands out a USED slot.
    [Test]
    public void ANeighbourComingAndGoing_LeavesEveryRingIntact()
    {
        using var scene = NewScene();
        var ringBefore = BorderPixels(Pixels(scene.Renderer));

        for (var lap = 0; lap < 3; lap++)
        {
            scene.Extras[1].RenderAction = s => s.DrawRectangle(Brushes.Green, new Rect(0, 0, 8, RowHeight));
            scene.Extras[1].Invalidate();
            scene.Draw();

            scene.Extras[1].RenderAction = null;
            scene.Extras[1].Invalidate();
            scene.Draw();

            Assert.That(BorderPixels(Pixels(scene.Renderer)), Is.EqualTo(ringBefore), $"after lap {lap}");
        }

        AssertMatchesAFullWalk(scene, Pixels(scene.Renderer), "three laps of appear/vanish must leave the rings where they were");
    }

    // A RESIZE re-lays out every row and re-bakes their instances - the path that carried today's other report ("dragged a
    // slider and it went"). A border must come out of it with the same ring it went in with.
    [Test]
    public void ResizingTheViewport_KeepsEveryRing()
    {
        using var scene = NewScene();
        var ringBefore = BorderPixels(Pixels(scene.Renderer));

        for (var i = 0; i < Rows; i++)
        {
            var y = i * RowHeight;
            var width = Dim - 8 - i;   // every card a different width, as a size drag gives them
            scene.Borders[i].RenderAction = s => s.DrawBorder(Brushes.Blue, new Rect(4, 2, width, RowHeight - 4),
                new CornerRadius(6, 0, 6, 0), Brushes.Red, new Thickness(2, 5, 2, 5));
            scene.Borders[i].Invalidate();
        }

        scene.Draw();
        var patched = Pixels(scene.Renderer);

        Assert.That(BorderPixels(patched), Is.GreaterThan(ringBefore - Rows * RowHeight),
            "narrowing the cards may shorten the rings a little; losing one outright is the failure this watches for");
        AssertMatchesAFullWalk(scene, patched, "a re-sized bordered card must match what a walk draws");
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
