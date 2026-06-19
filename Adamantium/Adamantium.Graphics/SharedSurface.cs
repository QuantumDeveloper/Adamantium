using System;
using System.Runtime.InteropServices;
using Adamantium.Core;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Extensions;
using Adamantium.Imaging;
using Adamantium.Win32;
using Adamantium.Vulkan.Core;
using Adamantium.Vulkan.Windows;
using VulkanImage = Adamantium.Vulkan.Core.Image;

namespace Adamantium.Graphics;

/// <summary>
/// A GPU surface shareable across processes/APIs via an OS handle. Two ways in:
/// <list type="bullet">
/// <item><see cref="CreateExportable"/> — allocate a dedicated, externally-shareable image and export OS handles
/// for its memory and its produce/consume semaphores; hand the resulting <see cref="Descriptor"/> to a consumer.</item>
/// <item><see cref="Import"/> — given a producer's descriptor, allocate a local image bound zero-copy to the
/// imported memory and import the same semaphores, then sample it during compositing.</item>
/// </list>
/// The handle flavour follows the surface's <see cref="SharedHandleType"/> (the producer decides), not the
/// consumer's OS: Win32/D3D11/D3D12 are NT-handle transports, OpaqueFd is a POSIX fd (Linux, macOS via MoltenVK,
/// and possible on Windows too). Backed by <see cref="Texture"/> (image + view + memory), so it is an
/// <see cref="ITexture"/> usable with <c>DrawImage</c>. Owners free it explicitly via <see cref="IDisposable.Dispose"/>
/// from their lifecycle hook (e.g. a control's detach) — controls themselves stay non-disposable.
/// </summary>
public sealed unsafe class SharedSurface : Texture
{
    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private Semaphore _produceSemaphore;   // signaled by the producer when a frame is ready; consumer waits on it
    private Semaphore _consumeSemaphore;   // signaled by the consumer after sampling; producer waits before reuse

    // The exporter owns the handles it created and must close them; the importer borrows the producer's.
    private bool _ownsHandles;
    // Transport for this surface's handles: POSIX fd vs Win32 NT handle. Chosen from the handle TYPE (the producer
    // decides), not the consumer's OS — an OpaqueFd producer can exist on Windows, a D3D/Win32 producer is an NT handle.
    private bool _useFd;
    private IntPtr _memoryHandle;
    private IntPtr _produceHandle;
    private IntPtr _consumeHandle;

    public SharedSurfaceDescriptor Descriptor { get; private set; }

    /// <summary>Producer→consumer semaphore (frame ready). Wait on this before sampling the surface.</summary>
    public Semaphore ProduceSemaphore => _produceSemaphore;

    /// <summary>Consumer→producer semaphore (sampling done). Signal this after sampling so the producer can reuse.</summary>
    public Semaphore ConsumeSemaphore => _consumeSemaphore;

    /// <summary>Current value of the produce timeline (last frame the producer finished writing).</summary>
    public ulong ProduceValue => GetCounter(_produceSemaphore);

    /// <summary>Current value of the consume timeline (last frame the consumer finished latching).</summary>
    public ulong ConsumeValue => GetCounter(_consumeSemaphore);

    private ulong GetCounter(Semaphore semaphore)
    {
        if (semaphore == null) return 0;
        GraphicsDevice.LogicalDevice.GetSemaphoreCounterValue(semaphore, out var value);
        return value;
    }

    private SharedSurface(IGraphicsDevice device, TextureDescription description, object imagePNext,
        Func<VulkanImage, object> allocPNextFactory, string name)
        : base(device, description, imagePNext, allocPNextFactory, name)
    {
    }

