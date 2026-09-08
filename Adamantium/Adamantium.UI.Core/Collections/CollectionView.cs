using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Adamantium.UI.Core.Collections;

/// <summary>
/// A live view over a collection: what an items control is bound to when the items shown are not simply the items in the
/// source. The stages run in ONE fixed order - filter, then sort, then page - because that order is the only one that
/// means anything: paging a set you then sort gives "sorted within the page", which looks right until you turn to the
/// next one. Baking the order into the type is what stops that being an author's mistake to make.
///
/// <para>Changes are reported as narrowly as they can be. An item that stops passing the filter leaves as a
/// <see cref="NotifyCollectionChangedAction.Remove"/> at ITS OWN index in this view, not as a Reset - hiding a row on
/// the fly has to be a row leaving a list, not a list being rebuilt, or nothing downstream can animate it and every
/// realized container is thrown away. The view therefore keeps a map from its own positions back to the source's, and
/// maintains it incrementally.</para>
///
/// <para>Reset is kept for the changes where nothing finer exists: a new filter or ordering, a re-ordered source, or the
/// source being replaced outright.</para>
///
/// <para>Ordering costs the map its shape. Unsorted, a view position and a source position rise together, so both are
/// found by binary search. SORTED, they do not, and the same lookups become a search by COMPARER for where an arrival
/// belongs and a scan for where a departure sat. That is the price of ordering on a long list, and it is paid per edit
/// rather than per frame.</para>
/// </summary>
public class CollectionView : IEnumerable, IReadOnlyList<object>, INotifyCollectionChanged, IPagedSource
{
    private readonly List<object> _snapshot = [];
    private readonly List<object> _view = [];
    private readonly List<int> _sourceIndexOfView = [];
    private readonly Dictionary<(Type, string), PropertyInfo> _properties = new();
    private readonly Dictionary<object, int> _subscribed = new();
    private readonly List<object> _page = [];
    private int _pageSize;
    private int _pageIndex;
    private IEnumerable _source;
    private Predicate<object> _filter;
    private IComparer _customSort;
    private bool _isLiveFiltering;
    private bool _isLiveSorting;

    /// <summary>Creates a view over <paramref name="source"/>; null is a valid (empty) source.</summary>
    public CollectionView(IEnumerable source = null)
    {
        SortDescriptions = new SortDescriptionCollection();
        SortDescriptions.CollectionChanged += (_, _) => Rebuild();
        LiveFilteringProperties = [];
        LiveSortingProperties = [];
        Source = source;
    }

    /// <inheritdoc/>
    public event NotifyCollectionChangedEventHandler CollectionChanged;

    /// <summary>The collection being viewed. Its own change notifications are mirrored while it is set, so the view
    /// stays live; assigning a new one re-reads it and raises a Reset.</summary>
    public IEnumerable Source
    {
        get => _source;
        set
        {
            if (ReferenceEquals(_source, value)) return;

            if (_source is INotifyCollectionChanged oldObservable)
                oldObservable.CollectionChanged -= OnSourceCollectionChanged;

            _source = value;

            if (_source is INotifyCollectionChanged observable)
                observable.CollectionChanged += OnSourceCollectionChanged;

            Rebuild();
        }
    }

    /// <summary>Which items are shown; null shows all of them. Setting it re-evaluates the whole set and raises a Reset -
    /// a new predicate says nothing about which items it agrees with the old one about, so there is nothing finer to
    /// report.
    /// <para>Changing an ITEM so that it starts or stops passing is a different matter: see the live-shaping properties,
    /// which report those one at a time.</para></summary>
    public Predicate<object> Filter
    {
        get => _filter;
        set
        {
            if (_filter == value) return;
            _filter = value;
            Rebuild();
        }
    }

    /// <summary>The ordering levels, in priority order. Empty leaves the source's own order alone.
    /// <para>Ignored while <see cref="CustomSort"/> is set - a comparer is the whole answer, and honouring both would
    /// mean guessing which one the author meant.</para></summary>
    public SortDescriptionCollection SortDescriptions { get; }

