using System.Collections.ObjectModel;
using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Root view-model of the control gallery: one child view-model per tab, exposed as a single collection the
/// TabControl binds to (<c>ItemsSource="{Binding Tabs}"</c>). Each tab renders its header via the TabControl's
/// ItemTemplate ({Binding Header}) and its body via a <see cref="TabViewSelector"/> that maps the tab view-model to its
/// View - so the whole gallery is data-driven, exactly like a real application shell.</summary>
[ViewModel]
public partial class GalleryViewModel
{
    public ObservableCollection<TabPageViewModel> Tabs { get; } = new()
    {
        new ButtonsViewModel(),
        new RangesViewModel(),
        new ScrollBarViewModel(),
        new ListsViewModel(),
        new ShapesViewModel(),
        new TextViewModel(),
        new ImageViewModel(),
        new LayoutViewModel(),
        new GameViewModel(),
    };

    [Bindable] private TabPageViewModel _selectedTab;
}
