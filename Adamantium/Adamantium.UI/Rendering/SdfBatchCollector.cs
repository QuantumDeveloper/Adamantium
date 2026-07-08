using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Mathematics;
using Adamantium.UI.Effects.Generated;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Rendering;

// Shared base for the SDF instancing family (rounded-rect + ellipse): both collect same-clip SOLID fills (each with an
// optional analytic stroke) baked to WORLD space into ONE instanced draw whose pixel shader reconstructs the shape from a
// signed-distance field (self-anti-aliasing, no tessellation, no AA fringe). They differ ONLY in the per-shape bake
// (TryAdd/CanBatch) and which draw pass runs; the instancing machinery - segment buffers, the storage-vs-vertex draw
// plumbing, blend/depth state - lives here once. Rects and ellipses stay SEPARATE instances/passes (different SDF pixel
// shaders); this just unifies the code.
internal abstract class SdfBatchCollector<TItem> : BatchCollector<TItem> where TItem : struct
{
    protected BatchEffect Effect;

    protected SdfBatchCollector(int initialCapacity) : base(initialCapacity) { }

    // Per-instance data lives in a BDA storage buffer (read in the vertex shader by SV_InstanceID) rather than a
    // per-instance vertex buffer, so it can be retained + patched incrementally. RectBatchCollector owns the master toggle.
    protected override bool UsesStorageBuffer => RectBatchCollector.UseStorageInstancing;

    protected override void OnBeginFrame(IGraphicsDevice device) => Effect ??= new BatchEffect(device);

    // The two SDF draw passes for this shape: the storage-instanced form (per-instance TItem from the buffer's device
    // address) and the vertex-buffer fallback (TItem bound as per-instance vertex attributes).
    protected abstract IEffectPass StorageDrawPass { get; }
    protected abstract IEffectPass VertexDrawPass { get; }

    // Straight-alpha AlphaBlend (matches solid fills); depth like the other main-pass units (Always, test+write).
    protected override void DrawSegment(IGraphicsDevice device, Buffer<TItem> buffer, uint count, uint firstInstance, Matrix4x4F projection)
    {
        device.ColorBlendEnabled = true;
        device.ColorBlendEquation = ColorBlendEquations.AlphaBlend;
        device.PrimitiveRestartEnable = true;
        device.DepthTestEnabled = true;
        device.DepthWriteEnable = true;
        device.DepthCompareFunction = CompareOp.Always;
        Effect.Projection.SetValue(projection);
        device.PrimitiveTopology = PrimitiveTopology.TriangleStrip;

        if (UsesStorageBuffer)
        {
            // Quad from SV_VertexID, per-instance TItem from the buffer's device address (no vertex buffer). Offset the
            // address by firstInstance + Draw at 0 so items[0..count-1] reads THIS segment regardless of SV_InstanceID's
            // base (whether it includes firstInstance is translation-defined).
            device.VertexType = null;
            var stride = (ulong)System.Runtime.InteropServices.Marshal.SizeOf<TItem>();
            Effect.InstancesAddress.SetValue(buffer.GetDeviceAddress() + firstInstance * stride);
            StorageDrawPass.Apply();
            device.Draw(4, count, 0, 0);
        }
        else
        {
            device.VertexType = typeof(TItem);
            device.SetVertexBuffer(buffer);
            VertexDrawPass.Apply();
            device.Draw(4, count, 0, firstInstance);
        }
    }
}
