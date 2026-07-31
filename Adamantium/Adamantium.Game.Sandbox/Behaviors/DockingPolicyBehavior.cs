using System.Linq;
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
/// <see cref="DockZone.Floating"/> leaves for a window of its own and cannot be docked back. What is left for an EVENT
/// is the rule a set of zones cannot express, because it is about the state of the layout right now - here: the last
/// tab of a panel may not be pulled out.</para>
/// <para>There was a second rule here once - a cap on tabs per group - and it was a bad example: no docking control
/// anywhere limits how many tabs a group may hold (overflow is a scrolling strip and a menu, not a refusal), and it
/// read as a broken drop rather than as a policy.</para>
/// </summary>
public class DockingPolicyBehavior : Behavior<DockingArea>
{
    /// <summary>Refuse to tear out the LAST tab of a group, which would take the group with it. A rule about the
    /// state of the layout at this moment, not about where a pane is allowed to live.</summary>
    public static readonly AdamantiumProperty KeepLastPaneProperty = AdamantiumProperty.Register(nameof(KeepLastPane),
        typeof(bool), typeof(DockingPolicyBehavior), new PropertyMetadata(false));

    public bool KeepLastPane
    {
        get => GetValue<bool>(KeepLastPaneProperty);
        set => SetValue(KeepLastPaneProperty, value);
    }

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

        if (KeepLastPane && !e.IsWholePanel && e.Panes.Count == 1
            && _area?.Layout.FindGroup(e.Panes[0]) is { PaneIds.Count: 1 })
        {
            e.Cancel = true;
            Answer($"REFUSED: '{e.Panes[0]}' is the last tab of its panel, and taking it would take the panel with it.");
            return;
        }

        Answer($"Tearing off the {what} ({string.Join(", ", e.Panes)}) - allowed.");
    }

    private void OnDocking(object sender, PaneDockingEventArgs e)
    {
        Answer($"Docking {string.Join(", ", e.Panes)} to the {e.Zone} - allowed.");
    }
}