    /// <summary>
    /// Allocates an exportable surface: a dedicated image whose memory and semaphores can be shared with another
    /// process/API. The returned <see cref="Descriptor"/> carries the OS handles to hand off to the consumer.
    /// <paramref name="handleType"/> defaults to the platform's opaque type (Win32 handle / POSIX fd).
    /// </summary>
    public static SharedSurface CreateExportable(IGraphicsDevice device, uint width, uint height, SurfaceFormat format,
        SharedHandleType? handleType = null, string name = "SharedSurface(export)")
    {
        var resolvedType = handleType ?? DefaultHandleType;
        var memHandleType = ToMemoryHandleType(resolvedType);
        var description = BuildDescription(width, height, format);

        var externalInfo = new ExternalMemoryImageCreateInfo { HandleTypes = memHandleType };
        // Exporting an image's memory requires a dedicated allocation that names the image.
        Func<VulkanImage, object> allocFactory = image => new ExportMemoryAllocateInfo
        {
            HandleTypes = memHandleType,
            PNext = new MemoryDedicatedAllocateInfo { Image = image }
        };

        var surface = new SharedSurface(device, description, externalInfo, allocFactory, name);
        surface._useFd = IsFdHandle(resolvedType);
        // TODO(Phase 4): producer/consumer ownership transfer (VK_QUEUE_FAMILY_EXTERNAL) on the shared queue.
        surface.TransitionImageLayout(description.DesiredImageLayout);

        var logical = surface.GraphicsDevice.LogicalDevice;
        surface._memoryHandle = surface.ExportMemoryHandle(memHandleType);
        logical.GetImageMemoryRequirements(surface.VulkanImage, out var req);

        surface._produceHandle = surface.CreateExportableSemaphore(out surface._produceSemaphore);
        surface._consumeHandle = surface.CreateExportableSemaphore(out surface._consumeSemaphore);
        surface._ownsHandles = true;

        surface.Descriptor = new SharedSurfaceDescriptor
        {
            HandleType = resolvedType,
            MemoryHandle = surface._memoryHandle,
            AllocationSize = req.Size,
            Width = width,
            Height = height,
            Format = format,
            ProduceSemaphoreHandle = surface._produceHandle,
            ConsumeSemaphoreHandle = surface._consumeHandle,
            BufferCount = 1
        };
        return surface;
    }

    /// <summary>
    /// Imports an externally produced surface zero-copy: allocates a local image bound to the producer's shared
    /// memory and imports its produce/consume semaphores. The image samples the producer's frames directly.
    /// </summary>
    public static SharedSurface Import(IGraphicsDevice device, SharedSurfaceDescriptor descriptor,
        string name = "SharedSurface(import)")
    {
        var memHandleType = ToMemoryHandleType(descriptor.HandleType);
        var useFd = IsFdHandle(descriptor.HandleType);
        var description = BuildDescription(descriptor.Width, descriptor.Height, descriptor.Format);

        var externalInfo = new ExternalMemoryImageCreateInfo { HandleTypes = memHandleType };
        // Import the producer's memory in place of a normal allocation; still a dedicated allocation for the image.
        // The transport (fd vs Win32 handle) follows the producer's handle type, not our OS.
        Func<VulkanImage, object> allocFactory = image => useFd
            ? (object)new ImportMemoryFdInfoKHR
            {
                HandleType = memHandleType,
                Fd = (int)descriptor.MemoryHandle,
                PNext = new MemoryDedicatedAllocateInfo { Image = image }
            }
            : new ImportMemoryWin32HandleInfoKHR
            {
                HandleType = memHandleType,
                Handle = (nuint)descriptor.MemoryHandle,
                PNext = new MemoryDedicatedAllocateInfo { Image = image }
            };

        var surface = new SharedSurface(device, description, externalInfo, allocFactory, name);
        surface._useFd = useFd;
        // TODO(Phase 4): acquire ownership from VK_QUEUE_FAMILY_EXTERNAL instead of a plain transition.
        surface.TransitionImageLayout(description.DesiredImageLayout);

        if (descriptor.ProduceSemaphoreHandle != IntPtr.Zero)
        {
            surface._produceSemaphore = surface.ImportSemaphore(descriptor.ProduceSemaphoreHandle);
        }
        if (descriptor.ConsumeSemaphoreHandle != IntPtr.Zero)
        {
            surface._consumeSemaphore = surface.ImportSemaphore(descriptor.ConsumeSemaphoreHandle);
        }

        surface._ownsHandles = false; // the producer owns and closes the handles
        surface.Descriptor = descriptor;
        return surface;
    }

