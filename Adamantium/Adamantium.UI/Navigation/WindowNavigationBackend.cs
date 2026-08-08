using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Adamantium.Navigation;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Navigation;
using Adamantium.UI.Core;

namespace Adamantium.UI.Navigation;

/// <summary>Bridges the UI-free navigation engine to real windows. Creates the window SHELL from the registry (a custom
/// window with your styles/chrome, or the default), loads the content view-model's View into it, and shows it - so a view
/// model can open a window without ever touching a UI type.</summary>
public sealed class WindowNavigationBackend : IWindowNavigationBackend
{
    private readonly IUIApplication _application;
    private readonly IViewLocator _viewLocator;
    private readonly IWindowShellRegistry _shells;
    private readonly Dictionary<object, IWindow> _windows = new();

    public WindowNavigationBackend(IUIApplication application, IViewLocator viewLocator, IWindowShellRegistry shells)
    {
        _application = application;
        _viewLocator = viewLocator;
        _shells = shells;
    }

    public async Task<object> OpenWindowAsync(object contentViewModel, NavigationParameters parameters, string windowShell, CancellationToken cancellationToken)
    {
        IWindow created = null;
        // Window + render-service creation must run on the UI thread.
        await _application.ExecuteOnUIThreadAsync(() =>
        {
            var key = windowShell ?? (contentViewModel as IWindowAware)?.WindowShellKey;
            var window = _shells.Create(key);
            var shell = window as WindowBase;

            // Title/size FIRST: the platform worker sizes the OS window from them during AttachContextAndInitialize
            // (SetWindow), so they must be set before init.
            if (shell != null && contentViewModel is IWindowAware aware)
            {
                if (!string.IsNullOrEmpty(aware.Title)) shell.Title = aware.Title;
                if (aware.Width > 0) shell.ClientWidth = aware.Width;
                if (aware.Height > 0) shell.ClientHeight = aware.Height;
            }

            // Initialize the window FIRST (builds its tree, creates the OS window + render service via WindowCreatedEvent,
            // applies the theme), THEN load the content so it joins a LIVE, themed, context-attached window - exactly how
            // a dynamic tab body attaches in the main window. Setting Content before init leaves the content subtree
            // detached: it misses the theme pass and its layout invalidations are orphaned (unthemed, never laid out).
            // No AddWindow here: SetWindow (inside init) publishes WindowCreatedEvent, and the app creates the render
            // service + tracks the window off that. Calling AddWindow too would create the render service twice.
            window.Show();   // attaches + initializes if needed, and activates itself on first display (Window.Show)
            if (shell != null)
            {
                // So the shell's OWN bindings resolve too (a caption slot, a command bar authored on the window). The
                // view keeps its own explicit DataContext from the ViewLocator.
                shell.DataContext = contentViewModel;
                shell.Content = _viewLocator.ResolveView(contentViewModel);   // load the chosen content into the live shell
            }
            _windows[contentViewModel] = window;
            // Untrack on close (including the OS title-bar X, which never routes through CloseWindow) - otherwise a stale
            // entry makes a single-instance re-open just "activate" a dead window instead of opening a fresh one.
            window.Closed += (_, _) => _windows.Remove(contentViewModel);
            created = window;
        });
        return created;
    }

    public object TryActivateExisting(System.Type contentViewModelType)
    {
        foreach (var pair in _windows)
        {
            if (pair.Key.GetType() != contentViewModelType) continue;
            var window = pair.Value;
            _application.ExecuteOnUIThread(() =>
            {
                (window as WindowBase)?.Activate();   // restore-if-minimized + bring to the foreground
                _application.SetActiveWindow(window);
            });
            return pair.Key;
        }
        return null;
    }

    public void CloseWindow(object contentViewModel)
    {
        if (contentViewModel == null || !_windows.TryGetValue(contentViewModel, out var window)) return;
        _windows.Remove(contentViewModel);
        _application.ExecuteOnUIThread(window.Close);
    }
}
