using System.Collections.ObjectModel;
using System.Linq;
using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>View-model for ItemsDemoView: a collection bound to an ItemsControl.ItemsSource. 50 rows so the
/// virtualizing items panel is exercised (only the visible window is realized).</summary>
[ViewModel]
public partial class ItemsDemoViewModel
{
    public ObservableCollection<DemoItem> People { get; } =
        new(Enumerable.Range(1, 500).Select(i => new DemoItem { Name = $"Item {i}" }));
}
