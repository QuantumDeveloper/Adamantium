using System;

namespace Adamantium.Graphics.Core
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

        VideoDecodeSrcKhr = 8192,

        VideoDecodeDstKhr = 16384,

        TransformFeedbackBufferExt = 2048,

        TransformFeedbackCounterBufferExt = 4096,

        ConditionalRenderingExt = 512,

        ExecutionGraphScratchAmdx = 33554432,

        DescriptorHeapExt = 268435456,

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

        TileMemoryQcom = 134217728,

        ShaderDeviceAddress = 131072,
    }
}
