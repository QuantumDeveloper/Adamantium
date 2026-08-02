using Adamantium.Core.TypeParsing;
using Adamantium.MVVM;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Controls;
using Adamantium.UI.Core.Media.Imaging;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>One animated image in the gallery, with its own playback controls: the frame range it loops over, how fast
/// it steps, whether it is running, and the four corner radii. Per-image rather than shared, because a range only means
/// something against THIS source's frame count - which the control reports back into <see cref="FrameCount"/> once the
/// file has decoded.</summary>
public partial class AnimatedImageViewModel : AdamantiumViewModel
{
    public AnimatedImageViewModel(string title, string path, uint delay)
    {
        Title = title;
        // Parsed HERE, not left as a path: in markup the same conversion happens at compile time, but a binding hands
        // the property whatever the view-model holds - and a string is not an image, so the control would load nothing.
        Source = TypeParser.Parse<ImageSource>(path);
        _delay = delay;
    }

    public string Title { get; }

    public ImageSource Source { get; }

    /// <summary>Frames the loaded source turned out to have - bound FROM the Image, and what bounds the range sliders.</summary>
    [Bindable] private uint _frameCount = 1;

    [Bindable] private uint _delay;
    [Bindable] private uint _startFrame;
    [Bindable] private uint _endFrame = uint.MaxValue;
    [Bindable] private bool _isPlaying = true;

    /// <summary>Which way the frames run: forward, backward, or bouncing between the ends of the range.</summary>
    [Bindable] private ReplayDirection _direction = ReplayDirection.Forward;

    [Bindable] private double _topLeft;
    [Bindable] private double _topRight;
    [Bindable] private double _bottomRight;
    [Bindable] private double _bottomLeft;

    /// <summary>The four radii as the control wants them - one value, rebuilt whenever a corner moves.</summary>
    [Bindable] private CornerRadius _cornerRadius = new(0);

    public string PlayPauseText => IsPlaying ? "Pause" : "Play";

    // The range slider's ceiling: a source with N frames is indexed 0..N-1.
    public uint LastFrame => FrameCount > 0 ? FrameCount - 1 : 0;

    /// <summary>Stops on the frame being shown, or carries on from it - the control keeps the cursor either way.</summary>
    [Command] private void TogglePlayback() => IsPlaying = !IsPlaying;

    partial void OnTopLeftChanged(double value) => RebuildCorners();
    partial void OnTopRightChanged(double value) => RebuildCorners();
    partial void OnBottomRightChanged(double value) => RebuildCorners();
    partial void OnBottomLeftChanged(double value) => RebuildCorners();

    partial void OnIsPlayingChanged(bool value) => RaisePropertyChanged(nameof(PlayPauseText));

    // A freshly loaded source can have fewer frames than the range asks for; keep the end inside it so the sliders and
    // the animation agree from the first frame.
    partial void OnFrameCountChanged(uint value)
    {
        RaisePropertyChanged(nameof(LastFrame));
        if (EndFrame > LastFrame) EndFrame = LastFrame;
        if (StartFrame > EndFrame) StartFrame = EndFrame;
    }

    private void RebuildCorners() => CornerRadius = new CornerRadius(TopLeft, TopRight, BottomRight, BottomLeft);
}
