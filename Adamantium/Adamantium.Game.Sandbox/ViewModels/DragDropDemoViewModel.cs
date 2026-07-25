using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Adamantium.MVVM;
using Adamantium.Navigation;
using Adamantium.UI.Core.Input;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>In-window drag-drop demo: drag items between two ListBoxes (Extended multi-select). The engine bakes the
/// pressed item into a cursor-following ghost (a real layered OS window) and delivers the payload to the drop target's
/// command; the whole selection travels. Move removes from the source, Copy (Ctrl) keeps it. No UI types in the VM.</summary>
[ViewModel]
public partial class DragDropDemoViewModel : TabPageViewModel
{
    [Bindable] private ObservableCollection<string> _left = new(["Apple", "Banana", "Cherry", "Date"]);
    [Bindable] private ObservableCollection<string> _right = new(["Dog", "Cat"]);

    // Two-way bound to each ListBox.SelectedItems, so a drag can pick up the whole selection.
    [Bindable] private ObservableCollection<string> _leftSelection = new();
    [Bindable] private ObservableCollection<string> _rightSelection = new();

    private readonly INavigationService _navigation;

    public DragDropDemoViewModel(INavigationService navigation) : base("Drag & Drop")
    {
        _navigation = navigation;
    }

    // Opens the DEDICATED drag-drop window (its own ListBox) - drag items between it and these lists, both ways.
    [Command]
    private Task OpenDropWindow() =>
        _navigation.OpenWindowAsync<DragDropWindowViewModel>(windowShell: "dragdrop", singleInstance: true);

    // The list the current drag started from + the exact items being dragged - recorded on DragStarted, BEFORE any target
    // touches the lists, so DragCompleted removes the right items from the right place.
    private ObservableCollection<string> _dragOrigin;
    private List<string> _dragItems;

    [Command]
    private void DragStarted(object arg)
    {
        if (arg is not DragDropEventArgs e) return;
        var pressed = e.Data?.Get<string>();
        _dragOrigin = Left.Contains(pressed) ? Left : Right.Contains(pressed) ? Right : null;
        var selection = ReferenceEquals(_dragOrigin, Left) ? LeftSelection : ReferenceEquals(_dragOrigin, Right) ? RightSelection : null;

        // Drag the whole selection when the pressed item is part of it; otherwise just the pressed item.
        _dragItems = selection != null && selection.Contains(pressed) && selection.Count > 1
            ? selection.ToList()
            : [pressed];
        e.Data?.Set(_dragItems);   // carried in the payload so a cross-window target can add them all
    }

    // A drop target only ADDS - it never removes from the source (which may live in another window / view-model).
    [Command]
    private void DropLeft(object arg) => Add(arg, Left);

    [Command]
    private void DropRight(object arg) => Add(arg, Right);

    private static void Add(object arg, ObservableCollection<string> target)
    {
        if (arg is not DragDropEventArgs e) return;
        foreach (var item in ItemsOf(e))
        {
            if (!target.Contains(item)) target.Add(item);
        }
    }

    // The SOURCE completes the gesture: on a Move it removes the dragged items from their origin (Copy leaves them). This
    // is who deletes from the old collection - and it works even when the drop landed in another window's view-model.
    [Command]
    private void DragCompleted(object arg)
    {
        if (arg is DragDropEventArgs { Effects: DragDropEffects.Move } && _dragOrigin != null && _dragItems != null)
        {
            foreach (var item in _dragItems) _dragOrigin.Remove(item);
        }
        _dragOrigin = null;
        _dragItems = null;
    }

    // The dragged items from the payload: the packaged selection if present, else the single pressed item.
    internal static IReadOnlyList<string> ItemsOf(DragDropEventArgs e) =>
        e.Data?.Get<List<string>>() ?? (e.Data?.Get<string>() is { } s ? [s] : []);
}
