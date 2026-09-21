using System.Collections.Generic;

namespace Adamantium.Navigation;

/// <summary>Per-region back/forward history.</summary>
public interface INavigationJournal
{
    bool CanGoBack { get; }
    bool CanGoForward { get; }
    NavigationJournalEntry Current { get; }
    IReadOnlyList<NavigationJournalEntry> BackStack { get; }
    IReadOnlyList<NavigationJournalEntry> ForwardStack { get; }

    /// <summary>Push the current entry onto the back stack, make <paramref name="entry"/> current, clear forward.</summary>
    void RecordNavigation(NavigationJournalEntry entry);

    /// <summary>Move current onto the forward stack, pop the back stack into current, and return it (null if empty).</summary>
    NavigationJournalEntry Back();

    NavigationJournalEntry Forward();
    void Clear();
}
