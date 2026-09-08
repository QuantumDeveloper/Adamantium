using System.Collections.ObjectModel;
using System.Linq;
using Adamantium.MVVM;
using Adamantium.UI.Core.Collections;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Layout tab: a large virtualized grid of tiles inside a scrolling, wrapping panel. Two sliders drive the cell
/// WIDTH and HEIGHT independently (bound to the WrapPanel cell), so the tile aspect ratio is adjustable - and a toggle
/// swaps the item DataTemplate between rounded RECTANGLES and ELLIPSES. Both are drawn by their SDF batch (rounded-rect /
/// ellipse), so this is the live visual test that the ellipse SDF renders a real ellipse (rx != ry) crisply at any aspect
/// and resolution. Only the on-screen tiles are realized (virtualization), so it stays smooth at hundreds of items.</summary>
[ViewModel]
public partial class LayoutViewModel : TabPageViewModel
{
    public LayoutViewModel() : base("Layout")
    {
        Rectangles = new(Enumerable.Range(0, 60000)
            .Select(i => new ColorRect { Color = Palette[i % Palette.Length] }));

        // No page size here: the PAGER owns it. It pushes its own into whatever source it is given, so a size stated in
        // both places is a size stated twice, and the one written here would simply be overwritten on attach.
        Paged = new CollectionView(Rectangles);
        TileSource = Rectangles;
    }

    // ── VIRTUALIZED, OR PAGED - the same 60 000 tiles either way ───────────────────────────────────────────────────
    //
    // Two answers to the same problem, side by side on the data that makes the problem real. Virtualization keeps the
    // whole collection and realizes a window of it; paging hands the list a SHORTER COLLECTION and lets it realize the
    // lot. This tab exists to measure the first, so the first is what it opens on - the switch is here so the second
    // can be measured against it on identical data rather than on a stand of its own with different rows.

    /// <summary>The paged view of the same tiles. Five hundred to a page: enough that a page is still a screenful of
    /// work, few enough that the difference from 60 000 is the point.</summary>
    public CollectionView Paged { get; }

    /// <summary>The page sizes THIS tab offers, and they are nothing like a list's. Ten tiles is not a page, it is a
    /// row; the interesting range here starts where a page is a real amount of layout and ends where it is most of the
    /// sixty thousand - which is the comparison the tab is for. The control's own 10/25/50/100 would put every choice
    /// below the point at which either mechanism is under any strain.</summary>
    public int[] PageSteps { get; } = [100, 250, 500, 1000, 2500, 5000];

    /// <summary>What the grid is actually showing - the whole collection, or one page of it.</summary>
    [Bindable] private object _tileSource;

    /// <summary>Off: the grid holds all 60 000 and virtualizes. On: it holds one page and does not have to.</summary>
    [Bindable] private bool _isPaged;

    partial void OnIsPagedChanged(bool value) => TileSource = value ? Paged : (object)Rectangles;

    private static readonly string[] Palette =
        ["#3B82F6", "#22C55E", "#F59E0B", "#EF4444", "#8B5CF6", "#14B8A6", "#EC4899", "#EAB308"];

    public ObservableCollection<ColorRect> Rectangles { get; }

    // The selected tile - bound two-way to ListBox.SelectedItem so the selection is state ON the view-model and survives a
    // tab switch (the view is recreated, this view-model persists), same idea as the tree's node-side selection.
    [Bindable] private ColorRect _selectedRect;

    /// <summary>The smallest cell the sliders offer, and the size the tab OPENS at. The smallest cell is the heaviest
    /// one - it is what fills a window with the most tiles - so it is the configuration a measurement wants, and having
    /// to drag two sliders to reach it makes every reading start from somewhere else. The sliders take their Minimum
    /// from here rather than restating it: the same number twice, in markup and in code, is how the two drift.</summary>
    public double MinCell => 24;

    /// <summary>The largest cell the sliders offer.</summary>
    public double MaxCell => 240;

    // Cell width/height are independent so the aspect ratio (width/height) is adjustable - a non-square cell shows a real
    // ellipse (rx != ry) / a stretched rounded rect.
    [Bindable] private double _cellWidth = 24;
    [Bindable] private double _cellHeight = 24;

    // False = rounded rectangles (RectBatch SDF), true = ellipses (EllipseBatch SDF). A DataTrigger in LayoutView.auml
    // swaps ItemTemplate between the two AUML templates (LayoutResources) off this flag.
    [Bindable] private bool _showEllipses;

    // Milliseconds one layout pass may spend (re)binding tile containers while scrolling; the rest is deferred to the
    // next pass and shows a skeleton. 0 = no budget, bind the whole window in one pass. Live on this tab because it is a
    // genuine trade with no universally right answer: measured here, 6 ms binds ~357 slots a pass and defers ~4487, so a
    // big window at the minimum cell never catches up - while a small window is better off binding everything at once.
    [Bindable] private double _bindBudgetMs = 6;
}
