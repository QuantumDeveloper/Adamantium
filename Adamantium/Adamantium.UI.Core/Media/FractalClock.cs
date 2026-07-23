using Adamantium.UI.Core.Media.Animation;

namespace Adamantium.UI.Core.Media;

/// <summary>Drives the fractal auto-morph clock. While any LIVE <see cref="FractalBrush"/> has <see cref="FractalBrush.Animate"/>
/// on (ref-counted through <see cref="Acquire"/>/<see cref="Release"/>), one AnimationManager ticker advances <see cref="Time"/>
/// each frame. The ticker has NO target, so it keeps the render loop presenting (HasActiveAnimations) WITHOUT dirtying the
/// scene - the retained fractal draw just replays with a fresh Time and the shader morphs (no re-bake, no full walk). The
/// last Release drops the ticker so the loop can idle again.</summary>
internal static class FractalClock
{
    private static int _active;
    private static bool _registered;

    /// <summary>The PHASE (not raw seconds): accumulates delta*Speed each frame. Read by the render thread each draw.</summary>
    public static double Time { get; private set; }

    /// <summary>Current morph speed the phase advances at. An animating brush sets it from its MorphSpeed. Changing it
    /// changes the RATE the phase grows, never the phase itself - so a speed change accelerates/decelerates the morph
    /// instead of jumping it. (One shared speed: multiple animating fractals with different speeds would share the last.)</summary>
    public static double Speed { get; set; } = 1.0;

    /// <summary>An animating fractal appeared: bump the ref-count and (lazily) start the ticker.</summary>
    public static void Acquire()
    {
        _active++;
        if (_registered) return;
        _registered = true;
        // AddTicker's delegate returns TRUE when done (dropped); FALSE keeps it. Advance Time and keep ticking while any
        // animating fractal is live; when the count hits zero, stop so the loop can idle.
        AnimationManager.AddTicker(delta =>
        {
            Time += delta * Speed;   // phase grows at the CURRENT speed - changing Speed changes the rate, never jumps the phase
            if (_active > 0) return false;
            _registered = false;
            return true;
        });
    }

    /// <summary>An animating fractal went away (Animate off, or its live brush released).</summary>
    public static void Release()
    {
        if (_active > 0) _active--;
    }
}
