using System.Collections.ObjectModel;
using System.Linq;
using Adamantium.MVVM;

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
    }

    private static readonly string[] Palette =
        ["#3B82F6", "#22C55E", "#F59E0B", "#EF4444", "#8B5CF6", "#14B8A6", "#EC4899", "#EAB308"];

    public ObservableCollection<ColorRect> Rectangles { get; }

    // The selected tile - bound two-way to ListBox.SelectedItem so the selection is state ON the view-model and survives a
    // tab switch (the view is recreated, this view-model persists), same idea as the tree's node-side selection.
    [Bindable] private ColorRect _selectedRect;

    // Cell width/height are independent so the aspect ratio (width/height) is adjustable - a non-square cell shows a real
    // ellipse (rx != ry) / a stretched rounded rect. Defaults are non-square so the aspect is visible immediately.
    [Bindable] private double _cellWidth = 120;
    [Bindable] private double _cellHeight = 72;

    // False = rounded rectangles (RectBatch SDF), true = ellipses (EllipseBatch SDF). A DataTrigger in LayoutView.auml
    // swaps ItemTemplate between the two AUML templates (LayoutResources) off this flag.
    [Bindable] private bool _showEllipses;

    // Milliseconds one layout pass may spend (re)binding tile containers while scrolling; the rest is deferred to the
    // next pass and shows a skeleton. 0 = no budget, bind the whole window in one pass. Live on this tab because it is a
    // genuine trade with no universally right answer: measured here, 6 ms binds ~357 slots a pass and defers ~4487, so a
    // big window at the minimum cell never catches up - while a small window is better off binding everything at once.
    [Bindable] private double _bindBudgetMs = 6;
}
