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

    /// <summary>Device address of the owning cache's <see cref="TransformTable"/> - the SDF vertex shaders fetch each
    /// instance's world matrix from it by the instance's slot index. Set by RenderCache every frame BEFORE any draw
    /// (the shader always reads it; slot 0 is identity, so legacy world-baked instances render unchanged).</summary>
    public ulong TransformsAddress { get; set; }

    protected SdfBatchCollector(int initialCapacity) : base(initialCapacity) { }

    protected override void OnBeginFrame(IGraphicsDevice device) => Effect ??= new BatchEffect(device);

    // The SDF draw pass for this shape (per-instance TItem read from the buffer's device address by SV_InstanceID).
    protected abstract IEffectPass DrawPass { get; }

    // Straight-alpha AlphaBlend (matches solid fills); depth like the other main-pass units (Always, test+write). The quad
    // comes from SV_VertexID and the per-instance TItem from the buffer's device address (no vertex buffer). The address
    // is offset by firstInstance and drawn at base 0 so items[0..count-1] read THIS segment regardless of SV_InstanceID's
    // base (whether it includes firstInstance is translation-defined).
    protected override void DrawSegment(IGraphicsDevice device, Buffer<TItem> buffer, uint count, uint firstInstance, Matrix4x4F projection)
    {
        device.ColorBlendEnabled = true;
        device.ColorBlendEquation = ColorBlendEquations.AlphaBlend;
        device.PrimitiveRestartEnable = true;
        device.DepthTestEnabled = true;
        device.DepthWriteEnable = true;
        device.DepthCompareFunction = CompareOp.Always;
        Effect.Projection.SetValue(projection);
        // The SDF shapes measure themselves in DEVICE pixels (SlotPixelScale), which needs the render target's pixel
        // size; without it the shader falls back to the raw slot space, where an anisotropically scaled slot smears the
        // shape's edge along its long axis.
        var vp = ((GraphicsDevice)device).CurrentViewports;
        if (vp is { Length: > 0 }) Effect.ViewportSize.SetValue(new Vector2F(vp[0].Width, vp[0].Height));
        device.PrimitiveTopology = PrimitiveTopology.TriangleStrip;

        device.VertexType = null;
        var stride = (ulong)System.Runtime.InteropServices.Marshal.SizeOf<TItem>();
        Effect.InstancesAddress.SetValue(buffer.GetDeviceAddress() + firstInstance * stride);
        Effect.TransformsAddress.SetValue(TransformsAddress);
        DrawPass.Apply();
        device.Draw(4, count, 0, 0);
    }
}
