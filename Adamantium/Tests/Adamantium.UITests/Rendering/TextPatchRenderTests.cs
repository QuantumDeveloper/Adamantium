using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Fonts;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering;
using Adamantium.Vulkan.Core;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// A re-rendered TEXT block must be patched into the glyph slots it already owns, not cost the frame a walk of the whole
/// scene. Measured before this existed: 340 frames replaying at 0.09 ms against single frames walking at 37 ms, and the
/// trace named the trigger - a diagnostics overlay rewriting its own text four times a second made an unrelated tab of
/// 600 tiles redraw entirely. See docs/RENDER_CACHE_REDESIGN.md §4q.
/// <para>The dangerous failure is not a missing patch - that only costs speed - but a WRONG one: a run written at the
/// wrong offset silently overwrites a neighbour's glyphs, and a run accepted when the block no longer matches it draws
/// stale text. So each test pins the patched frame against the SAME scene drawn by a full walk, pixel for pixel, and the
/// negative ones state what must NOT be patched.</para>
/// </summary>
[TestFixture]
[Category("Gpu")]
public class TextPatchRenderTests
{
    private const int Dim = 128;

    // Text is the one render path that needs a real FontRenderer, so the other fixtures' throwing stub won't do.
    private sealed class FontResourceFactory : IResourceFactory
    {
        private readonly Dictionary<IGraphicsDevice, FontRenderer> _renderers = new();

        public ITexture CreateTexture(TextureDescription description, byte[] pixelData) => throw new NotSupportedException();
        public ITexture CreateTextureArray(TextureDescription description, IReadOnlyList<byte[]> layers) => throw new NotSupportedException();
        public ITexture ImportSharedSurface(SharedSurfaceDescriptor descriptor) => throw new NotSupportedException();
        public IRenderTarget CreateRenderTarget(uint width, uint height, MSAALevel msaa, SurfaceFormat format, ImageLayout desiredLayout) => throw new NotSupportedException();

        public FontRenderer GetFontRenderer(IGraphicsDevice graphicsDevice)
        {
            if (!_renderers.TryGetValue(graphicsDevice, out var renderer))
            {
                _renderers[graphicsDevice] = renderer = new FontRenderer(graphicsDevice);
            }

            return renderer;
        }
    }

    private sealed class Scene : IDisposable
    {
        public OffscreenTestRenderer Renderer;
        public VisualRoot Root;
        public TextBlock[] Blocks;

        public void Draw()
        {
            ((IMeasurableComponent)Root).Measure(new Size(Dim, Dim));
            ((IMeasurableComponent)Root).Arrange(new Rect(0, 0, Dim, Dim));
            Assert.That(Renderer.RenderFrame(Root), Is.True, "off-screen frame must render");
            RenderDirty.Clear();   // no UIApplication here: the harness clears what the app normally clears per frame
        }

        public void Dispose() => Renderer.Dispose();
    }

