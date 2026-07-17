using System;

namespace Adamantium.Navigation;

/// <summary>One entry in a region's back/forward journal. Keeps the view-model INSTANCE so a back/forward move restores
/// exactly what was there; if it is ever dropped (null), the region re-resolves the type and re-hydrates from
/// <see cref="Parameters"/>.</summary>
public sealed class NavigationJournalEntry
{
    public NavigationJournalEntry(Type viewModelType, object viewModel, NavigationParameters parameters)
    {
        ViewModelType = viewModelType;
        ViewModel = viewModel;
        Parameters = parameters;
    }

    public Type ViewModelType { get; }
    public object ViewModel { get; }
    public NavigationParameters Parameters { get; }
}
