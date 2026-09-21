using System;

namespace Adamantium.Navigation;

/// <summary>Outcome of a navigation: succeeded (with the activated view model), vetoed by a guard/cancellation, or failed
/// with an exception.</summary>
public sealed class NavigationResult
{
    private NavigationResult(bool success, bool cancelled, Exception error, object viewModel)
    {
        Success = success;
        Cancelled = cancelled;
        Error = error;
        ViewModel = viewModel;
    }

    public bool Success { get; }
    public bool Cancelled { get; }
    public Exception Error { get; }
    public object ViewModel { get; }

    public static NavigationResult Ok(object viewModel) => new(true, false, null, viewModel);
    public static NavigationResult Vetoed() => new(false, true, null, null);
    public static NavigationResult Failed(Exception error) => new(false, false, error, null);
}
