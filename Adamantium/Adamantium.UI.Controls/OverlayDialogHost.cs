using Adamantium.Navigation;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Navigation;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls;

/// <summary>Overlay dialog host: shows the dialog as a dimmed, centred card on the ACTIVE window's popup layer (in-window,
/// never a separate OS window - it draws on top of all content and stays within the window bounds). The scrim dims and
/// blocks the content behind it; the dialog closes only when the view model raises RequestClose (its buttons).
/// This is presentation only - the open/close lifecycle and the result live in <see cref="DialogSession"/>.</summary>
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

        // The dialog card: the resolved view in a themed, rounded surface (the same brush menu/flyout surfaces use),
        // centred in the window.
        var card = new Border
        {
            Background = ThemeBrush("CardBackgroundFillColorDefault", 0xFF2B2B2B),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = view as IMeasurableComponent
        };

        // The scrim: a full-window layer (theme smoke brush) that dims the content and BLOCKS input to it (it hit-tests,
        // so clicks land on the scrim, not the content behind). It does NOT dismiss on click - the dialog closes only via
        // the view's buttons (RequestClose). Click-outside-to-dismiss can be added later with an explicit bounds check.
        var scrim = new Border
        {
            Background = ThemeBrush("SmokeFillColorDefault", 0x99000000),
            Child = card
        };

        var popup = new Popup { FillWindow = true, Child = scrim };   // a full-window overlay, not an edge-docked panel

        var session = DialogSession.Begin(dialogViewModel, parameters, () => host.PopupLayer.Remove(popup));
        host.PopupLayer.Add(popup);   // portal into the overlay (rendered last => on top of everything)
        return session.Completion;
    }

    // Brushes come from the active theme (FindResource); the ARGB fallback keeps the dialog usable if a key is missing.
    private Brush ThemeBrush(string key, uint fallbackArgb) =>
        _application.ResourceManager?.FindResource(key) as Brush ?? new SolidColorBrush(Color.FromArgb(fallbackArgb));
}
