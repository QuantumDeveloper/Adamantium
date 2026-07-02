using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Ranges tab: one shared <see cref="Value"/> is bound two-way to a horizontal and a vertical Slider and
/// (one-way) to a ProgressBar, so moving any slider moves everything at once. A second slider rescales the shared
/// <see cref="Maximum"/>. Demonstrates several range controls kept in sync through a single view-model value.</summary>
[ViewModel]
public partial class RangesViewModel : TabPageViewModel
{
    public RangesViewModel() : base("Ranges") { }

    [Bindable] private double _value = 40;

    [Bindable] private double _maximum = 100;

    [Command] private void Reset() => Value = 0;
}
