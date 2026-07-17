using System.Threading;
using System.Threading.Tasks;

namespace Adamantium.Navigation;

/// <summary>UI-free bridge the engine uses to open a real window without referencing any UI type. Implemented in the UI
/// layer (creates the window shell by key, loads the content view into it). Keeps window navigation drivable from a view
/// model.</summary>
public interface IWindowNavigationBackend
{
    /// <summary>Create the window shell for <paramref name="windowShell"/> (null = default) and load
    /// <paramref name="contentViewModel"/>'s view into it. Returns the created window as an opaque handle.</summary>
    Task<object> OpenWindowAsync(object contentViewModel, NavigationParameters parameters, string windowShell, CancellationToken cancellationToken);

    /// <summary>Single-instance support: if a window whose content view model is of <paramref name="contentViewModelType"/>
    /// is already open, bring it to the front and return that content view model; otherwise null (nothing open).</summary>
    object TryActivateExisting(System.Type contentViewModelType);

    void CloseWindow(object contentViewModel);
}
