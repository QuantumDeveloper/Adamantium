using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Collections;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>Which parts of a <see cref="DataPager"/> are shown. They compose, so a row can be as bare as two arrows or
/// as full as arrows, numbers, a size picker and a box to type a page into.</summary>
[Flags]
public enum PagerDisplayMode
{
    /// <summary>Nothing but whatever the template draws unconditionally.</summary>
    None = 0,

    /// <summary>Jump to the first and last page. The last is unavailable while the end is unknown.</summary>
    FirstLast = 1,

    /// <summary>Step one page back or forward - the pair that works even over a source that cannot count.</summary>
    PreviousNext = 2,

    /// <summary>Numbered buttons with ellipses. Needs a known total.</summary>
    Numeric = 4,

    /// <summary>A picker for how many items a page holds.</summary>
    PageSizeSelector = 8,

    /// <summary>"Page N of M", and a box to type a number into.</summary>
    PageInput = 16,

    /// <summary>Everything.</summary>
    All = FirstLast | PreviousNext | Numeric | PageSizeSelector | PageInput
}

/// <summary>
/// Turns the pages of a collection. It does not page anything itself: it drives an <see cref="IPagedSource"/> and shows
/// where that has got to, which is why the same control works over an in-memory view and over a server that hands out
/// one page at a time.
///
/// <para>WHO OWNS THE VIEW is the question this control's shape answers. Bind a plain collection to
/// <see cref="Source"/> and the pager wraps it; bind the LIST to <see cref="PagedSource"/> and the two are looking at
/// the same object. Had each made its own, they would have been turning different pages of the same data.</para>
///
/// <code>
/// &lt;DataPager x:Name="Pager" Source="{Binding Orders}" PageSize="25"/&gt;
/// &lt;ListBox ItemsSource="{Binding PagedSource, ElementName=Pager}"/&gt;
/// </code>
/// </summary>
public class DataPager : Control
{
    private Button _first;
    private Button _previous;
    private Button _next;
    private Button _last;
    private NumericUpDown _pageBox;
    private DropDown _pageSizes;
    private IPagedSource _paged;

    // The parts the WIDTH took away, as opposed to the ones the author turned off. Kept apart so the two can be told
    // apart: an author's DisplayMode is a decision and this is a consequence, and mixing them would make a part that
    // came back on a wider window look like a setting that changed itself.
    private PagerDisplayMode _shed = PagerDisplayMode.None;
    private readonly Dictionary<PagerDisplayMode, double> _partWidths = new();

    private static readonly string FitLog = Environment.GetEnvironmentVariable("ADAM_PAGER_FIT");

    // THE DEFAULT PROGRESSION, and it lives in the property's METADATA rather than in the constructor. A plain setter in
    // a constructor writes the Local slot, which outranks a binding for the life of the object - so `PageSizes="{Binding
    // Steps}"` was accepted, reported no error, and changed nothing: the pager kept 10/25/50/100 and an application's
    // own progression never reached the picker. A metadata default sits at Default priority, where anything can replace
    // it. Read-only because one instance is shared by every pager that has not been given its own: a default nobody can
    // edit in place is the only kind that can be shared, and an application that wants a LIVE progression supplies one.
    private static readonly IList<int> DefaultPageSizes = new ReadOnlyCollection<int>([10, 25, 50, 100]);

    /// <summary>The collection to page. ANY collection: an array, a list, an observable collection, a lazy query - all
    /// of them are enumerable, and an enumerable one is wrapped in a <see cref="CollectionView"/> so it can be paged.
    /// An <see cref="IPagedSource"/> is driven as it is, wrapped in nothing.
    /// <para>Typed as <c>object</c> rather than as a collection on purpose: a source that pages need not be enumerable
    /// at all - a server handing out one page at a time is a perfectly good one. Anything that is neither is a mistake
    /// and says so, rather than leaving an empty pager for someone to puzzle over.</para></summary>
    public static readonly AdamantiumProperty SourceProperty = AdamantiumProperty.Register(nameof(Source),
        typeof(object), typeof(DataPager), new PropertyMetadata(null, OnSourceChanged));

    /// <summary>What the ITEMS control binds to - the current page. Read-only: it is whatever <see cref="Source"/>
    /// resolved to.</summary>
    public static readonly AdamantiumProperty PagedSourceProperty = AdamantiumProperty.Register(nameof(PagedSource),
        typeof(object), typeof(DataPager), new PropertyMetadata(null));

    /// <summary>How many items a page holds.</summary>
    public static readonly AdamantiumProperty PageSizeProperty = AdamantiumProperty.Register(nameof(PageSize),
        typeof(int), typeof(DataPager), new PropertyMetadata(25, OnPageSizeChanged));

