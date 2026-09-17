using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.DrawingBoard;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Media;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>A BUTTON WITH NOTHING TO DO SAYS SO BY BEING OFF. Undo and redo are the two that are most often in that
/// state - at the start of a session there is nothing to take back, and after taking everything back there is nothing
/// to put on again - and a press that does nothing leaves a person wondering what they missed.</summary>
[TestFixture]
public class CanvasHistoryButtonTests
{
    private static Button Watching(CanvasHistory history, string path)
    {
        var button = new Button();

        button.SetBinding(UIComponent.IsEnabledProperty, new Binding(path)
        {
            Source = history,
            Mode = BindingMode.OneWay
        });

        BindingUpdateQueue.Flush();

        return button;
    }

    // One thing done, of the simplest kind there is: a property written and written back.
    private static ICanvasStep Step(CanvasScene scene)
    {
        var item = new ShapeItem(CanvasShape.Rectangle, new Rect(0, 0, 60, 40), Brushes.White, 2);
        scene.Add(item);

        var grid = new PropertyGrid();
        var definition = new NumericProperty { Header = "Thickness", Binding = new Binding("Thickness") };

        return new CanvasPropertyStep(grid, definition, [(item, (object)2.0, (object)4.0)]);
    }

    [Test]
    public void WithNothingDoneBothAreOff()
    {
        var history = new CanvasHistory();

        Assert.Multiple(() =>
        {
            Assert.That(Watching(history, nameof(CanvasHistory.CanUndo)).IsEnabled, Is.False,
                "undo is offered with nothing to take back");
            Assert.That(Watching(history, nameof(CanvasHistory.CanRedo)).IsEnabled, Is.False,
                "redo is offered with nothing to put back");
        });
    }

    [Test]
    public void TheyFollowWhatThereIsToDo()
    {
        var history = new CanvasHistory();
        var scene = new CanvasScene();

        var undo = Watching(history, nameof(CanvasHistory.CanUndo));
        var redo = Watching(history, nameof(CanvasHistory.CanRedo));

        history.Push(Step(scene));
        BindingUpdateQueue.Flush();

        Assert.Multiple(() =>
        {
            Assert.That(undo.IsEnabled, Is.True, "something was done and undo stayed off");
            Assert.That(redo.IsEnabled, Is.False, "nothing was taken back and redo came on");
        });

        history.Undo(scene);
        BindingUpdateQueue.Flush();

        Assert.Multiple(() =>
        {
            Assert.That(undo.IsEnabled, Is.False, "the last thing was taken back and undo stayed on");
            Assert.That(redo.IsEnabled, Is.True, "there is something to put back and redo stayed off");
        });

        history.Redo(scene);
        BindingUpdateQueue.Flush();

        Assert.Multiple(() =>
        {
            Assert.That(undo.IsEnabled, Is.True);
            Assert.That(redo.IsEnabled, Is.False, "everything is put back and redo stayed on");
        });
    }
}
