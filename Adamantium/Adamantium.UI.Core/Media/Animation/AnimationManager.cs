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

    // Membership mirror of Active, for the O(1) "was this cancelled mid-tick?" check. Tick used List.Contains, which is
    // a LINEAR scan - so a scene with N running animations cost O(N^2) comparisons PER FRAME (measured: 4606 pulsing
    // loading cards -> ~21M comparisons -> ~143 ms/frame, the bulk of the 60k-grid stall). Every mutation of Active goes
    // through the two helpers below so the two stay in lock-step.
    private static readonly HashSet<IRunningAnimation> ActiveSet = new();

    // Reused tick snapshot (Active.ToArray() allocated a fresh array every frame; see the iteration note in Tick).
    private static readonly List<IRunningAnimation> TickBuffer = new();

    private static void Add(IRunningAnimation animation)
    {
        Active.Add(animation);
        ActiveSet.Add(animation);
    }

    private static void Remove(IRunningAnimation animation)
    {
        if (ActiveSet.Remove(animation)) Active.Remove(animation);
    }

    /// <summary>Removes every matching animation (one compaction pass); returns how many went.</summary>
    private static int RemoveWhere(Predicate<IRunningAnimation> match) => Active.RemoveAll(a =>
    {
        if (!match(a)) return false;
        ActiveSet.Remove(a);
        return true;
    });

    /// <summary>True while any animation is in flight - the live designer polls this to decide whether to keep ticking.</summary>
    public static bool HasActiveAnimations => Active.Count > 0;

    /// <summary>How many animations/tickers are running right now (incl. scroll-inertia tickers) - for diagnostics.</summary>
    public static int ActiveCount => Active.Count;

    /// <summary>Drops every running animation without firing completion callbacks. The live designer calls this when it
    /// builds a fresh preview tree, so animations bound to the previous (discarded) tree don't linger in this shared
    /// static manager and get advanced against dead controls on the next tick.</summary>
    public static void Reset()
    {
        Active.Clear();
        ActiveSet.Clear();
        Holders.Clear();
        Compositor.Reset();
    }

    /// <summary>Registers a custom per-frame ticker driven by the same heartbeat as animations: <paramref name="advance"/>
    /// is called each frame with the frame delta and returns true when it's done (then it's dropped). Used for
    /// physics-style updates that aren't a property animation - e.g. scroll inertia.</summary>
    public static void AddTicker(Func<double, bool> advance) => Add(new DelegateTicker(advance));

    /// <summary>Advances every running animation by <paramref name="deltaSeconds"/>. Called once per frame.</summary>
    public static void Tick(double deltaSeconds)
    {
        // Re-publish what the render thread composes its matrices FROM (the element's base transform, where layout put it,
        // what size it is). Only the loop thread may read the live tree, and only it can change any of this - so while it is
        // stalled the render thread's copy cannot go stale: no loop, no layout.
        Compositor.RefreshBases();

        if (Active.Count == 0) return;

        // Advance a SNAPSHOT: a finishing animation's completion callback may start OR cancel animations (e.g. a tab
        // drag settling then committing the reorder + clearing transforms), which mutates Active. Iterating Active
        // directly would corrupt the loop (skip/re-advance/out-of-range). A finished animation is removed via Remove
        // (a no-op if a callback already cancelled it); animations started during a callback are advanced next tick.
        TickBuffer.Clear();
        TickBuffer.AddRange(Active);
        foreach (var animation in TickBuffer)
        {
            // An earlier animation's completion callback may have CANCELLED this one (removed it from Active). Don't
            // advance a cancelled animation: its Advance would re-write the Animation-priority value the cancel just
            // cleared, and - being gone from Active - nothing would clear it again (a stuck offset). O(1) via the set
            // mirror: this ran on EVERY animation of every frame, so a linear scan made the tick quadratic.
            if (!ActiveSet.Contains(animation)) continue;
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
                Remove(animation);
        }
    }

    internal static void Begin(AdamantiumComponent target, AdamantiumProperty property, DoubleAnimation animation, Action completed)
    {
        // Re-animating the same property restarts from the new animation - drop any in-flight one first.
        RemoveWhere(a => a.Animates(target, property));
        var running = new RunningAnimation(target, property, animation, completed);
        running.Advance(0);   // apply the From value immediately so there is no one-frame flash before the first tick
        Add(running);
    }

    /// <summary>Starts a keyframe <see cref="Animation"/> on <paramref name="target"/>, dropping any in-flight animation
    /// that drives one of the same properties.</summary>
    internal static void BeginKeyFrame(AdamantiumComponent target, Animation animation, Action completed, double resumeElapsed = 0)
    {
        var running = new RunningKeyFrameAnimation(target, animation, completed, resumeElapsed);
        foreach (var property in running.Properties)
            RemoveWhere(a => a.Animates(target, property));
        running.Advance(0);   // apply the start values immediately so there is no one-frame flash
        Add(running);
    }

    /// <summary>The elapsed time of the animation currently running on <paramref name="target"/> (any property), or null if
    /// none - so a re-templated trigger can hand the phase to its successor (see RunAnimationAction). Loop thread.</summary>
    internal static double? GetElapsed(AdamantiumComponent target)
    {
        foreach (var a in Active)
            if (a is RunningKeyFrameAnimation rk && rk.AnimatesTarget(target))
                return rk.CurrentElapsed;
        return null;
    }

    /// <summary>Stops the animation (if any) running on <paramref name="property"/> of <paramref name="target"/> without
    /// firing its completion callback. Returns true if one was running. The caller releases the held animation value.</summary>
    internal static bool Cancel(AdamantiumComponent target, AdamantiumProperty property)
    {
        var cancelled = RemoveWhere(a => a.Animates(target, property)) > 0;
        if (cancelled) Compositor.Release(target);   // the render thread must stop playing what the loop just stopped
        return cancelled;
    }

    /// <summary>Stops EVERY animation running on <paramref name="target"/> (any property), without firing completions -
    /// the WPF <c>StopStoryboard</c> analog. Used to end a looping animation on demand (e.g. a StopAnimationAction, or a
    /// recycled loading card whose pulse must stop once its real item has loaded). The held Animation-priority values
    /// stay until the caller/property system releases them.</summary>
    public static void Cancel(AdamantiumComponent target)
    {
        RemoveWhere(a => a.AnimatesTarget(target));
        Compositor.Release(target);
    }

    /// <summary>Stops everything running anywhere under <paramref name="root"/> - what parking a whole page needs. ONE
    /// pass over what is actually running, asking each animation whether its target sits in that subtree: a page has a
    /// thousand realized rows and hardly any of them animate, so asking every ROW instead cost a scan of the running list
    /// per row and dwarfed the work it was meant to save.</summary>
    public static void CancelSubtree(IUIComponent root)
    {
        if (root == null) return;

        var released = new List<AdamantiumComponent>();
        RemoveWhere(a =>
        {
            if (a.DirtyTarget is not { } target || !IsWithin(target, root)) return false;

            released.Add(target as AdamantiumComponent);
            return true;
        });

        foreach (var target in released)
        {
            if (target != null) Compositor.Release(target);
        }
    }

    private static bool IsWithin(IUIComponent node, IUIComponent root)
    {
        for (var current = node; current != null; current = current.VisualParent)
        {
            if (ReferenceEquals(current, root)) return true;
        }

        return false;
    }

    // --- Shared-target ownership -------------------------------------------------------------------------------------
    // An animation TARGET can be shared by many trigger hosts: the loading-skeleton pulse runs on ONE theme brush
    // ({ResourceReference SkeletonPulseFill}) that EVERY loading list animates, so a naive Stop from the first list to
    // finish would freeze the pulse of the others still loading. RunAnimationAction retains its host on the target and
    // StopAnimationAction releases it; the target's animations are cancelled only when the LAST host lets go. The holder
    // is the trigger's HOST (not the action, which is shared by every host of one style) and the set makes a repeated
    // Retain by the same host idempotent - a theme is free to re-Run on a target it already animates (the auto-hide
    // scrollbar re-Runs a fade-out from its ExitActions) without inflating a count. A single-host target - the norm -
    // goes {host} -> {} -> cancel, exactly as before.
    private static readonly Dictionary<AdamantiumComponent, HashSet<object>> Holders = new();

    /// <summary>Records that <paramref name="holder"/> wants an animation running on <paramref name="target"/>.</summary>
    public static void Retain(AdamantiumComponent target, object holder)
    {
        if (target == null || holder == null) return;
        if (!Holders.TryGetValue(target, out var holders)) 
            Holders[target] = holders = new HashSet<object>();
        holders.Add(holder);
    }

    /// <summary>Releases <paramref name="holder"/>'s claim on <paramref name="target"/> and stops the target's animations
    /// once nobody holds it any more. A release with no recorded holder still cancels (a Stop that was never preceded by
    /// a Run behaves exactly as <see cref="Cancel(AdamantiumComponent)"/>).</summary>
    public static void Release(AdamantiumComponent target, object holder)
    {
        if (target == null) return;
        if (Holders.TryGetValue(target, out var holders) && holder != null)
        {
            holders.Remove(holder);
            if (holders.Count > 0) return;   // another host still wants it - keep it running
            Holders.Remove(target);
        }
        Cancel(target);
    }

    // Wraps a delegate as a running "animation" so a non-property per-frame updater (scroll inertia) rides the heartbeat.
    private sealed class DelegateTicker : IRunningAnimation
    {
        private readonly Func<double, bool> _advance;
        public DelegateTicker(Func<double, bool> advance) => _advance = advance;
        public bool Advance(double deltaSeconds) => _advance(deltaSeconds);
        public bool Animates(AdamantiumComponent target, AdamantiumProperty property) => false;
        public bool AnimatesTarget(AdamantiumComponent target) => false;
        public IUIComponent DirtyTarget => null;
    }
}
