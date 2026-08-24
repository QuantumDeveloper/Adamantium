using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Fonts;
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
/// An element that MOVES and nothing else. Where a thing sits lives in its transform-table slot, so a move is a slot
/// write - yet the frame carried a single frame-wide "transforms are dirty" flag, and one moved element spoke for the
/// whole scene: dragging a slider thumb over a 22 064-node tab walked all of it, every frame, at 16 fps.
/// <para>Both halves are asserted together, and they have to be: the move must cost a PATCH (or the drag is slow) and
/// the patched picture must equal what a full walk draws (or the move was forgiven without being carried - which is
/// what the earlier attempt at this did, leaving the drag-and-drop gap shut until a walk arrived and then jumping).</para>
/// </summary>
[TestFixture]
[Category("Gpu")]
public class MovedElementPatchRenderTests
{
    private const int Dim = 96;

    private sealed class Scene : IDisposable
    {
        public OffscreenTestRenderer Renderer;
        public VisualRoot Root;
        public TestControl Mover;      // the thumb: moves, draws, clips nothing
        public TestControl Rider;      // its child - it has its OWN slot holding its FULL world, so it only follows
                                       // if the move is carried down the subtree

        public void Draw()
        {
            Assert.That(Renderer.RenderFrame(Root), Is.True, "off-screen frame must render");
            RenderDirty.Clear();
        }

        public void Dispose() => Renderer.Dispose();
    }

    private static TestControl Placed(Rect bounds) =>
        new() { Bounds = bounds, RenderSize = new Size(bounds.Width, bounds.Height) };

    // A still background plus a two-level mover. The background is what makes the assertion about the SCENE rather than
    // about the mover: a walk would redraw it too, so "patched and identical" is the whole claim.
    private static Scene NewScene()
    {
        var device = GpuTestDevice.Device;
        var factory = new RenderUnitFactory(device, new StubResourceFactory());
        var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Black };

        var stage = Placed(new Rect(0, 0, Dim, Dim));
        for (var i = 0; i < 4; i++)
        {
            var still = Placed(new Rect(0, i * 24, Dim, 20));
            still.RenderAction = s => s.DrawRectangle(Brushes.Green, new Rect(0, 0, Dim, 20));
            stage.Add(still);
        }

        var mover = Placed(new Rect(8, 8, 24, 24));
        mover.RenderAction = s => s.DrawRectangle(Brushes.Blue, new Rect(0, 0, 24, 24));
        stage.Add(mover);

        var rider = Placed(new Rect(4, 4, 8, 8));
        rider.RenderAction = s => s.DrawRectangle(Brushes.Red, new Rect(0, 0, 8, 8));
        mover.Add(rider);

        var scene = new Scene { Renderer = renderer, Root = new VisualRoot(stage, Dim, Dim), Mover = mover, Rider = rider };
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

    // THE DRAG. One element changes place; nothing else about the scene changes.
    [Test]
    public void MovingOneElement_PatchesTheFrame_AndLandsWhereAWalkPutsIt()
    {
        using var scene = NewScene();

        scene.Mover.Bounds = new Rect(48, 8, 24, 24);
        scene.Draw();

        var patched = Pixels(scene.Renderer);
        Assert.That(scene.Renderer.Cache.LastFrameReplayed, Is.True,
            "a move is a slot write - it must not cost the whole scene a walk");
        AssertMatchesAFullWalk(scene, patched, "the moved element must be drawn where a full walk draws it");
    }

    // The subtree. A motion node moves its descendants by ONE matrix; an ordinary mover cannot - every element under it
    // holds its own slot carrying its own full world, and each has to be written.
    [Test]
    public void MovingAContainer_CarriesItsChildrenWithIt()
    {
        using var scene = NewScene();

        scene.Mover.Bounds = new Rect(8, 56, 24, 24);
        scene.Draw();

        var patched = Pixels(scene.Renderer);
        Assert.That(scene.Renderer.Cache.LastFrameReplayed, Is.True, "the frame must still patch");
        AssertMatchesAFullWalk(scene, patched, "the child must have moved with its parent, not stayed behind");
    }

    // Repeated steps, as a drag actually arrives - each frame patching on top of the one before it. A slot written from a
    // STALE world memo would drift, and only a sequence shows that.
    [Test]
    public void DraggingItStepByStep_PatchesEveryFrame()
    {
        using var scene = NewScene();

        for (var step = 1; step <= 6; step++)
        {
            scene.Mover.Bounds = new Rect(8 + step * 8, 8, 24, 24);
            scene.Draw();
            Assert.That(scene.Renderer.Cache.LastFrameReplayed, Is.True, $"drag step {step} must patch, not walk");
        }

        AssertMatchesAFullWalk(scene, Pixels(scene.Renderer), "six patched steps must end where one walk would put it");
    }

