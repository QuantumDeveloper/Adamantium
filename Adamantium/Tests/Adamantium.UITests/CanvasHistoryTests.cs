using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Media;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>Undo and redo. A step is a BEFORE and an AFTER of the whole drawing, taken by comparison, so nothing new is
/// asked of an item - which is what makes a third-party one undoable without knowing any of this exists.</summary>
public class CanvasHistoryTests
{
    private static InfiniteCanvas Sized(CanvasScene scene, double width = 400, double height = 300)
    {
        var canvas = new InfiniteCanvas { Scene = scene, History = new CanvasHistory() };
        canvas.Measure(new Size(width, height), force: true);
        canvas.Arrange(new Rect(0, 0, width, height));

        return canvas;
    }

    private static ShapeItem Box(double x, double y, double size = 10) =>
        new(CanvasShape.Rectangle, new Rect(x, y, size, size), Brushes.White, 1);

    [Test]
    public void NothingToUndoToBeginWith()
    {
        var canvas = Sized(new CanvasScene());

        Assert.Multiple(() =>
        {
            Assert.That(canvas.History.CanUndo, Is.False);
            Assert.That(canvas.History.CanRedo, Is.False);
            Assert.That(canvas.Undo(), Is.False);
        });
    }

    // Something taken out comes back - in its own PLACE in paint order, not on top. Order here is what the drawing
    // looks like, so a step put back in the wrong order is a different drawing.
    [Test]
    public void DeletingIsUndoneInPlace()
    {
        var scene = new CanvasScene();
        var under = Box(0, 0);
        var middle = Box(20, 0);
        var over = Box(40, 0);

        foreach (var item in new[] { under, middle, over }) scene.Add(item);

        var canvas = Sized(scene);
        canvas.Select(middle, false);
        canvas.DeleteSelection();

        Assert.That(scene.Items, Is.EqualTo(new ICanvasItem[] { under, over }));

        Assert.That(canvas.Undo(), Is.True);
        Assert.That(scene.Items, Is.EqualTo(new ICanvasItem[] { under, middle, over }),
            "back where it was, not on top");
    }

    [Test]
    public void RedoPutsItBackAgain()
    {
        var scene = new CanvasScene();
        var only = Box(0, 0);
        scene.Add(only);

        var canvas = Sized(scene);
        canvas.Select(only, false);
        canvas.DeleteSelection();
        canvas.Undo();

        Assert.That(canvas.Redo(), Is.True);
        Assert.That(scene.Items, Is.Empty);
    }

    // A MOVE is put back by travelling, which is exact - not by being resized into its old box.
    [Test]
    public void AMoveIsUndoneExactly()
    {
        var scene = new CanvasScene();
        var item = Box(100, 100);
        scene.Add(item);

        var canvas = Sized(scene);
        canvas.BeginEdit("Move");
        item.Move(new Vector2(37.5, -12.25));
        canvas.EndEdit();

        Assert.That(canvas.Undo(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(item.Bounds.X, Is.EqualTo(100).Within(1e-9));
            Assert.That(item.Bounds.Y, Is.EqualTo(100).Within(1e-9));
        });
    }

    [Test]
    public void AResizeIsUndone()
    {
        var scene = new CanvasScene();
        var item = Box(0, 0);
        scene.Add(item);

        var canvas = Sized(scene);
        canvas.BeginEdit("Resize");
        item.Resize(new Rect(5, 5, 80, 40));
        canvas.EndEdit();

        canvas.Undo();

        Assert.That(item.Bounds, Is.EqualTo(new Rect(0, 0, 10, 10)));
    }

    // A gesture that changed nothing - a click that only selected - must not fill the history with steps that undo to
    // the same drawing.
    [Test]
    public void AnEditThatChangedNothingIsNotAStep()
    {
        var scene = new CanvasScene();
        scene.Add(Box(0, 0));

        var canvas = Sized(scene);
        canvas.BeginEdit("Nothing");
        canvas.EndEdit();

        Assert.That(canvas.History.CanUndo, Is.False);
    }

    // One step per GESTURE, however many things it touched: dragging ten things across the plane is one thing to take
    // back, not ten.
    [Test]
    public void OneEditIsOneStepHoweverManyThingsItTouched()
    {
        var scene = new CanvasScene();
        var first = Box(0, 0);
        var second = Box(40, 0);
        scene.Add(first);
        scene.Add(second);

        var canvas = Sized(scene);
        canvas.BeginEdit("Drag");
        first.Move(new Vector2(10, 10));
        second.Move(new Vector2(10, 10));
        canvas.EndEdit();

        canvas.Undo();

        Assert.Multiple(() =>
        {
            Assert.That(first.Bounds.X, Is.EqualTo(0).Within(1e-9));
            Assert.That(second.Bounds.X, Is.EqualTo(40).Within(1e-9));
            Assert.That(canvas.History.CanUndo, Is.False, "one step, and it is spent");
        });
    }

    // Opened inside an open one, an edit JOINS it: a tool that wraps its own work must not split a gesture in two.
    [Test]
    public void AnEditInsideAnEditIsTheSameStep()
    {
        var scene = new CanvasScene();
        var item = Box(0, 0);
        scene.Add(item);

        var canvas = Sized(scene);
        canvas.BeginEdit("Outer");
        canvas.BeginEdit("Inner");
        item.Move(new Vector2(10, 0));
        canvas.EndEdit();
        item.Move(new Vector2(10, 0));
        canvas.EndEdit();

        canvas.Undo();

        Assert.That(item.Bounds.X, Is.EqualTo(0).Within(1e-9), "both moves, one step");
    }

