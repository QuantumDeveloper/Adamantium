using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Adamantium.MVVM;
using Adamantium.Navigation;
using Adamantium.UI.Core.Input;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Content of the DEDICATED drag-drop window (its own shell). It carries a real ListBox, so items can be dragged
/// both ways between it and the main window's lists - a genuine cross-window list-to-list move. Same source-completion
/// model as the main tab: a drop target only ADDS; the source (whichever window the item came from) removes on Move.</summary>
[ViewModel]
public partial class DragDropWindowViewModel : IWindowAware
{
    public string WindowShellKey => "dragdrop";
    public string Title => "Drag & Drop Window";
    public double Width => 360;
    public double Height => 480;

    [Bindable] private ObservableCollection<string> _items = new(["Elephant", "Tiger", "Zebra"]);
    [Bindable] private ObservableCollection<string> _selection = new();

    private ObservableCollection<string> _dragOrigin;
    private List<string> _dragItems;

    [Command]
    private void DragStarted(object arg)
    {
        if (arg is not DragDropEventArgs e) return;
        var pressed = e.Data?.Get<string>();
        _dragOrigin = Items.Contains(pressed) ? Items : null;
        _dragItems = Selection.Contains(pressed) && Selection.Count > 1 ? Selection.ToList() : [pressed];
        e.Data?.Set(_dragItems);
    }

    [Command]
    private void Drop(object arg)
    {
        if (arg is not DragDropEventArgs e) return;
        foreach (var item in ItemsOf(e))
        {
            if (!Items.Contains(item)) Items.Add(item);
        }
    }

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

    private static IReadOnlyList<string> ItemsOf(DragDropEventArgs e) =>
        e.Data?.Get<List<string>>() ?? (e.Data?.Get<string>() is { } s ? [s] : []);
}
