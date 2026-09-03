using System.Runtime.InteropServices;
using Adamantium.Mathematics;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests.Rendering;

/// <summary>
/// A glow switched ON and OFF again on a shape that is ALREADY DRAWN - which is what a state trigger does, and which no
/// other halo fixture covers: the rest build a fresh control per frame, so every one of them is a full walk and the
/// patch path is never entered.
/// <para>Reproduced by hand first, on a slider knob that lights while it is dragged: switching the glow on worked and
/// switching it off left it on the screen until something unrelated forced a walk. Chasing that through the running
/// app cost several rounds of clicking and told us less each time - the frame either walks or patches depending on
/// whether the pointer moved a pixel, so the interesting case is the one a hand can least reliably produce. Here the
/// same sequence is three calls.</para>
/// </summary>
[TestFixture]
[Category("Gpu")]
public class HaloTogglePatchRenderTests
{
    private const int Dim = 220;
    private const int Size = 80;
    private const int At = 70;

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

    /// <summary>One control, kept across frames - the whole point. A fresh control per frame is a fresh walk, and a walk
    /// re-records everything, which is exactly the path that was never broken.</summary>
    private sealed class Scene
    {
        public TestControl Control;
        public VisualRoot Root;
        public Aura Aura;

        public byte[] Draw()
        {
            Assert.That(_renderer.RenderFrame(Root), Is.True, "off-screen frame must render");
            RenderDirty.Clear();   // no UIApplication here: the harness clears what the app clears per frame

            using var img = _renderer.RenderTarget.ResolveTexture.ReadbackToImage();
            var pixels = new byte[(int)img.TotalSizeInBytes];
            Marshal.Copy(img.DataPointer, pixels, 0, pixels.Length);
            return pixels;
        }
    }

    /// <summary>The knob's shape: a white fill wearing a shadow, and an aura that starts SWITCHED OFF. The shadow is
    /// there on purpose - it is what gives the unit a halo record to begin with, and the light appearance of the macOS
    /// theme (where the defect showed) has exactly this pair.</summary>
    private static Scene NewScene(bool withShadow)
    {
        var aura = new Aura { Radius = 3, Spread = 1, Color = Colors.Red, Opacity = 1.0, IsEnabled = false };
        var control = new TestControl
        {
            RenderAction = s => s.DrawRectangle(new SolidColorBrush(Colors.White), new Rect(0, 0, Size, Size)),
            Aura = aura,
            Shadow = withShadow ? new Shadow { OffsetY = 1, BlurRadius = 3, Color = Colors.Blue, Opacity = 1.0 } : null
        };
        control.Bounds = new Rect(0, 0, Size, Size);
        control.RenderSize = new Size(Size, Size);
        control.RenderTransform = new Transform { TranslateX = At, TranslateY = At };

        var scene = new Scene { Control = control, Aura = aura, Root = new VisualRoot(control, Dim, Dim) };
        scene.Draw();   // the first frame: the walk that records the shape and its bands
        return scene;
    }

    // A point just outside the shape's left edge, where the band lands and the fill does not.
    private static (byte R, byte G, byte B) BesideTheShape(byte[] p)
    {
        var i = ((At + Size / 2) * Dim + (At - 2)) * 4;
        return (p[i + 2], p[i + 1], p[i + 0]);
    }

    [TestCase(true, TestName = "WithAShadowAlreadyThere")]
    [TestCase(false, TestName = "WithNoOtherBand")]
    public void AGlowSwitchedOnAndOffAgain_LeavesNothingBehind(bool withShadow)
    {
        var scene = NewScene(withShadow);
        var atRest = BesideTheShape(scene.Draw());

        scene.Aura.IsEnabled = true;
        var lit = BesideTheShape(scene.Draw());
        TestContext.WriteLine($"rest={atRest} lit={lit}");
        Assert.That(lit.R, Is.GreaterThan(atRest.R + 20), "the glow must appear when it is switched on");

        scene.Aura.IsEnabled = false;
        var released = BesideTheShape(scene.Draw());
        TestContext.WriteLine($"released={released}");
        Assert.That(released.R, Is.EqualTo(atRest.R).Within(2),
            "and must be GONE when it is switched off - it stayed on screen until an unrelated frame walked");
    }

