using Adamantium.Graphics.Core;

namespace Adamantium.Graphics;

/// <summary>What a buffer is for, by intent, instead of raw <see cref="MemoryPropertyFlags"/>: each value picks VRAM, the
/// small CPU-writable BAR window, or system RAM. Converts implicitly to <see cref="MemoryPropertyFlags"/>.</summary>
public readonly struct BufferMemoryUsage
{
    /// <summary>The Vulkan memory properties this usage maps to.</summary>
    public MemoryPropertyFlags Flags { get; }

    private readonly string _name;

    private BufferMemoryUsage(MemoryPropertyFlags flags, string name)
    {
        Flags = flags;
        _name = name;
    }

    /// <summary>VRAM the CPU never maps: static meshes, baked tables, GPU-produced data. CPU writes go through a staging
    /// buffer, so use it for data the CPU writes rarely or never.</summary>
    public static readonly BufferMemoryUsage GpuOnly =
        new(MemoryPropertyFlags.DeviceLocal, nameof(GpuOnly));

    /// <summary>Small data the CPU rewrites every frame, in the CPU-writable BAR window of VRAM. The window is small (~214 MB
    /// without Resizable BAR); large data belongs in <see cref="StreamFromCpuToGpu"/>.</summary>
    public static readonly BufferMemoryUsage UploadFromCpuToGpu =
        new(MemoryPropertyFlags.DeviceLocal | MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.HostCoherent,
            nameof(UploadFromCpuToGpu));

    /// <summary>Large data the CPU streams to the GPU, in system RAM: plentiful, but the GPU reads it across PCIe. For small
    /// per-frame data use <see cref="UploadFromCpuToGpu"/>.</summary>
    public static readonly BufferMemoryUsage StreamFromCpuToGpu =
        new(MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.HostCoherent, nameof(StreamFromCpuToGpu));

    /// <summary>
    /// Data the GPU PRODUCES that the CPU then needs to read back. Examples: screenshots / <c>Texture.Save</c>,
    /// compute-shader output, query/occlusion results.
    /// <para><b>Lives in system RAM, host-cached</b> — the cache makes the CPU-side read fast (host-cached memory is
    /// optimized for CPU reads, unlike the write-combined memory used for uploads).</para>
    /// </summary>
    public static readonly BufferMemoryUsage ReadFromGpu =
        new(MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.HostCoherent | MemoryPropertyFlags.HostCached,
            nameof(ReadFromGpu));

    public static implicit operator MemoryPropertyFlags(BufferMemoryUsage usage) => usage.Flags;

    public override string ToString() => _name;
}
