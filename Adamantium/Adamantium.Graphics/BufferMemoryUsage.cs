using Adamantium.Graphics.Core;

namespace Adamantium.Graphics;

/// <summary>
/// Declares what a buffer is FOR, by intent, so you never reason about raw Vulkan <see cref="MemoryPropertyFlags"/>
/// (DEVICE_LOCAL / HOST_VISIBLE / HOST_CACHED) by hand. Each value documents which physical pool the buffer lands in:
/// <list type="bullet">
/// <item><b>VRAM</b> — the big on-card video memory (≈8 GB). Fastest for the GPU, but the CPU cannot touch it directly.</item>
/// <item><b>BAR window</b> — a small slice of VRAM the CPU CAN write through the PCIe BAR (≈214 MB without Resizable
/// BAR). VRAM-fast for the GPU and CPU-writable, but scarce — keep what goes here small.</item>
/// <item><b>system RAM</b> — ordinary RAM. Huge and CPU-accessible, but the GPU reads it across the (slower) PCIe bus.</item>
/// </list>
/// Implicitly converts to <see cref="MemoryPropertyFlags"/>, so it can be passed anywhere a <c>Buffer</c> factory
/// takes memory flags.
/// </summary>
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

    /// <summary>
    /// Lives entirely on the GPU — only the GPU touches it, the CPU never maps it. Covers both data the CPU uploads
    /// once and the GPU then reads many times (static meshes, index buffers, baked tables) AND data the GPU itself
    /// produces and consumes (compute storage / transform-feedback output). What the buffer is FOR is expressed by
    /// its usage flags; this value only says "GPU-side memory".
    /// <para><b>Lives in VRAM</b> (the big ≈8 GB pool) for maximum GPU speed. Not CPU-mappable: any initial CPU data
    /// is uploaded through a temporary staging buffer, and <c>SetData</c> also goes through staging — so prefer it
    /// for data the CPU writes rarely or never (data the GPU writes itself has no CPU upload cost at all).</para>
    /// </summary>
    public static readonly BufferMemoryUsage GpuOnly =
        new(MemoryPropertyFlags.DeviceLocal, nameof(GpuOnly));

    /// <summary>
    /// SMALL data the CPU rewrites frequently (typically every frame) and the GPU reads the same frame. Examples:
    /// per-frame uniform/constant buffers, sprite-batch vertices, dynamic/animated vertex buffers, descriptor data.
    /// <para><b>Lives in the BAR window</b> (CPU-visible VRAM): the CPU writes straight into it (no staging copy)
    /// and the GPU still reads at VRAM speed. The catch is the window is SMALL (≈214 MB without Resizable BAR), so
    /// only put small, frequently-updated buffers here — large data belongs in <see cref="StreamFromCpuToGpu"/>.</para>
    /// </summary>
    public static readonly BufferMemoryUsage UploadFromCpuToGpu =
        new(MemoryPropertyFlags.DeviceLocal | MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.HostCoherent,
            nameof(UploadFromCpuToGpu));

    /// <summary>
    /// LARGE data the CPU continuously feeds to the GPU — true streaming: geometry/textures paged in on the fly,
    /// big dynamic vertex/particle pools, anything too big for the small BAR window. The CPU writes it (often every
    /// frame, in bulk) and the GPU reads it.
    /// <para><b>Lives in system RAM</b>: plentiful, CPU-mappable, so multi-hundred-MB streams fit easily; the
    /// trade-off is the GPU reads it across PCIe (slower than VRAM). Use this when CAPACITY matters more than GPU
    /// read latency. (For small, latency-critical per-frame data use <see cref="UploadFromCpuToGpu"/> instead — that
    /// one is VRAM-fast but capped by the BAR window.)</para>
    /// </summary>
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
