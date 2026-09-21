using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>ONE ORDER FOR EVERYTHING ON THE PLANE. A control and a drawn thing stand in the same queue, and "bring to
/// front" moves either of them past the other - a stroke over a picture, a picture over a stroke.
/// <para>What that costs is layers: a control has to be a real child of one to be laid out, drawn and clicked, and a
/// layer is one place in paint order. So the canvas cuts a layer per RUN of neighbours of the same sort - not per
/// item, which is what keeps a plane of ten thousand things affordable.</para></summary>
[TestFixture]
public class CanvasOrderTests
{
    // The one part these are about: the place the canvas puts the layers it cuts from the scene. Built by hand rather
    // than taken from a theme, because the question is about order and not about how a theme dresses a canvas.
    private static Adamantium.UI.Core.Templates.ControlTemplate Template() =>
        new(() =>
        {
            var layers = new Adamantium.UI.Controls.Panels.Grid();
            var front = new CanvasFrontLayer();
            var root = new Adamantium.UI.Controls.Panels.Grid();

            root.Children.Add(layers);
            root.Children.Add(front);

            var result = new Adamantium.UI.Core.Templates.TemplateResult { RootComponent = root };

            result.RegisterName("PART_Layers", layers);
            result.RegisterName("PART_Front", front);
            return result;
        });

    private static InfiniteCanvas Canvas()
    {
        // The INK too: a pen with no brush does not start a stroke at all, and a test that drew nothing would pass
        // half its assertions without ever having drawn.
        var canvas = new InfiniteCanvas { Scene = new CanvasScene(), Ink = Brushes.Black, Template = Template() };

        canvas.Measure(new Size(800, 600));
        canvas.Arrange(new Rect(0, 0, 800, 600));

        return canvas;
    }

    private static StrokeItem Ink()
    {
        var ink = new StrokeItem(new Vector2(0, 0), Brushes.Black, 2);

        ink.Add(new Vector2(0, 0));
        ink.Add(new Vector2(50, 50));

        return ink;
    }

    private static ElementItem Picture() =>
        new(new Adamantium.UI.Controls.Image { Background = Brushes.White }, new Rect(0, 0, 100, 80));

    private static List<ICanvasItem> Order(InfiniteCanvas canvas)
    {
        var order = new List<ICanvasItem>();

        foreach (var item in canvas.ItemsHere()) order.Add(item);

        return order;
    }

    // WHAT IS DRAWN NOW GOES ON TOP - of everything, controls included. That is what drawing on a picture means, and
    // it needs no switch: a new thing goes at the top of the order like a new thing anywhere else.
    [Test]
    public void WhatIsDrawnNowGoesOnTopOfWhatIsAlreadyThere()
    {
        var canvas = Canvas();
        var picture = Picture();
        var ink = Ink();

        canvas.Scene.Add(picture);
        canvas.Place(ink);

        var order = Order(canvas);

        Assert.That(order[^1], Is.SameAs(ink), "the stroke went under the picture it was drawn on");
    }

    // ...AND IT CAN BE PUT BACK DOWN. This is the half a fixed ladder of bands could not do: sent to the back, the
    // stroke is under the picture, and the SAME order says so.
    [Test]
    public void ADrawnThingCanBeSentUnderAControl()
    {
        var canvas = Canvas();
        var picture = Picture();
        var ink = Ink();

        canvas.Scene.Add(picture);
        canvas.Place(ink);
        canvas.Scene.SendToBack(ink);

        Assert.That(Order(canvas)[0], Is.SameAs(ink), "the stroke would not go under the picture");
    }

    // ...AND A PICTURE CAN BE RAISED over a drawing, which is the other half and the one that was impossible.
    [Test]
    public void AControlCanBeRaisedOverWhatIsDrawn()
    {
        var canvas = Canvas();
        var picture = Picture();

        canvas.Scene.Add(picture);
        canvas.Place(Ink());
        canvas.Scene.BringToFront(picture);

        Assert.That(Order(canvas)[^1], Is.SameAs(picture), "the picture would not come up over the drawing");
    }

