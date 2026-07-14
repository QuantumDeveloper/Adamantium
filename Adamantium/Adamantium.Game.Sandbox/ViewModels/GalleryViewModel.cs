using System.Collections.ObjectModel;
using Adamantium.MVVM;
using Adamantium.UI.Controls;

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
        new ResourcesViewModel(),
        new ScrollBarViewModel(),
        new ListsViewModel(),
        new ShapesViewModel(),
        new LoadersViewModel(),
        new BrushesViewModel(),
        new TextViewModel(),
        new SlidePanelViewModel(),
        new ImageViewModel(),
        new LayoutViewModel(),
        new TilesViewModel(),
        new InstancingViewModel(),
        new GameViewModel(),
    };

    [Bindable] private TabPageViewModel _selectedTab;

    // Drives the TabControl.TabStripPlacement from a DropDown (enum-bound) so the strip can move to any edge live.
    [Bindable] private TabStripPlacement _tabPlacement = TabStripPlacement.Top;

    // Drives the TabControl.ContentTransition from a DropDown (enum-bound) so the tab-content slide mode switches live.
    [Bindable] private ContentTransition _slideMode = ContentTransition.SlideLeft;

    public GalleryViewModel()
    {
        SelectedTab = Tabs[0];
    }
}