    /// <summary>Which page is shown, counted from zero. Setting it turns to that page.</summary>
    public static readonly AdamantiumProperty PageIndexProperty = AdamantiumProperty.Register(nameof(PageIndex),
        typeof(int), typeof(DataPager), new PropertyMetadata(0, OnPageIndexChanged));

    /// <summary>How many pages there are, or -1 while the source cannot say - which is what a server paging a query
    /// legitimately may not know until the end is reached.</summary>
    public static readonly AdamantiumProperty PageCountProperty = AdamantiumProperty.Register(nameof(PageCount),
        typeof(int), typeof(DataPager), new PropertyMetadata(0));

    /// <summary>How many items there are to page through, or -1 while that is unknown.</summary>
    public static readonly AdamantiumProperty TotalItemCountProperty = AdamantiumProperty.Register(
        nameof(TotalItemCount), typeof(int), typeof(DataPager), new PropertyMetadata(0));

    /// <summary>True while the end has been found - either stated outright or discovered by a page coming back short.
    /// The numbered buttons and the "last page" arrow wait for it.</summary>
    public static readonly AdamantiumProperty IsEndKnownProperty = AdamantiumProperty.Register(nameof(IsEndKnown),
        typeof(bool), typeof(DataPager), new PropertyMetadata(true));

    /// <summary>True while a page is on its way. A template shows a spinner off this; the buttons go quiet.</summary>
    public static readonly AdamantiumProperty IsPageChangingProperty = AdamantiumProperty.Register(
        nameof(IsPageChanging), typeof(bool), typeof(DataPager), new PropertyMetadata(false));

    /// <summary>Where the row of buttons sits inside the width the pager is given.
    /// <para>Declared HERE because a pager is a <c>Control</c> and not a <c>ContentControl</c> - it has no content to
    /// align, it has a row - so the pair every template reaches for by that name does not otherwise exist on it, and a
    /// template binding to it resolved to nothing at all.</para></summary>
    public static readonly AdamantiumProperty HorizontalContentAlignmentProperty = AdamantiumProperty.Register(
        nameof(HorizontalContentAlignment), typeof(HorizontalAlignment), typeof(DataPager),
        new PropertyMetadata(HorizontalAlignment.Left, PropertyMetadataOptions.AffectsArrange));

    /// <inheritdoc cref="HorizontalContentAlignmentProperty"/>
    public static readonly AdamantiumProperty VerticalContentAlignmentProperty = AdamantiumProperty.Register(
        nameof(VerticalContentAlignment), typeof(VerticalAlignment), typeof(DataPager),
        new PropertyMetadata(VerticalAlignment.Center, PropertyMetadataOptions.AffectsArrange));

    /// <summary>Which parts of the pager are shown.</summary>
    public static readonly AdamantiumProperty DisplayModeProperty = AdamantiumProperty.Register(nameof(DisplayMode),
        typeof(PagerDisplayMode), typeof(DataPager), new PropertyMetadata(PagerDisplayMode.All, OnDisplayModeChanged));

    /// <summary>True while <see cref="PagerDisplayMode.FirstLast"/> is set.</summary>
    public static readonly AdamantiumProperty ShowsFirstLastProperty = AdamantiumProperty.Register(
        nameof(ShowsFirstLast), typeof(bool), typeof(DataPager), new PropertyMetadata(true));

    /// <summary>True while <see cref="PagerDisplayMode.PreviousNext"/> is set.</summary>
    public static readonly AdamantiumProperty ShowsPreviousNextProperty = AdamantiumProperty.Register(
        nameof(ShowsPreviousNext), typeof(bool), typeof(DataPager), new PropertyMetadata(true));

    /// <summary>True while <see cref="PagerDisplayMode.Numeric"/> is set AND the end is known - a row of numbers over a
    /// source that cannot count has nothing to number.</summary>
    public static readonly AdamantiumProperty ShowsNumbersProperty = AdamantiumProperty.Register(
        nameof(ShowsNumbers), typeof(bool), typeof(DataPager), new PropertyMetadata(true));

    /// <summary>True while <see cref="PagerDisplayMode.PageSizeSelector"/> is set.</summary>
    public static readonly AdamantiumProperty ShowsPageSizeSelectorProperty = AdamantiumProperty.Register(
        nameof(ShowsPageSizeSelector), typeof(bool), typeof(DataPager), new PropertyMetadata(true));

