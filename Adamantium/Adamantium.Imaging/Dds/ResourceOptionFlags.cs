using System;

namespace Adamantium.Imaging.Dds
{
    [Flags]
    public enum ResourceOptionFlags
    {
        None = 0,
        GenerateMipMaps = 1,
        Shared = 2,
        TextureCube = 4,
        DrawIndirectArguments = 16,
        BufferAllowRawViews = 32,
        BufferStructured = 64,
        ResourceClamp = 128,
        SharedKeyedmutex = 256,
        GdiCompatible = 512,
        SharedNthandle = 2048,
        RestrictedContent = 4096,
        RestrictSharedResource = 8192,
        RestrictSharedResourceDriver = 16384,
        Guarded = 32768,
        TilePool = 131072,
        Tiled = 262144,
        HwProtected = 524288
    }
}