    private IntPtr ExportMemoryHandle(ExternalMemoryHandleTypeFlagBits memHandleType)
    {
        var logical = GraphicsDevice.LogicalDevice;
        Result res;
        if (!_useFd)
        {
            var getInfo = new MemoryGetWin32HandleInfoKHR { Memory = ImageMemory, HandleType = memHandleType };
            res = logical.GetMemoryWin32HandleKHR(getInfo, out var handle);
            if (res != Result.Success) { Dispose(); throw new Exception($"failed to export memory win32 handle: {res}"); }
            return (nint)handle;
        }
        var getFdInfo = new MemoryGetFdInfoKHR { Memory = ImageMemory, HandleType = memHandleType };
        res = logical.GetMemoryFdKHR(getFdInfo, out var fd);
        if (res != Result.Success) { Dispose(); throw new Exception($"failed to export memory fd: {res}"); }
        return (nint)fd;
    }

    private IntPtr CreateExportableSemaphore(out Semaphore semaphore)
    {
        var logical = GraphicsDevice.LogicalDevice;
        var semHandleType = _useFd
            ? ExternalSemaphoreHandleTypeFlagBits.OpaqueFdBit
            : ExternalSemaphoreHandleTypeFlagBits.OpaqueWin32Bit;

        var info = new SemaphoreCreateInfo
        {
            // Timeline (monotonic) so producer/consumer can poll progress and order across queues without binary
            // ping-pong deadlocks. Exportable like a binary one (verified working + importable on this GPU).
            PNext = new ExportSemaphoreCreateInfo
            {
                HandleTypes = semHandleType,
                PNext = new SemaphoreTypeCreateInfo { SemaphoreType = SemaphoreType.Timeline, InitialValue = 0 }
            }
        };
        semaphore = logical.CreateSemaphore(info);

        Result res;
        if (!_useFd)
        {
            var getInfo = new SemaphoreGetWin32HandleInfoKHR { Semaphore = semaphore, HandleType = semHandleType };
            res = logical.GetSemaphoreWin32HandleKHR(getInfo, out var handle);
            if (res != Result.Success) throw new Exception($"failed to export semaphore win32 handle: {res}");
            return (nint)handle;
        }
        var getFdInfo = new SemaphoreGetFdInfoKHR { Semaphore = semaphore, HandleType = semHandleType };
        res = logical.GetSemaphoreFdKHR(getFdInfo, out var fd);
        if (res != Result.Success) throw new Exception($"failed to export semaphore fd: {res}");
        return (nint)fd;
    }

    private Semaphore ImportSemaphore(IntPtr handle)
    {
        var logical = GraphicsDevice.LogicalDevice;
        // Created as timeline before importing the producer's timeline payload.
        var semaphore = logical.CreateSemaphore(new SemaphoreCreateInfo
        {
            PNext = new SemaphoreTypeCreateInfo { SemaphoreType = SemaphoreType.Timeline, InitialValue = 0 }
        });
        Result res;
        if (!_useFd)
        {
            res = logical.ImportSemaphoreWin32HandleKHR(new ImportSemaphoreWin32HandleInfoKHR
            {
                Semaphore = semaphore,
                HandleType = ExternalSemaphoreHandleTypeFlagBits.OpaqueWin32Bit,
                Handle = (nuint)handle
            });
        }
        else
        {
            res = logical.ImportSemaphoreFdKHR(new ImportSemaphoreFdInfoKHR
            {
                Semaphore = semaphore,
                HandleType = ExternalSemaphoreHandleTypeFlagBits.OpaqueFdBit,
                Fd = (int)handle
            });
        }
        if (res != Result.Success)
        {
            logical.DestroySemaphore(semaphore);
            throw new Exception($"failed to import semaphore handle: {res}");
        }
        return semaphore;
    }

