using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>WHAT THE CANVAS CAN DO, as commands a button binds to.
/// <para>They are the control's own for one reason: a canvas that made every application write its own click handlers
/// for undo, zoom and delete would be a kit of parts. Whoever takes the control binds a button and is done - and the
/// button switches itself off when there is nothing to do, because the command says so.</para></summary>
public class CanvasCommandTests
{
    // IN A WINDOW, because the canvas asks its questions in the engine's own overlay dialog - and an overlay is shown
    // on a window. A canvas measured on its own has nowhere to put a question, which is a different state and not the
    // one a person is ever in.
    private static (InfiniteCanvas Canvas, CanvasScene Scene) Stage()
    {
        var canvas = new InfiniteCanvas();
        var window = new Window { Width = 800, Height = 600, Content = canvas };

        Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);

        canvas.Measure(new Size(800, 600), force: true);
        canvas.Arrange(new Rect(0, 0, 800, 600));

        var scene = new CanvasScene();
        canvas.Scene = scene;

        return (canvas, scene);
    }

    private static ShapeItem Box(double x, double y) =>
        new(CanvasShape.Rectangle, new Rect(x, y, 60, 40), Brushes.White, 2);

    // A SHAPE THE PAGE PUT THERE AS DATA MOVES LIKE ANY OTHER. Its box IS the object's - one storage, read and written
    // through - so a drag that wrote to the item's own field instead left the shape drawn where the object still said
    // it was, which looks exactly like a shape that cannot be dragged at all.
    [Test]
    public void AShapeMadeFromDataMovesWithIt()
    {
        var canvas = new InfiniteCanvas { Scene = new CanvasScene() };
        canvas.Measure(new Size(800, 600), force: true);
        canvas.Arrange(new Rect(0, 0, 800, 600));

        var model = new Graph.PlacedObject
        {
            Left = 10,
            Top = 20,
            Width = 100,
            Height = 60,
            Content = new Graph.PlacedShape(CanvasShape.Rectangle)
        };

        canvas.Objects = new Adamantium.Core.Collections.TrackingCollection<ICanvasObject> { model };

        var drawn = canvas.ItemsHere().OfType<ShapeItem>().SingleOrDefault();

        Assert.That(drawn, Is.Not.Null, "the object never reached the plane");

        drawn.Move(new Vector2(40, 15));

        Assert.Multiple(() =>
        {
            Assert.That(model.Left, Is.EqualTo(50).Within(1e-9), "the drag never reached the object");
            Assert.That(model.Top, Is.EqualTo(35).Within(1e-9));
            Assert.That(drawn.Bounds.X, Is.EqualTo(50).Within(1e-9), "the shape is drawn where it used to be");
        });
    }

    [Test]
    public void UndoAndRedoFollowWhatThereIsToTakeBack()
    {
        var (canvas, scene) = Stage();
        var item = Box(0, 0);
        scene.Add(item);

        Assert.That(canvas.UndoCommand.CanExecute(), Is.False, "undo is offered with nothing behind it");
        Assert.That(canvas.RedoCommand.CanExecute(), Is.False);

        canvas.BeginEdit("Move");
        item.Move(new Vector2(10, 0));
        canvas.EndEdit();

        Assert.That(canvas.UndoCommand.CanExecute(), Is.True, "something was done and undo stayed off");

        canvas.UndoCommand.Execute();

        Assert.Multiple(() =>
        {
            Assert.That(item.Bounds.X, Is.EqualTo(0).Within(1e-9), "the command did not take the move back");
            Assert.That(canvas.RedoCommand.CanExecute(), Is.True, "there is something to put back and redo is off");
        });

        canvas.RedoCommand.Execute();

        Assert.That(item.Bounds.X, Is.EqualTo(10).Within(1e-9), "the command did not put the move back");
    }

    // A command about the SELECTION is off while there is none - which is what a bound button shows without anyone
    // writing a line about it.
    [Test]
    public void WhatIsAboutTheSelectionIsOffWithoutOne()
    {
        var (canvas, scene) = Stage();
        var first = Box(0, 0);
        var second = Box(200, 0);
        scene.Add(first);
        scene.Add(second);

        Assert.Multiple(() =>
        {
            Assert.That(canvas.DeleteCommand.CanExecute(), Is.False);
            Assert.That(canvas.FitSelectionCommand.CanExecute(), Is.False);
            Assert.That(canvas.GroupCommand.CanExecute(), Is.False, "there is nothing to make one thing of");
        });

        canvas.SelectMany(new ICanvasItem[] { first, second }, false);

        Assert.Multiple(() =>
        {
            Assert.That(canvas.DeleteCommand.CanExecute(), Is.True);
            Assert.That(canvas.FitSelectionCommand.CanExecute(), Is.True);
            Assert.That(canvas.GroupCommand.CanExecute(), Is.True);
        });
    }

    // The camera is the canvas's to steer, so the buttons that steer it are its commands. The parameter is the factor;
    // with none, one step of the canvas's own.
    [Test]
    public void TheZoomCommandsMoveTheCamera()
    {
        var (canvas, _) = Stage();
        var was = canvas.Scale;

        // A zoom EASES to where it was sent, so the camera is read once it has got there.
        canvas.ZoomInCommand.Execute();
        Settle();

        Assert.That(canvas.Scale, Is.GreaterThan(was), "zooming in did nothing");

        canvas.ZoomOutCommand.Execute();
        Settle();

        Assert.That(canvas.Scale, Is.EqualTo(was).Within(1e-6), "a step in and a step out is where it started");

        canvas.ZoomInCommand.Execute(4.0);
        Settle();

        Assert.That(canvas.Scale, Is.EqualTo(was * 4).Within(1e-6), "the factor given was not the factor used");

        canvas.HomeCommand.Execute();

        Assert.That(canvas.Scale, Is.EqualTo(1).Within(1e-9), "home is one to one");
    }

    private static void Settle()
    {
        for (var i = 0; i < 40; i++) Adamantium.UI.Core.Media.Animation.AnimationManager.Tick(0.05);
    }

    // ASKING BEFORE DELETING is the canvas's own, under a switch. Off, it deletes; on, it asks in its own chrome and
    // nothing goes until that is answered - and an application writes none of it.
    [Test]
    public void AskingBeforeDeletingIsTheCanvasSOwn()
    {
        var (canvas, scene) = Stage();
        var item = Box(0, 0);
        scene.Add(item);
        canvas.SelectMany(new ICanvasItem[] { item }, false);

        canvas.ConfirmsDelete = true;

        Assert.That(canvas.DeleteCommand.CanExecute(), Is.True);
        canvas.DeleteCommand.Execute();

        Assert.Multiple(() =>
        {
            Assert.That(scene.Items, Has.Count.EqualTo(1), "it was taken out before the question was answered");
            Assert.That(canvas.IsAsking, Is.True, "nothing was asked");
        });
    }

    [Test]
    public void WithoutTheSwitchItJustDeletes()
    {
        var (canvas, scene) = Stage();
        var item = Box(0, 0);
        scene.Add(item);
        canvas.SelectMany(new ICanvasItem[] { item }, false);

        canvas.DeleteCommand.Execute();

        Assert.Multiple(() =>
        {
            Assert.That(scene.Items, Is.Empty, "the plain delete asked instead of deleting");
            Assert.That(canvas.IsAsking, Is.False, "a question was put up that nobody asked for");
        });
    }

    // THE QUESTION IS THE ENGINE'S OWN DIALOG - the modal overlay window everything else in an application asks with,
    // put up on the window the canvas stands in. Not a plate of the canvas's own invention: a second kind of dialog is
    // a second thing for a person to learn, and it read as one.
    [Test]
    public void TheQuestionIsAskedInTheOverlayDialog()
    {
        var canvas = new InfiniteCanvas { Scene = new CanvasScene() };
        var window = new Window { Width = 800, Height = 600, Content = canvas };

        Adamantium.UI.Extensions.WindowExtension.UpdateTree(window);
        canvas.Measure(new Size(800, 600), force: true);
        canvas.Arrange(new Rect(0, 0, 800, 600));

        canvas.Scene.Add(Box(0, 0));
        canvas.ConfirmsDelete = true;

        canvas.ClearCommand.Execute();

        var asked = Overlays(window);

        Assert.Multiple(() =>
        {
            Assert.That(canvas.IsAsking, Is.True, "nothing was asked");
            Assert.That(asked, Has.Count.EqualTo(1), "the question was not put in an overlay dialog");
            Assert.That(asked.Count > 0 ? asked[0].Title : null, Is.EqualTo("Clear the canvas"),
                "the dialog does not say what it is about");
        });
    }

    private static System.Collections.Generic.List<OverlayWindow> Overlays(Window window)
    {
        var found = new System.Collections.Generic.List<OverlayWindow>();

        foreach (var root in window.PopupRoots) Gather(root, found);

        return found;
    }

    private static void Gather(IUIComponent within, System.Collections.Generic.List<OverlayWindow> into)
    {
        if (within is OverlayWindow overlay) into.Add(overlay);

        foreach (var child in within.VisualChildren)
        {
            if (child is IUIComponent visual) Gather(visual, into);
        }
    }

    // A BUTTON IS ASKED AGAIN when what it answers from moves. A command left out of that list is asked exactly once -
    // while nothing is selected and the plane is empty - and its button is then grey for the rest of the session,
    // whatever is picked afterwards. Which is what "the buttons are disabled for no reason" was.
    [Test]
    public void EveryCommandSaysItCanBePressedAgainWhenTheSelectionChanges()
    {
        var (canvas, scene) = Stage();

        var told = new System.Collections.Generic.List<string>();

        void Watch(string name, CanvasCommand command) =>
            command.CanExecuteChanged += (_, _) => told.Add(name);

        Watch("align", canvas.AlignCommand);
        Watch("spread", canvas.SpreadCommand);
        Watch("frame", canvas.FrameCommand);
        Watch("clear", canvas.ClearCommand);
        Watch("delete", canvas.DeleteCommand);

        var one = Box(0, 0);
        var two = Box(200, 0);
        var three = Box(400, 0);

        scene.Add(one);
        scene.Add(two);
        scene.Add(three);
        canvas.SelectMany(new ICanvasItem[] { one, two, three }, false);

        Assert.Multiple(() =>
        {
            Assert.That(told, Does.Contain("align"), "the lining-up button was never asked again");
            Assert.That(told, Does.Contain("spread"), "the spreading button was never asked again");
            Assert.That(told, Does.Contain("frame"), "the framing button was never asked again");
            Assert.That(told, Does.Contain("clear"), "the bin was never asked again");
            Assert.That(told, Does.Contain("delete"), "the delete button was never asked again");

            // ...and what they answer NOW is what the state says.
            Assert.That(canvas.AlignCommand.CanExecute("Left"), Is.True);
            Assert.That(canvas.SpreadCommand.CanExecute("Vertical"), Is.True);
            Assert.That(canvas.FrameCommand.CanExecute(), Is.True);
            Assert.That(canvas.ClearCommand.CanExecute(), Is.True);
        });
    }

    // LINING UP, as a button says it: one command and the edge as its word, so a theme's four buttons are four lines of
    // markup and not four properties to keep in step.
    [Test]
    public void AligningTakesTheEdgeAsAWord()
    {
        var (canvas, scene) = Stage();
        var left = Box(10, 0);
        var right = Box(200, 300);

        scene.Add(left);
        scene.Add(right);
        canvas.SelectMany(new ICanvasItem[] { left, right }, false);

        Assert.That(canvas.AlignCommand.CanExecute("Left"), Is.True);
        canvas.AlignCommand.Execute("Left");

        Assert.Multiple(() =>
        {
            Assert.That(right.Bounds.X, Is.EqualTo(left.Bounds.X).Within(1e-9), "the edges were not lined up");
            Assert.That(canvas.AlignCommand.CanExecute("sideways"), Is.False, "a word that is no edge went through");
        });
    }

    [Test]
    public void LiningUpNeedsTwoThingsAndSpreadingThree()
    {
        var (canvas, scene) = Stage();
        var one = Box(10, 0);

        scene.Add(one);
        canvas.SelectMany(new ICanvasItem[] { one }, false);

        Assert.Multiple(() =>
        {
            Assert.That(canvas.AlignCommand.CanExecute("Left"), Is.False, "one thing was offered lining up");
            Assert.That(canvas.SpreadCommand.CanExecute("Vertical"), Is.False, "one thing was offered spreading out");
        });
    }

    // A COMMENT FRAME round what is selected, and the frame is then what is selected - the same as drawing one by hand.
    [Test]
    public void FramingDrawsOneRoundTheSelection()
    {
        var (canvas, scene) = Stage();
        var item = Box(0, 0);

        scene.Add(item);
        canvas.SelectMany(new ICanvasItem[] { item }, false);

        Assert.That(canvas.FrameCommand.CanExecute(), Is.True);
        canvas.FrameCommand.Execute("Notes");

        var frame = scene.Items.OfType<CanvasFrameItem>().SingleOrDefault();

        Assert.Multiple(() =>
        {
            Assert.That(frame, Is.Not.Null, "no frame was drawn");
            Assert.That(frame?.Title, Is.EqualTo("Notes"), "the frame was not given the title the button asked for");
        });
    }

    // EMPTYING THE PLANE goes through the same question as deleting a selection - and through the history, because it
    // is the action most worth being able to take back.
    [Test]
    public void ClearingEmptiesThePlaneAndAsksWhenToldTo()
    {
        var (canvas, scene) = Stage();

        scene.Add(Box(0, 0));
        scene.Add(Box(100, 100));

        canvas.ConfirmsDelete = true;

        Assert.That(canvas.ClearCommand.CanExecute(), Is.True);
        canvas.ClearCommand.Execute();

        Assert.Multiple(() =>
        {
            Assert.That(scene.Items, Has.Count.EqualTo(2), "the plane was emptied before the question was answered");
            Assert.That(canvas.IsAsking, Is.True, "nothing was asked");
        });

        canvas.ConfirmsDelete = false;
        canvas.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(scene.Items, Is.Empty, "the plane was not emptied");
            Assert.That(canvas.ClearCommand.CanExecute(), Is.False, "the bin stayed on over an empty plane");
        });
    }

    // ...AND IN A DRAWING TOO. The bin is about the canvas rather than about the mode it is being used in, so what it
    // can do is answered from the SCENE and not from what the mode is showing.
    [Test]
    public void TheBinIsOfferedInEitherMode()
    {
        var (canvas, scene) = Stage();

        canvas.Mode = CanvasMode.Nodes;
        scene.Add(Box(0, 0));

        Assert.That(canvas.ClearCommand.CanExecute(), Is.True, "a plane with a drawing on it read as empty");

        canvas.ClearCommand.Execute();

        Assert.That(scene.Items, Is.Empty, "the drawing was left behind by a graph's bin");
    }
}
