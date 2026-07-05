using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Buttons &amp; toggles tab: a click counter driven by Button/RepeatButton commands, plus a small settings
/// panel where a ToggleSwitch/ToggleButton/CheckBox/RadioButton group drive live status text - all through the
/// view-model, so the controls and the status stay in sync both ways.</summary>
[ViewModel]
public partial class ButtonsViewModel : TabPageViewModel
{
    public ButtonsViewModel() : base("Buttons")
    {
        SelectedColor = Palette[0];   // seed so the templated header shows a real row from the start (no null-through-template)
    }

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

    // DropDown (non-editable). A plain string list bound via ItemsSource; and an enum bound via EnumType (the dropdown
    // shows each member's [Display] name while SelectedPriority stays the real enum value).
    [Bindable] private string[] _cities = ["London", "Paris", "Berlin", "Tokyo", "New York", "Sydney"];
    [Bindable, Affects(nameof(CityStatus))] private string _selectedCity;
    public string CityStatus => SelectedCity != null ? $"City: {SelectedCity}" : "No city chosen";

    [Bindable, Affects(nameof(PriorityStatus))] private Priority _selectedPriority = Priority.Normal;
    public string PriorityStatus => $"Priority = {SelectedPriority}";

    // Template-bound DropDown: items are ColorOption objects rendered by an ItemTemplate (swatch + name), not plain text.
    [Bindable] private ColorOption[] _palette =
    [
        new ColorOption("Ocean", "#0091F7"),
        new ColorOption("Forest", "#107C10"),
        new ColorOption("Grape", "#8764B8"),
        new ColorOption("Ember", "#CA5010"),
        new ColorOption("Rose", "#C42B72"),
    ];
    [Bindable, Affects(nameof(ColorStatus))] private ColorOption _selectedColor;
    public string ColorStatus => SelectedColor != null ? $"Color: {SelectedColor.Name}" : "No color chosen";
}
