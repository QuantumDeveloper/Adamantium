using System;

namespace Adamantium.Navigation;

/// <summary>One entry in a region's back/forward journal. Keeps the view-model INSTANCE so a back/forward move restores
/// exactly what was there; if it is ever dropped (null), the region re-resolves the type and re-hydrates from
/// <see cref="Parameters"/>.</summary>
public sealed class NavigationJournalEntry
{
    public NavigationJournalEntry(Type viewModelType, object viewModel, NavigationParameters parameters, string viewKey = null)
    {
        ViewModelType = viewModelType;
        ViewModel = viewModel;
        Parameters = parameters;
        ViewKey = viewKey;
    }

    public Type ViewModelType { get; }
    public object ViewModel { get; }
    public NavigationParameters Parameters { get; }

    /// <summary>Which VIEW of that view-model was shown, or null for its default one. Recorded here because a
    /// view-model can have several: without it, going back to a different face of the SAME object would restore the
    /// object and the wrong face.</summary>
    public string ViewKey { get; }
}
