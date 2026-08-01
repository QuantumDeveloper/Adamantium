using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Adamantium.MVVM;
using Adamantium.Navigation;
using Adamantium.UI.Controls.Docking;
using Adamantium.UI.Controls;

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
    private readonly IDialogService _dialogs;
    private int _created;

    public DockingViewModel(INavigationService navigation, IDialogService dialogs) : base("Docking")
    {
        _navigation = navigation;
        _dialogs = dialogs;
        _navigation.Regions.GetOrCreateRegion(RegionName);

        Workspace = new DockingWorkspace();
        Workspace.Ready += OnWorkspaceReady;

        // The area ASKS before it closes anything; this is the application ANSWERING, out of its own state. Not a
        // behaviour reading a flag off the control: whether a document has unsaved work is a fact this view model owns,
        // and the control has no business holding it.
        Workspace.PaneClosing += OnPaneClosing;
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


    // Which documents have unsaved work - the application's own state, and the only thing a refusal is decided from.
    private readonly System.Collections.Generic.HashSet<string> _unsaved = [];

    /// <summary>The area's question, answered. Both kinds of answer live here now: the flat refusal, which needs no
    /// conversation, and the one that ASKS THE USER - possible only because the area waits for this Task.
    /// <para>Every close comes here: the tab's own button, the caption's, and each pane of a "close all". The dialog is
    /// no longer something only a menu could arrange.</para></summary>
    private async Task OnPaneClosing(object sender, PaneClosingEventArgs e)
    {
        if (_unsaved.Contains(e.PaneId))
        {
            e.Cancel = true;
            LastAnswer = $"REFUSED: '{e.PaneId}' has unsaved changes. Untick the box in it and try again.";
            return;
        }

        if (!_asks.Contains(e.PaneId)) return;

        var result = await _dialogs.ShowDialogAsync<ConfirmDialogViewModel>(new NavigationParameters()
            .Add("title", "Close document")
            .Add("message", $"Close '{e.PaneId}'?"));

        // Cancel STOPS THE WHOLE operation, not just this pane: after "no" to the first of five, being asked about the
        // other four is badgering, and that is what every editor's save-before-closing dialog means by Cancel.
        if (result.Result == DialogButtonResult.Ok)
        {
            LastAnswer = $"User said close '{e.PaneId}'.";
            return;
        }

        e.CancelAll = true;
        LastAnswer = $"User kept '{e.PaneId}' open - and stopped the rest of the operation.";
    }

    /// <summary>The "Unsaved changes" box inside a document. A command rather than a bound property because the demo's
    /// documents are addressed by ID - including the ones opened by code, which no property could have been written
    /// for in advance.</summary>
    [Command]
    private void ToggleUnsaved(object paneId)
    {
        var id = paneId?.ToString();
        if (id == null) return;

        var unsaved = !_unsaved.Remove(id) && _unsaved.Add(id);
        LastAnswer = unsaved
            ? $"'{id}' now has unsaved changes - closing it will be refused outright."
            : $"'{id}' is saved again - it will close normally.";
    }

    // --- Asking the USER, which is where the synchronous refusal above runs out ---------------------------------------
    // A flat "no" needs no conversation, so PaneClosing can answer it on the spot. "Do you want to close it anyway?"
    // cannot: the answer arrives later, and an event that must return Cancel immediately has nowhere to wait.
    // So the DIALOG path runs before the close is ever asked for: whoever wants to close (here the tab menu) asks the
    // application first, awaits the answer, and only then calls the area. The area stays synchronous and knows nothing
    // about dialogs.

    private readonly System.Collections.Generic.HashSet<string> _asks = [];

    /// <summary>Pinned tabs in a row of their own, or sharing the one row. Set on the AREA, which pushes it onto every
    /// panel - the same way it hands down the indicator's side and thickness.</summary>
    [Command]
    private void TogglePinnedRow()
    {
        // Unset means "whatever the theme says", which is a row of their own - so the first click has to take it the
        // other way. Reading it back as null and treating that as "not separate" flipped it to the mode it was already
        // in, and the first click looked like nothing happened at all.
        var current = Workspace.PinnedTabsPlacement ?? PinnedTabsPlacement.SeparateRow;
        var separate = current != PinnedTabsPlacement.SeparateRow;

        Workspace.PinnedTabsPlacement = separate ? PinnedTabsPlacement.SeparateRow : PinnedTabsPlacement.SameRow;

        LastAnswer = separate
            ? "Pinned tabs get a row of their own."
            : "Pinned tabs share the row with the rest.";
    }

    [Command]
    private void ToggleAsk(object paneId)
    {
        var id = paneId?.ToString();
        if (id == null) return;

        var asks = !_asks.Remove(id) && _asks.Add(id);
        LastAnswer = asks
            ? $"Closing '{id}' will now ASK first, in a dialog."
            : $"'{id}' closes without asking.";
    }
}
