using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adamantium.Graphics.Core;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering;
using Adamantium.Vulkan.Core;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// Where a newcomer's draw goes in an already-recorded frame (§5a). A recorded batch segment glues every control that fell
/// between two flushes, and what makes that legal is that none of them overlaps another: inside such a set the paint order
/// simply does not matter. So a control that starts drawing needs a place of its OWN only when it overlaps what the set
/// draws - then order decides what covers what. When it does not overlap, it joins the set and the set stays whole.
/// <para>Both halves are pinned here, and both by the same question - does the patched frame equal what a full walk draws -
/// because a cut avoided by mistake and a cut taken by mistake look identical in a segment count and opposite on screen.</para>
/// </summary>
[TestFixture]
[Category("Gpu")]
public class LayerPlacementRenderTests
{
    private const int Dim = 96;
    private const int Cards = 3;
    private const int CardHeight = 10;
    private const int FarY = 70;      // below everything the cards cover - a different region of the window

    private sealed class Scene : IDisposable
    {
        public OffscreenTestRenderer Renderer;
        public VisualRoot Root;
        // Both draw nothing until asked, and both are ranked BETWEEN the cards - what differs is WHERE they are: a control's
        // own footprint is what the placement asks about (a patch is described by the component, not by the rect it happened
        // to draw), so "away" and "over" have to be two different controls.
        public TestControl[] Far;
        public TestControl[] Over;
        public TestControl[] Cards;

        public void Draw()
        {
            Assert.That(Renderer.RenderFrame(Root), Is.True, "off-screen frame must render");
            RenderDirty.Clear();
        }

        public void Dispose() => Renderer.Dispose();
    }

    private static TestControl Placed(Rect bounds) =>
        new() { Bounds = bounds, RenderSize = new Size(bounds.Width, bounds.Height) };

    // Cards stacked in the TOP band (disjoint, so one segment holds them all), each followed by an empty sibling whose rank
    // therefore lands INSIDE that segment's paint span - which is what makes it a placement question at all.
    private static Scene NewScene()
    {
        var device = GpuTestDevice.Device;
        var factory = new RenderUnitFactory(device, new StubResourceFactory());
        var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Black };

        var stage = Placed(new Rect(0, 0, Dim, Dim));
        var cards = new TestControl[Cards];
        var far = new TestControl[Cards];
        var over = new TestControl[Cards];
        for (var i = 0; i < Cards; i++)
        {
            var y = i * (CardHeight + 2);
            cards[i] = Placed(new Rect(0, y, Dim, CardHeight));
            cards[i].RenderAction = s => s.DrawRectangle(Brushes.Blue, new Rect(4, 0, Dim - 8, CardHeight));
            stage.Add(cards[i]);

            far[i] = Placed(new Rect(0, FarY, Dim, CardHeight));
            stage.Add(far[i]);

            over[i] = Placed(new Rect(0, y, 20, CardHeight));
            stage.Add(over[i]);
        }

        var scene = new Scene { Renderer = renderer, Root = new VisualRoot(stage, Dim, Dim), Far = far, Over = over, Cards = cards };
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
        Assert.That(DifferingPixels(patched, Pixels(scene.Renderer)), Is.Zero, because);
    }

    // A newcomer in ANOTHER region of the window: its rank lands inside the cards' segment, but it touches none of them, so
    // its order relative to them cannot matter - and cutting the segment at its rank buys nothing. Before the overlap test,
    // the rank alone decided and every such placement cost a cut.
    [Test]
    public void ANewcomerAwayFromTheLayer_JoinsItWithoutCuttingIt()
    {
        using var scene = NewScene();

        LayerProbe.Reset();
        scene.Far[1].RenderAction = s => s.DrawRectangle(Brushes.Green, new Rect(4, 0, 20, CardHeight));
        scene.Far[1].Invalidate();
        scene.Draw();

        var patched = Pixels(scene.Renderer);
        Assert.That(LayerProbe.Splits, Is.Zero, "nothing it draws is covered by that layer - there is nothing to cut it for");
        Assert.That(LayerProbe.SplitsAvoided, Is.GreaterThan(0), "...and the placement did consider that layer");
        AssertMatchesAFullWalk(scene, patched, "joining a layer must draw what a full walk draws");
    }

    // ...and the other half: a newcomer that DOES cover part of a card. Now order is the whole question - it paints after
    // that card and must cover it - so the layer is cut at its rank, exactly as before.
    [Test]
    public void ANewcomerOverTheLayer_CutsItAndLandsOnTop()
    {
        using var scene = NewScene();

        LayerProbe.Reset();
        scene.Over[1].RenderAction = s => s.DrawRectangle(Brushes.Green, new Rect(0, 2, 20, CardHeight - 4));
        scene.Over[1].Invalidate();
        scene.Draw();

        var patched = Pixels(scene.Renderer);
        Assert.That(LayerProbe.Splits, Is.GreaterThan(0), "it covers a card in that layer, so the layer has to be cut at its rank");
        AssertMatchesAFullWalk(scene, patched, "the newcomer must land exactly where a full walk puts it");
    }

    // The one that must hold whatever the placement decided: a newcomer away from the layer, then one over it, in the same
    // recorded frame's lifetime. Repeated laps, because the arena hands freed blocks back out and the second lap gets a
    // range somebody else used.
    [Test]
    public void PlacementsOfBothKinds_LeaveTheFrameEqualToAWalk()
    {
        using var scene = NewScene();

        for (var lap = 0; lap < 3; lap++)
        {
            scene.Far[0].RenderAction = s => s.DrawRectangle(Brushes.Green, new Rect(4, 0, 20, CardHeight));
            scene.Far[0].Invalidate();
            scene.Draw();

            scene.Over[2].RenderAction = s => s.DrawRectangle(Brushes.Yellow, new Rect(0, 2, 16, CardHeight - 4));
            scene.Over[2].Invalidate();
            scene.Draw();

            var patched = Pixels(scene.Renderer);
            AssertMatchesAFullWalk(scene, patched, $"after lap {lap} the patched frame must equal a walk");

            scene.Far[0].RenderAction = null;
            scene.Far[0].Invalidate();
            scene.Over[2].RenderAction = null;
            scene.Over[2].Invalidate();
            scene.Draw();
        }
    }

    // The unit factory needs one, but nothing here draws a texture or text.
    private sealed class StubResourceFactory : IResourceFactory
    {
        public ITexture CreateTexture(TextureDescription description, byte[] pixelData) => throw new NotSupportedException();
        public ITexture CreateTextureArray(TextureDescription description, IReadOnlyList<byte[]> layers) => throw new NotSupportedException();
        public ITexture ImportSharedSurface(SharedSurfaceDescriptor descriptor) => throw new NotSupportedException();
        public IRenderTarget CreateRenderTarget(uint width, uint height, MSAALevel msaa, SurfaceFormat format, ImageLayout desiredLayout)
            => throw new NotSupportedException();
        public Adamantium.Graphics.Fonts.FontRenderer GetFontRenderer(IGraphicsDevice graphicsDevice) => throw new NotSupportedException();
    }
}
