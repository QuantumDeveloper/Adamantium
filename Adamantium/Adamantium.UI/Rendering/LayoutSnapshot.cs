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
    IUIComponent visualParent)
{
    public Matrix4x4F LocalTransform { get; } = localTransform;
    public Size RenderSize { get; } = renderSize;
    public bool ClipToBounds { get; } = clipToBounds;
    public bool IsMotionNode { get; } = isMotionNode;
    public IUIComponent VisualParent { get; } = visualParent;
}