    // A PICTURE, A STROKE OVER IT, ANOTHER PICTURE OVER THAT - the arrangement that says the two really are in one
    // queue. Three runs, and the canvas cuts three layers for them.
    [Test]
    public void ThingsInterleaveAndEachRunGetsItsLayer()
    {
        var canvas = Canvas();

        canvas.Scene.Add(Picture());
        canvas.Place(Ink());
        canvas.Scene.Add(Picture());

        canvas.Measure(new Size(800, 600));
        canvas.Arrange(new Rect(0, 0, 800, 600));

        var stack = canvas.GetTemplateChild("PART_Layers") as Adamantium.UI.Controls.Panels.Panel;

        // Without a template there is nowhere to put them, and that is not what this test is about - it is about the
        // order, which stands whether or not anything has been arranged.
        var order = Order(canvas);

        Assert.Multiple(() =>
        {
            Assert.That(order, Has.Count.EqualTo(3));
            Assert.That(order[0], Is.InstanceOf<ElementItem>());
            Assert.That(order[1], Is.InstanceOf<StrokeItem>(), "the stroke is not between the two pictures");
            Assert.That(order[2], Is.InstanceOf<ElementItem>());

            if (stack != null)
            {
                Assert.That(stack.Children.Count, Is.EqualTo(3), "three runs want three layers");
            }
        });
    }

    // A RUN IS NEIGHBOURS, not kinds: ten controls in a row are one layer however many they are, which is what keeps a
    // graph of ten thousand nodes costing exactly what it always did.
    [Test]
    public void NeighboursOfOneSortShareALayer()
    {
        var canvas = Canvas();

        for (var i = 0; i < 10; i++) canvas.Scene.Add(Picture());

        canvas.Measure(new Size(800, 600));
        canvas.Arrange(new Rect(0, 0, 800, 600));

        var stack = canvas.GetTemplateChild("PART_Layers") as Adamantium.UI.Controls.Panels.Panel;

        if (stack != null) Assert.That(stack.Children.Count, Is.EqualTo(1), "ten neighbours took ten layers");
    }

    // AMONG THEMSELVES. "One order" is worth nothing if it only sorts the two SORTS: a shape has to be able to go over
    // another shape, and a control over another control, or what was built is two buckets with extra steps.
    [Test]
    public void OneDrawnThingCanGoOverAnother()
    {
        var canvas = Canvas();
        var first = new ShapeItem(CanvasShape.Rectangle, new Rect(0, 0, 40, 40), Brushes.Black, 1);
        var second = new ShapeItem(CanvasShape.Ellipse, new Rect(10, 10, 40, 40), Brushes.Black, 1);

        canvas.Place(first);
        canvas.Place(second);

        canvas.Measure(new Size(800, 600));
        canvas.Arrange(new Rect(0, 0, 800, 600));

        var drawn = Layer(canvas);

        Assert.That(drawn, Is.Not.Null, "the shapes got no layer at all");
        Assert.That(Painted(drawn), Is.EqualTo(new ICanvasItem[] { first, second }), "they start as they were added");

        canvas.Scene.BringToFront(first);
        canvas.Measure(new Size(800, 600));
        canvas.Arrange(new Rect(0, 0, 800, 600));

        Assert.That(Painted(Layer(canvas)), Is.EqualTo(new ICanvasItem[] { second, first }),
            "one shape could not be brought over another");
    }

    [Test]
    public void OneControlCanGoOverAnother()
    {
        var canvas = Canvas();
        var first = Picture();
        var second = Picture();

        canvas.Scene.Add(first);
        canvas.Scene.Add(second);

        canvas.Measure(new Size(800, 600));
        canvas.Arrange(new Rect(0, 0, 800, 600));

        var host = Hosting(canvas);

        Assert.That(host, Is.Not.Null, "the controls got no layer at all");
        Assert.That(At(host, first), Is.LessThan(At(host, second)), "they start as they were added");

        canvas.Scene.BringToFront(first);
        canvas.Measure(new Size(800, 600));
        canvas.Arrange(new Rect(0, 0, 800, 600));

        host = Hosting(canvas);

        Assert.That(At(host, first), Is.GreaterThan(At(host, second)),
            "one control could not be brought over another");
    }

    // Where a control sits in the layer's children, which is the order they are painted in.
    private static int At(CanvasElementLayer host, ElementItem item)
    {
        for (var i = 0; i < host.Children.Count; i++)
        {
            if (ReferenceEquals(host.Children[i], item.Element)) return i;
        }

        return -1;
    }

    private static CanvasDrawLayer Layer(InfiniteCanvas canvas) => Find<CanvasDrawLayer>(canvas);

