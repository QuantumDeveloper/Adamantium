using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Adamantium.MVVM;
using Adamantium.UI.Controls;
using Adamantium.UI.Core.Collections;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Lists tab: one collection shown by a single-select and a multiple-select ListBox. The selection is bound
/// two-way, and Add/Remove commands mutate the collection live - so every list view stays in sync through the
/// view-model.
/// <para>Plus the LARGE list, which is a different demonstration entirely: 20 000 rows behind a CollectionView that
/// filters as you type and pages the result. Typing is where live filtering earns its keep - the predicate changes on
/// every keystroke, and rows leave one at a time rather than the list being rebuilt.</para></summary>
[ViewModel]
public partial class ListsViewModel : TabPageViewModel
{
    public ListsViewModel() : base("Lists")
    {
        Large = new CollectionView(BigItems) { PageSize = 25 };
        Large.SortDescriptions.Add(new SortDescription(nameof(BigItem.Name)));

        PageSteps.CollectionChanged += (_, _) => PageStepsText = string.Join("   ", PageSteps);
        PageStepsText = string.Join("   ", PageSteps);

        Server = new AsyncPagedSource(FetchServerPage, 25);
        Server.PropertyChanged += OnServerChanged;
        _ = Server.MoveToPageAsync(0);   // nobody fetches the first page for you: a source that pages is asked, not read
    }

    public ObservableCollection<string> Items { get; } = new()
    {
        "Mercury", "Venus", "Earth", "Mars", "Jupiter", "Saturn", "Uranus", "Neptune"
    };

    [Bindable] private string _selectedItem = "Earth";

    private int _counter;

    [Command] private void Add() => Items.Add($"New planet {++_counter}");

    [Command] private void RemoveSelected()
    {
        if (SelectedItem != null) Items.Remove(SelectedItem);
    }

    // --- The large list ------------------------------------------------------------------------------------------

    /// <summary>Twenty thousand rows - enough that showing them all is a real decision rather than a formality.</summary>
    public ObservableCollection<BigItem> BigItems { get; } =
        new(Enumerable.Range(1, 20000).Select(i => new BigItem($"Item {i:D5}", i)));

    /// <summary>The view the list and the pager BOTH look at: filtered, sorted, and cut into pages, in that order.</summary>
    public CollectionView Large { get; }

    /// <summary>The search box. Every keystroke re-filters, which is the point of the stand.</summary>
    [Bindable] private string _search = string.Empty;

    // ── A SERVER THAT HANDS OUT ONE PAGE AT A TIME ─────────────────────────────────────────────────────────────────
    //
    // The half of the contract an in-memory view can never show: a page that ARRIVES rather than being sliced, a total
    // nobody knows until the end is walked into, a request superseded by the next one, and a failure that has to leave
    // the reader looking at something. Everything here is a stand-in for a network - the delay and the failure are
    // switches so the behaviour can be produced on demand instead of waited for.

    /// <summary>How long the fake server takes to answer, in milliseconds. Long enough to SEE the in-flight state.</summary>
    [Bindable] private int _serverDelay = 600;

    /// <summary>Armed by the button; the next request fails and disarms it. One failure, deliberately: a source that
    /// always failed would only show an empty list, and the point is what survives ONE.</summary>
    private bool _failNextRequest;

    /// <summary>What the fake server holds. Deliberately NOT a round number of pages - the last one comes back short,
    /// which is how the end is discovered rather than announced.</summary>
    private const int ServerRowCount = 237;

    /// <summary>The paged source the second list and its pager both look at.</summary>
    public AsyncPagedSource Server { get; }

    /// <summary>What the server is doing, in words - the only place the reader can see a request that is in flight, a
    /// total that is not known yet, or a page that failed to arrive.</summary>
    [Bindable] private string _serverStatus = string.Empty;

    [Command]
    private void FailNextServerRequest()
    {
        _failNextRequest = true;
        ServerStatus = "armed: the next page will fail";
    }

    [Command]
    private void ReloadServerPage() => _ = Server.MoveToPageAsync(Server.PageIndex);

