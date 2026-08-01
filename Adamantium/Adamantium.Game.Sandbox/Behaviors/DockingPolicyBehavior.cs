using Adamantium.UI.Controls.Docking;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Behaviors;

namespace Adamantium.Game.Sandbox.Behaviors;

/// <summary>
/// The APPLICATION saying no. A docking area asks before it moves anything (<see cref="DockingArea.PaneDocking"/>) and
/// before anything leaves for a window of its own (<see cref="DockingArea.PaneTearingOff"/>); this answers those two
/// questions and writes down what it answered, so the demo can show it.
/// <para>Deliberately a behaviour rather than code inside the view: this is exactly how an application is meant to plug
/// its own rules in - without reaching inside the control or subclassing it.</para>
/// <para><see cref="Pane.Allowed"/> already covers "where may this pane go at all", in data that serialises - including
/// the one restriction docking libraries actually ship (Telerik's FloatingOnly): a pane allowed only
/// <see cref="DockZone.Floating"/> leaves for a window of its own and cannot be docked back.</para>
/// <para>It answers YES to everything and only says so out loud. Two rules lived here before and both were bad examples:
/// a cap on tabs per group (no docking control anywhere limits that - overflow is a scrolling strip, not a refusal), and
/// "the last tab of a panel may not be pulled out", which refused the tab while dragging the same panel by its CAPTION
/// did the very same thing - one move, allowed or refused depending on where it was grabbed.</para>
/// </summary>
public class DockingPolicyBehavior : Behavior<DockingArea>
{
    private DockingArea _area;

    protected override void OnAttached(DockingArea area)
    {
        _area = area;
        area.PaneDocking += OnDocking;
        area.PaneTearingOff += OnTearingOff;
    }

    protected override void OnDetached(DockingArea area)
    {
        area.PaneDocking -= OnDocking;
        area.PaneTearingOff -= OnTearingOff;
        _area = null;
    }

    /// <summary>Says what was answered, out loud. Through the VIEW MODEL rather than a property on this behaviour: a
    /// behaviour is not an element of the visual tree, so nothing in the markup can bind to it by name - the view model
    /// is what both the view and this share.</summary>
    private void Answer(string text)
    {
        if (_area?.DataContext is ViewModels.DockingViewModel viewModel)
        {
            viewModel.LastAnswer = text;
        }
    }

    private void OnTearingOff(object sender, PaneTearingOffEventArgs e)
    {
        var what = e.IsWholePanel ? "panel" : "tab";

        Answer($"Tearing off the {what} ({string.Join(", ", e.Panes)}) - allowed.");
    }

    private void OnDocking(object sender, PaneDockingEventArgs e)
    {
        Answer($"Docking {string.Join(", ", e.Panes)} to the {e.Zone} - allowed.");
    }
}
