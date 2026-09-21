using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Adamantium.UI.Core.Collections;

/// <summary>
/// What a pager needs and nothing else: which page is shown, how big it is, how much there is, and how to turn to
/// another one. Deliberately narrow - a pager has no business knowing whether the thing underneath sorts or filters,
/// and a server that only serves pages should not have to pretend it can.
/// </summary>
public interface IPagedSource : INotifyPropertyChanged
{
    /// <summary>How many items a page holds. Setting it re-pages from where the reader already is.</summary>
    int PageSize { get; set; }

    /// <summary>Which page is shown, counted from zero.</summary>
    int PageIndex { get; }

    /// <summary>How many items there are in total, or NULL when that is not known - which a server paging a query
    /// legitimately may not know until the end is reached. Not a sentinel like -1: a magic number passes arithmetic and
    /// quietly produces a page count of zero, and nothing about that failure looks like a failure.</summary>
    int? TotalItemCount { get; }

    /// <summary>How many pages there are, or null while <see cref="TotalItemCount"/> is unknown.</summary>
    int? PageCount { get; }

    /// <summary>False while a page change is in flight or refused - what a pager's buttons follow.</summary>
    bool CanChangePage { get; }

    /// <summary>True from the moment a page is asked for until it arrives or fails.</summary>
    bool IsPageChanging { get; }

    /// <summary>Turns to <paramref name="pageIndex"/>. False = it did not happen (out of range, vetoed, superseded by a
    /// later request, or the fetch failed).</summary>
    Task<bool> MoveToPageAsync(int pageIndex);

    /// <summary>Raised before a page is turned to; cancelling it refuses the turn - which is how a view holding an
    /// unsaved edit keeps the reader where they are.</summary>
    event EventHandler<PageChangingEventArgs> PageChanging;

    /// <summary>Raised once a page is actually showing.</summary>
    event EventHandler<PageChangedEventArgs> PageChanged;
}

/// <summary>The page about to be turned to, and the chance to refuse it.</summary>
public sealed class PageChangingEventArgs(int pageIndex) : CancelEventArgs
{
    /// <summary>The page being turned to.</summary>
    public int PageIndex { get; } = pageIndex;
}

/// <summary>The page now showing.</summary>
public sealed class PageChangedEventArgs(int pageIndex) : EventArgs
{
    /// <summary>The page now showing.</summary>
    public int PageIndex { get; } = pageIndex;
}

/// <summary>What a page fetch is asked for.</summary>
public readonly struct PageRequest(int pageIndex, int pageSize)
{
    /// <summary>Which page, counted from zero.</summary>
    public int PageIndex { get; } = pageIndex;

    /// <summary>How many items were asked for.</summary>
    public int PageSize { get; } = pageSize;
}

/// <summary>What a page fetch came back with.</summary>
public readonly struct PageResult(IReadOnlyList<object> items, int? totalItemCount = null)
{
    /// <summary>The page's items.</summary>
    public IReadOnlyList<object> Items { get; } = items;

    /// <summary>How many there are in total, if the source knows. Null leaves it unknown - and then FEWER items than
    /// were asked for is what says this was the last page, so the total becomes known by arriving at it.</summary>
    public int? TotalItemCount { get; } = totalItemCount;
}

/// <summary>Fetches one page. The token is cancelled when a later request supersedes this one.</summary>
public delegate Task<PageResult> PageFetch(PageRequest request, CancellationToken cancellation);