    /// <summary>True while <see cref="PagerDisplayMode.PageInput"/> is set.</summary>
    public static readonly AdamantiumProperty ShowsPageInputProperty = AdamantiumProperty.Register(
        nameof(ShowsPageInput), typeof(bool), typeof(DataPager), new PropertyMetadata(true));

    /// <summary>The PROGRESSION of page sizes on offer - 10, 25, 50, 100 unless an application says otherwise, and the
    /// set <see cref="PageSize"/> is chosen from.
    /// <para>Bind it or assign it: <c>PageSizes="{Binding Steps}"</c>. Replacing it re-offers the choices, and a
    /// <see cref="PageSize"/> that is not among them moves to the nearest one that is - a picker showing a value it
    /// cannot offer is a picker lying about its choices.</para></summary>
    public static readonly AdamantiumProperty PageSizesProperty = AdamantiumProperty.Register(nameof(PageSizes),
        typeof(IList<int>), typeof(DataPager), new PropertyMetadata(DefaultPageSizes, OnPageSizesChanged));

    /// <summary>True while there is a page before this one.</summary>
    public static readonly AdamantiumProperty CanGoBackProperty = AdamantiumProperty.Register(nameof(CanGoBack),
        typeof(bool), typeof(DataPager), new PropertyMetadata(false));

    /// <summary>True while there is a page after this one. Over a source that cannot count this stays true until a
    /// short page proves otherwise - which is the honest answer, not an optimistic one.</summary>
    public static readonly AdamantiumProperty CanGoForwardProperty = AdamantiumProperty.Register(nameof(CanGoForward),
        typeof(bool), typeof(DataPager), new PropertyMetadata(false));

    /// <summary>Where the reader is, as a sentence: "Page 3 of 12", or "Page 3 of ?" while the end has not been found.
    /// A question mark rather than a number the pager does not have - "of 0" would be a lie with a straight face.</summary>
    public static readonly AdamantiumProperty PageTextProperty = AdamantiumProperty.Register(nameof(PageText),
        typeof(string), typeof(DataPager), new PropertyMetadata(string.Empty));

    /// <summary>The current page as a bare number, counted from one - what the box a reader types into holds. Separate
    /// from <see cref="PageText"/> because that is a sentence and this is an editable value.</summary>
    public static readonly AdamantiumProperty PageNumberTextProperty = AdamantiumProperty.Register(
        nameof(PageNumberText), typeof(string), typeof(DataPager), new PropertyMetadata(string.Empty));

    /// <summary>The tail of the sentence the box sits inside: "of 12", or "of ?" while the end has not been found.</summary>
    public static readonly AdamantiumProperty PageCountTextProperty = AdamantiumProperty.Register(
        nameof(PageCountText), typeof(string), typeof(DataPager), new PropertyMetadata(string.Empty));

    /// <summary>The row of page numbers, gaps included - see <see cref="PagerPageItem"/>. Rebuilt whenever the page or
    /// the count moves.</summary>
    public static readonly AdamantiumProperty PageItemsProperty = AdamantiumProperty.Register(nameof(PageItems),
        typeof(IReadOnlyList<PagerPageItem>), typeof(DataPager), new PropertyMetadata(null));

    /// <summary>How many entries the row holds - GAPS INCLUDED, because the count is the row's length and an ellipsis
    /// takes a place in it like any other.
    /// <para>The row is exactly this long at every page and every page count. A gap that came and went as the reader
    /// moved would shift every button beside it, so where there is nothing to leave out the freed place is spent on one
    /// more page rather than left empty: at the start the row reads "1 2 3 4 …", and the ellipsis only appears when
    /// there is genuinely a page hidden behind it. Anything further off is reached by typing the number.</para></summary>
    public static readonly AdamantiumProperty PageButtonCountProperty = AdamantiumProperty.Register(
        nameof(PageButtonCount), typeof(int), typeof(DataPager), new PropertyMetadata(5, OnPageButtonCountChanged));

