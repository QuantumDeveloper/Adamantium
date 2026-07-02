using System.Collections.ObjectModel;
using System.Linq;
using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Layout tab: a large virtualized grid of rectangles inside a scrolling, wrapping panel. One slider drives
/// every tile's size (bound to the WrapPanel cell), and only the on-screen tiles are realized (virtualization) - so it
/// stays smooth at hundreds of items.</summary>
[ViewModel]
public partial class LayoutViewModel : TabPageViewModel
{
    public LayoutViewModel() : base("Layout") { }

    private static readonly string[] Palette =
        ["#3B82F6", "#22C55E", "#F59E0B", "#EF4444", "#8B5CF6", "#14B8A6", "#EC4899", "#EAB308"];

    public ObservableCollection<ColorRect> Rectangles { get; } =
        new(Enumerable.Range(0, 600).Select(i => new ColorRect { Color = Palette[i % Palette.Length] }));

    [Bindable] private double _rectSize = 64;
}