    private static CanvasElementLayer Hosting(InfiniteCanvas canvas) => Find<CanvasElementLayer>(canvas);

    private static T Find<T>(InfiniteCanvas canvas) where T : class
    {
        if (canvas.GetTemplateChild("PART_Layers") is not Adamantium.UI.Controls.Panels.Panel stack) return null;

        foreach (var child in stack.Children)
        {
            if (child is T wanted) return wanted;
        }

        return null;
    }

    // What the layer will paint, in the order it will paint it.
    private static ICanvasItem[] Painted(CanvasDrawLayer layer) => layer.Painted;

    // BETWEEN TWO OTHERS, which is where arranging actually happens. The ends are reachable by the two commands that
    // go all the way; everything in the middle needs a step, and without one a shape is either under everything or
    // over everything and never in its place.
    [Test]
    public void AShapeCanBePutBetweenTwoOthers()
    {
        var canvas = Canvas();
        var bottom = Rect(0);
        var middle = Rect(10);
        var top = Rect(20);

        canvas.Place(bottom);
        canvas.Place(middle);
        canvas.Place(top);

        // Send the TOP one down one place: it should land between the other two, not at the bottom.
        canvas.Select(top, false);
        canvas.SendBackwardCommand.Execute(null);

        Assert.That(Order(canvas), Is.EqualTo(new ICanvasItem[] { bottom, top, middle }),
            "a step took it further than one place");

        // ...and back up again, which must return it exactly where it was.
        canvas.BringForwardCommand.Execute(null);

        Assert.That(Order(canvas), Is.EqualTo(new ICanvasItem[] { bottom, middle, top }),
            "a step forward did not undo a step back");
    }

    // AT THE END IT SAYS SO, rather than doing nothing: a button that looks pressable and changes nothing reads as
    // broken, and this is what greys it out.
    [Test]
    public void AtTheEndThereIsNothingToStepPast()
    {
        var canvas = Canvas();
        var bottom = Rect(0);
        var top = Rect(10);

        canvas.Place(bottom);
        canvas.Place(top);

        canvas.Select(top, false);

        Assert.Multiple(() =>
        {
            Assert.That(canvas.BringForwardCommand.CanExecute(null), Is.False, "the top one offered to go higher");
            Assert.That(canvas.SendBackwardCommand.CanExecute(null), Is.True);
        });

        canvas.Select(bottom, false);

        Assert.Multiple(() =>
        {
            Assert.That(canvas.SendBackwardCommand.CanExecute(null), Is.False, "the bottom one offered to go lower");
            Assert.That(canvas.BringForwardCommand.CanExecute(null), Is.True);
        });
    }

    // ONE PRESS ON A CROWDED PLANE. Five hundred things, and the two that cover each other are the two hundredth and
    // the five hundredth: a step defined by the LIST would be three hundred presses - three hundred whose number
    // nobody could know, since how deep a thing sits is not something a drawing shows. Defined by what OVERLAPS, it is
    // one, and everything in between is left exactly where it was.
    [Test]
    public void OnePressIsEnoughHoweverMuchLiesBetween()
    {
        var canvas = Canvas();
        var under = Rect(0);

        canvas.Place(under);

        // THREE HUNDRED somewhere else entirely - none of them touches the two that matter.
        for (var i = 0; i < 300; i++) canvas.Place(Rect(500 + i * 60));

        var over = Rect(10);   // back over the first one

        canvas.Place(over);

        Assert.That(Order(canvas)[0], Is.SameAs(under), "the stage is not what this test says it is");

        canvas.Select(under, false);
        canvas.BringForwardCommand.Execute(null);

        var order = Order(canvas);

        Assert.Multiple(() =>
        {
            Assert.That(order[^1], Is.SameAs(under), "one press did not put it over the thing covering it");
            Assert.That(order[^2], Is.SameAs(over), "it went further than the thing it had to get past");
            Assert.That(order, Has.Count.EqualTo(302), "something was lost on the way");
        });
    }

    // ...AND NOTHING IN THE WAY MEANS NOTHING TO DO. A button that is offered and changes nothing reads as broken;
    // this says "there is nothing in front of this" by going grey.
    [Test]
    public void WithNothingOverlappingThereIsNothingToStepPast()
    {
        var canvas = Canvas();
        var alone = Rect(0);

        canvas.Place(alone);
        canvas.Place(Rect(400));   // far away, touching nothing

        canvas.Select(alone, false);

        Assert.That(canvas.BringForwardCommand.CanExecute(null), Is.False,
            "it offered to get over something that is not in front of it");
    }

