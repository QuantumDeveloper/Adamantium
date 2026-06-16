using System;
using Adamantium.Imaging;

namespace Adamantium.Graphics.Core;

/// <summary>
/// How the shared memory/semaphore handles were produced. The producer (a game engine, possibly on another
/// API) picks this; the importing side maps it to the matching Vulkan external-handle type.
/// </summary>
public enum SharedHandleType
{
    /// <summary>Opaque NT handle, Windows (VK_EXTERNAL_MEMORY_HANDLE_TYPE_OPAQUE_WIN32_BIT). Also the handoff
    /// type for an OpenGL consumer/producer on Windows (GL_EXT_memory_object_win32).</summary>
    OpaqueWin32,
    /// <summary>Opaque POSIX file descriptor, Linux/macOS-via-MoltenVK (VK_EXTERNAL_MEMORY_HANDLE_TYPE_OPAQUE_FD_BIT).
    /// Also the handoff type for an OpenGL consumer/producer on those platforms (GL_EXT_memory_object_fd).</summary>
    OpaqueFd,
    /// <summary>A D3D11 texture's shared NT handle, Windows (VK_EXTERNAL_MEMORY_HANDLE_TYPE_D3D11_TEXTURE_BIT).</summary>
    D3D11Texture,
    /// <summary>A D3D12 resource's shared NT handle, Windows (VK_EXTERNAL_MEMORY_HANDLE_TYPE_D3D12_RESOURCE_BIT).</summary>
    D3D12Resource
    // Native Metal (MTLTexture) interop would need VK_EXT_metal_objects, which the binding does not currently
    // generate; on macOS use OpaqueFd (MoltenVK) until that extension is added to the generator.
}

/// <summary>
/// The cross-API contract describing an externally produced surface so it can be imported zero-copy. The
/// producer fills it in after exporting its image memory and synchronization primitives; the consumer (e.g.
/// <c>RenderTargetPanel</c>) imports the surface from it and samples the frame during compositing. Lives in
/// Graphics.Core so both the UI layer (which only sees the graphics interfaces) and the Vulkan implementation
/// can pass it across the <see cref="IGraphicsDevice"/> boundary.
/// </summary>
public sealed class SharedSurfaceDescriptor
{
    /// <summary>The kind of handle in <see cref="MemoryHandle"/> (decides the Vulkan handle type on import).</summary>
    public SharedHandleType HandleType { get; set; } = SharedHandleType.OpaqueWin32;

    /// <summary>Shared handle to the surface's device memory, exported by the producer (NT handle or POSIX fd).</summary>
    public IntPtr MemoryHandle { get; set; }

    /// <summary>Size in bytes of the exported allocation (used to validate the import against local requirements).</summary>
    public ulong AllocationSize { get; set; }

    public uint Width { get; set; }
    public uint Height { get; set; }
    public SurfaceFormat Format { get; set; }

    /// <summary>Producer→consumer semaphore (signaled when a frame is ready); imported and waited on before sampling.</summary>
    public IntPtr ProduceSemaphoreHandle { get; set; }

    /// <summary>Consumer→producer semaphore (signaled after sampling); lets the producer reuse the surface.</summary>
    public IntPtr ConsumeSemaphoreHandle { get; set; }

    /// <summary>Number of surfaces the producer rotates through (1 = single-buffered).</summary>
    public uint BufferCount { get; set; } = 1;
}