    /// <inheritdoc cref="SourceProperty"/>
    public object Source
    {
        get => GetValue<object>(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <inheritdoc cref="PagedSourceProperty"/>
    public object PagedSource
    {
        get => GetValue<object>(PagedSourceProperty);
        private set => SetValue(PagedSourceProperty, value);
    }

    /// <inheritdoc cref="PageSizeProperty"/>
    public int PageSize
    {
        get => GetValue<int>(PageSizeProperty);
        set => SetValue(PageSizeProperty, value);
    }

    /// <inheritdoc cref="PageIndexProperty"/>
    public int PageIndex
    {
        get => GetValue<int>(PageIndexProperty);
        set => SetValue(PageIndexProperty, value);
    }

    /// <inheritdoc cref="PageCountProperty"/>
    public int PageCount
    {
        get => GetValue<int>(PageCountProperty);
        private set => SetValue(PageCountProperty, value);
    }

    /// <inheritdoc cref="TotalItemCountProperty"/>
    public int TotalItemCount
    {
        get => GetValue<int>(TotalItemCountProperty);
        private set => SetValue(TotalItemCountProperty, value);
    }

    /// <inheritdoc cref="IsEndKnownProperty"/>
    public bool IsEndKnown
    {
        get => GetValue<bool>(IsEndKnownProperty);
        private set => SetValue(IsEndKnownProperty, value);
    }

    /// <inheritdoc cref="IsPageChangingProperty"/>
    public bool IsPageChanging
    {
        get => GetValue<bool>(IsPageChangingProperty);
        private set => SetValue(IsPageChangingProperty, value);
    }

    /// <inheritdoc cref="HorizontalContentAlignmentProperty"/>
    public HorizontalAlignment HorizontalContentAlignment
    {
        get => GetValue<HorizontalAlignment>(HorizontalContentAlignmentProperty);
        set => SetValue(HorizontalContentAlignmentProperty, value);
    }

    /// <inheritdoc cref="VerticalContentAlignmentProperty"/>
    public VerticalAlignment VerticalContentAlignment
    {
        get => GetValue<VerticalAlignment>(VerticalContentAlignmentProperty);
        set => SetValue(VerticalContentAlignmentProperty, value);
    }

    /// <inheritdoc cref="DisplayModeProperty"/>
    public PagerDisplayMode DisplayMode
    {
        get => GetValue<PagerDisplayMode>(DisplayModeProperty);
        set => SetValue(DisplayModeProperty, value);
    }

    /// <inheritdoc cref="ShowsFirstLastProperty"/>
    public bool ShowsFirstLast
    {
        get => GetValue<bool>(ShowsFirstLastProperty);
        private set => SetValue(ShowsFirstLastProperty, value);
    }

    /// <inheritdoc cref="ShowsPreviousNextProperty"/>
    public bool ShowsPreviousNext
    {
        get => GetValue<bool>(ShowsPreviousNextProperty);
        private set => SetValue(ShowsPreviousNextProperty, value);
    }

    /// <inheritdoc cref="ShowsNumbersProperty"/>
    public bool ShowsNumbers
    {
        get => GetValue<bool>(ShowsNumbersProperty);
        private set => SetValue(ShowsNumbersProperty, value);
    }

    /// <inheritdoc cref="ShowsPageSizeSelectorProperty"/>
    public bool ShowsPageSizeSelector
    {
        get => GetValue<bool>(ShowsPageSizeSelectorProperty);
        private set => SetValue(ShowsPageSizeSelectorProperty, value);
    }

    /// <inheritdoc cref="ShowsPageInputProperty"/>
    public bool ShowsPageInput
    {
        get => GetValue<bool>(ShowsPageInputProperty);
        private set => SetValue(ShowsPageInputProperty, value);
    }

    /// <inheritdoc cref="PageSizesProperty"/>
    public IList<int> PageSizes
    {
        get => GetValue<IList<int>>(PageSizesProperty);
        set => SetValue(PageSizesProperty, value);
    }

    /// <inheritdoc cref="CanGoBackProperty"/>
    public bool CanGoBack
    {
        get => GetValue<bool>(CanGoBackProperty);
        private set => SetValue(CanGoBackProperty, value);
    }

    /// <inheritdoc cref="CanGoForwardProperty"/>
    public bool CanGoForward
    {
        get => GetValue<bool>(CanGoForwardProperty);
        private set => SetValue(CanGoForwardProperty, value);
    }

    /// <inheritdoc cref="PageTextProperty"/>
    public string PageText
    {
        get => GetValue<string>(PageTextProperty);
        private set => SetValue(PageTextProperty, value);
    }

    /// <inheritdoc cref="PageNumberTextProperty"/>
    public string PageNumberText
    {
        get => GetValue<string>(PageNumberTextProperty);
        private set => SetValue(PageNumberTextProperty, value);
    }

    /// <inheritdoc cref="PageCountTextProperty"/>
    public string PageCountText
    {
        get => GetValue<string>(PageCountTextProperty);
        private set => SetValue(PageCountTextProperty, value);
    }

    /// <inheritdoc cref="PageItemsProperty"/>
    public IReadOnlyList<PagerPageItem> PageItems
    {
        get => GetValue<IReadOnlyList<PagerPageItem>>(PageItemsProperty);
        private set => SetValue(PageItemsProperty, value);
    }

    /// <inheritdoc cref="PageButtonCountProperty"/>
    public int PageButtonCount
    {
        get => GetValue<int>(PageButtonCountProperty);
        set => SetValue(PageButtonCountProperty, value);
    }

    /// <summary>Turns to the first page.</summary>
    public void MoveToFirstPage() => MoveToPage(0);

    /// <summary>Turns to the page before this one.</summary>
    public void MoveToPreviousPage() => MoveToPage(PageIndex - 1);

    /// <summary>Turns to the page after this one.</summary>
    public void MoveToNextPage() => MoveToPage(PageIndex + 1);

    /// <summary>Turns to the last page. Does nothing while the end is unknown - there is no last page to go to yet.</summary>
    public void MoveToLastPage()
    {
        if (!IsEndKnown || PageCount <= 0) return;
        MoveToPage(PageCount - 1);
    }

    /// <summary>Turns to <paramref name="pageIndex"/>, if the source will have it.</summary>
    public void MoveToPage(int pageIndex)
    {
        if (_paged == null || pageIndex < 0) return;
        _ = _paged.MoveToPageAsync(pageIndex);
    }

    /// <inheritdoc/>
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        DetachParts();

        _first = GetTemplateChild("PART_FirstPage") as Button;
        _previous = GetTemplateChild("PART_PreviousPage") as Button;
        _next = GetTemplateChild("PART_NextPage") as Button;
        _last = GetTemplateChild("PART_LastPage") as Button;
        _pageBox = GetTemplateChild("PART_PageBox") as NumericUpDown;

        if (_first != null) _first.Click += OnFirstClick;
        if (_previous != null) _previous.Click += OnPreviousClick;
        if (_next != null) _next.Click += OnNextClick;
        if (_last != null) _last.Click += OnLastClick;

        if (_pageBox != null)
        {
            _pageBox.ValueChanged += OnPageBoxValueChanged;
            RefreshPageBox();
        }

        _pageSizes = GetTemplateChild("PART_PageSizeSelector") as DropDown;
        if (_pageSizes != null)
        {
            // ITS ITEMS ARRIVE LATER THAN IT DOES. The picker is built with the template and filled by a binding that
            // resolves after, so a selection written here lands on an empty list and is dropped - which left the pager
            // starting with no page size showing at all, over a picker that had every step in it. Re-asserted whenever
            // the list changes, which is the moment the selection can actually take.
            _pageSizes.Items.CollectionChanged += OnPageSizeItemsChanged;
            _pageSizes.SelectionChanged += OnPageSizeSelected;
            SyncPageSizePicker();
        }
    }

