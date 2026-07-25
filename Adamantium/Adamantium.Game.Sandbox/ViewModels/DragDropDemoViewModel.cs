using System.Collections.ObjectModel;
using Adamantium.MVVM;
using Adamantium.UI.Core.Input;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>In-window drag-drop demo: drag items between two panels. The engine bakes the pressed item into a ghost that
/// follows the cursor (a real layered OS window) and, on release, invokes the drop panel's DropCommand with the payload -
/// this VM just moves the string between collections; the lists rebind. No UI types in the VM.</summary>
[ViewModel]
public partial class DragDropDemoViewModel : TabPageViewModel
{
    [Bindable] private ObservableCollection<string> _left = new(["Apple", "Banana", "Cherry", "Date"]);
    [Bindable] private ObservableCollection<string> _right = new(["Dog", "Cat"]);

    public DragDropDemoViewModel() : base("Drag & Drop")
    {
    }

    [Command]
    private void DropLeft(object arg) => Move(arg, Left);

    [Command]
    private void DropRight(object arg) => Move(arg, Right);

    private void Move(object arg, ObservableCollection<string> target)
    {
        if (arg is not DragDropEventArgs e) return;
        var item = e.Data?.Get<string>();
        if (item == null) return;

        // The item lives in exactly one list; drop it into the target (no-op if it's already there).
        Left.Remove(item);
        Right.Remove(item);
        if (!target.Contains(item)) target.Add(item);
    }
}
