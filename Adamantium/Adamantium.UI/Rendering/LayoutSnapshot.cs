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
    float selfOpacity = 1f)
{
    public Matrix4x4F LocalTransform { get; } = localTransform;
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
}
