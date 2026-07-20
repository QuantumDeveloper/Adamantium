using System.Linq;
using System.Threading.Tasks;
using Adamantium.MVVM;
using Adamantium.Navigation;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Navigation tab: a ContentControl region driven entirely from this view model. The buttons navigate by view-model
/// TYPE (never a view); the region resolves each page's view by convention. Details is opened with a typed parameter and
/// guards its own exit (IConfirmNavigation).</summary>
[ViewModel]
public partial class NavigationDemoViewModel : TabPageViewModel
{
    private int _cardCounter;
    private int _overlayCounter;
    private readonly IDialogService _dialogService;
    private readonly IOverlayService _overService;

    public NavigationDemoViewModel(INavigationService navigation, WindowDemoSettings windowSettings, IDialogService dialogService, IOverlayService overService) : base("Navigation")
    {
        WindowSettings = windowSettings;
        _dialogService = dialogService;
        _overService = overService;
        Region = navigation.Regions.CreateRegion();
        _ = Region.NavigateToAsync<HomePageViewModel>();

        // A SECOND, VM-owned region bound to an ItemsControl (ItemsControlRegionAdapter): all active view models show at
        // once as a list. Seed a couple of cards so it isn't empty on open.
        CardsRegion = navigation.Regions.CreateRegion();
        CardsRegion.Add(new CardPageViewModel(++_cardCounter));
        CardsRegion.Add(new CardPageViewModel(++_cardCounter));
    }

    /// <summary>The VM-owned region the view binds to (<c>nav:Region.Source="{Binding Region}"</c>).</summary>
    public IRegion Region { get; }

    /// <summary>Second VM-owned region shown on a plain ItemsControl (every active view model visible at once).</summary>
    public IRegion CardsRegion { get; }

    [Command] private void AddCard() => CardsRegion.Add(new CardPageViewModel(++_cardCounter));

    [Command] private void ClearCards()
    {
        foreach (var vm in CardsRegion.ActiveViewModels.ToList()) CardsRegion.Remove(vm);
    }

    /// <summary>Shared window-demo setting; the "Allow duplicate windows" toggle in this tab drives the title-bar
    /// Workspace command's single-instance mode (same instance both view models see).</summary>
    public WindowDemoSettings WindowSettings { get; }

    // A floating in-window OverlayWindow shown VM-first through IOverlayService (its OWN service - an overlay is a window,
    // not a dialog; the framework resolves OverlayDemoView and hosts it, no UI is built here). Fire-and-forget: a non-modal
    // window is meant to stack, so awaiting (which completes only on close) would keep the command busy and disable the
    // button. Each click cascades a new one.
    [Command]
    private void ShowOverlayWindow() =>
        _ = _overService.ShowOverlayAsync<OverlayDemoViewModel>(
            new NavigationParameters().Add("title", $"Overlay window {++_overlayCounter}"));

    // A modal dialog on the overlay (dims + blocks the content behind), draggable by its title bar. Awaited: the button
    // stays disabled while the modal is up, which is the intended modal behaviour.
    [Command]
    private Task ShowOverlayModal() =>
        _dialogService.ShowDialogAsync<ConfirmDialogViewModel>(
            new NavigationParameters()
                .Add("title", "Modal overlay")
                .Add("message", "This modal dims and blocks the window behind it. Drag me by the title bar."),
            DialogHostKind.Overlay);

    [Command] private Task GoHome() => Region.NavigateToAsync<HomePageViewModel>();

    [Command] private Task OpenDetails() =>
        Region.NavigateToAsync<DetailsPageViewModel>(new NavigationParameters().Add("id", 42));

    [Command] private Task GoSettings() => Region.NavigateToAsync<SettingsPageViewModel>();

    [Command] private Task Back() => Region.GoBackAsync();

    [Command] private Task Forward() => Region.GoForwardAsync();
}
