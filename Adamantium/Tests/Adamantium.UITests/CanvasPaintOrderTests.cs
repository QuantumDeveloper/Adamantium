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
        // THE STACK IS EMPTY HERE, and that is the point: how many layers there are and what sort each is depends on
        // the order of the scene, so the canvas fills it. The template only says where it goes.
        var layers = new Grid();
        var front = new CanvasFrontLayer();
        var grid = new Grid();
        grid.Children.Add(layers);
        grid.Children.Add(front);

        var result = new TemplateResult { RootComponent = grid };
        result.RegisterName("PART_Layers", layers);
        result.RegisterName("PART_Front", front);
        return result;
    });

    // Something drawn in a GRAPH that is not a node or a wire: a note pointing at a node, or the frame a group of them
    // is gathered by. WHERE IN THE ORDER it stands against the controls is the whole question these ask.
    private sealed class Marker : ICanvasItem
    {
        public Rect Bounds { get; init; }
        public int Order { get; set; }
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
        scene.Add(new Marker { Bounds = new Rect(0, 0, 120, 120) });
        Frame(root, canvas, cache);

        var layer = Layer<CanvasElementLayer>(canvas);

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
        scene.Add(new Marker { Bounds = new Rect(0, 0, 120, 120) });

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

    // THE SCENE DECIDES, AND IT DECIDES ACROSS CONTROLS TOO. There is one order on the plane and everything stands in
    // it: a drawn thing brought to the front goes over a hosted control, and a control brought to the front goes over
    // the drawing. That is what "equal" means here, and it is the whole reason the layers are cut from the order
    // rather than fixed by the template.
    [Test]
    public void BringingADrawnItemToTheFrontPutsItOverAHostedControl()
    {
        var (root, canvas, scene, cache) = Stage();

        var marker = new Marker { Bounds = new Rect(0, 0, 120, 120) };
        var node = new CanvasNode { Title = "Multiply" };
        var element = new ElementItem(node, new Rect(40, 40, 160, 90));

        scene.Add(element);
        scene.Add(marker);
        Frame(root, canvas, cache);

        var drawn = Layer<CanvasDrawLayer>(canvas);

        Assert.That(cache.PaintRankOf(drawn), Is.GreaterThan(cache.PaintRankOf(node)),
            "the thing added last is under the control that was added first");

        // ...AND BACK AGAIN. The same order that lifted it puts it down.
        scene.SendToBack(marker);
        Frame(root, canvas, cache);

        drawn = Layer<CanvasDrawLayer>(canvas);

        Assert.That(cache.PaintRankOf(drawn), Is.LessThan(cache.PaintRankOf(node)),
            "sent to the back, it is still over the control");
    }

    // A PICTURE CAN BE RAISED over a drawing just as a drawing can be drawn over a picture - the same order, read the
    // same way. This is the half that a fixed ladder of bands could never do.
    [Test]
    public void BringingAControlToTheFrontPutsItOverWhatIsDrawn()
    {
        var (root, canvas, scene, cache) = Stage();

        var node = new CanvasNode { Title = "Multiply" };
        var element = new ElementItem(node, new Rect(40, 40, 160, 90));

        scene.Add(element);
        scene.Add(new Marker { Bounds = new Rect(0, 0, 120, 120) });
        Frame(root, canvas, cache);

        scene.BringToFront(element);
        Frame(root, canvas, cache);

        Assert.That(cache.PaintRankOf(node), Is.GreaterThan(cache.PaintRankOf(Layer<CanvasDrawLayer>(canvas))),
            "the control was brought to the front and stayed under the drawing");
    }

    // ...AND WITH SOMETHING REALLY DRAWN UNDER IT. The two above use a Marker, which draws NOTHING - so they pin the
    // ranks of the layers and say nothing about the pixels. A shape with a fill is drawn through the batches, and a
    // batch is flushed by its own layer number rather than by the order it was collected in: that is a second order,
    // running alongside the scene's, and it is the one a person sees.
    [Test]
    public void AControlRaisedOverAFilledShapeIsDrawnOverIt()
    {
        var (root, canvas, scene, cache) = Stage();

        canvas.Mode = CanvasMode.Drawing;

        var shape = new ShapeItem(CanvasShape.Rectangle, new Rect(40, 40, 200, 140), null, 0, Brushes.DeepPink);
        var picture = new Image { Background = Brushes.White, Width = 160, Height = 90 };
        var element = new ElementItem(picture, new Rect(60, 60, 160, 90));

        scene.Add(element);
        scene.Add(shape);
        Frame(root, canvas, cache);

        scene.BringToFront(element);
        Frame(root, canvas, cache);

        Assert.That(cache.PaintRankOf(picture), Is.GreaterThan(cache.PaintRankOf(Layer<CanvasDrawLayer>(canvas))),
            "the picture was raised over the shape and is still recorded under it");
    }

    // The first layer of the stack of the wanted sort. The stack is the canvas's own, so a test reads it the way the
    // canvas built it rather than by a name a template would have had to know in advance.
    private static T Layer<T>(InfiniteCanvas canvas) where T : class
    {
        var stack = canvas.GetTemplateChild("PART_Layers") as Panel;

        if (stack == null) return null;

        foreach (var child in stack.Children)
        {
            if (child is T wanted) return wanted;
        }

        return null;
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

    // A LAYER PER RUN, and no more than that: the affordability of the whole thing rests on it. Two pictures with a
    // stroke between them is three layers; a hundred nodes in a row is one.
    [Test]
    public void OneLayerPerRunOfNeighboursAndNoMore()
    {
        var (root, canvas, scene, cache) = Stage();

        scene.Add(new ElementItem(new CanvasNode { Title = "One" }, new Rect(0, 0, 80, 40)));
        scene.Add(new ElementItem(new CanvasNode { Title = "Two" }, new Rect(100, 0, 80, 40)));
        scene.Add(new Marker { Bounds = new Rect(0, 60, 120, 40) });
        scene.Add(new ElementItem(new CanvasNode { Title = "Three" }, new Rect(0, 120, 80, 40)));

        Frame(root, canvas, cache);

        var stack = canvas.GetTemplateChild("PART_Layers") as Panel;

        Assert.That(stack.Children.Count, Is.EqualTo(3),
            "two controls, a drawn thing and a control is three runs - one layer each");
    }

    // THE GLASS takes no presses: what a gesture is making is drawn over everything, and if it answered the pointer it
    // would be the thing found instead of the plane.
    [Test]
    public void TheGlassDoesNotTakeThePresses()
    {
        var (root, canvas, _, cache) = Stage();

        Frame(root, canvas, cache);

        var front = canvas.GetTemplateChild("PART_Front") as CanvasFrontLayer;

        Assert.That(front.IsHitTestVisible, Is.False);
    }

    // WHAT IS ON THE PLANE IS DRAWN BY THE LAYERS, so a repaint that marked only the canvas left the drawing showing
    // what it looked like a moment ago - in as many ages of itself as there are layers.
    [Test]
    public void RepaintingTheCanvasAlsoRepaintsTheLayersThatDrawIt()
    {
        var (root, canvas, scene, cache) = Stage();

        scene.Add(new Marker { Bounds = new Rect(0, 0, 120, 120) });
        Frame(root, canvas, cache);

        var drawn = Layer<CanvasDrawLayer>(canvas);

        Assert.That(drawn, Is.Not.Null, "the drawn item got no layer at all");
        Assert.That(drawn.IsGeometryValid, Is.True, "nothing was recorded for it");

        canvas.Scale = 2.0;

        Assert.That(drawn.IsGeometryValid, Is.False, "the camera moved and the layer did not hear about it");
    }
}