    // A STEP IS PAST WHAT IS SEEN. A scene may hold a drawing and a graph at once; a step that moved a shape past a
    // node nobody is looking at is a press that appears to do nothing.
    [Test]
    public void AStepPassesWhatIsOnScreenAndNotWhatIsHidden()
    {
        var canvas = Canvas();
        var under = Rect(0);
        var over = Rect(10);

        canvas.Mode = CanvasMode.Drawing;
        canvas.Place(under);

        // A NODE between them, which a drawing does not show.
        canvas.Scene.Add(new ElementItem(new CanvasNode { Title = "Add" }, new Rect(0, 0, 80, 40)));
        canvas.Place(over);

        canvas.Select(over, false);
        canvas.SendBackwardCommand.Execute(null);

        Assert.That(Order(canvas), Is.EqualTo(new ICanvasItem[] { over, under }),
            "the step was spent on something the drawing does not show");
    }

    private static ShapeItem Rect(double at) =>
        new(CanvasShape.Rectangle, new Rect(at, at, 40, 40), Brushes.Black, 1);

    // A COMMENT FRAME is made at the BOTTOM and stays there. It used to be held down by a band; now the order is the
    // only thing holding it, so this is what keeps a frame from being a sheet of colour over its own nodes.
    [Test]
    public void ACommentFrameIsMadeAtTheBottom()
    {
        var canvas = Canvas();

        canvas.Mode = CanvasMode.Nodes;
        canvas.Scene.Add(new ElementItem(new CanvasNode { Title = "Add" }, new Rect(0, 0, 120, 60)));
        canvas.SelectMany(Order(canvas), false);

        var frame = canvas.FrameSelection("Comment");

        Assert.That(frame, Is.Not.Null, "no frame was drawn at all");
        Assert.That(Order(canvas)[0], Is.SameAs(frame), "the frame was left over the nodes it is drawn round");
    }

    // WHAT A GESTURE SHOWS IS WHERE IT WILL LAND. The half-made thing under the pen is drawn by the TOOL, over
    // everything, because that is where it is going - drawn anywhere else it would show one thing and mean another.
    [Test]
    public void WhatIsBeingDrawnShowsOverEverything()
    {
        var canvas = Canvas();
        var tool = new PenTool();

        canvas.Tool = tool;
        canvas.Scene.Add(Picture());

        tool.OnPressed(canvas, Pointer(new Vector2(10, 10)));
        tool.OnMoved(canvas, Pointer(new Vector2(60, 40)));

        var glass = new Rendering.RecordingDrawingSession();

        canvas.DrawInProgress(glass);

        Assert.That(glass.Rectangles, Is.Not.Empty, "the stroke under the pen was not drawn on the glass");
    }

    // A CONTROL GROUPED WITH A DRAWING IS STILL ON THE PLANE. A group draws its children by asking each to draw itself,
    // and a control draws nothing that way: it is seen because it is a living child of a layer. So a picture gathered
    // into a group vanished - it was taken out of the scene, and nothing put it into a layer again.
    [Test]
    public void APictureGatheredIntoAGroupIsStillHosted()
    {
        var canvas = Canvas();
        var picture = Picture();

        canvas.Scene.Add(Ink());
        canvas.Scene.Add(picture);
        canvas.SelectMany(Order(canvas), false);

        var group = canvas.GroupSelection();

        canvas.Measure(new Size(800, 600));
        canvas.Arrange(new Rect(0, 0, 800, 600));

        Assert.That(group, Is.Not.Null, "nothing was gathered at all");
        Assert.That(Hosted(canvas), Does.Contain(picture.Element),
            "the picture went into a group and off the plane with it");
    }