    /// <summary>An ordering the descriptions cannot express (case rules, a computed key, a domain order). Takes
    /// precedence over <see cref="SortDescriptions"/>.
    /// <para>The cost of the escape hatch: a comparer is opaque, so live sorting cannot work out which properties to
    /// listen to and they have to be named in <see cref="LiveSortingProperties"/>.</para></summary>
    public IComparer CustomSort
    {
        get => _customSort;
        set
        {
            if (ReferenceEquals(_customSort, value)) return;
            _customSort = value;
            Rebuild();
        }
    }

    /// <summary>True while anything is ordering the view - which is also when its order stops matching the source's.</summary>
    public bool IsSorted => _customSort != null || SortDescriptions.Count > 0;

    /// <summary>Re-evaluate the filter when an ITEM changes, not only when the source or the predicate does. Without it
    /// a row that stops qualifying stays on screen until something else disturbs the list, which is what makes
    /// search-as-you-type and "hide the finished ones" impossible to write honestly.
    /// <para>OFF by default, and deliberately: this subscribes to every item, and the panel below happily holds tens of
    /// thousands. Turning it on is a decision about a particular list, not a default anyone should inherit.</para></summary>
    public bool IsLiveFiltering
    {
        get => _isLiveFiltering;
        set
        {
            if (_isLiveFiltering == value) return;
            _isLiveFiltering = value;
            ResubscribeItems();
        }
    }

    /// <summary>Re-place an item when the property it is ordered by changes, so it moves to where it now belongs
    /// instead of staying put until the next rebuild. Off by default, for the same reason as <see cref="IsLiveFiltering"/>.</summary>
    public bool IsLiveSorting
    {
        get => _isLiveSorting;
        set
        {
            if (_isLiveSorting == value) return;
            _isLiveSorting = value;
            ResubscribeItems();
        }
    }

    /// <summary>Which property changes the FILTER cares about. Empty means every change re-evaluates the predicate,
    /// which on a long list is the expensive answer - name them.</summary>
    public ObservableCollection<string> LiveFilteringProperties { get; }

    /// <summary>Which property changes the ORDER cares about. Left empty it is answered by
    /// <see cref="SortDescriptions"/> - sorting by <c>Title</c> is exactly saying that <c>Title</c> matters.
    /// <para>With a <see cref="CustomSort"/> there is nothing to answer with: a comparer cannot be asked what it reads,
    /// so an empty list means EVERY change re-places the item. That is correct and slow; naming them here narrows it,
    /// and is the only reason to.</para></summary>
    public ObservableCollection<string> LiveSortingProperties { get; }

    /// <summary>How many items a page holds; 0 - the default - shows the whole shaped set and pages nothing.
    /// <para>Changing it keeps the FIRST ITEM of the current page in view rather than dropping to the beginning: the
    /// page is renumbered around where the reader already is.</para></summary>
    public int PageSize
    {
        get => _pageSize;
        set
        {
            var size = Math.Max(0, value);
            if (_pageSize == size) return;

            var firstItem = _pageSize > 0 ? _pageIndex * _pageSize : 0;
            _pageSize = size;
            _pageIndex = size > 0 ? firstItem / size : 0;
            ClampPage();
            RebuildPage();
        }
    }

    /// <summary>Which page is shown, counted from zero. Out-of-range values are clamped to what exists.</summary>
    public int PageIndex
    {
        get => _pageIndex;
        set
        {
            var index = Math.Max(0, value);
            if (_pageIndex == index) return;
            _pageIndex = index;
            ClampPage();
            RebuildPage();
        }
    }

    /// <summary>How many pages the shaped set makes; 0 when nothing is shown, 1 when paging is off.</summary>
    public int PageCount => IsPaged ? (_view.Count + _pageSize - 1) / _pageSize : _view.Count > 0 ? 1 : 0;

    /// <summary>How many items there are to page THROUGH - the shaped count, which is what the numbers on a pager are
    /// about. Not the source's count: a filtered-out item is not on any page.</summary>
    public int TotalItemCount => _view.Count;

    /// <summary>True while <see cref="PageSize"/> is cutting the shaped set into pages.</summary>
    public bool IsPaged => _pageSize > 0;

