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

        ShaderDeviceAddress = 131072,

        VideoDecodeSrcKhr = 8192,

        VideoDecodeDstKhr = 16384,

        TransformFeedbackBufferExt = 2048,

        TransformFeedbackCounterBufferExt = 4096,

        ConditionalRenderingExt = 512,

        AccelerationStructureBuildInputReadOnlyKhr = 524288,

        AccelerationStructureStorageKhr = 1048576,

        ShaderBindingTableKhr = 1024,

        VideoEncodeDstKhr = 32768,

        VideoEncodeSrcKhr = 65536,

        SamplerDescriptorBufferExt = 2097152,

        ResourceDescriptorBufferExt = 4194304,

        PushDescriptorsDescriptorBufferExt = 67108864,

        MicromapBuildInputReadOnlyExt = 8388608,

        MicromapStorageExt = 16777216,

        FlagsMaxEnum = 2147483647,
    }
}
