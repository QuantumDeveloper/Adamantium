using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Extensions;
using Adamantium.UI.Rendering;
using Adamantium.UITests.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>WHAT IS IN FRONT OF WHAT, and that it stays that way.
/// <para>A canvas draws its items itself and hosts its CONTROLS in a layer of the template, so which of the two is in
/// front is decided by paint order in the render cache and not by the scene. These ask that question directly, because
/// asking it of the screen costs a hand on the mouse per hypothesis.</para></summary>
public class CanvasPaintOrderTests
{
    private sealed class Root : Grid, IRootVisualComponent
    {
        public Vector2 PointToClient(PixelPoint point) => new((float)point.X, (float)point.Y);
        public PixelPoint PointToScreen(Vector2 point) => new(point.X, point.Y);
        public PixelPoint Position { get; set; }
        public void AttachContextAndInitialize(IUIContext context) { }
        public double Left { get; set; }
        public double Top { get; set; }
        public string Title { get; set; }
        public double ClientWidth { get; set; }
        public double ClientHeight { get; set; }
        public IUIContext UIContext => null;
    }

    // The one part this is about: the layer the canvas puts its hosted controls in. Built by hand rather than taken
    // from a theme, because the question is about paint order and not about how a theme dresses a canvas.
    private static ControlTemplate Template() => new(() =>
    {
        var layer = new CanvasElementLayer();
        var front = new CanvasFrontLayer();
        var grid = new Grid();
        grid.Children.Add(layer);
        grid.Children.Add(front);

        var result = new TemplateResult { RootComponent = grid };
        result.RegisterName("PART_Elements", layer);
        result.RegisterName("PART_Front", front);
        return result;
    });

    // Something drawn in a GRAPH that is not a node or a wire: a note pointing at a node, or the frame a group of them
    // is gathered by. Which band it is in is the whole question these ask.
    private sealed class Marker : ICanvasItem
    {
        public Rect Bounds { get; init; }
        public CanvasBand Band { get; init; }
        public CanvasMode Mode => CanvasMode.Nodes;
        public bool HitTest(Vector2 world, double tolerance) => Bounds.Contains(world);
        public void Render(IDrawingSession session, InfiniteCanvas canvas) { }
        public void Move(Vector2 worldDelta) { }
        public void Resize(Rect world) { }
    }

    private static (Root root, InfiniteCanvas canvas, CanvasScene scene, RenderCache cache) Stage()
    {
        var canvas = new InfiniteCanvas
        {
            Template = Template(),
            Width = 800,
            Height = 600,
            Mode = CanvasMode.Nodes
        };
        var scene = new CanvasScene();
        canvas.Scene = scene;

        var root = new Root { ClientWidth = 800, ClientHeight = 600 };
        root.Children.Add(canvas);

        var cache = new RenderCache(new DrawingContext(), new FakeRenderUnitFactory());

        return (root, canvas, scene, cache);
    }

    private static void Frame(Root root, InfiniteCanvas canvas, RenderCache cache)
    {
        // Measured and placed BY HAND, and TWICE: nothing here is a window, and the pass that gives the canvas its size
        // is also the one that asks it what can be seen - which it answers from the size it had BEFORE that pass. One
        // pass and the layer is handed an empty list, exactly as it is on the first frame of the real thing.
        for (var pass = 0; pass < 3; pass++)
        {
            canvas.InvalidateArrange();
            root.Measure(new Size(800, 600), force: true);
            root.Arrange(new Rect(0, 0, 800, 600));
        }

        WindowExtension.UpdateTree(root);
        cache.RecordFrame(root);
        cache.ApplyFrame();
    }

    [Test]
    [Explicit("Measurement probe")]
    public void Probe()
    {
        var (root, canvas, scene, cache) = Stage();

        var node = new CanvasNode { Title = "Multiply" };
        var element = new ElementItem(node, new Rect(40, 40, 160, 90));

        scene.Add(element);
        scene.Add(new Marker { Bounds = new Rect(0, 0, 120, 120), Band = CanvasBand.Under });
        Frame(root, canvas, cache);

        var layer = canvas.GetTemplateChild("PART_Elements") as CanvasElementLayer;

        TestContext.Out.WriteLine($"canvas size   = {canvas.RenderSize}");
        TestContext.Out.WriteLine($"visible world = {canvas.VisibleWorld}");
        TestContext.Out.WriteLine($"layer         = {(layer == null ? "NULL" : layer.Children.Count + " children")}");
        TestContext.Out.WriteLine($"node parent   = {node.VisualParent?.GetType().Name ?? "none"}");
        TestContext.Out.WriteLine($"rank canvas   = {cache.PaintRankOf(canvas)}");
        TestContext.Out.WriteLine($"rank layer    = {cache.PaintRankOf(layer)}");
        TestContext.Out.WriteLine($"rank node     = {cache.PaintRankOf(node)}");
        TestContext.Out.WriteLine($"scene items   = {scene.Items.Count}");
        TestContext.Out.WriteLine($"element bounds= {element.Bounds}");

        var found = 0;
        foreach (var item in scene.ItemsIn(canvas.VisibleWorld)) found++;
        TestContext.Out.WriteLine($"items in view = {found}");
    }

