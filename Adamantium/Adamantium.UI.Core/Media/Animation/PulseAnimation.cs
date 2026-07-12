using Adamantium.UI.Core.Resources;

namespace Adamantium.UI.Core.Media.Animation;

/// <summary>
/// A prebuilt "breathe" animation: eases a single double property between <see cref="Min"/> and <see cref="Max"/> and
/// back, forever - the classic loading pulse. The pulse SHAPE is baked (Min→Max, AutoReverse, looping); markup exposes
/// only the knobs: which <see cref="Property"/> to pulse, its <see cref="Min"/>/<see cref="Max"/>, plus the inherited
/// <see cref="Animation.Duration"/> and <see cref="Animation.Easing"/>. Authored as
/// <c>&lt;PulseAnimation Property="Opacity" Min="0.05" Max="0.16" Duration="0:0:0.9" Easing="CubicInOut"/&gt;</c> - no
/// hand-written keyframes. The pattern extends to other named primitives (a bounce, a shake, a spin) the same way.
/// </summary>
public class PulseAnimation : Animation
{
    /// <summary>Name of the double property to pulse on the animation's target (e.g. <c>Opacity</c>).</summary>
    public string Property { get; set; }

    /// <summary>The low end of the pulse.</summary>
    public double Min { get; set; }

    /// <summary>The high end of the pulse.</summary>
    public double Max { get; set; }

    protected override void Prepare()
    {
        // Bake the pulse: Min -> Max over the duration (eased), AutoReverse brings it back, loop forever.
        AutoReverse = true;
        IterationCount = double.PositiveInfinity;
        KeyFrames.Clear();
        KeyFrames.Add(Frame(0, Min));
        KeyFrames.Add(Frame(1, Max));
    }

    private KeyFrame Frame(double cue, double value)
    {
        var frame = new KeyFrame { Cue = cue };
        frame.Setters.Add(new Setter(Property, value));
        return frame;
    }
}
