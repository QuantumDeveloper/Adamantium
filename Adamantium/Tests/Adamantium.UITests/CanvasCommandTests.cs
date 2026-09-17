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
    private static (InfiniteCanvas Canvas, CanvasScene Scene) Stage()
    {
        var canvas = new InfiniteCanvas();
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

        var panes = canvas.Chrome.Count;

        Assert.That(canvas.DeleteCommand.CanExecute(), Is.True);
        canvas.DeleteCommand.Execute();

        Assert.Multiple(() =>
        {
            Assert.That(scene.Items, Has.Count.EqualTo(1), "it was taken out before the question was answered");
            Assert.That(canvas.Chrome, Has.Count.EqualTo(panes + 1), "nothing was asked");
        });
    }

    [Test]
    public void WithoutTheSwitchItJustDeletes()
    {
        var (canvas, scene) = Stage();
        var item = Box(0, 0);
        scene.Add(item);
        canvas.SelectMany(new ICanvasItem[] { item }, false);

        var panes = canvas.Chrome.Count;

        canvas.DeleteCommand.Execute();

        Assert.Multiple(() =>
        {
            Assert.That(scene.Items, Is.Empty, "the plain delete asked instead of deleting");
            Assert.That(canvas.Chrome, Has.Count.EqualTo(panes), "a question was put up that nobody asked for");
        });
    }
}