    // ...AND THE SAME THING THE WAY A HAND DOES IT: a picture put down with the texture tool, two strokes drawn over
    // it with the pen, and the lot gathered up. The tools are what an application actually goes through, and what they
    // leave on the plane is not quite what a test builds by hand.
    [Test]
    public void APicturePutDownWithTheToolSurvivesBeingGathered()
    {
        var canvas = Canvas();
        var texture = new TextureTool();
        var pen = new PenTool();

        canvas.Tool = texture;
        texture.OnPressed(canvas, Pointer(new Vector2(0, 0)));
        texture.OnMoved(canvas, Pointer(new Vector2(120, 90)));
        texture.OnReleased(canvas, Pointer(new Vector2(120, 90)));

        canvas.Tool = pen;

        for (var stroke = 0; stroke < 2; stroke++)
        {
            pen.OnPressed(canvas, Pointer(new Vector2(10, 10 + stroke * 20)));
            pen.OnMoved(canvas, Pointer(new Vector2(90, 40 + stroke * 20)));
            pen.OnReleased(canvas, Pointer(new Vector2(90, 40 + stroke * 20)));
        }

        canvas.Measure(new Size(800, 600));
        canvas.Arrange(new Rect(0, 0, 800, 600));

        var placed = new List<ICanvasItem>(canvas.ItemsHere());
        ElementItem picture = null;

        foreach (var item in placed)
        {
            if (item is ElementItem element) picture = element;
        }

        Assert.That(picture, Is.Not.Null, "the texture tool put nothing down");

        Assert.That(Hosted(canvas), Does.Contain(picture.Element), "the picture was not on the plane to begin with");

        canvas.SelectMany(placed, false);
        canvas.GroupSelection();

        canvas.Measure(new Size(800, 600));
        canvas.Arrange(new Rect(0, 0, 800, 600));

        Assert.That(Hosted(canvas), Does.Contain(picture.Element), "the picture went off the plane when it was gathered");
    }

    // WHAT THE POINTER SAYS IS WHAT THE NEXT PRESS DOES. A press inside the frame belongs to the tool - that is what
    // lets a stroke be drawn over a picture already selected - so with a pen in hand the body of the selection drags
    // nothing, and a cursor saying "this moves" is a promise the press breaks.
    [Test]
    public void OverSomethingSelectedThePointerWearsTheToolThatIsHeld()
    {
        var canvas = Canvas();
        var picture = Picture();

        canvas.SelectionBrush = Brushes.DodgerBlue;
        canvas.HandleBrush = Brushes.White;

        canvas.Scene.Add(picture);
        canvas.Select(picture, false);

        var over = canvas.WorldToScreen(new Vector2(50, 40));

        canvas.Tool = new SelectTool();
        canvas.ShowPointerAt(over);

        Assert.That(canvas.Cursor, Is.EqualTo(Adamantium.UI.Core.Input.Cursors.SizeAll),
            "with the select tool in hand the body of the selection does move");

        canvas.Tool = new PenTool();
        canvas.ShowPointerAt(over);

        Assert.That(canvas.Cursor, Is.Not.EqualTo(Adamantium.UI.Core.Input.Cursors.SizeAll),
            "with a pen in hand the pointer still promised a move it would not make");
    }

    // The controls the canvas is actually holding - what a layer has as its own children, which is the only way a
    // control is laid out, drawn or clicked.
    private static List<IUIComponent> Hosted(InfiniteCanvas canvas)
    {
        var found = new List<IUIComponent>();

        Walk(canvas, found);

        return found;
    }

    private static void Walk(IUIComponent at, List<IUIComponent> into)
    {
        if (at is CanvasElementLayer layer)
        {
            foreach (var child in layer.Children) into.Add(child);
        }

        foreach (var child in at.VisualChildren) Walk(child, into);
    }

    // THE FRAME STANDS WHERE ITS OBJECT STANDS. Where a thing is in the order is the one question somebody moving it is
    // asking, and a frame floating over everything answers a different one - so it is drawn by the layer that holds the
    // thing, straight after it, and what was put on the plane afterwards passes over it like it passes over the object.
    [Test]
    public void TheFrameStandsWhereTheThingItIsRoundStands()
    {
        var canvas = Canvas();
        var under = Ink();

        canvas.Scene.Add(under);
        canvas.Scene.Add(Ink());
        canvas.Select(under, false);

        Assert.That(canvas.ChromeGoesAfter(under), Is.True, "the frame was not put with the item it is round");
        Assert.That(canvas.ChromeGoesOnGlass, Is.False, "the frame floated over the thing standing on top of it");
    }

