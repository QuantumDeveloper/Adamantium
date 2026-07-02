using System.Collections.ObjectModel;
using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Lists tab: one collection shown by a single-select and a multiple-select ListBox. The selection is bound
/// two-way, and Add/Remove commands mutate the collection live - so every list view stays in sync through the
/// view-model.</summary>
[ViewModel]
public partial class ListsViewModel : TabPageViewModel
{
    public ListsViewModel() : base("Lists") { }

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
}
