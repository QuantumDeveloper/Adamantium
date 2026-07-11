using System;
using System.Collections.Generic;

namespace Adamantium.UI.Core.Media.Animation;

// (RenderDirty lives in the Adamantium.UI.Core namespace - same assembly.)

/// <summary>
/// The animation heartbeat: holds the running animations and advances them once per frame. The application loop calls
/// <see cref="Tick"/> with the frame delta (see UIApplication.Update); animations register themselves via
/// <see cref="AdamantiumComponent.BeginAnimation"/>. Minimal and allocation-light - finished animations are removed in
/// place, so an idle UI does no per-frame work.
/// </summary>
public static class AnimationManager
{
    private static readonly List<IRunningAnimation> Active = new();

    /// <summary>True while any animation is in flight - the live designer polls this to decide whether to keep ticking.</summary>
    public static bool HasActiveAnimations => Active.Count > 0;

    /// <summary>How many animations/tickers are running right now (incl. scroll-inertia tickers) - for diagnostics.</summary>
    public static int ActiveCount => Active.Count;

    /// <summary>Drops every running animation without firing completion callbacks. The live designer calls this when it
    /// builds a fresh preview tree, so animations bound to the previous (discarded) tree don't linger in this shared
    /// static manager and get advanced against dead controls on the next tick.</summary>
    public static void Reset() => Active.Clear();

    /// <summary>Registers a custom per-frame ticker driven by the same heartbeat as animations: <paramref name="advance"/>
    /// is called each frame with the frame delta and returns true when it's done (then it's dropped). Used for
    /// physics-style updates that aren't a property animation - e.g. scroll inertia.</summary>
    public static void AddTicker(Func<double, bool> advance) => Active.Add(new DelegateTicker(advance));

    /// <summary>Advances every running animation by <paramref name="deltaSeconds"/>. Called once per frame.</summary>
    public static void Tick(double deltaSeconds)
    {
        if (Active.Count == 0) return;

        // Advance a SNAPSHOT: a finishing animation's completion callback may start OR cancel animations (e.g. a tab
        // drag settling then committing the reorder + clearing transforms), which mutates Active. Iterating Active
        // directly would corrupt the loop (skip/re-advance/out-of-range). A finished animation is removed via Remove
        // (a no-op if a callback already cancelled it); animations started during a callback are advanced next tick.
        foreach (var animation in Active.ToArray())
        {
            // An earlier animation's completion callback may have CANCELLED this one (removed it from Active). Don't
            // advance a cancelled animation: its Advance would re-write the Animation-priority value the cancel just
            // cleared, and - being gone from Active - nothing would clear it again (a stuck offset). Active is small,
            // so the linear Contains check is negligible.
            if (!Active.Contains(animation)) continue;
            // A property animation re-renders every tick. Its property write usually marks precisely on its own
            // (AffectsRender -> MarkGeometry on that one component; a Transform's inner value self-marks the Transform
            // path; a layout-affecting property re-arranges and the moved components' Bounds setters mark). The heartbeat
            // keeps ONE safety net - the animation's TARGET re-renders this tick - but marks it PER COMPONENT (geometry),
            // NOT the global Transform flag: the global flag disabled every O(dirty) partial render path (in-place replay
            // + spliced patch) for the whole duration of ANY animation, so e.g. an auto-hide scrollbar's fade-out
            // full-walked + re-baked a 60k-unit scene every frame (~25 FPS for seconds). A DelegateTicker (scroll
            // inertia, the diagnostics overlay) has no target and dirties the scene only through its effects.
            if (animation.DirtyTarget is { } dirtyTarget) RenderDirty.MarkGeometry(dirtyTarget);
            if (animation.Advance(deltaSeconds))
                Active.Remove(animation);
        }
    }

    internal static void Begin(AdamantiumComponent target, AdamantiumProperty property, DoubleAnimation animation, Action completed)
    {
        // Re-animating the same property restarts from the new animation - drop any in-flight one first.
        Active.RemoveAll(a => a.Animates(target, property));
        var running = new RunningAnimation(target, property, animation, completed);
        running.Advance(0);   // apply the From value immediately so there is no one-frame flash before the first tick
        Active.Add(running);
    }

    /// <summary>Starts a keyframe <see cref="Animation"/> on <paramref name="target"/>, dropping any in-flight animation
    /// that drives one of the same properties.</summary>
    internal static void BeginKeyFrame(AnimatableUIComponent target, Animation animation, Action completed)
    {
        var running = new RunningKeyFrameAnimation(target, animation, completed);
        foreach (var property in running.Properties)
            Active.RemoveAll(a => a.Animates(target, property));
        running.Advance(0);   // apply the start values immediately so there is no one-frame flash
        Active.Add(running);
    }

    /// <summary>Stops the animation (if any) running on <paramref name="property"/> of <paramref name="target"/> without
    /// firing its completion callback. Returns true if one was running. The caller releases the held animation value.</summary>
    internal static bool Cancel(AdamantiumComponent target, AdamantiumProperty property)
    {
        return Active.RemoveAll(a => a.Animates(target, property)) > 0;
    }

    // Wraps a delegate as a running "animation" so a non-property per-frame updater (scroll inertia) rides the heartbeat.
    private sealed class DelegateTicker : IRunningAnimation
    {
        private readonly Func<double, bool> _advance;
        public DelegateTicker(Func<double, bool> advance) => _advance = advance;
        public bool Advance(double deltaSeconds) => _advance(deltaSeconds);
        public bool Animates(AdamantiumComponent target, AdamantiumProperty property) => false;
        public IUIComponent DirtyTarget => null;
    }
}