    /// <inheritdoc/>
    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();
        DetachParts();
    }

    private void DetachParts()
    {
        if (_first != null) _first.Click -= OnFirstClick;
        if (_previous != null) _previous.Click -= OnPreviousClick;
        if (_next != null) _next.Click -= OnNextClick;
        if (_last != null) _last.Click -= OnLastClick;
        if (_pageBox != null) _pageBox.ValueChanged -= OnPageBoxValueChanged;

        if (_pageSizes != null)
        {
            _pageSizes.SelectionChanged -= OnPageSizeSelected;
            _pageSizes.Items.CollectionChanged -= OnPageSizeItemsChanged;
        }

        _first = _previous = _next = _last = null;
        _pageBox = null;
        _pageSizes = null;
    }

    private void OnPageSizeSelected(object sender, EventArgs e)
    {
        if (_pageSizes?.SelectedItem is int size && size > 0) SetCurrentValue(PageSizeProperty, size);
    }

    private void OnPageSizeItemsChanged(object sender, NotifyCollectionChangedEventArgs e) => SyncPageSizePicker();

    // One answer to "how big is a page", shown wherever it is shown. Idempotent on purpose: it is called from the
    // template, from the size changing and from the list of steps changing, and any of those can be the one that
    // finally makes the selection possible.
    private void SyncPageSizePicker()
    {
        if (_pageSizes == null || Equals(_pageSizes.SelectedItem, PageSize)) return;
        if (_pageSizes.Items.Contains(PageSize)) _pageSizes.SelectedItem = PageSize;
    }

    private void OnFirstClick(object sender, RoutedEventArgs e) => MoveToFirstPage();

    private void OnPreviousClick(object sender, RoutedEventArgs e) => MoveToPreviousPage();

    private void OnNextClick(object sender, RoutedEventArgs e) => MoveToNextPage();

    private void OnLastClick(object sender, RoutedEventArgs e) => MoveToLastPage();

