using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.MVVM;
using Adamantium.UI.Controls;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Infinite canvas tab: an unbounded plane. The wheel zooms toward the cursor, the middle button - or space
/// and the left one - pans, and neither ever reaches an edge: there is no scrollable extent here at all.
/// <para>The camera's <c>Scale</c> and <c>Offset</c> are bound both ways, so the readout under the canvas is the state
/// itself rather than a copy of it.</para></summary>
[ViewModel]
public partial class InfiniteCanvasViewModel : TabPageViewModel
{
    public InfiniteCanvasViewModel() : base("Infinite canvas") { }

    public IReadOnlyList<CanvasGridStyle> GridStyles { get; } =
        [CanvasGridStyle.Dots, CanvasGridStyle.Lines, CanvasGridStyle.None];

    [Bindable] private CanvasGridStyle _gridStyle = CanvasGridStyle.Dots;

    [Bindable] private double _scale = 1;

    [Bindable] private Vector2 _offset;

    /// <summary>What the camera is, in words. An edgeless plane has no position to read off it, so the numbers are the
    /// only way to see that panning a long way out costs nothing and loses nothing.</summary>
    public string Camera =>
        $"scale {_scale:0.###}x     world origin at ({_offset.X:0}, {_offset.Y:0}) px on screen";

    partial void OnScaleChanged(double value) => RaisePropertyChanged(nameof(Camera));

    partial void OnOffsetChanged(Vector2 value) => RaisePropertyChanged(nameof(Camera));
}
