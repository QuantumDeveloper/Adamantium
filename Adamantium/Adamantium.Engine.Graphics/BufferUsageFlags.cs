using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics
{
    [Flags]
    public enum BufferUsageFlags
    {
        TransferSrc = 1,

        TransferDst = 2,

        UniformTexelBuffer = 4,

        StorageTexelBuffer = 8,

        UniformBuffer = 16,

        StorageBuffer = 32,

        IndexBuffer = 64,

        VertexBuffer = 128,

        IndirectBuffer = 256,

        ConditionalRenderingExt = 512,

        RayTracingNv = 1024,

        TransformFeedbackBufferExt = 2048,

        TransformFeedbackCounterBufferExt = 4096,

        ShaderDeviceAddressExt = 131072,
    }
}
