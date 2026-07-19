using System.Threading;
using System.Threading.Tasks;
using Adamantium.Navigation;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Navigation;
using Adamantium.UI.Core;

namespace Adamantium.UI.Navigation;

/// <summary>Window dialog host: shows the dialog in its OWN window (a shell from the window registry) instead of an
/// in-window overlay. Loads the resolved view into the shell and closes it when the view model raises RequestClose;
/// the title-bar close also completes the dialog (as Cancel). Lifecycle/result via <see cref="DialogSession"/>.
/// NOT yet modal - input to the owner window is not blocked; that wiring is a follow-up.</summary>
public sealed class WindowDialogHost : IDialogHost
{
    private readonly IUIApplication _application;
    private readonly IViewLocator _viewLocator;
    private readonly IWindowShellRegistry _shells;

    public WindowDialogHost(IUIApplication application, IViewLocator viewLocator, IWindowShellRegistry shells)
    {
        _application = application;
        _viewLocator = viewLocator;
        _shells = shells;
    }

    public DialogHostKind Kind => DialogHostKind.Window;

    public async Task<IDialogResult> ShowAsync(object dialogViewModel, NavigationParameters parameters, CancellationToken cancellationToken = default)
    {
        DialogSession session = null;

        // Window + render-service creation must run on the UI thread (as in WindowNavigationBackend).
        await _application.ExecuteOnUIThreadAsync(() =>
        {
            var aware = dialogViewModel as IWindowAware;
            if (_shells.Create(aware?.WindowShellKey) is not WindowBase shell) return;   // need a WindowBase to host content

            shell.Title = !string.IsNullOrEmpty(aware?.Title) ? aware.Title : "Dialog";
            shell.ClientWidth = aware is { Width: > 0 } ? aware.Width : 440;
            shell.ClientHeight = aware is { Height: > 0 } ? aware.Height : 260;

            // Initialize the window FIRST (builds its tree + render service + theme), THEN load the content, so the view
            // joins a live, themed, context-attached window (same order as WindowNavigationBackend).
            shell.AttachContextAndInitialize(_application.UIContext);
            shell.Content = _viewLocator.ResolveView(dialogViewModel);

            var closed = false;
            session = DialogSession.Begin(dialogViewModel, parameters, () =>
            {
                if (closed) return;   // idempotent: RequestClose -> close once
                closed = true;
                shell.Close();
            });
            // The user closing the window (title-bar X) must also complete the dialog, else the await below hangs. Set the
            // guard first so the resulting RequestClose does not try to close the (already closing) window again.
            shell.Closed += (_, _) => { closed = true; session.RequestClose(DialogResult.Cancel()); };

            shell.Show();
        });

        return session == null ? new DialogResult(DialogButtonResult.None) : await session.Completion;
    }
}
