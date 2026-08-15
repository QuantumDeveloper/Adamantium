using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Fonts;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry.Shapes;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Diagnostics;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering;
using Adamantium.Vulkan.Core;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// A control whose batched unit COUNT changes - the hover backdrop that appears under a row - must be repaired without
/// re-walking the scene, and it must land in ITS OWN paint position (docs/RENDER_CACHE_REDESIGN.md §4q).
/// <para>The invariant these tests hold the cache to: <b>what a control costs, and where it lands, depend on that control
/// and its paint rank - never on what else happens to be in the frame.</b> Every regression this path went through was a
/// breach of it, and each looked like a rendering fault rather than a cache one: a highlight drawn OVER instanced
/// geometry because it borrowed a neighbour's place; rows blinking out because a layer was rebuilt from bookkeeping that
/// does not describe every slot; and - invisible in any picture - the patch quietly giving up because an unrelated text
/// label sat between the neighbours, which cost a plain mouse move a full walk of the scene.</para>
/// <para>So the frames here are asserted TWICE: the picture must equal what a full walk draws, and the path taken must
/// still be the patch. A test that only compares pixels passes happily while the frame rate collapses.</para>
/// </summary>
[TestFixture]
[Category("Gpu")]
public class LayerPatchRenderTests
{
    private const int Dim = 128;
    private const int Rows = 6;
    private const int RowHeight = 16;

    private sealed class Scene : IDisposable
    {
        public OffscreenTestRenderer Renderer;
        public VisualRoot Root;
        public TestControl[] Backdrops;   // one per row, drawing nothing until it is given something to draw

        public void Draw()
        {
            Assert.That(Renderer.RenderFrame(Root), Is.True, "off-screen frame must render");
            RenderDirty.Clear();
        }

        public void Dispose() => Renderer.Dispose();
    }

    private static TestControl Placed(Rect bounds)
    {
        var c = new TestControl { Bounds = bounds, RenderSize = new Size(bounds.Width, bounds.Height) };
        return c;
    }

    // Rows of painted bars, each with an EMPTY sibling in front of it - the "hover backdrop": it draws nothing until it is
    // given a command, so giving it one is the 0 -> 1 unit-count change this whole path exists for. Built from visual
    // children with explicit bounds rather than a panel, so the test states exactly one thing: what the cache does with a
    // control that starts drawing.
    private static Scene NewScene(bool withInstancedGeometry = false, bool withText = false)
    {
        FrameTrace.Enabled = true;   // so a refusal names itself in the failure message
        var device = GpuTestDevice.Device;
        var factory = new RenderUnitFactory(device, new StubResourceFactory());
        var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Black };

        var stage = Placed(new Rect(0, 0, Dim, Dim));
        var backdrops = new TestControl[Rows];
        for (var i = 0; i < Rows; i++)
        {
            var y = i * RowHeight;
            backdrops[i] = Placed(new Rect(0, y, Dim, RowHeight));
            stage.Add(backdrops[i]);                              // behind: this is what materialises

            var bar = Placed(new Rect(0, y + 4, 100, 8));
            bar.RenderAction = s => s.DrawRectangle(Brushes.Blue, new Rect(0, 0, 100, 8));
            stage.Add(bar);                                       // in front: what the backdrop must stay under
        }

        // Arbitrary geometry sharing one mesh = the instanced-fill path, which puts a flush op in the recorded stream.
        // A backdrop appearing must never end up drawn on the far side of it.
        if (withInstancedGeometry)
        {
            var shapes = Placed(new Rect(0, Rows * RowHeight, Dim, 24));
            shapes.RenderAction = s => s
                .DrawGeometry(Brushes.Green, new RectangleGeometry(new Rect(2, 2, 20, 20)))
                .DrawGeometry(Brushes.Green, new RectangleGeometry(new Rect(30, 2, 20, 20)));
            stage.Add(shapes);
        }

