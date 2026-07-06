using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>SlidePanel tab: toggles that open panels sliding in from each window edge (plus a full-window one). Each
/// panel's IsOpen is two-way bound to its toggle, so the panel's × button flows the closed state back to the switch.</summary>
[ViewModel]
public partial class SlidePanelViewModel : TabPageViewModel
{
    public SlidePanelViewModel() : base("SlidePanel") { }

    [Bindable] private bool _leftOpen;
    [Bindable] private bool _rightOpen;
    [Bindable] private bool _topOpen;
    [Bindable] private bool _bottomOpen;
    [Bindable] private bool _fullOpen;
}
