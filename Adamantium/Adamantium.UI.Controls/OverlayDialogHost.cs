using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Adamantium.Navigation;
using Adamantium.UI.Controls.Navigation;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>Overlay dialog host: shows the dialog inside a draggable, modal <see cref="OverlayWindow"/> on the active
/// window's overlay (in-window, never a separate OS window). The dim scrim blocks + dims the content behind; the dialog
/// closes when the view model raises RequestClose, or when the user closes the window (x / Escape) - which the view model
/// can veto via CanCloseDialog. Presentation only; the lifecycle + result live in <see cref="DialogSession"/>.</summary>
public sealed class OverlayDialogHost : IDialogHost
{
    private readonly IUIApplication _application;
    private readonly IViewLocator _viewLocator;

    public OverlayDialogHost(IUIApplication application, IViewLocator viewLocator)
    {
        _application = application;
        _viewLocator = viewLocator;
    }

    public DialogHostKind Kind => DialogHostKind.Overlay;

    public Task<IDialogResult> ShowAsync(object dialogViewModel, NavigationParameters parameters, CancellationToken cancellationToken = default)
    {
        // The overlay lives on a window's popup layer; without a window there is nowhere to show it.
        if ((_application.ActiveWindow ?? _application.MainWindow) is not IPopupHost host)
            return Task.FromResult<IDialogResult>(new DialogResult(DialogButtonResult.None));

        var view = _viewLocator.ResolveView(dialogViewModel);
        var aware = dialogViewModel as IDialogAware;

        var window = new OverlayWindow
        {
            Title = aware?.Title ?? string.Empty,
            IsModal = true,          // a dialog dims + blocks the content behind
            AllowMove = true,        // draggable by the title bar
            CloseOnOverlay = false,  // a dialog closes via its buttons / RequestClose, not a scrim click
            Content = view
        };

        // Live title: the dialog may change its Title while open; reflect it on the bar.
        if (aware is INotifyPropertyChanged inpc)
        {
            void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(IDialogAware.Title)) window.Title = aware.Title;
            }
            inpc.PropertyChanged += OnPropertyChanged;
            window.Closed += (_, _) => inpc.PropertyChanged -= OnPropertyChanged;
        }

        var session = DialogSession.Begin(dialogViewModel, parameters, () => window.Close());
        // A user-initiated close (x / Escape) routes back through the session as a None result, still honouring
        // CanCloseDialog: veto the window close when the dialog refuses to close.
        window.Closing += (_, args) => { if (aware != null && !aware.CanCloseDialog()) args.Cancel = true; };
        window.Closed += (_, _) => session.RequestClose(new DialogResult(DialogButtonResult.None));

        OverlayWindowManager.GetFor(host).Show(window);
        return session.Completion;
    }
}
