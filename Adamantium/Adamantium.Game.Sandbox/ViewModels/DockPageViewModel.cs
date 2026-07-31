using Adamantium.MVVM;
using Adamantium.Navigation;
using Adamantium.UI.Controls.Docking;
using Adamantium.UI.Controls.Navigation;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>A pane opened into the docking area BY NAVIGATION rather than by the markup. Its name is its identity: asking
/// for a name that is already open reuses this instance (<see cref="IsNavigationTarget"/>), which is what makes the
/// region activate the existing tab instead of opening a second one just like it.</summary>
[ViewModel]
public partial class DockPageViewModel : INavigationAware, IDockablePane, IRestorablePane
{
    public const string PageKey = "page";
    public const string ZoneKey = "zone";

    [Bindable] private string _title = "Page";
    [Bindable] private string _openedAt = "";

    public string PaneId => Title;
    public string PaneTitle => Title;
    public DockZone PaneZone { get; private set; } = DockZone.Center;

    /// <summary>Opened floating means float-ONLY here, so the demo shows both halves of the idea: where a pane opens
    /// (<see cref="PaneZone"/>) and where it may ever be (this). Docked ones stay dockable anywhere.</summary>
    public DockZone PaneAllowed => PaneZone == DockZone.Floating ? DockZone.Floating : DockZone.All;

    public void OnNavigatedTo(NavigationContext context)
    {
        Title = context.Parameters.GetValue(PageKey, Title);

        // Only where it is first opened: the zone the pane already lives in belongs to the user by then, and the adapter
        // reads this when it CREATES the pane.
        PaneZone = context.Parameters.GetValue(ZoneKey, DockZone.Center);
        OpenedAt = PaneZone switch
        {
            DockZone.Center => "the document well",
            DockZone.Floating => "a window of its own - and float-only: it cannot be docked back",
            _ => PaneZone.ToString().ToLowerInvariant()
        };
    }

    public void OnNavigatedFrom(NavigationContext context)
    {
    }

    /// <summary>Back from a saved layout: the name IS the identity here, so the pane's id is the whole of what this
    /// page has to remember. Where it lands is the layout's business, not its own.</summary>
    public void RestoreFrom(string paneId)
    {
        Title = paneId;
        OpenedAt = "restored with the layout";
    }

    public bool IsNavigationTarget(NavigationContext context)
    {
        return Title == context.Parameters.GetValue<string>(PageKey);
    }
}