    // SEVERAL THINGS HELD AT ONCE STILL GET A FRAME - one frame, round the lot. It stands where the topmost of them
    // stands, because anywhere lower and the selection's own contents would be drawn over it.
    [Test]
    public void AFrameIsDrawnRoundSeveralThingsHeldAtOnce()
    {
        var canvas = Canvas();
        var under = Ink();
        var over = Ink();

        canvas.Scene.Add(under);
        canvas.Scene.Add(over);
        canvas.SelectMany(new List<ICanvasItem> { under, over }, false);

        canvas.Measure(new Size(800, 600));
        canvas.Arrange(new Rect(0, 0, 800, 600));

        var somewhere = canvas.ChromeGoesAfter(over) || canvas.ChromeGoesBefore(over) || canvas.ChromeGoesOnGlass;

        Assert.That(somewhere, Is.True, "a band round several things is drawn nowhere at all");

        // ...AND IT IS ACTUALLY DRAWN. Two strokes offer no grips between them - a line is shown by its own ends - and
        // the box used to be left out with them, so a band round several things answered with nothing at all.
        canvas.SelectionBrush = Brushes.DodgerBlue;
        canvas.HandleBrush = Brushes.White;

        var glass = new Rendering.RecordingDrawingSession();

        canvas.DrawChrome(glass);

        Assert.That(glass.Rectangles, Is.Not.Empty, "the band round several things drew no box");
    }

    // ...AND FOLLOWS IT UP. Raised to the top, the thing has nothing drawn over it any more and the frame comes with it.
    [Test]
    public void TheFrameFollowsWhatItIsRoundToTheTop()
    {
        var canvas = Canvas();
        var moved = Ink();

        canvas.Scene.Add(moved);
        canvas.Scene.Add(Ink());
        canvas.Select(moved, false);
        canvas.Scene.BringToFront(moved);
        canvas.Measure(new Size(800, 600));
        canvas.Arrange(new Rect(0, 0, 800, 600));

        Assert.That(canvas.ChromeGoesAfter(moved), Is.True, "the frame stayed behind the thing it is round");
    }

    // A CONTROL IS DRAWN BY NOBODY - it is a child of its own layer - so the frame round one goes at the start of the
    // first drawn run above it: over the control, under whatever was put on the plane after it.
    [Test]
    public void TheFrameRoundAControlGoesOverItAndUnderWhatIsAbove()
    {
        var canvas = Canvas();
        var picture = Picture();
        var over = Ink();

        canvas.Scene.Add(picture);
        canvas.Scene.Add(over);
        canvas.Select(picture, false);

        Assert.That(canvas.ChromeGoesBefore(over), Is.True, "the frame round a control was not put over the control");
        Assert.That(canvas.ChromeGoesOnGlass, Is.False, "the frame floated over the stroke drawn on top of the picture");
    }

    // ...and a control at the very top has no drawn run above it to hold the frame, so the glass is where it belongs.
    // Nothing draws a control, so without this the frame round the topmost picture would be drawn nowhere at all.
    [Test]
    public void TheFrameFallsToTheGlassWhenNothingIsDrawnOverWhatIsHeld()
    {
        var canvas = Canvas();
        var top = Picture();

        canvas.Scene.Add(Ink());
        canvas.Scene.Add(top);
        canvas.Select(top, false);

        Assert.That(canvas.ChromeGoesOnGlass, Is.True, "the frame round the topmost picture was drawn nowhere at all");
    }

    // THE NUMBER IS THE PLACE, WRITTEN DOWN. Every item knows where it stands, and the scene is what writes it - so
    // the panel can show it without anybody counting, and there is no second truth to drift.
    [Test]
    public void EveryItemKnowsWhereItStands()
    {
        var scene = new CanvasScene();
        var back = Ink();
        var middle = Ink();
        var front = Ink();

        scene.Add(back);
        scene.Add(middle);
        scene.Add(front);

        Assert.That(new[] { back.Order, middle.Order, front.Order }, Is.EqualTo(new[] { 0, 1, 2 }));
    }

    // AND IT IS REWRITTEN WHEN THE QUEUE MOVES. A number left over from before the move would say the old place, which
    // is worse than saying nothing at all.
    [Test]
    public void TheNumberFollowsTheMove()
    {
        var scene = new CanvasScene();
        var one = Ink();
        var two = Ink();
        var three = Ink();

        scene.Add(one);
        scene.Add(two);
        scene.Add(three);
        scene.BringToFront(one);

        Assert.That(new[] { two.Order, three.Order, one.Order }, Is.EqualTo(new[] { 0, 1, 2 }));
    }

