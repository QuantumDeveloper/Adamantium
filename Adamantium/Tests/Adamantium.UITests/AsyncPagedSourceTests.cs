using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Adamantium.UI.Core.Collections;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// A page that takes time to arrive brings three problems a slice does not, and each of them is a way for a list to lie
/// to the reader: an overtaken result applied late, work that carries on after nobody wants it, and an empty list shown
/// where an error belongs. Each has a test here, driven by a fetch the test itself decides when to complete - so the
/// races are exercised deterministically rather than hopefully.
/// </summary>
[TestFixture]
public class AsyncPagedSourceTests
{
    private sealed class Server
    {
        private readonly Dictionary<int, TaskCompletionSource<PageResult>> _pending = new();

        public int? Total { get; set; }
        public int Calls { get; private set; }
        public List<int> Cancelled { get; } = [];

        public PageFetch Fetch => (request, cancellation) =>
        {
            Calls++;
            var completion = new TaskCompletionSource<PageResult>();
            cancellation.Register(() => Cancelled.Add(request.PageIndex));
            _pending[request.PageIndex] = completion;
            return completion.Task;
        };

        public void Deliver(int pageIndex, params object[] items) =>
            _pending[pageIndex].SetResult(new PageResult(items, Total));

        public void Fail(int pageIndex, Exception error) => _pending[pageIndex].SetException(error);
    }

    private static AsyncPagedSource Source(Server server, int pageSize = 3) => new(server.Fetch, pageSize);

    [Test]
    public async Task APageThatArrives_IsShown()
    {
        var server = new Server { Total = 10 };
        var source = Source(server);

        var turning = source.MoveToPageAsync(1);
        Assert.That(source.IsPageChanging, Is.True, "a fetch in flight is a state the buttons follow");
        Assert.That(source.CanChangePage, Is.False);

        server.Deliver(1, "d", "e", "f");
        Assert.That(await turning, Is.True);

        Assert.That(source.Cast<object>(), Is.EqualTo(new object[] { "d", "e", "f" }));
        Assert.That(source.PageIndex, Is.EqualTo(1));
        Assert.That(source.PageCount, Is.EqualTo(4), "10 over 3");
        Assert.That(source.IsPageChanging, Is.False);
    }

    /// <summary>The overtaking case: ask for 1, then 2 before 1 lands. Page 1 must never be shown, even though its
    /// answer arrives second.</summary>
    [Test]
    public async Task AnOvertakenPage_IsNeverShown()
    {
        var server = new Server { Total = 30 };
        var source = Source(server);

        var first = source.MoveToPageAsync(1);
        var second = source.MoveToPageAsync(2);

        server.Deliver(2, "later");
        Assert.That(await second, Is.True);

        server.Deliver(1, "earlier");
        Assert.That(await first, Is.False, "the overtaken request reports that it did not happen");

        Assert.That(source.Cast<object>(), Is.EqualTo(new object[] { "later" }));
        Assert.That(source.PageIndex, Is.EqualTo(2), "the LAST request wins, whatever order the answers come in");
    }

    /// <summary>...and it is cancelled, not merely ignored: the work stops.</summary>
    [Test]
    public void AnOvertakenRequest_IsCancelled()
    {
        var server = new Server { Total = 30 };
        var source = Source(server);

        _ = source.MoveToPageAsync(1);
        _ = source.MoveToPageAsync(2);

        Assert.That(server.Cancelled, Is.EqualTo(new[] { 1 }));
    }

    /// <summary>A failed fetch leaves the previous page on screen. An emptied list would tell the reader there is
    /// nothing here, which is a worse lie than "that did not work".</summary>
    [Test]
    public async Task AFailedPage_LeavesThePreviousOneShowing()
    {
        var server = new Server { Total = 30 };
        var source = Source(server);

        var first = source.MoveToPageAsync(0);
        server.Deliver(0, "a", "b", "c");
        await first;

        var second = source.MoveToPageAsync(1);
        server.Fail(1, new InvalidOperationException("no connection"));

        Assert.That(await second, Is.False);
        Assert.That(source.Cast<object>(), Is.EqualTo(new object[] { "a", "b", "c" }), "the reader keeps what they had");
        Assert.That(source.PageIndex, Is.EqualTo(0), "and is still on the page they were on");
        Assert.That(source.LastError, Is.TypeOf<InvalidOperationException>());
        Assert.That(source.IsPageChanging, Is.False, "the failure must release the buttons, or the pager sticks");
        Assert.That(source.CanChangePage, Is.True);
    }

