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

    // The state the tab opens in, named once so Reset cannot drift away from it.
    private const double InitialValue = 40;
    private const double InitialMaximum = 100;
    private const double InitialSpanStart = 20;
    private const double InitialSpanEnd = 70;

    [Bindable] private double _value = InitialValue;

    [Bindable] private double _maximum = InitialMaximum;

    [Bindable] private RingStartPosition _startPosition = RingStartPosition.Top;

    [Bindable] private SweepDirection _direction = SweepDirection.Clockwise;

    /// <summary>The span a RangeSlider selects out of the same 0..Maximum scale as everything else on the tab.</summary>
    [Bindable] private double _spanStart = InitialSpanStart;

    [Bindable] private double _spanEnd = InitialSpanEnd;

    /// <summary>Puts the whole tab back exactly as it opens.</summary>
    [Command]
    private void Reset()
    {
        // The ceiling first: everything else is clamped against it, and a value that has to be clamped is the one the
        // control publishes back here - so setting it while the ceiling is still lower would lose it for good.
        Maximum = InitialMaximum;
        Value = InitialValue;

        // Same reasoning between the two bounds, which clamp against EACH OTHER: move the one travelling away from the
        // other first. Coming down, that is the start (it has room below); going up, it is the end.
        if (SpanStart > InitialSpanStart)
        {
            SpanStart = InitialSpanStart;
            SpanEnd = InitialSpanEnd;
        }
        else
        {
            SpanEnd = InitialSpanEnd;
            SpanStart = InitialSpanStart;
        }
    }
}
