using Adamantium.Mathematics;
using Adamantium.UI.Core;

namespace Adamantium.UI.Rendering;

/// <summary>
/// A component's FROZEN per-frame layout inputs - the ONE channel through which the render/draw path reads a component's
/// MUTABLE layout state (transform, size, clip flag, motion-node flag, parent link). The compose helpers (World /
/// CumulativeClip / NodeOf / RelWorld / LogicalBounds / ResolveScissor) read ONLY from these, never off a live
/// <see cref="IUIComponent"/>: that is what makes the draw pass a pure function of the snapshot, and so safe to run on a
/// render thread while layout mutates the tree (docs/RENDER_THREAD_PLAN.md).
///
/// Recorded on the update thread and handed to the applier as a per-frame DELTA (<see cref="RenderPacket.SnapDelta"/>) -
/// only the entries that actually changed. RenderId stays a live read: an immutable identity is thread-safe to read.
/// </summary>
internal readonly struct LayoutSnapshot(
    Matrix4x4F localTransform,
    Size renderSize,
    bool clipToBounds,
    bool isMotionNode,
    IUIComponent renderParent,
    float opacity = 1f,
    float selfOpacity = 1f) : System.IEquatable<LayoutSnapshot>
{
    // A FIELD, not a get-only property: 64 bytes that Equals wants to compare by reference, and a property cannot be
    // passed as `in` (it is not addressable, so the compiler copies it first - the very copy this avoids).
    public readonly Matrix4x4F LocalTransform = localTransform;
    public Size RenderSize { get; } = renderSize;
    public bool ClipToBounds { get; } = clipToBounds;
    public bool IsMotionNode { get; } = isMotionNode;

    /// <summary>The element's OWN opacity - the part that composites DOWN onto descendants. The bake multiplies this up the
    /// <see cref="RenderParent"/> chain (see RenderCache.EffectiveOpacity); frozen here so the draw never reads the live
    /// property.</summary>
    public float Opacity { get; } = opacity;

    /// <summary>The element's SelfOpacity - fades only its own draws, NOT composited onto descendants.</summary>
    public float SelfOpacity { get; } = selfOpacity;

    /// <summary>The component this one is composed ON TOP OF - <see cref="IUIComponent.RenderParent"/>, i.e. the visual
    /// parent for everything but an adorner (which draws in its adorned element's space, not in the visual tree).</summary>
    public IUIComponent RenderParent { get; } = renderParent;

    /// <summary>Field-by-field, and spelled out rather than left to the default for two reasons. The struct holds a
    /// reference (the parent), so <c>ValueType.Equals</c> would fall back to REFLECTION - and this is asked per re-frozen
    /// component per frame. And the matrix's own <c>==</c> compares with a TOLERANCE, which is exactly wrong here: this
    /// answers "is this the same frozen state", not "is it close enough to look the same". A sub-tolerance move would
    /// otherwise be published as nothing, the applier would keep composing from the older transform, and successive small
    /// moves would drift without ever announcing themselves. Compared EXACTLY, on every field there is.</summary>
    public bool Equals(LayoutSnapshot other)
    {
        return ExactlySame(in LocalTransform, in other.LocalTransform)
               && RenderSize.Width == other.RenderSize.Width
               && RenderSize.Height == other.RenderSize.Height
               && ClipToBounds == other.ClipToBounds
               && IsMotionNode == other.IsMotionNode
               && Opacity == other.Opacity
               && SelfOpacity == other.SelfOpacity
               && ReferenceEquals(RenderParent, other.RenderParent);
    }

    // BY REFERENCE: a Matrix4x4F is 64 bytes, and this is asked once per re-frozen component per frame - passing two of
    // them by value copied 128 bytes to compare sixteen floats that usually differ in the first one.
    private static bool ExactlySame(in Matrix4x4F a, in Matrix4x4F b)
    {
        return a.M11 == b.M11 && a.M12 == b.M12 && a.M13 == b.M13 && a.M14 == b.M14
               && a.M21 == b.M21 && a.M22 == b.M22 && a.M23 == b.M23 && a.M24 == b.M24
               && a.M31 == b.M31 && a.M32 == b.M32 && a.M33 == b.M33 && a.M34 == b.M34
               && a.M41 == b.M41 && a.M42 == b.M42 && a.M43 == b.M43 && a.M44 == b.M44;
    }

    public override bool Equals(object obj) => obj is LayoutSnapshot other && Equals(other);

    public override int GetHashCode() => System.HashCode.Combine(RenderSize, ClipToBounds, IsMotionNode, Opacity, SelfOpacity, RenderParent);
}
