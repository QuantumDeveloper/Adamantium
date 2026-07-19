using System;

namespace Adamantium.Core.Commands;

/// <summary>
/// Subscribes to a command's <see cref="ICommand.CanExecuteChanged"/> on behalf of a target, holding the target
/// <em>weakly</em>. A command often lives as long as its view-model; without this, the subscription's delegate would
/// keep the target (e.g. a Button) — and its whole visual subtree — alive after it's gone from the UI. Here the
/// command keeps only the relay; the relay keeps the target weakly and detaches itself the first time it fires after
/// the target has been collected. The <c>invoke</c> callback must be a <em>static</em> delegate (no captured target),
/// otherwise it would re-introduce a strong reference and defeat the purpose.
/// </summary>
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
