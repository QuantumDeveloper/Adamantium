namespace Adamantium.UI.Core.Media.Animation;

/// <summary>Where an animation can be APPLIED - which decides whether the render thread can play it by itself.</summary>
/// <remarks>
/// This is the engine's own read of the property, not something an author declares: the same split UWP makes between
/// "independent" (compositor) and "dependent" (UI thread, and off by default because it stutters) animations, decided the
/// same way - by what the animated property actually touches.
/// </remarks>
public enum CompositorChannel
{
    /// <summary>The loop thread must play it: applying it needs layout, a re-record, or the property system itself.</summary>
    None,

    /// <summary>A pure matrix change: the render thread rewrites the element's motion-node matrix and draws.</summary>
    Transform,

    /// <summary>A pure colour change: the render thread re-bakes the batch slots the element already owns.</summary>
    Paint,

    /// <summary>The ELEMENT's own opacity: the render thread writes one float into its opacity slot, and every instance
    /// under it composes that at draw time. The twin of <see cref="Transform"/> - one write moves a whole subtree - and
    /// possible for the same reason: the value lives in the transform table, not folded into what was recorded.</summary>
    Opacity
}