    // THE DROP GAP. A tile inside a scrolling panel is batched NODE-RELATIVE: its place is in the instance, not in a
    // slot of its own, so writing slots moves everything EXCEPT it. That is exactly what the drag-and-drop demo showed -
    // the labels (per-unit draws, re-pointed at replay) slid to the new slot and the tiles stayed behind. Re-baking the
    // moved subtree answers both, and this is the test that tells them apart.
    [Test]
    public void MovingSomethingInsideAMotionNode_TakesItsBatchedTileWithIt()
    {
        var device = GpuTestDevice.Device;
        var factory = new RenderUnitFactory(device, new StubResourceFactory());
        using var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Black };

        var stage = Placed(new Rect(0, 0, Dim, Dim));
        var panel = Placed(new Rect(0, 0, Dim, Dim));
        panel.IsRenderMotionNode = true;      // the scrolling host: its subtree rides ITS slot
        stage.Add(panel);

        var still = Placed(new Rect(0, 60, Dim, 20));
        still.RenderAction = s => s.DrawRectangle(Brushes.Green, new Rect(0, 0, Dim, 20));
        panel.Add(still);

        var container = Placed(new Rect(8, 8, 24, 24));
        var tile = Placed(new Rect(0, 0, 24, 24));
        tile.RenderAction = s => s.DrawRectangle(Brushes.Blue, new Rect(0, 0, 24, 24));
        container.Add(tile);
        panel.Add(container);

        var scene = new Scene { Renderer = renderer, Root = new VisualRoot(stage, Dim, Dim), Mover = container, Rider = tile };
        scene.Draw();

        container.Bounds = new Rect(48, 8, 24, 24);   // the gap opens: the container slides inside the node
        scene.Draw();

