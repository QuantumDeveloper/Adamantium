using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>ZoomBox tab: a large "map" shown through a fixed-size ZoomBox - wheel zooms toward the cursor, drag pans,
/// scrollbars track the zoomed extent. Two-way sliders drive ScaleX/ScaleY to show the state is bindable both ways.</summary>
[ViewModel]
public partial class ZoomBoxViewModel : TabPageViewModel
{
    public ZoomBoxViewModel() : base("ZoomBox") { }
}
