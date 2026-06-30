using System.Collections.ObjectModel;
using System.Linq;
using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>View-model for ListBoxDemoView: one collection shown by two ListBoxes (a vertical stack and a wrap panel)
/// to exercise selection + hover/press visuals across both item layouts. <see cref="SelectedFruits"/> is bound two-way to
/// the stack list's SelectedItems - the control keeps this collection in sync, and the "Selected" list shows it live.</summary>
[ViewModel]
public partial class ListBoxDemoViewModel
{
    public ObservableCollection<string> Fruits { get; } =
        new(Enumerable.Range(1, 3000).Select(i => $"Item {i}"));

    // The control mutates this same instance as the user (multi-)selects; we just observe it. No attached behaviour.
    public ObservableCollection<string> SelectedFruits { get; } = new();
}