    /// <summary>A shape's shadow must fall ON what was drawn BEFORE it. Asked because a range slider's blue span is
    /// laid between its two knobs - the span first, the knobs over it - and the knobs' shadows looked cut off where the
    /// span meets them.
    /// <para>An outer band goes into the batch that draws beneath EVERY fill, so without help it would end up under the
    /// span as well as under its own knob. The walk's answer is to flush what is already pending when a band overlaps
    /// it (OverlapsHigherLayer, layer -1), which leaves the earlier fill below and the band above. This is that rule in
    /// pixels. The other order needs no test: an opaque fill drawn LATER covering an earlier shadow is just painter's
    /// order, and asserting otherwise was this test's first, wrong, shape.</para></summary>
    [Test]
    public void AShadowFallsOnWhatWasDrawnBeforeIt()
    {
        // Real controls laid out by the root, not hand-placed: a child's Bounds are written by the arrange pass, so
        // setting them by hand and skipping it leaves the second shape unarranged and undrawn.
        var shadowed = new Adamantium.UI.Controls.Decorators.Border
        {
            Width = Size, Height = Size,
            Background = new SolidColorBrush(Colors.White),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(At, At, 0, 0),
            Shadow = new Shadow { OffsetY = 0, BlurRadius = 8, Spread = 2, Color = Colors.Red, Opacity = 1.0 }
        };

        // Butted against the shadowed shape's right edge, so it lies exactly where that shadow falls - the range
        // slider's span against its knob. Added FIRST, as the span is: the knob and its shadow come over it.
        var earlier = new Adamantium.UI.Controls.Decorators.Border
        {
            Width = Size, Height = 20,
            Background = new SolidColorBrush(Colors.Blue),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(At + Size, At + Size / 2 - 10, 0, 0)
        };

        var host = new Adamantium.UI.Controls.Panels.Grid();
        ((IContainer)host).AddOrSetChildComponent(earlier);
        ((IContainer)host).AddOrSetChildComponent(shadowed);

        var root = new VisualRoot(host, Dim, Dim);
        ((IMeasurableComponent)root).Measure(new Size(Dim, Dim));
        ((IMeasurableComponent)root).Arrange(new Rect(0, 0, Dim, Dim));
        Assert.That(_renderer.RenderFrame(root), Is.True);
        RenderDirty.Clear();

        using var img = _renderer.RenderTarget.ResolveTexture.ReadbackToImage();
        var pixels = new byte[(int)img.TotalSizeInBytes];
        Marshal.Copy(img.DataPointer, pixels, 0, pixels.Length);

        // Just past the shadowed shape's right edge, on the line the earlier fill occupies.
        var i = ((At + Size / 2) * Dim + (At + Size + 2)) * 4;
        var (r, g, b) = (pixels[i + 2], pixels[i + 1], pixels[i + 0]);
        TestContext.WriteLine($"on the earlier fill, where the shadow falls: ({r}, {g}, {b})");

        Assert.That(r, Is.GreaterThan(30),
            $"the earlier fill hid the shadow that falls on it - the band was left under it: ({r}, {g}, {b})");
    }

    /// <summary>...and the frame that switches it must not cost a walk of the scene. Stated apart from the pixels
    /// because they are two different promises: one is that the picture is right, the other that it was cheap.</summary>
    [Test]
    public void SwitchingTheGlow_IsPatched_NotWalked()
    {
        var scene = NewScene(withShadow: true);

        scene.Aura.IsEnabled = true;
        scene.Draw();
        Assert.That(_renderer.Cache.LastFrameReplayed, Is.True, "switching a glow ON must not walk the scene");

        scene.Aura.IsEnabled = false;
        scene.Draw();
        Assert.That(_renderer.Cache.LastFrameReplayed, Is.True, "...nor switching it off");
    }
}
