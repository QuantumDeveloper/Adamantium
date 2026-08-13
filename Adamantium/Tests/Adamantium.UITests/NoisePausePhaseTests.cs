using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Animation;
using Adamantium.UI.Rendering;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>The phase a noise brush flows at across a pause. The clock is SHARED and keeps advancing while ANY brush
/// animates, so these hold a second brush animating throughout - which is the case the sandbox hits (a wall of animated
/// swatches sits on the same tab as the one being paused).</summary>
[TestFixture]
public class NoisePausePhaseTests
{
    /// <summary>What the shader computes: animating rides the clock minus the brush's own offset, paused holds the frozen one.</summary>
    private static double ShaderPhase(NoiseBrush brush)
    {
        Assert.That(PatternBrushRecord.TryDescribe(brush, out var record), Is.True);
        if (!brush.Animate) return record.FrozenPhase;
        return NoiseClock.Time - record.PhaseOffset;
    }

    [Test]
    public void PausedNoise_HoldsItsPhase_WhileTheSharedClockKeepsRunning()
    {
        var other = new NoiseBrush { Animate = true };   // holds the clock alive
        var brush = new NoiseBrush { Animate = true };

        AnimationManager.Tick(0.5);
        var running = ShaderPhase(brush);
        brush.Animate = false;

        // The frame it stops on is the frame it was showing - pausing must not jump the field either.
        Assert.That(ShaderPhase(brush), Is.EqualTo(running).Within(1e-9));

        AnimationManager.Tick(2.0);

        Assert.That(ShaderPhase(brush), Is.EqualTo(running).Within(1e-9));
        other.Animate = false;
    }

    [Test]
    public void ResumingNoise_ContinuesFromWhereItStopped_NotFromTheSharedClock()
    {
        var other = new NoiseBrush { Animate = true };
        var brush = new NoiseBrush { Animate = true };

        AnimationManager.Tick(0.5);
        brush.Animate = false;
        var stopped = ShaderPhase(brush);

        AnimationManager.Tick(2.0);   // the pause the shared clock runs straight through
        brush.Animate = true;

        Assert.That(ShaderPhase(brush), Is.EqualTo(stopped).Within(1e-9));
        other.Animate = false;
    }

    [Test]
    public void RepeatedPauses_DoNotAccumulateDrift()
    {
        var other = new NoiseBrush { Animate = true };
        var brush = new NoiseBrush { Animate = true };

        AnimationManager.Tick(0.25);
        var flowed = 0.25;

        for (var i = 0; i < 4; i++)
        {
            brush.Animate = false;
            AnimationManager.Tick(1.0);   // pause: must not add to the brush's own phase
            brush.Animate = true;
            AnimationManager.Tick(0.25);  // running: must
            flowed += 0.25;
        }

        Assert.That(ShaderPhase(brush), Is.EqualTo(flowed).Within(1e-9));
        other.Animate = false;
    }

    [Test]
    public void RunningNoise_StillAdvances()
    {
        var brush = new NoiseBrush { Animate = true };

        var before = ShaderPhase(brush);
        AnimationManager.Tick(0.5);

        Assert.That(ShaderPhase(brush), Is.EqualTo(before + 0.5).Within(1e-9));
        brush.Animate = false;
    }
}
