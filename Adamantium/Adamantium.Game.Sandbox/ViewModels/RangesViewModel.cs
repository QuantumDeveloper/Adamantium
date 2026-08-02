using Adamantium.MVVM;
using Adamantium.UI.Controls;
using Adamantium.UI.Core.Media;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Ranges tab: one shared <see cref="Value"/> is bound two-way to a horizontal and a vertical Slider and
/// (one-way) to a ProgressBar + RingProgressBar, so moving any slider moves everything at once. A second slider rescales
/// the shared <see cref="Maximum"/>; two dropdowns drive the ring's arc start and sweep direction. Demonstrates several
/// range controls kept in sync through a single view-model value.</summary>
[ViewModel]
public partial class RangesViewModel : TabPageViewModel
{
    public RangesViewModel() : base("Ranges") { }

    [Bindable] private double _value = 40;

    [Bindable] private double _maximum = 100;

    [Bindable] private RingStartPosition _startPosition = RingStartPosition.Top;

    [Bindable] private SweepDirection _direction = SweepDirection.Clockwise;

    /// <summary>The span a RangeSlider selects out of the same 0..Maximum scale as everything else on the tab.</summary>
    [Bindable] private double _spanStart = 20;

    [Bindable] private double _spanEnd = 70;

    [Command] private void Reset() => Value = 0;
}
