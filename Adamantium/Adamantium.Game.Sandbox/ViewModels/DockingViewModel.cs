using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Adamantium.MVVM;
using Adamantium.Navigation;
using Adamantium.UI.Controls.Docking;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Docking tab: an editor-shaped arrangement built from ZONES alone - the markup says where each group goes,
/// never what share of what it takes, and the split tree is derived from that.
/// <para>The area is also a navigation REGION, so panes can be opened and reached by name instead of by hand: see
/// <see cref="Navigate"/>.</para></summary>
[ViewModel]
public partial class DockingViewModel : TabPageViewModel
{
    public const string RegionName = "docking";

    private readonly INavigationService _navigation;
    private int _created;

    public DockingViewModel(INavigationService navigation) : base("Docking")
    {
        _navigation = navigation;
        _navigation.Regions.GetOrCreateRegion(RegionName);

        Workspace = new DockingWorkspace();
        Workspace.Ready += OnWorkspaceReady;
    }

    /// <summary>What the application last answered when the docking area asked it (see
    /// <see cref="Behaviors.DockingPolicyBehavior"/>). Shown above the area, because a refusal that is not said out loud
    /// reads as a gesture that mysteriously did nothing.</summary>
    [Bindable] private string _lastAnswer =
        "Nothing asked yet. Try dropping a fourth tab into a panel, or pulling out a panel's only tab.";

    /// <summary>Every pane the region knows how to reach. New ones join it, so a tab created here can be navigated back
    /// to after it is closed - the name is the identity, not the instance.</summary>
    public ObservableCollection<string> Pages { get; } = ["Assets", "Terminal", "Profiler", "Notes"];

    [Bindable] private string _selectedPage = "Assets";

    /// <summary>Where a NEW pane is created. Centre means the document well, the middle four are the area's own edges,
    /// and Floating opens it in a window of its own - which is also how to get a pane that is only ever floating (see
    /// the Watch pane's Pane.Allowed in the view).</summary>
    public ObservableCollection<DockZone> Places { get; } =
        [DockZone.Center, DockZone.Left, DockZone.Right, DockZone.Top, DockZone.Bottom, DockZone.Floating];

    [Bindable] private DockZone _selectedPlace = DockZone.Center;

    private IRegion Region => _navigation.Regions[RegionName];

    /// <summary>Go to the selected name: open where the place says if it is not there, activate it where it is if it
    /// is. Both are the same call - which of the two happens is the region's business, not the caller's.</summary>
    [Command]
    private Task Navigate()
    {
        return Open(SelectedPage);
    }

    /// <summary>A pane that did not exist before, so there is always something new to place.</summary>
    [Command]
    private Task NewTab()
    {
        var name = $"Page {++_created}";
        Pages.Add(name);
        SelectedPage = name;
        return Open(name);
    }

    private Task Open(string page)
    {
        return Region.NavigateToAsync<DockPageViewModel>(new NavigationParameters()
            .Add(DockPageViewModel.PageKey, page)
            .Add(DockPageViewModel.ZoneKey, SelectedPlace));
    }

    // --- The arrangement, owned by the VIEW MODEL --------------------------------------------------------------------
    // The view points the area at this object (docking:Workspace.Source); everything else happens here. The control
    // never learns where a layout is kept - a file, a setting, a server - which is the application's business.

    /// <summary>The docking area's arrangement. The view binds the area to it; these commands drive it.</summary>
    public DockingWorkspace Workspace { get; }

    [Bindable] private string _layoutState = "";

    /// <summary>Where this application keeps its window layout - beside its other settings, per user. The CONTROL has
    /// no opinion about this: it hands out text and reads text back, and where that lives is the application's
    /// business (a file here, a settings store or a server elsewhere).</summary>
    private static string LayoutFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Adamantium", "Sandbox", "docking-layout.json");

    // Restoring happens the moment the view exists, not in the constructor: before that there is no area to restore
    // into, and a view model deliberately cannot see the view.
    private void OnWorkspaceReady(object sender, System.EventArgs e)
    {
        if (!File.Exists(LayoutFile))
        {
            LayoutState = $"No saved layout yet ({LayoutFile}). Rearrange the panels and press Save.";
            return;
        }

        LayoutState = Workspace.Load(File.ReadAllText(LayoutFile))
            ? $"Restored the layout saved in {LayoutFile}."
            : "The saved layout could not be read - starting from the arrangement in the markup.";
    }

    /// <summary>Writes where the panels are right now: the tree, the edge bars, which panel is put away, which tab is
    /// on top and every floating window's place on screen.</summary>
    [Command]
    private void SaveLayout()
    {
        var state = Workspace.Save();
        if (state == null)
        {
            LayoutState = "Nothing to save - the area is not on screen yet.";
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(LayoutFile)!);
        File.WriteAllText(LayoutFile, state);

        LayoutState = $"Saved to {LayoutFile}. Rearrange things and press Restore - or restart the app, it survives that.";
    }

    /// <summary>Puts the saved arrangement back, floating windows and all.</summary>
    [Command]
    private void RestoreLayout()
    {
        if (!File.Exists(LayoutFile))
        {
            LayoutState = "Nothing saved yet - press Save first.";
            return;
        }

        LayoutState = Workspace.Load(File.ReadAllText(LayoutFile))
            ? "Restored. Documents are NOT part of it: Pane.Restore says they belong to a session, not to the workspace."
            : "The saved text names nothing this area still has.";
    }

    /// <summary>What a real application's "reset window layout" does.</summary>
    [Command]
    private void ForgetLayout()
    {
        if (File.Exists(LayoutFile)) File.Delete(LayoutFile);

        LayoutState = "Forgotten. Next start gives you the arrangement written in the markup.";
    }
}