    // WRITING THE NUMBER MOVES THE THING. What a person types in the panel is a request to stand there, and a panel
    // where typing a number changed only the number would be a panel that lies.
    [Test]
    public void WritingTheNumberPutsItThere()
    {
        var scene = new CanvasScene();
        var one = Ink();
        var two = Ink();
        var three = Ink();

        scene.Add(one);
        scene.Add(two);
        scene.Add(three);

        Assert.That(scene.Reposition(three, 0), Is.True);
        Assert.That(scene.Items, Is.EqualTo(new ICanvasItem[] { three, one, two }));
        Assert.That(three.Order, Is.EqualTo(0));
    }

    // A NUMBER PAST THE END MEANS THE TOP. Asked for the hundredth place in a scene of three, a person means "on top of
    // everything" - and a box that refused what was typed would teach them nothing about what to type instead.
    [Test]
    public void ANumberPastTheEndMeansTheTop()
    {
        var scene = new CanvasScene();
        var one = Ink();
        var two = Ink();

        scene.Add(one);
        scene.Add(two);

        Assert.That(scene.Reposition(one, 500), Is.True);
        Assert.That(one.Order, Is.EqualTo(1));
        Assert.That(scene.Items[1], Is.SameAs(one));
    }

    // COUNTED AMONG WHAT CAN BE SEEN. A plane may hold a drawing and a graph at once and shows one of them; numbered
    // through both, a drawing of three things gave its topmost the number eight, because five nodes nobody can see
    // were standing between them. A number a person cannot arrive at by looking is not a number they can type.
    [Test]
    public void TheNumberDoesNotCountWhatTheModeDoesNotShow()
    {
        var scene = new CanvasScene();
        var first = Ink();
        var second = Ink();

        scene.Add(first);
        scene.Add(new ElementItem(new CanvasNode { Title = "Add" }, new Rect(0, 0, 120, 60)));
        scene.Add(new ElementItem(new CanvasNode { Title = "Mul" }, new Rect(0, 0, 120, 60)));
        scene.Add(second);

        Assert.That(new[] { first.Order, second.Order }, Is.EqualTo(new[] { 0, 1 }),
            "the drawing was numbered through nodes that are not on show with it");
    }

    // ...AND PUT THERE THE SAME WAY. Asked for the top of a drawing, an item has to land over the last drawn thing -
    // not at that index of the list, which in a plane holding both kinds is somebody else's place entirely.
    [Test]
    public void WritingTheNumberCountsAmongWhatCanBeSeen()
    {
        var scene = new CanvasScene();
        var first = Ink();
        var second = Ink();
        var node = new ElementItem(new CanvasNode { Title = "Add" }, new Rect(0, 0, 120, 60));

        scene.Add(first);
        scene.Add(node);
        scene.Add(second);

        Assert.That(scene.Reposition(first, 1), Is.True);
        Assert.That(new[] { first.Order, second.Order }, Is.EqualTo(new[] { 1, 0 }));
        // The drawing's OWN order is what was asked about; where the node ended up among them means nothing, because
        // the two are never on show together.
        var drawn = new List<ICanvasItem>();

        foreach (var item in scene.Items)
        {
            if (item.Mode == CanvasMode.Drawing) drawn.Add(item);
        }

        Assert.That(drawn, Is.EqualTo(new ICanvasItem[] { second, first }),
            "the ink asked for the top of the drawing did not end up over the other ink");
    }

    // THE PANEL IS THE WAY IN. Writing the number on the item and telling the canvas an edit happened has to be enough
    // - the canvas finds the disagreement between where an item says it is and where it actually is, and settles it.
    [Test]
    public void TheCanvasObeysTheNumberWrittenInThePanel()
    {
        var canvas = Canvas();
        var back = Ink();
        var front = Ink();

        canvas.Scene.Add(back);
        canvas.Scene.Add(front);
        canvas.SelectMany(new List<ICanvasItem> { front }, false);

        front.Order = 0;
        canvas.SettleOrder();

        Assert.That(Order(canvas)[0], Is.SameAs(front), "the number written in the panel did not move the item");
        Assert.That(back.Order, Is.EqualTo(1), "the one it went under was not renumbered");
    }

    private static CanvasPointerEventArgs Pointer(Vector2 at) => new()
    {
        World = at,
        Pointer = at,
        Button = Adamantium.UI.Core.Input.MouseButtons.Left,
        ClickCount = 1
    };
}
