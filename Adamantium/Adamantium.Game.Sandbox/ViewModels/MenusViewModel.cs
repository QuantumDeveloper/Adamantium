using System.Collections.ObjectModel;
using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Menus tab: a right-click ContextMenu whose whole tree - nested submenus and separators - comes from this
/// view-model (<see cref="MenuItems"/>), projected by a HierarchicalDataTemplate. Picking a leaf runs its command, which
/// writes to <see cref="LastAction"/> so the binding round-trip is visible.</summary>
[ViewModel]
public partial class MenusViewModel : TabPageViewModel
{
    [Bindable] private string _lastAction = "— nothing yet (right-click a surface and pick an item)";

    /// <summary>The menu tree, built in the VM. The view binds ContextMenu.ItemsSource to it.</summary>
    public ObservableCollection<MenuNode> MenuItems { get; }

    public MenusViewModel() : base("Menus")
    {
        // A deliberately long submenu (taller than the window) to show the flyout scrolls instead of clipping - and each
        // entry has its OWN submenu, so a submenu opens off a SCROLLED row (it anchors to the row's live position).
        var recent = new MenuNode { Title = "Recent files" };
        for (var i = 1; i <= 40; i++)
        {
            var name = $"project_{i:00}.auml";
            recent.Children.Add(MenuNode.Parent(name,
                MenuNode.Leaf("Open", new AdamantiumCommand(() => Pick($"Open {name}"))),
                MenuNode.Leaf("Reveal in Explorer", new AdamantiumCommand(() => Pick($"Reveal {name}"))),
                MenuNode.Divider(),
                MenuNode.Leaf("Remove from list", new AdamantiumCommand(() => Pick($"Remove {name}")))));
        }

        MenuItems =
        [
            MenuNode.Leaf("Cut", new AdamantiumCommand(() => Pick("Cut")), "Ctrl+X"),
            MenuNode.Leaf("Copy", new AdamantiumCommand(() => Pick("Copy")), "Ctrl+C"),
            MenuNode.Leaf("Paste", new AdamantiumCommand(() => Pick("Paste")), "Ctrl+V"),
            MenuNode.Divider(),
            MenuNode.Parent("Zoom",
                MenuNode.Leaf("Reset to 100%", new AdamantiumCommand(() => Pick("Zoom · Reset to 100%"))),
                MenuNode.Leaf("Fit to window", new AdamantiumCommand(() => Pick("Zoom · Fit to window"))),
                MenuNode.Divider(),
                MenuNode.Parent("Presets",
                    MenuNode.Leaf("50%", new AdamantiumCommand(() => Pick("Zoom · 50%"))),
                    MenuNode.Leaf("200%", new AdamantiumCommand(() => Pick("Zoom · 200%"))),
                    MenuNode.Leaf("400%", new AdamantiumCommand(() => Pick("Zoom · 400%"))))),
            MenuNode.Parent("Arrange",
                MenuNode.Leaf("Bring to front", new AdamantiumCommand(() => Pick("Arrange · Bring to front"))),
                MenuNode.Leaf("Send to back", new AdamantiumCommand(() => Pick("Arrange · Send to back")))),
            recent,
            MenuNode.Divider(),
            MenuNode.Leaf("Properties…", new AdamantiumCommand(() => Pick("Properties…"))),
        ];
    }

    private void Pick(string what) => LastAction = $"Picked: {what}";
}