        var scene = new Scene { Renderer = renderer, Root = new VisualRoot(stage, Dim, Dim), Backdrops = backdrops };
        scene.Draw();
        return scene;
    }

    private static void Show(TestControl backdrop, Brush brush)
    {
        backdrop.RenderAction = brush == null
            ? null
            : s => s.DrawRectangle(brush, new Rect(0, 0, Dim, RowHeight));
        backdrop.Invalidate();
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

    // The reference: the SAME scene drawn by a full WALK, which orders everything by construction. Taken from the same
    // harness - a second renderer means a second render target, and the device runs out of BAR memory once a few fixtures
    // do that.
    private static void AssertMatchesAFullWalk(Scene scene, byte[] patched, string because)
    {
        RenderDirty.MarkStructural();
        scene.Draw();

        Assert.That(scene.Renderer.Cache.LastFrameReplayed, Is.False, "the reference frame has to actually walk");
        Assert.That(DifferingPixels(patched, Pixels(scene.Renderer)), Is.Zero, because);
    }

    [Test]
    public void ABackdropAppearing_IsPatched_AndMatchesAFullWalk()
    {
        using var scene = NewScene();

        Show(scene.Backdrops[2], Brushes.Red);
        scene.Draw();

        Assert.That(scene.Renderer.Cache.LastFrameReplayed, Is.True,
            "a control that starts drawing must be patched into place, not cost the scene a walk");
        AssertMatchesAFullWalk(scene, Pixels(scene.Renderer),
            "the patched frame must be pixel-identical to the same scene drawn by a full walk");
    }

    [Test]
    public void ABackdropAppearing_StaysUnderItsRow_NotOverIt()
    {
        // The placement itself: the newcomer's paint rank puts it BEHIND the bar of its row. Borrowing a neighbour's place
        // instead of using its own rank is how a selection ended up on top of the thing it was meant to sit behind.
        using var scene = NewScene();

        Show(scene.Backdrops[3], Brushes.Red);
        scene.Draw();

        Assert.That(scene.Renderer.Cache.LastFrameReplayed, Is.True, "the patch must be taken for this frame");
        AssertMatchesAFullWalk(scene, Pixels(scene.Renderer), "the backdrop must sit under its row, exactly as a walk puts it");
    }

    [Test]
    public void ABackdropAppearing_NeverDrawsOverInstancedGeometry()
    {
        // NEGATIVE: an instanced flush is a real barrier in the op stream. Whether the frame patches or walks is the
        // cache's business - the picture is not allowed to differ either way.
        using var scene = NewScene(withInstancedGeometry: true);

        Show(scene.Backdrops[3], Brushes.Red);
        scene.Draw();

        AssertMatchesAFullWalk(scene, Pixels(scene.Renderer),
            "a backdrop appearing in a frame that also draws instanced geometry must land where a walk puts it");
    }

    [Test]
    public void InstancedGeometryElsewhere_DoesNotCostTheFrameItsPatch()
    {
        // THE INVARIANT, stated as a test: geometry that has nothing to do with this control must not change what this
        // control costs. This is the one that fails the moment somebody reintroduces a frame-wide "does this frame
        // contain X" flag - which is exactly how every hover in a window came to cost a full walk of the scene.
        using var scene = NewScene(withInstancedGeometry: true);

        Show(scene.Backdrops[1], Brushes.Red);
        scene.Draw();

        Assert.That(scene.Renderer.Cache.LastFrameReplayed, Is.True,
            "instanced geometry elsewhere in the frame must not decide whether THIS control can be patched");
    }

    [Test]
    public void RepeatedAppearAndVanish_KeepsPatching_AndDoesNotExhaustTheArena()
    {
        // NEGATIVE, and about the PATH rather than the picture: the arena used to fill with blocks one item too small to
        // reuse, so after a couple of moves every frame silently fell back to the full walk - the pixels stayed perfect
        // and the frame rate collapsed. A picture test cannot see that; this one can.
        using var scene = NewScene();

        for (var i = 0; i < 12; i++)
        {
            Show(scene.Backdrops[i % Rows], Brushes.Red);
            scene.Draw();
            Assert.That(scene.Renderer.Cache.LastFrameReplayed, Is.True, $"appearing backdrop {i} must patch, not walk");

            Show(scene.Backdrops[i % Rows], null);
            scene.Draw();
            Assert.That(scene.Renderer.Cache.LastFrameReplayed, Is.True,
                $"vanishing backdrop {i} must patch, not walk (frame was {scene.Renderer.Cache.LastBuildKind}, gate {scene.Renderer.Cache.LastWalkReason}, refused by {FrameTrace.Refuser})");
        }

        AssertMatchesAFullWalk(scene, Pixels(scene.Renderer), "and after all that the scene must still be drawn correctly");
    }

    private sealed class StubResourceFactory : IResourceFactory
    {
        public ITexture CreateTexture(TextureDescription description, byte[] pixelData) => throw new NotSupportedException();
        public ITexture CreateTextureArray(TextureDescription description, IReadOnlyList<byte[]> layers) => throw new NotSupportedException();
        public ITexture ImportSharedSurface(SharedSurfaceDescriptor descriptor) => throw new NotSupportedException();
        public IRenderTarget CreateRenderTarget(uint width, uint height, MSAALevel msaa, SurfaceFormat format, ImageLayout desiredLayout) => throw new NotSupportedException();
        public FontRenderer GetFontRenderer(IGraphicsDevice graphicsDevice) => throw new NotSupportedException();
    }
}