    /// <summary>How many items are shown right now: a page's worth while paging, everything shaped otherwise.</summary>
    public int Count => IsPaged ? _page.Count : _view.Count;

    /// <inheritdoc/>
    public object this[int index] => IsPaged ? _page[index] : _view[index];

    /// <summary>How many items the SOURCE holds, before any of the stages. What a pager needs to say how much there is
    /// in total, as opposed to how much of it is shown.</summary>
    public int SourceCount => _snapshot.Count;

    /// <inheritdoc/>
    public IEnumerator<object> GetEnumerator() => IsPaged ? _page.GetEnumerator() : _view.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private bool Passes(object item) => _filter == null || _filter(item);

    private void Rebuild()
    {
        _snapshot.Clear();
        _view.Clear();
        _sourceIndexOfView.Clear();

        if (_source != null)
        {
            foreach (var item in _source) _snapshot.Add(item);
        }

        ResubscribeItems();

        for (var i = 0; i < _snapshot.Count; i++)
        {
            if (!Passes(_snapshot[i])) continue;
            _view.Add(_snapshot[i]);
            _sourceIndexOfView.Add(i);
        }

        if (IsSorted) SortView();

        ClampPage();
        RebuildPage();
    }

    private void SortView()
    {
        var order = Enumerable.Range(0, _view.Count).ToList();
        order.Sort((a, b) =>
        {
            var byKey = Compare(_view[a], _view[b]);
            return byKey != 0 ? byKey : _sourceIndexOfView[a].CompareTo(_sourceIndexOfView[b]);
        });

        var items = order.Select(i => _view[i]).ToList();
        var indexes = order.Select(i => _sourceIndexOfView[i]).ToList();
        _view.Clear();
        _view.AddRange(items);
        _sourceIndexOfView.Clear();
        _sourceIndexOfView.AddRange(indexes);
    }

    private int Compare(object left, object right)
    {
        if (_customSort != null) return _customSort.Compare(left, right);

        foreach (var description in SortDescriptions)
        {
            var result = Comparer<object>.Default.Compare(
                ValueOf(left, description.PropertyName), ValueOf(right, description.PropertyName));
            if (result == 0) continue;
            return description.Direction == SortDirection.Descending ? -result : result;
        }

        return 0;
    }

    private object ValueOf(object item, string propertyName)
    {
        if (item == null || propertyName == null) return null;

        var key = (item.GetType(), propertyName);
        if (!_properties.TryGetValue(key, out var property))
        {
            property = item.GetType().GetProperty(propertyName);
            _properties[key] = property;
        }

        return property?.GetValue(item);
    }

