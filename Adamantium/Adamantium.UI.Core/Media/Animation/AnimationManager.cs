using System;
using System.Collections.Generic;

namespace Adamantium.UI.Core.Media.Animation;

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

    /// <summary>Drops every running animation without firing completion callbacks. The live designer calls this when it
    /// builds a fresh preview tree, so animations bound to the previous (discarded) tree don't linger in this shared
    /// static manager and get advanced against dead controls on the next tick.</summary>
    public static void Reset() => Active.Clear();

    /// <summary>Advances every running animation by <paramref name="deltaSeconds"/>. Called once per frame.</summary>
    public static void Tick(double deltaSeconds)
    {
        for (var i = Active.Count - 1; i >= 0; i--)
        {
            if (Active[i].Advance(deltaSeconds))
                Active.RemoveAt(i);
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
}