    // Grouping is undoable, and undoing it must put the children back where they were in paint order.
    [Test]
    public void GroupingIsUndone()
    {
        var scene = new CanvasScene();
        var under = Box(0, 0);
        var lower = Box(20, 0);
        var upper = Box(40, 0);

        foreach (var item in new[] { under, lower, upper }) scene.Add(item);

        var canvas = Sized(scene);
        canvas.SelectMany([lower, upper], false);
        canvas.GroupSelection();

        Assert.That(scene.Items, Has.Count.EqualTo(2));

        canvas.Undo();

        Assert.That(scene.Items, Is.EqualTo(new ICanvasItem[] { under, lower, upper }));
    }

    // A new step throws away what was undone: the drawing has taken a different turn, and a redo onto it would put back
    // something that no longer follows from anything.
    [Test]
    public void DoingSomethingElseForgetsWhatWasUndone()
    {
        var scene = new CanvasScene();
        var item = Box(0, 0);
        scene.Add(item);

        var canvas = Sized(scene);
        canvas.BeginEdit("First");
        item.Move(new Vector2(10, 0));
        canvas.EndEdit();
        canvas.Undo();

        Assert.That(canvas.History.CanRedo, Is.True);

        canvas.BeginEdit("Second");
        item.Move(new Vector2(0, 10));
        canvas.EndEdit();

        Assert.That(canvas.History.CanRedo, Is.False);
    }

    // A CANVAS REMEMBERS BY ITSELF. Taking the last thing back is part of what an editor IS, so the control brings its
    // own memory and undo works with nothing wired up - an application that had to hand one over got a canvas that
    // could not undo a single stroke until it did.
    [Test]
    public void ACanvasRemembersByItselfAndCanBeToldNotTo()
    {
        var scene = new CanvasScene();
        var item = Box(0, 0);
        scene.Add(item);

        var canvas = new InfiniteCanvas { Scene = scene };
        canvas.Measure(new Size(400, 300), force: true);
        canvas.Arrange(new Rect(0, 0, 400, 300));

        Assert.That(canvas.History, Is.Not.Null, "a canvas with no memory of its own remembers nothing at all");

        canvas.BeginEdit("Move");
        item.Move(new Vector2(10, 0));
        canvas.EndEdit();

        Assert.Multiple(() =>
        {
            Assert.That(canvas.Undo(), Is.True, "the canvas did not remember a move it made itself");
            Assert.That(item.Bounds.X, Is.EqualTo(0).Within(1e-9), "the move was not taken back");
        });

        // ...and TOLD NOT TO, it does not: remembering nothing has to cost nothing, so no snapshot is taken at all.
        canvas.History = null;

        canvas.BeginEdit("Move");
        item.Move(new Vector2(10, 0));
        canvas.EndEdit();

        Assert.Multiple(() =>
        {
            Assert.That(canvas.Undo(), Is.False);
            Assert.That(item.Bounds.X, Is.EqualTo(10).Within(1e-9));
        });
    }

    // A line written in an INSPECTOR changes no bounds at all - a colour, a thickness, a corner radius - so the
    // canvas's before-and-after of where things are records nothing. The grid's own step is what covers it, written
    // from the value that was there just before the write.
    [Test]
    public void APropertyWrittenInThePanelIsUndone()
    {
        var scene = new CanvasScene();
        var item = Box(0, 0);
        scene.Add(item);

        var grid = new PropertyGrid();
        var definition = new NumericProperty { Header = "Thickness", Binding = new Binding("Thickness") };
        var history = new CanvasHistory();

        var was = grid.ValueOf(item, definition);
        grid.WriteTo(item, definition, 12.0);

        history.Push(new CanvasPropertyStep(grid, definition,
            [(item, was, grid.ValueOf(item, definition))]));

        Assert.That(item.Thickness, Is.EqualTo(12).Within(1e-9));

        history.Undo(scene);
        Assert.That(item.Thickness, Is.EqualTo(1).Within(1e-9), "back to what it was");

        history.Redo(scene);
        Assert.That(item.Thickness, Is.EqualTo(12).Within(1e-9));
    }

    // ...and a line re-typed with the same number in it is not a step: undoing to what it already was is worse than
    // there being nothing to undo.
    [Test]
    public void APropertyWrittenWithTheSameValueIsNotAStep()
    {
        var item = Box(0, 0);
        var grid = new PropertyGrid();
        var definition = new NumericProperty { Header = "Thickness", Binding = new Binding("Thickness") };
        var history = new CanvasHistory();

        history.Push(new CanvasPropertyStep(grid, definition, [(item, 1.0, 1.0)]));

        Assert.That(history.CanUndo, Is.False);
    }

    // What was selected may have left the drawing: a frame drawn round something the scene no longer holds is a frame
    // round nothing.
    [Test]
    public void RedoingADeleteLetsGoOfWhatWent()
    {
        var scene = new CanvasScene();
        var item = Box(0, 0);
        scene.Add(item);

        var canvas = Sized(scene);
        canvas.Select(item, false);
        canvas.DeleteSelection();
        canvas.Undo();
        canvas.Select(item, false);
        canvas.Redo();

        Assert.That(canvas.Selection, Is.Empty);
    }
}
