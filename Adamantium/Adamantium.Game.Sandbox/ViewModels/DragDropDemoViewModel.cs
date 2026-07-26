using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Adamantium.MVVM;
using Adamantium.Navigation;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core.Input;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>In-window drag-drop demo: drag items between two ListBoxes (Extended multi-select). The engine bakes the
/// pressed item into a cursor-following ghost (a real layered OS window) and delivers the payload to the drop target's
/// command; the whole selection travels. Move removes from the source, Copy (Ctrl) keeps it. No UI types in the VM.</summary>
[ViewModel]
public partial class DragDropDemoViewModel : TabPageViewModel
{
    // A long list so it scrolls/virtualizes - to verify the insertion index stays correct on a virtualized list.
    [Bindable] private ObservableCollection<string> _left = new(Enumerable.Range(1, 24).Select(i => $"Item {i:00}"));
    [Bindable] private ObservableCollection<string> _right = new(["Dog", "Cat"]);
    // A WrapPanel-hosted list: items flow left-to-right and wrap, so the insertion caret must run VERTICALLY (between
    // side-by-side chips) instead of horizontally - the orientation is derived from the host panel by the drag engine.
    [Bindable] private ObservableCollection<string> _wrap = new([
        "Red", "Green", "Blue", "Amber", "Violet", "Teal", "Coral", "Cyan", "Lime", "Pink",
        "Gold", "Indigo", "Olive", "Peach", "Mint", "Rose", "Sky", "Rust", "Plum", "Sand",
        "Jade", "Ruby", "Slate", "Ivory"]);

    // Two-way bound to each ListBox.SelectedItems, so a drag can pick up the whole selection.
    [Bindable] private ObservableCollection<string> _leftSelection = new();
    [Bindable] private ObservableCollection<string> _rightSelection = new();
    [Bindable] private ObservableCollection<string> _wrapSelection = new();

    // Flips the "Colors" list's WrapPanel flow at runtime (bound two-way to a DropDown + into WrapPanel.Orientation), so the
    // insertion caret can be checked live in both flows: Horizontal flow -> vertical caret, Vertical flow -> horizontal caret.
    public Orientation[] Orientations { get; } = [Orientation.Horizontal, Orientation.Vertical];
    [Bindable] private Orientation _wrapOrientation = Orientation.Horizontal;

    [Bindable] private ScrollBarVisibility _wrapHScroll = ScrollBarVisibility.Disabled;
    [Bindable] private ScrollBarVisibility _wrapVScroll = ScrollBarVisibility.Auto;

    partial void OnWrapOrientationChanged(Orientation value)
    {
        var vertical = value == Orientation.Vertical;
        WrapHScroll = vertical ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        WrapVScroll = vertical ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
    }

    // A small in-memory tree to watch drag-drop on a TreeView: ghost, insertion caret between rows, and spring-load
    // (dwell over a folder -> it auto-expands). Nodes are draggable out; dropped items land as new root nodes (for now).
    [Bindable] private ObservableCollection<DragTreeNode> _tree = new([
        new DragTreeNode("Folder A", new DragTreeNode("A-1"), new DragTreeNode("A-2")),
        new DragTreeNode("Folder B",
            new DragTreeNode("B-1"),
            new DragTreeNode("Sub", new DragTreeNode("Deep-1"), new DragTreeNode("Deep-2"))),
        new DragTreeNode("Loose item"),
    ]);

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

    // Each drop list paired with its SelectedItems, so origin/selection resolve uniformly across all of them.
    private (ObservableCollection<string> List, ObservableCollection<string> Selection)[] Lists =>
        [(Left, LeftSelection), (Right, RightSelection), (Wrap, WrapSelection)];

    [Command]
    private void DragStarted(object arg)
    {
        if (arg is not DragDropEventArgs e) return;
        var pressed = e.Data?.Get<string>();
        var match = Lists.FirstOrDefault(l => l.List.Contains(pressed));
        _dragOrigin = match.List;
        var selection = match.Selection;

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

    [Command]
    private void DropWrap(object arg) => Add(arg, Wrap);

    // Tree drop (observation stage): drop lands as a new ROOT node. Precise position/into-node targeting comes with the
    // hybrid tree-drop (the engine can't yet hand the VM the hovered node - the caret's InsertBefore is an internal row).
    [Command]
    private void DropTree(object arg)
    {
        if (arg is not DragDropEventArgs e) return;
        foreach (var item in ItemsOf(e)) Tree.Add(new DragTreeNode(item));
    }

    private static void Add(object arg, ObservableCollection<string> target)
    {
        if (arg is not DragDropEventArgs e) return;
        // Insert BEFORE the reference item (survives the source removal shifting indices in the same list); append if none.
        var at = e.InsertBefore is string before && target.IndexOf(before) is var idx and >= 0 ? idx : target.Count;
        foreach (var item in ItemsOf(e))
        {
            if (target.Contains(item)) continue;
            target.Insert(at > target.Count ? target.Count : at, item);
            at++;
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
