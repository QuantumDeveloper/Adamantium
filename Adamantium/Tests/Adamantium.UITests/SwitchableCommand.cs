using System;
using Adamantium.Core.Commands;

namespace Adamantium.UITests;

/// <summary>A command whose answer can be flipped, announcing the change - what a control that follows availability is
/// supposed to react to. Shared by the button and menu tests, which ask the same question of two controls.</summary>
internal sealed class SwitchableCommand : ICommand
{
    private bool _can;

    public bool CanRun
    {
        get => _can;
        set
        {
            _can = value;
            RaiseCanExecuteChanged();
        }
    }

    public int Runs { get; private set; }

    public object LastParameter { get; private set; }

    public bool CanExecute(object parameter = null) => _can;

    public void Execute(object parameter = null)
    {
        Runs++;
        LastParameter = parameter;
    }

    public event EventHandler CanExecuteChanged;

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
