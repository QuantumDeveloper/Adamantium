namespace Adamantium.UI.Core.Media.Animation;

/// <summary>Decides which <see cref="CompositorChannel"/> an animation belongs to - i.e. whether the render thread can play
/// it without the loop thread.</summary>
/// <remarks>
/// Automatic, from what the property ACTUALLY touches - never from a flag the animation's author sets. An author who has to
/// declare "this one is cheap" will eventually declare it about something that isn't, and the wrong answer here is a torn
/// frame or a data race, not a slow one. The two facts the engine already knows are enough:
///
/// - the target is a <see cref="Transform"/>: every property on it folds into one matrix and touches nothing else;
/// - the property is <see cref="PropertyMetadataOptions.AffectsPaint"/>: by its own declaration it changes only the colour
///   of what is already recorded (a brush's colour or opacity, a gradient stop, a gradient's geometry).
///
/// Everything else stays on the loop thread. Notably the ELEMENT's own Opacity, which looks composable but is not yet: it is
/// folded into RenderData at RECORD time (and into every descendant's), so the render thread has nowhere to write it. Moving
/// it into the frozen snapshot is what earns it a channel - until then, saying it has one would be a lie the renderer pays for.
/// </remarks>
public static class AnimationChannels
{
    /// <summary>The channel one animated property of one target belongs to.</summary>
    public static CompositorChannel Of(AdamantiumComponent target, AdamantiumProperty property)
    {
        if (target is Transform) return CompositorChannel.Transform;

        var metadata = property.GetDefaultMetadata(target.GetType());
        return metadata is { AffectsPaint: true } ? CompositorChannel.Paint : CompositorChannel.None;
    }

    /// <summary>The channel a whole curve belongs to: every track must land in the SAME non-None channel, or the loop thread
    /// keeps the animation. A curve is one clock over several properties (a scale in X and Y, a wave across three stops) -
    /// splitting it across threads would let its own tracks drift apart, which is worse than not compositing it at all.</summary>
    public static CompositorChannel Of(AdamantiumComponent target, AnimationCurve curve)
    {
        if (curve.Tracks.Length == 0) return CompositorChannel.None;

        var channel = Of(target, curve.Tracks[0].Property);
        if (channel == CompositorChannel.None) return CompositorChannel.None;

        for (var i = 1; i < curve.Tracks.Length; i++)
            if (Of(target, curve.Tracks[i].Property) != channel)
                return CompositorChannel.None;

        return channel;
    }
}
