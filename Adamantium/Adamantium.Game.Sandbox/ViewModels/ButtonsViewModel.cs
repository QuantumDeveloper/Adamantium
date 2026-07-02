using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Buttons &amp; toggles tab: a click counter driven by Button/RepeatButton commands, plus a small settings
/// panel where a ToggleSwitch/ToggleButton/CheckBox/RadioButton group drive live status text - all through the
/// view-model, so the controls and the status stay in sync both ways.</summary>
[ViewModel]
public partial class ButtonsViewModel : TabPageViewModel
{
    public ButtonsViewModel() : base("Buttons") { }

    // Click counter: a plain Button and an auto-repeating RepeatButton both invoke Add; the count shows live.
    [Bindable] private int _clickCount;

    [Command] private void Add() => ClickCount++;

    [Command] private void Reset() => ClickCount = 0;

    // ToggleSwitch: also gates the two buttons' IsEnabled (a cross-control interaction driven purely by the VM).
    [Bindable, Affects(nameof(ActionsStatus))] private bool _actionsEnabled = true;
    public string ActionsStatus => ActionsEnabled ? "Actions enabled" : "Actions disabled";

    // ToggleButton.
    [Bindable, Affects(nameof(NotifyStatus))] private bool _notify = true;
    public string NotifyStatus => Notify ? "Notifications ON" : "Notifications OFF";

    // Three-state CheckBox (bool? matches IsChecked exactly).
    [Bindable, Affects(nameof(TermsStatus))] private bool? _termsAccepted = false;
    public string TermsStatus => TermsAccepted switch { true => "accepted", false => "declined", _ => "undecided" };

    // Mutually-exclusive RadioButton group (same GroupName in markup); the checked one drives PlanStatus.
    [Bindable, Affects(nameof(PlanStatus))] private bool _planFree = true;
    [Bindable, Affects(nameof(PlanStatus))] private bool _planPro;
    public string PlanStatus => PlanPro ? "Pro" : "Free";
}
