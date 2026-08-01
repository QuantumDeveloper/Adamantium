using System.Linq;
using Adamantium.MVVM;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Docking;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core.Behaviors;

namespace Adamantium.Game.Sandbox.Behaviors;

/// <summary>
/// The tab context menu, built by the APPLICATION. In a real editor it also holds "Save", source control and "reveal in
/// explorer" - things a docking control knows nothing about - which is why the menu is assembled out here and not
/// shipped inside the control.
/// <para>It works on the PANE and the AREA directly, not through the workspace: this is view-side code standing next to
/// the control, so it has both in hand. The workspace is for what a VIEW MODEL needs - the arrangement and the questions
/// it must answer - and routing a menu click through it would only be indirection.</para>
/// <para>Every item calls the docking area's own API, so a menu close is the same close as the tab's own button:
/// <see cref="DockingArea.ClosePane"/> and friends, all of which pass through the area's closing policy.</para>
/// </summary>
public class TabContextMenuBehavior : Behavior<DockingArea>
{
    private DockingArea _area;

    protected override void OnAttached(DockingArea area)
    {
        _area = area;
        area.ActivePaneChanged += OnActivePaneChanged;
    }

    protected override void OnDetached(DockingArea area)
    {
        area.ActivePaneChanged -= OnActivePaneChanged;
        _area = null;
    }

    // Panes arrive over time - the markup's at build, the region's whenever something navigates - so the menu is given
    // to whoever hasn't got one yet, each time the active pane changes. A pane opened by code becomes active as it
    // opens, which is exactly when it gets its menu.
    private void OnActivePaneChanged(object sender, System.EventArgs e)
    {
        if (_area == null) return;

        foreach (var pane in _area.Panes.ToList())
        {
            if (pane.Kind != PaneKind.Document || pane.ContextMenu != null) continue;

            pane.ContextMenu = MenuFor(pane);
        }
    }

    private ContextMenu MenuFor(Pane pane)
    {
        var menu = new ContextMenu();

        menu.Items.Add(Item("Close", () => _ = _area.ClosePaneAsync(pane.Id)));
        menu.Items.Add(Item("Close other tabs", () => _ = _area.CloseOtherPanesAsync(pane.Id)));
        menu.Items.Add(Item("Close all tabs in this panel", () => _ = _area.ClosePanesOfGroupAsync(pane.Id)));
        menu.Items.Add(Item("Close all but pinned", () => _ = _area.CloseUnpinnedPanesAsync(pane.Id)));
        menu.Items.Add(Item("Close all tabs (everywhere)", () => _ = _area.CloseAllPanesAsync()));
        menu.Items.Add(Item("Pin / unpin this tab", () => pane.IsPinned = !pane.IsPinned));

        return menu;
    }

    private static MenuItem Item(string header, System.Action execute) =>
        new() { Header = header, Command = new AdamantiumCommand(execute) };
}
