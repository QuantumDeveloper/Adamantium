using System;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;

namespace Adamantium.UI.Rendering;

// Hands out reusable GPU buffer allocations to a renderer's render units (the "buffer manager" of
// GPU_BUFFER_REUSE_PLAN §3). Instead of allocating a fresh Vulkan buffer whenever a control's geometry changes (the
// per-frame churn that tanks resize/animation FPS), a render component rents a <see cref="ReusableBuffer"/> here: a
// frames-in-flight ring with a high-water-mark capacity, so steady-state animation rewrites in place with zero
// allocation. One per renderer (created by RenderUnitFactory); a lightweight context - the rented handles are owned and
// disposed by the components that hold them. (Cross-unit free-list reuse on Return is a later step of the plan.)
public sealed class GpuBufferManager
{
    private readonly GraphicsDevice _device;
    private readonly uint _ringSize;

    public GpuBufferManager(IGraphicsDevice device)
    {
        _device = (GraphicsDevice)device;
        // One buffer per in-flight frame so the slot written this frame is never the one a previous frame still reads.
        _ringSize = Math.Max(1u, _device.MaxFramesInFlight);
    }

    internal GraphicsDevice Device => _device;
    internal uint RingSize => _ringSize;
    internal uint CurrentFrame => _device.CurrentFrame;

    public ReusableBuffer CreateBuffer(BufferUsageFlags usage, MemoryPropertyFlags memory)
        => new(this, usage, memory);
}