        var patched = Pixels(renderer);
        Assert.That(renderer.Cache.LastFrameReplayed, Is.True, "a tile sliding inside a scrolling panel must still patch");
        AssertMatchesAFullWalk(scene, patched, "the TILE must have moved, not only what is drawn per unit beside it");
    }

    // THE TAB TRANSITION. A slide moves a whole view rigidly, and every view worth sliding has a scroll area somewhere
    // inside it - so "anything under the mover clips" refused every single frame of every switch. A recorded Scissor is
    // the one world-space rect in the stream; it is derived again now instead of costing a re-record, and this asserts
    // the derivation actually lands (a stale one leaves the clipped band behind while its content moves).
    [Test]
    public void MovingAMotionNodeThatClips_TakesItsViewportWithIt()
    {
        var device = GpuTestDevice.Device;
        var factory = new RenderUnitFactory(device, new StubResourceFactory());
        using var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Black };

        var stage = Placed(new Rect(0, 0, Dim, Dim));
        var node = Placed(new Rect(4, 4, 40, 40));
        node.IsRenderMotionNode = true;          // the sliding view
        stage.Add(node);

        var viewport = Placed(new Rect(0, 0, 24, 24));
        viewport.ClipToBounds = true;            // the scroll area inside it
        node.Add(viewport);

        var content = Placed(new Rect(0, 0, 80, 80));   // bigger than the viewport, so the clip is visible at all
        content.RenderAction = s => s.DrawRectangle(Brushes.Blue, new Rect(0, 0, 80, 80));
        viewport.Add(content);

        var scene = new Scene { Renderer = renderer, Root = new VisualRoot(stage, Dim, Dim), Mover = node, Rider = content };
        scene.Draw();

        node.Bounds = new Rect(40, 40, 40, 40);
        scene.Draw();

        var patched = Pixels(renderer);
        Assert.That(renderer.Cache.LastFrameReplayed, Is.True,
            "a rigid slide is one matrix write - a clip inside it must not cost the window a re-record");
        AssertMatchesAFullWalk(scene, patched, "the clipped band must have moved with its viewport");
    }

    // ...and a view that slides is a motion node with the scroll list's own motion node INSIDE it. A node's slot holds
    // its OWN world, so the inner one has to be written too - nothing else writes it, and its whole subtree would sit
    // still while everything around it moved.
    //
    // This asserts the PICTURE, not the path. Whether such a frame may patch is a separate question and the answer is
    // currently no: a node with no units of its own has nobody to vouch that writing its matrix carries everything
    // under it, and both ways of assuming it could were wrong on a live stand - vector icons that never materialised,
    // and an aura that left its shape for the corner. So the frame is allowed to walk; what it may not do is draw the
    // inner subtree anywhere but where a walk draws it.
    [Test]
    public void MovingAMotionNodeThatContainsAnother_CarriesTheInnerOne()
    {
        var device = GpuTestDevice.Device;
        var factory = new RenderUnitFactory(device, new StubResourceFactory());
        using var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Black };

        var stage = Placed(new Rect(0, 0, Dim, Dim));
        var outer = Placed(new Rect(4, 4, 60, 60));
        outer.IsRenderMotionNode = true;
        stage.Add(outer);

        var inner = Placed(new Rect(2, 2, 40, 40));
        inner.IsRenderMotionNode = true;
        outer.Add(inner);

        var tile = Placed(new Rect(0, 0, 20, 20));
        tile.RenderAction = s => s.DrawRectangle(Brushes.Blue, new Rect(0, 0, 20, 20));
        inner.Add(tile);

        var scene = new Scene { Renderer = renderer, Root = new VisualRoot(stage, Dim, Dim), Mover = outer, Rider = tile };
        scene.Draw();

        outer.Bounds = new Rect(40, 40, 60, 60);
        scene.Draw();

        AssertMatchesAFullWalk(scene, Pixels(renderer), "the inner node's subtree must have moved with the outer one");
    }

    // THE TAB SLIDE, as the sandbox actually builds one. A view worth sliding contains a ROTATED thing somewhere - a
    // turned label, a collapsed docking tab, a knob - and a rotated unit cannot ride the node's slot: an axis-aligned
    // instance has nowhere to put the rotation, so it takes a slot of its own holding its FULL world and the node is
    // recorded as not carrying all of its content. The frame was then refused WHOLESALE, and one turned label made
    // every frame of every slide a full walk of the scene.
    //
    // Measured on the stand (30 switches, tab sweep): "not node-aware ViewboxView <- TextBlock 68, <- Border 17,
    // RangesView <- Ellipse 24" against "patch refusals: movedNode 83". The straggler does not have to cost the frame -
    // it only has to be carried, exactly as an ordinary mover's subtree is.
    [Test]
    public void MovingAMotionNodeWithRotatedContent_CarriesItInstead_OfRefusingTheFrame()
    {
        var device = GpuTestDevice.Device;
        var factory = new RenderUnitFactory(device, new StubResourceFactory());
        using var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Black };

        var stage = Placed(new Rect(0, 0, Dim, Dim));
        var view = Placed(new Rect(0, 0, Dim, Dim));
        view.IsRenderMotionNode = true;      // the sliding view
        stage.Add(view);

        var upright = Placed(new Rect(4, 4, 24, 24));
        upright.RenderAction = s => s.DrawRectangle(Brushes.Green, new Rect(0, 0, 24, 24));
        view.Add(upright);

        // The turned label. Its world under the node is sheared, so it holds its own slot rather than the node's.
        var turned = Placed(new Rect(40, 40, 24, 24));
        turned.RenderTransform = new Transform { RotationAngle = 30 };
        turned.RenderAction = s => s.DrawRectangle(Brushes.Red, new Rect(0, 0, 24, 24));
        view.Add(turned);

        var scene = new Scene { Renderer = renderer, Root = new VisualRoot(stage, Dim, Dim), Mover = view, Rider = turned };
        scene.Draw();

        view.Bounds = new Rect(20, 12, Dim, Dim);   // the slide
        scene.Draw();

        var patched = Pixels(renderer);
        Assert.That(renderer.Cache.LastFrameReplayed, Is.True,
            "one turned label must not cost the whole slide a walk of the scene");
        AssertMatchesAFullWalk(scene, patched, "the turned label must have slid with the view, not stayed behind");
    }

    // A mover that CLIPS carries a viewport past, and a recorded Scissor is a world-space rect. This used to hand the
    // frame to the walk for that reason, and the reason has since been answered: the scissors are derived again, for all
    // three carriers of a clip (the Scissor op, the batch segment, the instanced flush).
    //
    // The rule it replaces was not free. Anything worth sliding has a scroll area somewhere inside it, so "something
    // under the mover clips" condemned every one of them - measured on a tab switch into a maximized 3198x1762 window of
    // 24x24 tiles, one 105-129 ms walk of an 8960-tile scene per switch, named by the probe as movedClips<LayoutView>.
    [Test]
    public void AMoverThatClips_PatchesAndTakesItsViewportWithIt()
    {
        using var scene = NewScene();
        scene.Mover.ClipToBounds = true;
        RenderDirty.MarkStructural();
        scene.Draw();

        scene.Mover.Bounds = new Rect(48, 8, 24, 24);
        scene.Draw();

        var patched = Pixels(scene.Renderer);
        Assert.That(scene.Renderer.Cache.LastFrameReplayed, Is.True,
            "a clipping mover must not cost the frame a walk - its scissors are derived again");
        AssertMatchesAFullWalk(scene, patched, "the moved viewport must clip where a full walk clips it");
    }

    // A drag is not only a move. The slider's accent fill and both halves of its track RESIZE on every step, and a size
    // lives in the drawn payload rather than in the matrix - so this one is forgiven on a condition the move does not
    // need: the element must be re-baked on the same frame. It always is (arrange marks a resized element
    // geometry-invalid), and the assertion against the walk is what proves the new size actually reached the picture.
    [Test]
    public void ResizingOneElement_PatchesTheFrame_AndIsDrawnAtItsNewSize()
    {
        using var scene = NewScene();

        scene.Mover.Bounds = new Rect(8, 8, 56, 24);
        scene.Mover.RenderSize = new Size(56, 24);
        scene.Mover.RenderAction = s => s.DrawRectangle(Brushes.Blue, new Rect(0, 0, 56, 24));
        scene.Mover.Invalidate();
        scene.Draw();

        var patched = Pixels(scene.Renderer);
        Assert.That(scene.Renderer.Cache.LastFrameReplayed, Is.True,
            "a resize that the patch is already re-baking must not also cost the scene a walk");
        AssertMatchesAFullWalk(scene, patched, "the element must be drawn at its NEW size, not its old one");
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