    private async Task<PageResult> FetchServerPage(PageRequest request, CancellationToken cancellation)
    {
        await Task.Delay(Math.Max(0, ServerDelay), cancellation);

        if (_failNextRequest)
        {
            _failNextRequest = false;
            throw new InvalidOperationException($"the server refused page {request.PageIndex + 1}");
        }

        var from = request.PageIndex * request.PageSize;
        var count = Math.Max(0, Math.Min(request.PageSize, ServerRowCount - from));
        var rows = Enumerable.Range(from, count)
            .Select(object (i) => $"Row {i + 1:0000} — served")
            .ToList();

        // The total is NOT reported. The pager has to discover the end by walking into a short page, which is the
        // honest state for a query whose count nobody has run.
        return new PageResult(rows);
    }

    private void OnServerChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var total = Server.TotalItemCount is { } count ? count.ToString() : "?";
        ServerStatus = Server.IsPageChanging
            ? $"loading page {Server.PageIndex + 1}…"
            : Server.LastError is { } error
                ? $"failed: {error.Message} — the page you were on is still here"
                : $"page {Server.PageIndex + 1}, {total} rows known";
    }

    /// <summary>The page sizes this stand offers - NOT the control's own 10/25/50/100, on purpose: the progression
    /// belongs to the application, and a stand that took the default would never show that.
    /// <para>Observable, and edited in place by the two commands below, because "does it update live" is a question
    /// about the COLLECTION and not about the property - handing over a new list would answer a different one.</para></summary>
    public ObservableCollection<int> PageSteps { get; } = [15, 30, 60, 120];

    /// <summary>The progression as one line of text. A LABEL, not a list: an items host lays its rows out on a uniform
    /// pitch, which is right for rows of equal size and wrong for numbers of two, three and four digits - the narrow
    /// ones got a gap and the wide ones ran together. A readout has no need of a host.</summary>
    [Bindable] private string _pageStepsText = string.Empty;

    [Command]
    private void AddStep()
    {
        var next = PageSteps.Count > 0 ? PageSteps[^1] * 2 : 15;
        if (!PageSteps.Contains(next)) PageSteps.Add(next);
    }

    [Command]
    private void DropCurrentStep() => PageSteps.Remove(Large.PageSize);

    // THE FLAGS ENUM AS FIVE SWITCHES. DisplayMode is one value carrying five independent bits, and a check box is the
    // control for a bit - so the stand offers the bits and assembles the value, which is also the only way to try a
    // combination without editing markup and rebuilding.
    [Bindable] private bool _showFirstLast = true;
    [Bindable] private bool _showPreviousNext = true;
    [Bindable] private bool _showNumeric = true;
    [Bindable] private bool _showPageSizeSelector = true;
    [Bindable] private bool _showPageInput = true;

    /// <summary>What the switches add up to - the value both pagers are given.</summary>
    [Bindable] private PagerDisplayMode _pagerMode = PagerDisplayMode.All;

    partial void OnShowFirstLastChanged(bool value) => RebuildPagerMode();

    partial void OnShowPreviousNextChanged(bool value) => RebuildPagerMode();

    partial void OnShowNumericChanged(bool value) => RebuildPagerMode();

    partial void OnShowPageSizeSelectorChanged(bool value) => RebuildPagerMode();

    partial void OnShowPageInputChanged(bool value) => RebuildPagerMode();

    private void RebuildPagerMode()
    {
        var mode = PagerDisplayMode.None;
        if (ShowFirstLast) mode |= PagerDisplayMode.FirstLast;
        if (ShowPreviousNext) mode |= PagerDisplayMode.PreviousNext;
        if (ShowNumeric) mode |= PagerDisplayMode.Numeric;
        if (ShowPageSizeSelector) mode |= PagerDisplayMode.PageSizeSelector;
        if (ShowPageInput) mode |= PagerDisplayMode.PageInput;
        PagerMode = mode;
    }

    partial void OnSearchChanged(string value)
    {
        var text = value?.Trim();
        Large.Filter = string.IsNullOrEmpty(text)
            ? null
            : o => ((BigItem)o).Name.Contains(text, System.StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>A row of the large list: something to show and something to sort by.</summary>
public sealed class BigItem(string name, int number)
{
    /// <summary>What the row shows.</summary>
    public string Name { get; } = name;

    /// <summary>Its position in the original set, so a sort has something of its own to order by.</summary>
    public int Number { get; } = number;

    /// <inheritdoc/>
    public override string ToString() => Name;
}
