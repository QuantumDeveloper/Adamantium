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
/// - the target is an element and the property is its own Opacity: that value now lives in an opacity SLOT of the transform
///   table, so the render thread writes one float and every instance under it composes it at draw time.
///
/// Everything else stays on the loop thread.
/// </remarks>
public static class AnimationChannels
{
    /// <summary>The channel one animated property of one target belongs to.</summary>
    public static CompositorChannel Of(AdamantiumComponent target, AdamantiumProperty property)
    {
        if (target is Transform) return CompositorChannel.Transform;

        // The element's OWN opacity, told apart from every other AffectsPaint property BEFORE they are considered: it is
        // the one whose applied form is a slot write rather than a re-bake. Asked of the interface, because the property
        // is registered a layer above this one - what identifies it here is the pair (an element, its Opacity).
        // NOT handed to the compositor, deliberately. It COULD be - the machinery is here (CompositorChannel.Opacity,
        // ApplyCompositedOpacity), and the slot makes it a one-float write. What stops it is that the compositor keeps
        // the value to ITSELF: the element's Opacity property would stop advancing while the animation plays, the way a
        // Transform's does. A Transform is read by the renderer; Opacity is read by bindings, triggers and app code, so
        // that trade is the owner's call, not a silent one. And it is not needed for the cost: Opacity no longer marks
        // its subtree (see UIComponent.OnOpacityChanged), so a fade already reaches the patch as ONE dirty element.
        if (target is IUIComponent && ReferenceEquals(property, OpacityOf(target))) return CompositorChannel.None;

        var metadata = property.GetDefaultMetadata(target.GetType());
        return metadata is { AffectsPaint: true } ? CompositorChannel.Paint : CompositorChannel.None;
    }

    // The Opacity property as REGISTERED for this target's type. Looked up by name once per type through the property
    // system's own registry - the same lookup a binding does - rather than referencing UIComponent, which lives above.
    private static AdamantiumProperty OpacityOf(AdamantiumComponent target)
    {
        if (OpacityByType.TryGetValue(target.GetType(), out var known)) return known;

        AdamantiumProperty found = null;
        foreach (var property in AdamantiumPropertyMap.GetRegistered(target.GetType()))
            if (property.Name == "Opacity") { found = property; break; }

        OpacityByType[target.GetType()] = found;
        return found;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, AdamantiumProperty> OpacityByType = new();

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
