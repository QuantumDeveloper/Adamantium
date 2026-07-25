using Adamantium.UI.Core.Media.Animation;

namespace Adamantium.UI.Core.Media;

/// <summary>Drives the noise flow clock. While any LIVE <see cref="NoiseBrush"/> has <see cref="NoiseBrush.Animate"/> on
/// (ref-counted through <see cref="Acquire"/>/<see cref="Release"/>), one AnimationManager ticker advances <see cref="Time"/>
/// each frame. The ticker has NO target, so it keeps the render loop presenting (HasActiveAnimations) WITHOUT dirtying the
/// scene - the retained pattern draw just replays with a fresh Time and the Worley feature points orbit (no re-bake). The
/// last Release drops the ticker so the loop can idle again. Mirrors <see cref="FractalClock"/>.</summary>
internal static class NoiseClock
{
    private static int _active;
    private static bool _registered;

    /// <summary>The PHASE (not raw seconds): accumulates delta*Speed each frame. Read by the render thread each draw.</summary>
    public static double Time { get; private set; }

    /// <summary>Current flow speed the phase advances at. An animating brush sets it from its FlowSpeed. Changing it changes
    /// the RATE the phase grows, never the phase itself - so a speed change accelerates/decelerates the flow. (One shared
    /// speed: multiple animating noise brushes with different speeds would share the last.)</summary>
    public static double Speed { get; set; } = 1.0;

    /// <summary>An animating noise brush appeared: bump the ref-count and (lazily) start the ticker.</summary>
    public static void Acquire()
    {
        _active++;
        if (_registered) return;
        _registered = true;
        AnimationManager.AddTicker(delta =>
        {
            Time += delta * Speed;
            if (_active > 0) return false;
            _registered = false;
            return true;
        });
    }

    /// <summary>An animating noise brush went away (Animate off, or its live brush released).</summary>
    public static void Release()
    {
        if (_active > 0) _active--;
    }
}
