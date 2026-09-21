using Adamantium.UI.Controls.Docking;

namespace Adamantium.UI.Controls.Navigation;

/// <summary>What a view model says about the pane a docking region opens for it. A view model that does not implement it
/// still navigates - it lands in the document well under its type name - but then the type name is the whole of its
/// identity, so a second view model of the same type would activate the first one's pane instead of opening its own.
/// <para>Read only when the pane is CREATED: navigating to something already open activates it where the user last put
/// it, rather than dragging it back to where it was first opened.</para></summary>
public interface IDockablePane
{
    /// <summary>Identity in the layout. Two view models standing for different things must not share one.</summary>
    string PaneId { get; }

    /// <summary>The tab's label.</summary>
    string PaneTitle { get; }

    /// <summary>Where a new pane goes; <see cref="DockZone.Center"/> is the document well.</summary>
    DockZone PaneZone { get; }

    /// <summary>Where the pane may be at all - <see cref="Pane.Allowed"/> for a view model. Distinct from
    /// <see cref="PaneZone"/>, which only says where it OPENS: a pane opened floating may still be dockable, and
    /// <see cref="DockZone.Floating"/> alone is the float-only pane (Telerik's FloatingOnly) that can never come back
    /// into the layout.</summary>
    DockZone PaneAllowed { get; }
}