    private static Scene NewScene(params string[] texts)
    {
        var device = GpuTestDevice.Device;
        var factory = new RenderUnitFactory(device, new FontResourceFactory());
        var renderer = new OffscreenTestRenderer(device, factory, Dim, Dim) { ClearColor = Colors.Black };

        var stack = new StackPanel { Orientation = Orientation.Vertical };
        var blocks = new TextBlock[texts.Length];
        for (var i = 0; i < texts.Length; i++)
        {
            blocks[i] = new TextBlock { Text = texts[i], Foreground = Brushes.White, FontSize = 18 };
            stack.Children.Add(blocks[i]);
        }

        var scene = new Scene { Renderer = renderer, Root = new VisualRoot(stack, Dim, Dim), Blocks = blocks };
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

    // The reference: the SAME scene drawn by a full WALK. Taken from the same harness on purpose - a second
    // OffscreenTestRenderer means a second render target, and the device runs out of BAR memory once a few fixtures do
    // that. Comparing within one scene is also the stronger statement: patch and walk must agree on THIS frame.
    private static void AssertMatchesAFullWalk(Scene scene, byte[] patched, string because)
    {
        RenderDirty.MarkStructural();   // a structural build takes neither replay path - it re-records everything
        scene.Draw();

        Assert.That(scene.Renderer.Cache.LastFrameReplayed, Is.False, "the reference frame has to actually walk");
        Assert.That(DifferingPixels(patched, Pixels(scene.Renderer)), Is.Zero, because);
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

    [Test]
    public void SameGlyphCount_PatchesTheRun_InsteadOfWalkingTheScene()
    {
        using var scene = NewScene("600 fps");

        scene.Blocks[0].Text = "598 fps";   // the overlay's case: the number ticks, the glyph count does not
        scene.Draw();

        Assert.That(scene.Renderer.Cache.LastFrameReplayed, Is.True,
            "a text block whose glyph count is unchanged must be patched in place, not cost the frame a full walk");
        AssertMatchesAFullWalk(scene, Pixels(scene.Renderer),
            "the patched frame must be pixel-identical to the same text drawn by a full walk");
    }

    [Test]
    public void PatchingOneBlock_DoesNotDisturbItsNeighbour()
    {
        // The failure this guards: a run written at the wrong offset lands in the NEXT block's glyphs. Both blocks share
        // one batch, so a neighbour is exactly what a bad offset hits - and it looks like a font bug, not a cache bug.
        using var scene = NewScene("600 fps", "layout 0.02");

        scene.Blocks[0].Text = "598 fps";
        scene.Draw();

        Assert.That(scene.Renderer.Cache.LastFrameReplayed, Is.True, "the patch must be taken for this frame");
        AssertMatchesAFullWalk(scene, Pixels(scene.Renderer),
            "the untouched neighbour must be drawn exactly as a full walk draws it");
    }

    // The count change with a NEIGHBOUR in the same batch - which is what a diagnostics plate always has, and what an
    // app always has. Re-issuing a run means everything after it in that segment shifts, so this is where an offset that
    // is right for one block on its own goes wrong: the frame keeps showing the text it had, and only something that
    // forces a walk (moving the mouse) puts the new one up.
    [Test]
    public void GlyphCountChange_WithANeighbour_StillShowsTheNewText()
    {
        using var scene = NewScene("600 fps", "layout 0.02");

        scene.Blocks[0].Text = "1200 fps";
        scene.Draw();
        Assert.That(scene.Renderer.Cache.LastFrameReplayed, Is.True, "it must be spliced, not walked");
        AssertMatchesAFullWalk(scene, Pixels(scene.Renderer), "the longer text AND its neighbour must be what a walk draws");

        scene.Blocks[0].Text = "6 fps";
        scene.Draw();
        Assert.That(scene.Renderer.Cache.LastFrameReplayed, Is.True, "...and so must the shrink");
        AssertMatchesAFullWalk(scene, Pixels(scene.Renderer), "the shorter text must not leave a tail, nor move its neighbour");
    }

    [Test]
    public void GlyphCountChange_ReIssuesTheRun_InsteadOfWalkingTheScene()
    {
        // The run is a FIXED span, so more (or fewer) glyphs cannot be written INTO it - but that is an argument about
        // WHERE they go, not about redrawing the window. The block's run is re-issued inside the segment it lives in, the
        // same repair every batched family gets. Measured live before this: one fps plate ticking cost a 25 ms walk of the
        // whole scene, four times a second, on top of whatever the app was actually doing.
        // Both directions, because they fail differently: a longer run has to fit somewhere, and a SHORTER one has to take
        // the tail of the old text off the screen with it.
        using var scene = NewScene("600 fps");

        scene.Blocks[0].Text = "1200 fps";
        scene.Draw();

        Assert.That(scene.Renderer.Cache.LastFrameReplayed, Is.True, "a block that GREW must be re-issued, not walked");
        AssertMatchesAFullWalk(scene, Pixels(scene.Renderer), "and the patched frame must show the new, longer text");

        scene.Blocks[0].Text = "6 fps";
        scene.Draw();

        Assert.That(scene.Renderer.Cache.LastFrameReplayed, Is.True, "...and so must one that SHRANK");
        AssertMatchesAFullWalk(scene, Pixels(scene.Renderer), "with no tail of the longer text left behind");
    }

    [Test]
    public void ColourChangeAlone_IsPatched()
    {
        // Same glyphs, different per-instance colour: the whole point of baking foreground per instance. If this walks,
        // every hover/selection highlight over text costs the scene a redraw.
        using var scene = NewScene("600 fps");
        var white = Pixels(scene.Renderer);

        scene.Blocks[0].Foreground = Brushes.Red;
        scene.Draw();
        var red = Pixels(scene.Renderer);

        Assert.That(scene.Renderer.Cache.LastFrameReplayed, Is.True, "a colour-only text change must patch");
        Assert.That(DifferingPixels(white, red), Is.Not.Zero,
            "the colour really did change - otherwise this would pass on a frame that drew nothing");
        AssertMatchesAFullWalk(scene, red, "the recoloured glyphs must match what a full walk bakes");
    }
    [Test]
    public void ZZ_ProbeGlyphSplice()
    {
        var dir = Path.Combine(Path.GetTempPath(), "AdamantiumTests") + Path.DirectorySeparatorChar;
        Directory.CreateDirectory(dir);
        using var scene = NewScene("600 fps");
        Shot(scene, dir + "1-before.png");

        scene.Blocks[0].Text = "1200 fps";
        scene.Draw();
        Shot(scene, dir + "2-patched.png");
        TestContext.Out.WriteLine("replayed=" + scene.Renderer.Cache.LastFrameReplayed + " " + scene.Renderer.Cache.DumpGroups());

        RenderDirty.MarkStructural();
        scene.Draw();
        Shot(scene, dir + "3-walk.png");
        TestContext.Out.WriteLine("walk " + scene.Renderer.Cache.DumpGroups());
    }

    private static void Shot(Scene scene, string path)
    {
        using var img = scene.Renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        img.Save(path, Adamantium.Imaging.ImageFileType.Png);
    }
}