    private void OnSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add when e.NewStartingIndex >= 0:
                SourceInserted(e.NewStartingIndex, e.NewItems);
                break;
            case NotifyCollectionChangedAction.Remove when e.OldStartingIndex >= 0:
                SourceRemoved(e.OldStartingIndex, e.OldItems.Count);
                break;
            case NotifyCollectionChangedAction.Replace when e.OldStartingIndex >= 0:
                SourceRemoved(e.OldStartingIndex, e.OldItems.Count);
                SourceInserted(e.OldStartingIndex, e.NewItems);
                break;
            default:
                Rebuild();
                break;
        }
    }

    private void SourceInserted(int sourceIndex, IList items)
    {
        ShiftSourceIndexes(sourceIndex, items.Count);

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var at = sourceIndex + i;
            _snapshot.Insert(at, item);
            Subscribe(item);
            if (!Passes(item)) continue;

            var viewIndex = InsertionPointFor(item, at);
            _view.Insert(viewIndex, item);
            _sourceIndexOfView.Insert(viewIndex, at);
            ShapedInserted(viewIndex, item);
        }
    }

    private void SourceRemoved(int sourceIndex, int count)
    {
        for (var i = count - 1; i >= 0; i--)
        {
            var at = sourceIndex + i;
            var found = ViewPositionOfSource(at);
            if (found >= 0)
            {
                var item = _view[found];
                _view.RemoveAt(found);
                _sourceIndexOfView.RemoveAt(found);
                ShapedRemoved(found, item);
            }

            Unsubscribe(_snapshot[at]);
            _snapshot.RemoveAt(at);
        }

        ShiftSourceIndexes(sourceIndex, -count);
    }

    private void ShiftSourceIndexes(int fromSourceIndex, int delta)
    {
        for (var i = _sourceIndexOfView.Count - 1; i >= 0; i--)
        {
            if (_sourceIndexOfView[i] < fromSourceIndex)
            {
                if (IsSorted) continue;
                break;
            }

            _sourceIndexOfView[i] += delta;
        }
    }

    private int ViewPositionOfSource(int sourceIndex)
    {
        if (!IsSorted) return _sourceIndexOfView.BinarySearch(sourceIndex);

        for (var i = 0; i < _sourceIndexOfView.Count; i++)
        {
            if (_sourceIndexOfView[i] == sourceIndex) return i;
        }

        return -1;
    }

    private int InsertionPointFor(object item, int sourceIndex)
    {
        if (!IsSorted)
        {
            var found = _sourceIndexOfView.BinarySearch(sourceIndex);
            return found >= 0 ? found : ~found;
        }

        var low = 0;
        var high = _view.Count;
        while (low < high)
        {
            var mid = (low + high) / 2;
            var order = Compare(_view[mid], item);
            if (order == 0) order = _sourceIndexOfView[mid].CompareTo(sourceIndex);
            if (order <= 0) low = mid + 1;
            else high = mid;
        }

        return low;
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler PropertyChanged;

    /// <inheritdoc/>
    public event EventHandler<PageChangingEventArgs> PageChanging;

    /// <inheritdoc/>
    public event EventHandler<PageChangedEventArgs> PageChanged;

    /// <summary>Always true for an in-memory view: turning a page is a slice, and there is nothing to wait for.</summary>
    public bool CanChangePage => true;

    /// <summary>Always false for an in-memory view - see <see cref="CanChangePage"/>.</summary>
    public bool IsPageChanging => false;

    int? IPagedSource.TotalItemCount => TotalItemCount;

    int? IPagedSource.PageCount => PageCount;

    /// <summary>Turns to <paramref name="pageIndex"/>, honouring a <see cref="PageChanging"/> veto. Completed by the
    /// time it returns: the same contract as a server source, so a pager has ONE path rather than one per kind.</summary>
    public Task<bool> MoveToPageAsync(int pageIndex)
    {
        if (pageIndex < 0 || (IsPaged && pageIndex >= PageCount && PageCount > 0)) return Task.FromResult(false);

        var changing = new PageChangingEventArgs(pageIndex);
        PageChanging?.Invoke(this, changing);
        if (changing.Cancel) return Task.FromResult(false);

        PageIndex = pageIndex;
        PageChanged?.Invoke(this, new PageChangedEventArgs(_pageIndex));
        return Task.FromResult(true);
    }

    private int PageStart => _pageIndex * _pageSize;

    private void Notify(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

    private void ClampPage()
    {
        if (!IsPaged) { _pageIndex = 0; return; }
        var last = Math.Max(0, PageCount - 1);
        if (_pageIndex > last) _pageIndex = last;
    }

    private void RebuildPage()
    {
        if (!IsPaged)
        {
            _page.Clear();
            Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            return;
        }

        _page.Clear();
        for (var i = PageStart; i < _view.Count && _page.Count < _pageSize; i++) _page.Add(_view[i]);
        Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        Notify(nameof(PageIndex));
        Notify(nameof(PageCount));
        Notify(nameof(TotalItemCount));
        Notify(nameof(Count));
    }

    private void ShapedInserted(int shapedIndex, object item)
    {
        if (!IsPaged)
        {
            Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, shapedIndex));
            return;
        }

        if (shapedIndex >= PageStart + _pageSize) return;

        var at = Math.Max(0, shapedIndex - PageStart);
        var entering = _view[PageStart + at];
        _page.Insert(at, entering);
        Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, entering, at));

        if (_page.Count <= _pageSize) return;

        var pushed = _page[_pageSize];
        _page.RemoveAt(_pageSize);
        Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, pushed, _pageSize));
    }

    private void ShapedRemoved(int shapedIndex, object item)
    {
        if (!IsPaged)
        {
            Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, shapedIndex));
            return;
        }

        if (PageStart >= _view.Count && _view.Count > 0)
        {
            ClampPage();
            RebuildPage();
            return;
        }

        if (shapedIndex >= PageStart + _pageSize) return;

        var at = Math.Max(0, shapedIndex - PageStart);
        if (at >= _page.Count) return;

        var leaving = _page[at];
        _page.RemoveAt(at);
        Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, leaving, at));

        var next = PageStart + _page.Count;
        if (next >= _view.Count) return;

        _page.Add(_view[next]);
        Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, _view[next], _page.Count - 1));
    }

    private void ShapedMoved(int from, int to, object item)
    {
        if (!IsPaged)
        {
            Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, item, to, from));
            return;
        }

        var end = PageStart + _pageSize;
        if (from >= PageStart && from < end && to >= PageStart && to < end)
        {
            _page.RemoveAt(from - PageStart);
            _page.Insert(to - PageStart, item);
            Raise(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Move, item, to - PageStart, from - PageStart));
            return;
        }

        RebuildPage();
    }

    private bool IsLiveShaping => _isLiveFiltering || _isLiveSorting;

    private void ResubscribeItems()
    {
        foreach (var item in _subscribed.Keys)
        {
            if (item is INotifyPropertyChanged observable) observable.PropertyChanged -= OnItemPropertyChanged;
        }

        _subscribed.Clear();
        if (!IsLiveShaping) return;

        foreach (var item in _snapshot) Subscribe(item);
    }

    private void Subscribe(object item)
    {
        if (!IsLiveShaping || item is not INotifyPropertyChanged observable) return;

        if (_subscribed.TryGetValue(item, out var count))
        {
            _subscribed[item] = count + 1;
            return;
        }

        _subscribed[item] = 1;
        observable.PropertyChanged += OnItemPropertyChanged;
    }

    private void Unsubscribe(object item)
    {
        if (item is not INotifyPropertyChanged observable) return;
        if (!_subscribed.TryGetValue(item, out var count)) return;

        if (count > 1)
        {
            _subscribed[item] = count - 1;
            return;
        }

        _subscribed.Remove(item);
        observable.PropertyChanged -= OnItemPropertyChanged;
    }

    private bool Affects(ICollection<string> names, string changed) =>
        string.IsNullOrEmpty(changed) || names.Count == 0 || names.Contains(changed);

    private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        var refiltered = _isLiveFiltering && Affects(LiveFilteringProperties, e.PropertyName);
        var reordered = _isLiveSorting && IsSorted && Affects(SortingPropertiesFor(), e.PropertyName);
        if (!refiltered && !reordered) return;

        var sourceIndex = _snapshot.IndexOf(sender);
        if (sourceIndex < 0) return;

        var position = ViewPositionOfSource(sourceIndex);
        var shown = position >= 0;
        var passes = Passes(sender);

        if (shown && !passes)
        {
            _view.RemoveAt(position);
            _sourceIndexOfView.RemoveAt(position);
            ShapedRemoved(position, sender);
            return;
        }

        if (!shown && passes)
        {
            var at = InsertionPointFor(sender, sourceIndex);
            _view.Insert(at, sender);
            _sourceIndexOfView.Insert(at, sourceIndex);
            ShapedInserted(at, sender);
            return;
        }

        if (!shown || !reordered) return;

        _view.RemoveAt(position);
        _sourceIndexOfView.RemoveAt(position);
        var moved = InsertionPointFor(sender, sourceIndex);
        _view.Insert(moved, sender);
        _sourceIndexOfView.Insert(moved, sourceIndex);

        if (moved != position) ShapedMoved(position, moved, sender);
    }

    private ICollection<string> SortingPropertiesFor()
    {
        if (LiveSortingProperties.Count > 0 || _customSort != null) return LiveSortingProperties;

        var names = new List<string>(SortDescriptions.Count);
        foreach (var description in SortDescriptions) names.Add(description.PropertyName);
        return names;
    }

    private void Raise(NotifyCollectionChangedEventArgs e) => CollectionChanged?.Invoke(this, e);
}
