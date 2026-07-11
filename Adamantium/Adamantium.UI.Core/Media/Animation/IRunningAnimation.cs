namespace Adamantium.UI.Core.Media.Animation;

/// <summary>A live animation advanced each frame by <see cref="AnimationManager"/> (single-property or keyframe).</summary>
internal interface IRunningAnimation
{
    /// <summary>Advance by the frame delta; returns true once finished, so the manager drops it.</summary>
    bool Advance(double deltaSeconds);

    /// <summary>True when this animation drives <paramref name="property"/> on <paramref name="target"/> - used to drop
    /// a conflicting in-flight animation when a new one starts on the same property.</summary>
    bool Animates(AdamantiumComponent target, AdamantiumProperty property);

    /// <summary>The component whose rendered output this animation drives (null for a delegate ticker). The per-tick
    /// heartbeat marks exactly this component's geometry dirty - a PER-COMPONENT safety net, so an animating frame stays
    /// on the O(dirty) partial render paths instead of a global-flag full re-bake.</summary>
    IUIComponent DirtyTarget { get; }
}
