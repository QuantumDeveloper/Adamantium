using System;

namespace Adamantium.Core.Commands;

/// <summary>Subscribes to a command's <see cref="ICommand.CanExecuteChanged"/> holding the target weakly, so a long-lived
/// command does not keep a control and its subtree alive. The <c>invoke</c> callback must be a static delegate.</summary>
public sealed class WeakCanExecuteChangedRelay<TTarget> where TTarget : class
{
    private readonly WeakReference<TTarget> _target;
    private readonly Action<TTarget> _invoke;
    private ICommand _command;

    public WeakCanExecuteChangedRelay(ICommand command, TTarget target, Action<TTarget> invoke)
    {
        _command = command ?? throw new ArgumentNullException(nameof(command));
        _target = new WeakReference<TTarget>(target ?? throw new ArgumentNullException(nameof(target)));
        _invoke = invoke ?? throw new ArgumentNullException(nameof(invoke));
        command.CanExecuteChanged += OnCanExecuteChanged;
    }

    private void OnCanExecuteChanged(object sender, EventArgs e)
    {
        if (_target.TryGetTarget(out var target)) _invoke(target);
        else Detach();
    }

    public void Detach()
    {
        var command = _command;
        if (command is null) return;
        command.CanExecuteChanged -= OnCanExecuteChanged;
        _command = null;
    }
}
