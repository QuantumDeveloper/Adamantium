using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Adamantium.UI.Core.Collections;

/// <summary>
/// A page at a time from somewhere that does not hand over everything at once - a query, a service, a file. The items
/// shown are whatever the last completed fetch returned, so this is bound to a list exactly as an in-memory view is.
///
/// <para>Three things a fetch that takes time forces, and all three are the reason this is not just a lambda:</para>
/// <list type="number">
/// <item>SUPERSEDING. Click 3, then 5 before 3 lands, and the later one wins - the earlier result is dropped rather
/// than applied when it turns up, or the list flashes page three after page five.</item>
/// <item>CANCELLATION. A superseded request has its token cancelled, so the work actually stops instead of merely
/// having its answer ignored. <see cref="PageChanging"/> can refuse the turn outright.</item>
/// <item>FAILURE. A fetch that throws leaves the PREVIOUS page on screen: emptying the list on an error shows the
/// reader "there is nothing here", which is a different and worse lie than "that did not work".</item>
/// </list>
///
/// <para>An unknown total is not a defect either. A source that cannot count says so, and the end is then discovered:
/// a page that comes back SHORT is the last one, and the total becomes known by arriving at it - at which point the
/// page numbers and the "last page" button can appear.</para>
/// </summary>
// INotifyCollectionChanged IS DECLARED, not merely raised. The event was here from the start and fired on every page
// that arrived, but the interface was missing from this line - and a list subscribes by ASKING (`source is
// INotifyCollectionChanged`), never by looking for an event of that name. So the answer was no, nobody subscribed, and a
// page fetched from a server landed in this object and was never shown: the request completed, the state properties all
// updated, and the rows on screen stayed as they were.
public class AsyncPagedSource : IPagedSource, IEnumerable, IReadOnlyList<object>, INotifyCollectionChanged
{
    private readonly PageFetch _fetch;
    private readonly List<object> _items = [];
    private CancellationTokenSource _inFlight;
    private int _requestGeneration;
    private int _pageSize;
    private int _pageIndex;
    private int? _totalItemCount;
    private bool _isPageChanging;

    /// <summary>Creates a source that calls <paramref name="fetch"/> for each page.</summary>
    public AsyncPagedSource(PageFetch fetch, int pageSize = 25)
    {
        _fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));
        _pageSize = Math.Max(1, pageSize);
    }

    /// <inheritdoc/>
    public event NotifyCollectionChangedEventHandler CollectionChanged;

    /// <inheritdoc/>
    public event PropertyChangedEventHandler PropertyChanged;

    /// <inheritdoc/>
    public event EventHandler<PageChangingEventArgs> PageChanging;

    /// <inheritdoc/>
    public event EventHandler<PageChangedEventArgs> PageChanged;

    /// <summary>Why the last page failed to arrive, or null. Cleared by the next successful fetch.</summary>
    public Exception LastError { get; private set; }

    /// <inheritdoc/>
    public int PageSize
    {
        get => _pageSize;
        set
        {
            var size = Math.Max(1, value);
            if (_pageSize == size) return;

            var firstItem = _pageIndex * _pageSize;
            _pageSize = size;
            Notify(nameof(PageSize));
            Notify(nameof(PageCount));
            _ = MoveToPageAsync(firstItem / size);
        }
    }

    /// <inheritdoc/>
    public int PageIndex => _pageIndex;

    /// <inheritdoc/>
    public int? TotalItemCount => _totalItemCount;

    /// <inheritdoc/>
    public int? PageCount => _totalItemCount is { } total ? (total + _pageSize - 1) / _pageSize : null;

    /// <inheritdoc/>
    public bool CanChangePage => !_isPageChanging;

    /// <inheritdoc/>
    public bool IsPageChanging
    {
        get => _isPageChanging;
        private set
        {
            if (_isPageChanging == value) return;
            _isPageChanging = value;
            Notify(nameof(IsPageChanging));
            Notify(nameof(CanChangePage));
        }
    }

    /// <summary>How many items the current page holds - short on the last one.</summary>
    public int Count => _items.Count;

    /// <inheritdoc/>
    public object this[int index] => _items[index];

    /// <summary>True once a short page has revealed where the end is, or the source said so outright.</summary>
    public bool IsEndKnown => _totalItemCount.HasValue;

    /// <inheritdoc/>
    public IEnumerator<object> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    public async Task<bool> MoveToPageAsync(int pageIndex)
    {
        if (pageIndex < 0) return false;
        if (PageCount is { } pages && pageIndex >= pages && pages > 0) return false;

        var changing = new PageChangingEventArgs(pageIndex);
        PageChanging?.Invoke(this, changing);
        if (changing.Cancel) return false;

        var generation = ++_requestGeneration;
        _inFlight?.Cancel();
        _inFlight?.Dispose();
        var cancellation = new CancellationTokenSource();
        _inFlight = cancellation;

        IsPageChanging = true;

        try
        {
            var result = await _fetch(new PageRequest(pageIndex, _pageSize), cancellation.Token).ConfigureAwait(true);
            if (generation != _requestGeneration) return false;

            Apply(pageIndex, result);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception error)
        {
            if (generation != _requestGeneration) return false;
            LastError = error;
            Notify(nameof(LastError));
            return false;
        }
        finally
        {
            if (generation == _requestGeneration) IsPageChanging = false;
        }
    }

    private void Apply(int pageIndex, PageResult result)
    {
        var items = result.Items ?? [];

        _items.Clear();
        _items.AddRange(items);
        _pageIndex = pageIndex;
        LastError = null;

        if (result.TotalItemCount is { } stated) _totalItemCount = stated;
        else if (items.Count < _pageSize) _totalItemCount = pageIndex * _pageSize + items.Count;

        Notify(nameof(PageIndex));
        Notify(nameof(TotalItemCount));
        Notify(nameof(PageCount));
        Notify(nameof(IsEndKnown));
        Notify(nameof(Count));
        Notify(nameof(LastError));
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        PageChanged?.Invoke(this, new PageChangedEventArgs(pageIndex));
    }

    private void Notify(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
}