    // A NUMBER BOX, not a text box: a page is an integer between one and the last, and the control that knows that is
    // the one that should be enforcing it. It refuses a decimal separator at the KEYSTROKE and clamps to its range, so
    // there is no "what if they typed a word" case left here to hand-write - which is what the previous version of this
    // method was, and it could only check after the fact.
    private void OnPageBoxValueChanged(object sender, NullableValueChangedEventArgs e)
    {
        if (_pageBox?.Value is { } typed) MoveToPage((int)typed - 1);
    }

    private void RefreshPageBox()
    {
        if (_pageBox == null) return;

        // The last page is the box's own ceiling, so a number past the end is refused where it is typed rather than
        // silently turned into something else. Unknown end = no ceiling: there IS no last page to name yet.
        _pageBox.Minimum = 1;
        _pageBox.Maximum = IsEndKnown && PageCount > 0 ? PageCount : Double.MaxValue;
        _pageBox.Value = PageIndex + 1;
    }

    private static void OnSourceChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is DataPager pager) pager.AttachSource(e.NewValue);
    }

    private static void OnPageSizeChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not DataPager pager) return;

        var size = (int)e.NewValue;
        if (pager._paged != null) pager._paged.PageSize = size;

        // The picker follows a size set from anywhere else - a binding, code, another pager over the same view - so the
        // control has ONE answer to "how big is a page" rather than one on screen and another in the source.
        pager.SyncPageSizePicker();
    }

    private static void OnPageIndexChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is DataPager pager) pager.MoveToPage((int)e.NewValue);
    }

    private static void OnPageButtonCountChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is DataPager pager) pager.RebuildPageItems();
    }

    private static void OnPageSizesChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not DataPager pager) return;

        // A progression that is EDITED rather than replaced is the ordinary case - an application that lets the reader
        // add a step hands the same collection over and adds to it - so the pager listens to the collection as well as
        // to the property. Without this the picker still grew (the items control is watching the same collection), but
        // the size could be left pointing at a step that had just been removed.
        if (e.OldValue is INotifyCollectionChanged wasObservable)
            wasObservable.CollectionChanged -= pager.OnPageSizesCollectionChanged;

        if (e.NewValue is INotifyCollectionChanged isObservable)
            isObservable.CollectionChanged += pager.OnPageSizesCollectionChanged;

        pager.SnapPageSizeToProgression();
    }

    private void OnPageSizesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) =>
        SnapPageSizeToProgression();

    // The progression is what the control OFFERS; the page size is which of those it is on. Let them disagree - replace
    // the list, or take the current step out of it - and the picker shows a number nobody can choose again. Nearest
    // rather than first, so a size of 40 among 25/50 lands on 50 instead of walking the reader back to the top.
    private void SnapPageSizeToProgression()
    {
        if (PageSizes is not { Count: > 0 } steps || steps.Contains(PageSize)) return;

        var nearest = steps[0];
        foreach (var step in steps)
            if (Math.Abs(step - PageSize) < Math.Abs(nearest - PageSize))
                nearest = step;

        SetCurrentValue(PageSizeProperty, nearest);
    }

    private static void OnDisplayModeChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not DataPager pager) return;

        // THE OLD ANSWER IS VOID, and saying so is the whole of this. Shedding hides a part by masking it out of the
        // mode, so turning the same part back ON changed nothing anyone could see: the mask still hid it, no Visibility
        // moved, nothing invalidated, and the part never came back however many times it was asked for. Clearing the
        // mask and asking for a fresh measure lets the width decide again, which is the only thing entitled to.
        pager._shed = PagerDisplayMode.None;
        pager.RefreshDisplayFlags();
        pager.InvalidateMeasure();
    }

    /// <summary>What a pager gives up first when it is not wide enough to show everything on one line, least
    /// navigational first. The pair it never gives up is step-back / step-forward and the page it is on: those work over
    /// any source, and a pager that cannot say where it is has stopped being one.</summary>
    private static readonly PagerDisplayMode[] ShedOrder =
    [
        // A preference, not navigation - and one that can wait until there is room for it.
        PagerDisplayMode.PageSizeSelector,
        // Two jumps that are reachable by typing a page number, which is still on the line.
        PagerDisplayMode.FirstLast,
        // The numbers say where you are AMONG NEIGHBOURS; "Page 5 of 800" says it outright and costs a fifth of the room.
        PagerDisplayMode.Numeric
    ];

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        // AN UNBOUNDED OR EMPTY OFFER IS A QUESTION, NOT A WIDTH. Infinity is a parent asking how big this would like to
        // be; zero is a star column that has not been resolved yet, and it arrives on the first pass of every layout
        // that puts a pager in one. Shedding against either produces an answer to a question nobody asked - and against
        // zero it produces the worst one, a pager with everything given away, for the frame before the real width lands.
        if (Double.IsInfinity(availableSize.Width) || Double.IsNaN(availableSize.Width) || availableSize.Width <= 0)
            return base.MeasureOverride(availableSize);

        // MEASURED AGAINST INFINITY, not against the width we have. Asked at the real width the answer always fits: the
        // Grid clamps its own desired size to what it was offered and the row overflows INSIDE it, so "too wide" never
        // shows up as a number bigger than the offer - which is why the first version of this shed nothing at all and
        // the narrow pager drew its numbers over the size picker. What the row WANTS is only visible with no limit.
        var unbounded = new Size(Double.PositiveInfinity, availableSize.Height);
        var wanted = base.MeasureOverride(unbounded).Width;
        RecordPartWidths();

        // DECIDED ON PAPER, then applied ONCE. The obvious version tries each arrangement by turning a part on or off
        // and measuring again - and every one of those writes changes a Visibility, which invalidates layout from inside
        // the layout pass. In a steady state it still wrote (turn everything on, measure, shed again), so the pager
        // never came to rest and the row flickered as it was pulled between two answers. Priced from what each part
        // measured while it was last visible, the arithmetic costs no writes at all, and a state that is already right
        // produces none.
        var target = _shed;

        foreach (var part in ShedOrder)
        {
            if (wanted <= availableSize.Width) break;
            if (!DisplayMode.HasFlag(part) || target.HasFlag(part)) continue;
            if (!_partWidths.TryGetValue(part, out var width)) continue;   // never seen it - cannot price it

            target |= part;
            wanted -= width;
        }

        // ...and put back whatever still fits. Dropping in order stops at the first arrangement that fits, which is not
        // the best one: the parts are not the same size, and the one that finally makes room is the row of numbers, five
        // buttons wide. Everything given up BEFORE it was given up for nothing - which is how the narrow pager ended up
        // showing two arrows in half a line of empty space.
        foreach (var part in ShedOrder)
        {
            if (!target.HasFlag(part) || !DisplayMode.HasFlag(part)) continue;
            if (!_partWidths.TryGetValue(part, out var width) || wanted + width > availableSize.Width) continue;

            target &= ~part;
            wanted += width;
        }

        if (target != _shed)
        {
            _shed = target;
            RefreshDisplayFlags();
        }

        // WHY a part is missing, in numbers, on demand (ADAM_PAGER_FIT=<file>). "It should have fitted" is the one
        // question this control raises and the one that cannot be answered by looking at it - the answer is a width
        // against a price. Read ONCE into a static: this sits in the measure pass, and an environment lookup per measure
        // would be a probe that costs something even when nobody asked for it.
        if (FitLog != null)
            System.IO.File.AppendAllText(FitLog,
                $"available={availableSize.Width} wantedAfter={wanted} shed={target} mode={DisplayMode} " +
                $"size={PageSize} steps=[{string.Join(",", PageSizes ?? [])}] " +
                $"prices=" +
                string.Join(" ", _partWidths.Select(p => $"{p.Key}:{p.Value}")) + "\n");

        return base.MeasureOverride(availableSize);
    }

    // What each droppable part costs on the line, remembered from when it was last visible. A hidden part measures zero,
    // so its price cannot be read at the moment the question is asked - and asking by TRYING is what made the row
    // flicker. The two arrows are priced together because they are given up together.
    private void RecordPartWidths()
    {
        Price(PagerDisplayMode.PageSizeSelector, WidthOf("PageSizeGroup"));
        Price(PagerDisplayMode.Numeric, WidthOf("NumbersGroup"));
        Price(PagerDisplayMode.FirstLast, WidthOf("PART_FirstPage") + WidthOf("PART_LastPage"));

        void Price(PagerDisplayMode part, double width)
        {
            if (width > 0) _partWidths[part] = width;
        }
    }

    private double WidthOf(string partName) =>
        GetTemplateChild(partName) is MeasurableUIComponent part ? part.DesiredSize.Width : 0;

    // The flags enum, spread into one boolean per part. Markup SETS the combined value fine ("FirstLast|Numeric" and
    // "FirstLast,Numeric" both read as the pair); what it cannot do is ASK whether one bit is set - a PropertyTrigger
    // matches a WHOLE value and the engine ships no converters. One property per part is what a trigger can read.
    private void RefreshDisplayFlags()
    {
        var mode = DisplayMode & ~_shed;
        ShowsFirstLast = mode.HasFlag(PagerDisplayMode.FirstLast);
        ShowsPreviousNext = mode.HasFlag(PagerDisplayMode.PreviousNext);
        ShowsPageSizeSelector = mode.HasFlag(PagerDisplayMode.PageSizeSelector);
        ShowsPageInput = mode.HasFlag(PagerDisplayMode.PageInput);

        // The numbers need a total as well as permission: over a source that cannot count there is nothing to number,
        // and a row that appeared and vanished as the end was discovered would be worse than one that waits for it.
        ShowsNumbers = mode.HasFlag(PagerDisplayMode.Numeric) && IsEndKnown;
    }

    private void RebuildPageItems()
    {
        if (_paged?.PageCount is not { } pages || pages <= 0)
        {
            PageItems = [];
            return;
        }

        var slots = Math.Max(3, PageButtonCount);
        var current = _paged.PageIndex;
        var items = new List<PagerPageItem>();

        if (pages <= slots)
        {
            for (var page = 0; page < pages; page++) items.Add(Numbered(page, current));
            PageItems = items;
            return;
        }

        var block = slots - 1;
        int from, to;
        bool leading, trailing;

        if (current < block)
        {
            (from, to, leading, trailing) = (0, block - 1, false, true);
        }
        else if (current >= pages - block)
        {
            (from, to, leading, trailing) = (pages - block, pages - 1, true, false);
        }
        else
        {
            var run = slots - 2;
            from = current - (run - 1) / 2;
            (to, leading, trailing) = (from + run - 1, true, true);
        }

        if (leading) items.Add(Gap(current - 1));
        for (var page = from; page <= to; page++) items.Add(Numbered(page, current));
        if (trailing) items.Add(Gap(current + 1));

        PageItems = items;
    }

    private PagerPageItem Numbered(int pageIndex, int current) =>
        new((pageIndex + 1).ToString(CultureInfo.CurrentCulture), pageIndex == current, true, new GoToPage(this, pageIndex));

    private PagerPageItem Gap(int target) => new("…", false, true, new GoToPage(this, target));

    private sealed class GoToPage(DataPager pager, int pageIndex) : ICommand
    {
        public event EventHandler CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object parameter = null) => true;

        public void Execute(object parameter = null) => pager.MoveToPage(pageIndex);

        public void RaiseCanExecuteChanged() { }
    }

    private void AttachSource(object source)
    {
        if (_paged != null) _paged.PropertyChanged -= OnPagedPropertyChanged;

        _paged = source switch
        {
            null => null,
            IPagedSource paged => paged,
            IEnumerable items => new CollectionView(items),
            _ => throw new ArgumentException(
                $"DataPager.Source must be a collection or an {nameof(IPagedSource)}, and was given a " +
                $"{source.GetType().Name}. Showing an empty pager instead would hide the mistake rather than report it.",
                nameof(source))
        };

        if (_paged != null)
        {
            _paged.PageSize = PageSize;
            _paged.PropertyChanged += OnPagedPropertyChanged;
        }

        PagedSource = _paged;
        Refresh();
    }

    private void OnPagedPropertyChanged(object sender, PropertyChangedEventArgs e) => Refresh();

    private void Refresh()
    {
        if (_paged == null)
        {
            PageCount = 0;
            TotalItemCount = 0;
            IsEndKnown = true;
            IsPageChanging = false;
            CanGoBack = false;
            CanGoForward = false;
            PageText = string.Empty;
            PageNumberText = string.Empty;
            PageCountText = string.Empty;
            PageItems = [];
            return;
        }

        var pages = _paged.PageCount;
        IsEndKnown = pages.HasValue;
        RefreshDisplayFlags();
        PageCount = pages ?? -1;
        TotalItemCount = _paged.TotalItemCount ?? -1;
        IsPageChanging = _paged.IsPageChanging;

        if (PageIndex != _paged.PageIndex) SetCurrentValue(PageIndexProperty, _paged.PageIndex);

        CanGoBack = _paged.CanChangePage && _paged.PageIndex > 0;
        CanGoForward = _paged.CanChangePage && (!pages.HasValue || _paged.PageIndex < pages.Value - 1);
        var countText = pages.HasValue ? pages.Value.ToString(CultureInfo.CurrentCulture) : "?";
        PageNumberText = (_paged.PageIndex + 1).ToString(CultureInfo.CurrentCulture);
        PageCountText = $"of {countText}";
        PageText = $"Page {PageNumberText} {PageCountText}";

        if (_pageBox is { IsFocused: false }) RefreshPageBox();

        RebuildPageItems();
    }
}
