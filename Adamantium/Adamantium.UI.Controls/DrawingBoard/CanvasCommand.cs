using System;
using Adamantium.Core.Commands;

namespace Adamantium.UI.Controls.DrawingBoard;

/// <summary>ONE THING THE CANVAS CAN DO, as a command a button binds to.
/// <para>On the canvas because it is the canvas that does it. A control that could undo but made every application
/// write the wiring for its own buttons would be handing out an instruction sheet instead of a control: whoever takes
/// it would write the same handful of click handlers again, and get them subtly different each time.</para>
/// <para>What it can do right now it says itself - undo with nothing behind it is off, and a button bound to it is off
/// with it, which is the whole point of asking.</para></summary>
public sealed class CanvasCommand : ICommand
{
    private readonly Func<object, bool> _can;
    private readonly Action<object> _does;

    internal CanvasCommand(Action<object> does, Func<object, bool> can = null)
    {
        _does = does;
        _can = can;
    }

    public event EventHandler CanExecuteChanged;

    public bool CanExecute(object parameter = null) => _can?.Invoke(parameter) ?? true;

    public void Execute(object parameter = null)
    {
        if (CanExecute(parameter)) _does(parameter);
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
