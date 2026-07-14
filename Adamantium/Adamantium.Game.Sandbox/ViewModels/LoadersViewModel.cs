using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Loaders tab: the busy-indicator pack. One <see cref="IsActive"/> drives every indicator on the page, so the
/// toggle also demonstrates the thing that is easy to get wrong - an indicator that is switched OFF must actually STOP
/// its (infinite) animation, not just hide.</summary>
[ViewModel]
public partial class LoadersViewModel : TabPageViewModel
{
    public LoadersViewModel() : base("Loaders") { }

    [Bindable] private bool _isActive = true;
}
