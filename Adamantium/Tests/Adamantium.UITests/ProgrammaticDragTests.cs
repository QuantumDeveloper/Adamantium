using System;
using Adamantium.Core.Commands;
using Adamantium.Mathematics;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Input;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// <c>DragDrop.DoDragDrop</c> - starting a drag from CODE, for the gestures the engine cannot recognise itself (a
/// context menu's "Move to…", a keyboard pick-up, a source that is not an element). No pointer is involved in starting
/// one, which is exactly what makes it testable here; the drop itself needs a window, so what is covered is the session:
/// it starts, it carries the payload, it refuses to overlap another, and it lets go cleanly.
/// </summary>
[TestFixture]
public class ProgrammaticDragTests
{
    private Border _source;

    [SetUp]
    public void SetUp()
    {
        _source = new Border { Width = 100, Height = 40 };
        var root = new Grid();
        root.Children.Add(_source);
        root.Measure(new Size(100, 40));
        root.Arrange(new Rect(0, 0, 100, 40));
    }

    // The drag session is app-global static state: a test that left one running would break every test after it.
    [TearDown]
    public void TearDown() => ReleaseButton();

    // Ending a drag the way the engine does - the left button coming up. DoDragDrop subscribes the same handler a press
    // would, so this is the real completion path, not a test-only back door.
    private void ReleaseButton()
    {
        var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, MouseButtons.Left, MouseButtonState.Released,
            InputModifiers.None, 0) { RoutedEvent = InputUIComponent.MouseLeftButtonUpEvent };
        ((IObservableComponent)_source).RaiseEvent(args);
    }

    [Test]
    public void DoDragDrop_StartsWithoutAPress_AndHandsThePayloadToDragStarted()
    {
        object payload = null;
        DragDrop.SetDragStartedCommand(_source, new TestCommand(arg => payload = ((DragDropEventArgs)arg).Data?.Get<string>()));

        var started = DragDrop.DoDragDrop(_source, "Item 42");

        Assert.That(started, Is.True);
        Assert.That(payload, Is.EqualTo("Item 42"), "the payload passed to DoDragDrop must reach the source unchanged");
    }

    // No payload given: the source's own DragDrop.DragData stands in, so a code-started drag on a source that already
    // declares its data needs to say nothing twice.
    [Test]
    public void DoDragDrop_WithoutData_FallsBackToTheSourcesDragData()
    {
        object payload = null;
        DragDrop.SetDragData(_source, "from the attached property");
        DragDrop.SetDragStartedCommand(_source, new TestCommand(arg => payload = ((DragDropEventArgs)arg).Data?.Get<string>()));

        DragDrop.DoDragDrop(_source);

        Assert.That(payload, Is.EqualTo("from the attached property"));
    }

    // One drag at a time, and the session must be given back afterwards - a leak here would leave the whole app unable
    // to drag anything again.
    [Test]
    public void DoDragDrop_RefusesToOverlapAnother_AndFreesTheSessionOnRelease()
    {
        Assert.That(DragDrop.DoDragDrop(_source, "first"), Is.True);
        Assert.That(DragDrop.DoDragDrop(_source, "second"), Is.False, "a second drag must not start while one is in flight");

        ReleaseButton();

        Assert.That(DragDrop.DoDragDrop(_source, "third"), Is.True, "the session must be free again once the gesture ended");
    }

    // The gesture ends through the ordinary path, so the source hears the outcome - None here, because there is no
    // window and therefore no drop target under the pointer.
    [Test]
    public void ReleasingTheButton_RunsDragCompletedWithTheOutcome()
    {
        DragDropEffects? outcome = null;
        DragDrop.SetDragCompletedCommand(_source, new TestCommand(arg => outcome = ((DragDropEventArgs)arg).Effects));

        DragDrop.DoDragDrop(_source, "Item 42");
        ReleaseButton();

        Assert.That(outcome, Is.EqualTo(DragDropEffects.None));
    }

    [Test]
    public void DoDragDrop_RefusesAnEmptyEffectMask()
    {
        Assert.That(DragDrop.DoDragDrop(_source, "nothing allowed", DragDropEffects.None), Is.False);
    }

    private sealed class TestCommand : ICommand
    {
        private readonly Action<object> _run;
        public TestCommand(Action<object> run) => _run = run;
        public bool CanExecute(object parameter = null) => true;
        public void Execute(object parameter = null) => _run(parameter);
        public event EventHandler CanExecuteChanged;
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