    [Test]
    public async Task ASuccessfulPage_ClearsTheError()
    {
        var server = new Server { Total = 30 };
        var source = Source(server);

        var failing = source.MoveToPageAsync(0);
        server.Fail(0, new InvalidOperationException());
        await failing;
        Assert.That(source.LastError, Is.Not.Null);

        var retry = source.MoveToPageAsync(0);
        server.Deliver(0, "a");
        await retry;

        Assert.That(source.LastError, Is.Null);
    }

    /// <summary>A source that cannot count says nothing, and the numbers stay unknown until the end turns up.</summary>
    [Test]
    public async Task AnUnknownTotal_StaysUnknown()
    {
        var server = new Server { Total = null };
        var source = Source(server);

        var turning = source.MoveToPageAsync(0);
        server.Deliver(0, "a", "b", "c");
        await turning;

        Assert.That(source.TotalItemCount, Is.Null);
        Assert.That(source.PageCount, Is.Null, "no page numbers can be drawn, and no 'last page' offered");
        Assert.That(source.IsEndKnown, Is.False);
    }

    /// <summary>...and the end is DISCOVERED: a short page is the last one, so the total becomes known by reaching it.
    /// That is the moment a pager can grow its numbers.</summary>
    [Test]
    public async Task AShortPage_DiscoversTheEnd()
    {
        var server = new Server { Total = null };
        var source = Source(server);

        var first = source.MoveToPageAsync(0);
        server.Deliver(0, "a", "b", "c");
        await first;
        Assert.That(source.IsEndKnown, Is.False);

        var second = source.MoveToPageAsync(1);
        server.Deliver(1, "d");
        await second;

        Assert.That(source.IsEndKnown, Is.True);
        Assert.That(source.TotalItemCount, Is.EqualTo(4), "three on the full page, one on the short one");
        Assert.That(source.PageCount, Is.EqualTo(2));
    }

    [Test]
    public async Task AVetoedTurn_DoesNotHappen()
    {
        var server = new Server { Total = 30 };
        var source = Source(server);
        source.PageChanging += (_, e) => e.Cancel = true;

        Assert.That(await source.MoveToPageAsync(2), Is.False);
        Assert.That(server.Calls, Is.Zero, "a refused turn must not even ask the server");
        Assert.That(source.PageIndex, Is.Zero);
    }

    [Test]
    public async Task APageBeyondTheEnd_IsRefused()
    {
        var server = new Server { Total = 7 };
        var source = Source(server);

        var first = source.MoveToPageAsync(0);
        server.Deliver(0, "a", "b", "c");
        await first;

        Assert.That(await source.MoveToPageAsync(3), Is.False, "7 over 3 is three pages: 0, 1, 2");
        Assert.That(await source.MoveToPageAsync(-1), Is.False);
    }

    /// <summary>An in-memory view answers the SAME interface, so a pager written against it works over either without
    /// knowing which it has.</summary>
    [Test]
    public async Task AnInMemoryView_AnswersTheSameContract()
    {
        IPagedSource source = new CollectionView(Enumerable.Range(1, 10).Cast<object>().ToList()) { PageSize = 3 };

        Assert.That(source.CanChangePage, Is.True);
        Assert.That(source.IsPageChanging, Is.False);
        Assert.That(source.TotalItemCount, Is.EqualTo(10));
        Assert.That(source.PageCount, Is.EqualTo(4));

        Assert.That(await source.MoveToPageAsync(3), Is.True);
        Assert.That(source.PageIndex, Is.EqualTo(3));
        Assert.That(await source.MoveToPageAsync(4), Is.False);
    }
}
