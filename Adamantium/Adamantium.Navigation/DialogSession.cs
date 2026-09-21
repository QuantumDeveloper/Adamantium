using System;
using System.Threading.Tasks;

namespace Adamantium.Navigation;

/// <summary>Shared, UI-free glue between a dialog host and an <see cref="IDialogAware"/> view model: fires
/// <see cref="IDialogAware.OnDialogOpened"/>, and on <see cref="IDialogAware.RequestClose"/> honours
/// <see cref="IDialogAware.CanCloseDialog"/>, tears down the presentation, and completes <see cref="Completion"/> with the
/// result. Every <see cref="IDialogHost"/> reuses this so the lifecycle lives in one place.</summary>
public sealed class DialogSession
{
    private readonly TaskCompletionSource<IDialogResult> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly IDialogAware _aware;
    private readonly Action _dismiss;

    private DialogSession(object dialogViewModel, Action dismiss)
    {
        _aware = dialogViewModel as IDialogAware;
        _dismiss = dismiss;
    }

    /// <summary>Begins a session for a just-presented dialog. <paramref name="dismiss"/> tears down the host's
    /// presentation (remove the overlay / close the window) and runs once, when the dialog actually closes.</summary>
    public static DialogSession Begin(object dialogViewModel, NavigationParameters parameters, Action dismiss)
    {
        var session = new DialogSession(dialogViewModel, dismiss);
        if (session._aware != null)
        {
            session._aware.RequestClose += session.OnRequestClose;
            session._aware.OnDialogOpened(parameters ?? new NavigationParameters());
        }
        return session;
    }

    public Task<IDialogResult> Completion => _completion.Task;

    /// <summary>Closes the dialog from the host side (e.g. a scrim click / Esc) as if the view model requested it,
    /// still respecting <see cref="IDialogAware.CanCloseDialog"/>.</summary>
    public void RequestClose(IDialogResult result) => OnRequestClose(result);

    private void OnRequestClose(IDialogResult result)
    {
        if (_aware != null && !_aware.CanCloseDialog()) return;
        if (_aware != null) _aware.RequestClose -= OnRequestClose;
        // Complete BEFORE dismissing: a window host's dismiss (Window.Close) synchronously raises Closed, which re-enters
        // here with Cancel; completing first makes that re-entry a no-op so the real result (e.g. Ok) wins.
        _completion.TrySetResult(result ?? new DialogResult(DialogButtonResult.None));
        _dismiss?.Invoke();
    }
}
