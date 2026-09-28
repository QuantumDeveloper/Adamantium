using Adamantium.Vulkan.Core;

namespace Adamantium.Engine.Rendering;

// Per-object render state applied to the device just before a draw. Kept separate from RenderGeometry (which is shared
// per mesh+format) because two entities can share the same geometry yet draw it with different cull/depth/wireframe/
// topology. Built by the caller from the entity's MeshData.
public readonly struct RenderState
{
    public RenderState(
        bool wireFrame,
        CullModeFlagBits cullMode,
        bool depthTest,
        bool depthWrite,
        PrimitiveTopology? topologyOverride)
    {
        WireFrame = wireFrame;
        CullMode = cullMode;
        DepthTest = depthTest;
        DepthWrite = depthWrite;
        TopologyOverride = topologyOverride;
    }

    public bool WireFrame { get; }
    public CullModeFlagBits CullMode { get; }
    public bool DepthTest { get; }
    public bool DepthWrite { get; }
    public PrimitiveTopology? TopologyOverride { get; }

    public static readonly RenderState Default = new(false, CullModeFlagBits.None, true, true, null);
}