    private static SharedHandleType DefaultHandleType => IsWindows ? SharedHandleType.OpaqueWin32 : SharedHandleType.OpaqueFd;

    // Only the opaque-fd type uses the POSIX fd transport; Win32/D3D11/D3D12 are all NT-handle (Win32) transports.
    private static bool IsFdHandle(SharedHandleType type) => type == SharedHandleType.OpaqueFd;

    private static ExternalMemoryHandleTypeFlagBits ToMemoryHandleType(SharedHandleType type) => type switch
    {
        SharedHandleType.OpaqueWin32 => ExternalMemoryHandleTypeFlagBits.OpaqueWin32Bit,
        SharedHandleType.OpaqueFd => ExternalMemoryHandleTypeFlagBits.OpaqueFdBit,
        SharedHandleType.D3D11Texture => ExternalMemoryHandleTypeFlagBits.D3d11TextureBit,
        SharedHandleType.D3D12Resource => ExternalMemoryHandleTypeFlagBits.D3d12ResourceBit,
        _ => ExternalMemoryHandleTypeFlagBits.OpaqueWin32Bit
    };

    private static TextureDescription BuildDescription(uint width, uint height, SurfaceFormat format) => new()
    {
        Dimension = TextureDimension.Texture2D,
        ImageType = ImageType._2d,
        Width = width,
        Height = height,
        Depth = 1,
        ArrayLayers = 1,
        MipLevels = 1,
        Format = format,
        Samples = MSAALevel.None,
        // Producer copies its resolved frame INTO this surface (TransferDst, then transitions it to ShaderReadOnly);
        // consumer samples it directly. Producer and consumer are different queues of the SAME graphics family, so a
        // semaphore (the Produce/Consume timeline) is enough to order the layout transition + write vs the sample (no
        // queue-family ownership transfer needed).
        Usage = ImageUsageFlagBits.SampledBit | ImageUsageFlagBits.TransferDstBit,
        SharingMode = SharingMode.Exclusive,
        ImageTiling = ImageTiling.Optimal,
        InitialLayout = ImageLayout.Undefined,
        DesiredImageLayout = ImageLayout.ShaderReadOnlyOptimal,
        ImageAspect = ImageAspectFlagBits.ColorBit
    };

    private void CloseSharedHandle(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;
        // POSIX fd vs Win32 NT handle, per this surface's transport. The unused platform's library is never resolved.
        if (_useFd) PosixInterop.Close((int)handle);
        else Win32Interop.CloseHandle(handle);
    }

    protected override void Dispose(bool disposeManaged)
    {
        var logical = GraphicsDevice.LogicalDevice;
        if (_produceSemaphore != null) { logical.DestroySemaphore(_produceSemaphore); _produceSemaphore = null; }
        if (_consumeSemaphore != null) { logical.DestroySemaphore(_consumeSemaphore); _consumeSemaphore = null; }

        // Close the handles we exported; the importer borrows the producer's and must not close them. Null each
        // out after closing so a second pass can never double-close (the zero check would otherwise re-enter).
        if (_ownsHandles)
        {
            CloseSharedHandle(_memoryHandle); _memoryHandle = IntPtr.Zero;
            CloseSharedHandle(_produceHandle); _produceHandle = IntPtr.Zero;
            CloseSharedHandle(_consumeHandle); _consumeHandle = IntPtr.Zero;
        }

        // Frees the image, view and (imported or exported) memory.
        base.Dispose(disposeManaged);
    }
}
