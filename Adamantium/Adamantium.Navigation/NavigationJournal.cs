using System.Collections.Generic;

namespace Adamantium.Navigation;

/// <summary>Default two-stack back/forward journal.</summary>
public sealed class NavigationJournal : INavigationJournal
{
    private readonly List<NavigationJournalEntry> _back = [];
    private readonly List<NavigationJournalEntry> _forward = [];

    public bool CanGoBack => _back.Count > 0;
    public bool CanGoForward => _forward.Count > 0;
    public NavigationJournalEntry Current { get; private set; }
    public IReadOnlyList<NavigationJournalEntry> BackStack => _back;
    public IReadOnlyList<NavigationJournalEntry> ForwardStack => _forward;

    public void RecordNavigation(NavigationJournalEntry entry)
    {
        if (Current != null) _back.Add(Current);
        Current = entry;
        _forward.Clear();
    }

    public NavigationJournalEntry Back()
    {
        if (_back.Count == 0) return null;
        var previous = _back[^1];
        _back.RemoveAt(_back.Count - 1);
        if (Current != null) _forward.Add(Current);
        Current = previous;
        return previous;
    }

    public NavigationJournalEntry Forward()
    {
        if (_forward.Count == 0) return null;
        var next = _forward[^1];
        _forward.RemoveAt(_forward.Count - 1);
        if (Current != null) _back.Add(Current);
        Current = next;
        return next;
    }

    public void Clear()
    {
        _back.Clear();
        _forward.Clear();
        Current = null;
    }
}
