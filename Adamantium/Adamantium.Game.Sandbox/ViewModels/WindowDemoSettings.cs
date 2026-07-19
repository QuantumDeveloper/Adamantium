using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Shared demo setting: whether opening the Workspace window creates a NEW instance each time or focuses the
/// existing one. Registered as a singleton so the title-bar command (MainViewModel) and the toggle in the Navigation tab
/// (NavigationDemoViewModel) read/write the SAME flag - a single source of truth for the two views.</summary>
[ViewModel]
public partial class WindowDemoSettings
{
    [Bindable] private bool _allowDuplicateWindows;

    // When on, the title-bar dialog commands (Confirm / About) open in their OWN window instead of an in-window overlay.
    [Bindable] private bool _dialogsAsWindows;
}