    // THE QUESTION: something drawn and a hosted control overlap, and which is in front must not depend on the camera.
    //
    // A zoom re-asks the canvas which controls can be seen and hands the layer a fresh list, so a control can LEAVE the
    // visual tree and come back - and what comes back could have been ranked afresh.
    [TestCase(2.0, TestName = "zoomed in")]
    [TestCase(0.5, TestName = "zoomed out")]
    [TestCase(1.0, TestName = "back where it started")]
    public void ZoomingDoesNotChangeWhatIsInFront(double scale)
    {
        var (root, canvas, scene, cache) = Stage();

        var node = new CanvasNode { Title = "Multiply" };
        var element = new ElementItem(node, new Rect(40, 40, 160, 90));

        scene.Add(element);
        scene.Add(new Marker { Bounds = new Rect(0, 0, 120, 120), Band = CanvasBand.Under });

        Frame(root, canvas, cache);

        var before = cache.PaintRankOf(node);
        Assert.That(before, Is.Not.Null, "the node never reached the cache at all");

        var overShape = before > cache.PaintRankOf(canvas);

        canvas.Scale = scale;
        Frame(root, canvas, cache);

        var after = cache.PaintRankOf(node);
        Assert.That(after, Is.Not.Null, "the node fell out of the cache when the camera moved");
        Assert.That(after > cache.PaintRankOf(canvas), Is.EqualTo(overShape),
            "the camera changed which of the two is in front");
    }

    // THE SCENE DOES NOT DECIDE ACROSS A BAND, and no amount of bringing to front will make it. A hosted control lives
    // in a layer of the template and a drawn item is drawn by the canvas itself, so within one band the control is
    // always in front - which is why what is in front is said by the BAND and not by the order.
    [Test]
    public void BringingADrawnItemToTheFrontDoesNotPutItOverAHostedControl()
    {
        var (root, canvas, scene, cache) = Stage();

        var marker = new Marker { Bounds = new Rect(0, 0, 120, 120), Band = CanvasBand.Under };
        var node = new CanvasNode { Title = "Multiply" };
        var element = new ElementItem(node, new Rect(40, 40, 160, 90));

        scene.Add(element);
        scene.Add(marker);
        Frame(root, canvas, cache);

        scene.BringToFront(marker);
        scene.SendToBack(element);
        Frame(root, canvas, cache);

        Assert.That(cache.PaintRankOf(node), Is.GreaterThan(cache.PaintRankOf(canvas)),
            "the node is behind it now - which would mean the order decides across a band, and it does not");
    }

    // A NODE STAYS LIVE. What is in one is a field, a switch, a list, so a node that went deaf to the pointer while the
    // plane is being arranged would be a picture of a node - it is dragged by its title strip instead. Everything else
    // on the plane keeps the old rule: editing, a press on it belongs to the plane.
    [Test]
    public void ANodeAnswersThePointerWhileOtherControlsDoNot()
    {
        var (root, canvas, scene, cache) = Stage();
        canvas.IsDesignMode = true;

        var node = new CanvasNode { Title = "Multiply" };
        scene.Add(new ElementItem(node, new Rect(40, 40, 160, 90)));

        Frame(root, canvas, cache);

        Assert.That(node.IsHitTestVisible, Is.True, "the node is deaf, so what is inside it cannot be used");
    }

    [Test]
    public void AnOrdinaryControlIsDeafWhileThePlaneIsArranged()
    {
        var (root, canvas, scene, cache) = Stage();
        canvas.Mode = CanvasMode.Drawing;
        canvas.IsDesignMode = true;

        var button = new Adamantium.UI.Controls.Buttons.Button();
        scene.Add(new ElementItem(button, new Rect(40, 40, 120, 40)));

        Frame(root, canvas, cache);

        Assert.That(button.IsHitTestVisible, Is.False, "a press on it would work it instead of picking it up");

        canvas.IsDesignMode = false;

        Assert.That(button.IsHitTestVisible, Is.True, "...and using the plane makes it live again");
    }

    // ...and THAT is what the bands are for. What a thing is drawn in front of is not a number it carries but which
    // side of the controls it is on, and there is a layer for each side.
    [Test]
    public void AnItemInTheFrontBandIsDrawnAfterTheControls()
    {
        var (root, canvas, scene, cache) = Stage();

        var node = new CanvasNode { Title = "Multiply" };
        scene.Add(new ElementItem(node, new Rect(40, 40, 160, 90)));
        scene.Add(new Marker { Bounds = new Rect(0, 0, 120, 120), Band = CanvasBand.Over });

        Frame(root, canvas, cache);

        var front = canvas.GetTemplateChild("PART_Front") as CanvasFrontLayer;

        Assert.Multiple(() =>
        {
            Assert.That(cache.PaintRankOf(front), Is.GreaterThan(cache.PaintRankOf(node)),
                "a note drawn over a node would come out under it");
            Assert.That(front.IsHitTestVisible, Is.False,
                "the front band would take the presses meant for what is under it");
        });
    }

    // The band in front is drawn by ANOTHER component, so a repaint that only marked the canvas left it showing what
    // the plane looked like a moment ago - the drawing in two ages of itself.
    [Test]
    public void RepaintingTheCanvasAlsoRepaintsTheBandInFrontOfIt()
    {
        var (root, canvas, scene, cache) = Stage();

        scene.Add(new Marker { Bounds = new Rect(0, 0, 120, 120), Band = CanvasBand.Over });
        Frame(root, canvas, cache);

        var front = canvas.GetTemplateChild("PART_Front") as CanvasFrontLayer;
        Assert.That(front.IsGeometryValid, Is.True, "nothing was recorded for the front band at all");

        canvas.Scale = 2.0;

        Assert.That(front.IsGeometryValid, Is.False, "the camera moved and the band in front did not hear about it");
    }
}
