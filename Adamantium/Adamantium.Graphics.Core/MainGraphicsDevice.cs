using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Adamantium.Core;
using Adamantium.Graphics.Core.Extensions;
using Adamantium.Vulkan.Core;
using QuantumBinding.Utils;
using Serilog;

namespace Adamantium.Graphics.Core
{
    public class MainGraphicsDevice : DisposableBase
    {
        private uint _availableGraphicsQueueIndex;

        private uint _availableComputeQueueIndex;

        private uint _availableTransferQueueIndex;

        private readonly List<IGraphicsDevice> graphicsDevices;

        private readonly Dictionary<Guid, IGraphicsDevice> deviceMap;
        private Mutex _submissionSync;

        public uint BuffersCount { get; }

        public bool EnableDynamicRendering { get; }

        /// <summary>True when VK_EXT_device_fault was enabled, so a device-lost can be queried for its real cause.</summary>
        public bool DeviceFaultSupported { get; private set; }
        public VulkanInstance VulkanInstance { get; private set; }

        public GraphicsAdapter GraphicsAdapter { get; private set; }

        // The resource-loader device and the global descriptor heap belong to the main (logical) device for its
        // whole lifetime, so the main device creates and owns them itself (see ctor) — callers only read the getters.
        public IGraphicsDevice ResourceLoaderDevice { get; private set; }

        // Deferred disposal, for EVERY GPU resource of this logical device. It lives here rather than per device wrapper
        // because the wrappers share one VkDevice and freely share resources: a texture is made by the resource loader,
        // which draws no frames at all, and sampled by any window that paints with it. A per-wrapper queue can only
        // promise that ITS OWN frames finished, and freeing an image a moment too early is not a wrong pixel - the GPU
        // reads a recycled address and the device is lost.
        // So a resource is held until EVERY drawing wrapper has begun enough frames to have retired whatever was in
        // flight when it was handed over. A window that stops drawing (minimised) simply delays that - it is alive and
        // will paint again, so its resources must stay. Only DESTROYING a wrapper drops its vote.
        private readonly List<(IDisposable Resource, IGraphicsDevice[] Devices, ulong[] Due)> _retired = [];
        private readonly object _retireSync = new();

        /// <summary>Hand a GPU resource over for disposal once no submitted work can still reference it.</summary>
        public void RetireResource(IDisposable resource)
        {
            if (resource == null)
            {
                return;
            }

            lock (_retireSync)
            {
                var devices = graphicsDevices.ToArray();
                var due = new ulong[devices.Length];
                for (var i = 0; i < devices.Length; i++)
                {
                    // Its frame in flight NOW is retired once this many more frames have begun - each begin waits on the
                    // fence of the frame MaxFramesInFlight back.
                    due[i] = devices[i].FrameTicket + devices[i].MaxFramesInFlight + 1;
                }

                _retired.Add((resource, devices, due));
            }
        }

        /// <summary>Called by a drawing wrapper as it begins a frame, once its own fence wait has returned.</summary>
        public void OnFrameStarted()
        {
            List<IDisposable> due = null;
            lock (_retireSync)
            {
                for (var i = _retired.Count - 1; i >= 0; i--)
                {
                    if (!IsDue(_retired[i].Devices, _retired[i].Due))
                    {
                        continue;
                    }

                    (due ??= []).Add(_retired[i].Resource);
                    _retired.RemoveAt(i);
                }
            }

            if (due == null)
            {
                return;
            }

            foreach (var resource in due)
            {
                resource.Dispose();
            }
        }

        /// <summary>Dispose everything retired, WITHOUT waiting for frames to retire it. The caller must have waited the
        /// device idle first - idle proves no submitted work can reference any of it, which is a stronger guarantee than
        /// the frame tickets give. For teardown and for anything that stops drawing: the ordinary path frees a resource
        /// only when a wrapper BEGINS another frame, so whatever is retired after the last frame is never freed at all.
        /// A test run pays for that directly - each fixture's targets accumulate until the device runs out of memory.</summary>
        public void FlushRetiredAfterIdle()
        {
            List<IDisposable> due;
            lock (_retireSync)
            {
                if (_retired.Count == 0)
                {
                    return;
                }

                due = _retired.Select(r => r.Resource).ToList();
                _retired.Clear();
            }

            foreach (var resource in due)
            {
                resource.Dispose();
            }
        }

        // A wrapper that is gone cannot be executing anything, so its vote is dropped rather than waited on for ever.
        private bool IsDue(IGraphicsDevice[] devices, ulong[] due)
        {
            for (var i = 0; i < devices.Length; i++)
            {
                if (graphicsDevices.Contains(devices[i]) && devices[i].FrameTicket < due[i])
                {
                    return false;
                }
            }

            return true;
        }

        // One shared heap per logical device, used by every render-device wrapper (they share one VkDevice).
        public IDescriptorHeapManager DescriptorHeapManager { get; private set; }

        // One shared device-memory allocator per logical device (same reason as the heap). Render devices reach it via
        // this and sub-allocate from its blocks, so no render device grabs its own big host-visible BAR block. Created
        // lazily on first use (once ResourceLoaderDevice exists) but OWNED here - a render device never creates one.
        private IDeviceMemoryAllocator _memoryAllocator;
        public IDeviceMemoryAllocator MemoryAllocator => _memoryAllocator ??= _graphicsDeviceFactory.CreateMemoryAllocator(ResourceLoaderDevice);

        public Device LogicalDevice { get; private set; }

        public uint AvailableQueuesCount { get; private set; }

        public QueueFamilyContainer QueueFamilyContainer { get; private set; }

        public static ReadOnlyCollection<string> DeviceExtensions { get; private set; }

        // Optional (best-effort) extensions: enabled if the device supports them, never required. See CreateLogicalDevice.
        public static ReadOnlyCollection<string> OptionalDeviceExtensions { get; private set; }

        /// <summary>Device extensions this device actually enabled - the required ones plus whichever optional ones the
        /// driver turned out to support. Ask this, never the wish list.</summary>
        public IReadOnlySet<string> EnabledDeviceExtensions { get; private set; } = new HashSet<string>();

        public bool IsExtensionEnabled(string extension) => EnabledDeviceExtensions.Contains(extension);

        /// <summary>A present fence per present, present-mode changes without recreating the swapchain, and defined
        /// scaling on resize. Advertised as KHR after promotion, as EXT before it - either will do.</summary>
        public bool SupportsSwapchainMaintenance =>
            IsExtensionEnabled(Constants.VK_KHR_SWAPCHAIN_MAINTENANCE_1_EXTENSION_NAME)
            || IsExtensionEnabled(Constants.VK_EXT_SWAPCHAIN_MAINTENANCE_1_EXTENSION_NAME);

        /// <summary>Wait for a NAMED present instead of for an image to come free - explicit frame pacing.</summary>
        public bool SupportsPresentWait =>
            IsExtensionEnabled(Constants.VK_KHR_PRESENT_WAIT_EXTENSION_NAME)
            && IsExtensionEnabled(Constants.VK_KHR_PRESENT_ID_EXTENSION_NAME);

        /// <summary>Present only the rectangles that changed.</summary>
        public bool SupportsIncrementalPresent =>
            IsExtensionEnabled(Constants.VK_KHR_INCREMENTAL_PRESENT_EXTENSION_NAME);

        internal Fence[] InFlightFences { get; private set; }

        public uint CurrentFrame { get; private set; }

        public Dictionary<uint, Queue> UsedGraphicsQueues { get; }

        public IReadOnlyList<IGraphicsDevice> GraphicsDevices => graphicsDevices.AsReadOnly();

        private IGraphicsDeviceFactory _graphicsDeviceFactory;

        static MainGraphicsDevice()
        {
            var deviceExt = new List<string>();
            deviceExt.Add(Constants.VK_KHR_SWAPCHAIN_EXTENSION_NAME);
            deviceExt.Add(Constants.VK_KHR_MAINTENANCE_4_EXTENSION_NAME);
            deviceExt.Add(Constants.VK_GOOGLE_HLSL_FUNCTIONALITY_1_EXTENSION_NAME);
            deviceExt.Add(Constants.VK_GOOGLE_USER_TYPE_EXTENSION_NAME);
            deviceExt.Add(Constants.VK_KHR_DYNAMIC_RENDERING_EXTENSION_NAME);
            deviceExt.Add(Constants.VK_EXT_SHADER_OBJECT_EXTENSION_NAME);
            deviceExt.Add(Constants.VK_EXT_DESCRIPTOR_BUFFER_EXTENSION_NAME);
            deviceExt.Add(Constants.VK_KHR_BUFFER_DEVICE_ADDRESS_EXTENSION_NAME);
            deviceExt.Add(Constants.VK_KHR_SYNCHRONIZATION_2_EXTENSION_NAME);
            deviceExt.Add(Constants.VK_EXT_DESCRIPTOR_HEAP_EXTENSION_NAME);
            DeviceExtensions = new ReadOnlyCollection<string>(deviceExt);

            // Cross-API shared-surface interop is OPTIONAL: enable every external-memory/semaphore transport the
            // driver actually supports — both Win32 NT handles AND POSIX fds — instead of hard-coding one per OS.
            // The producer dictates the handle flavour, not the consumer's OS: a D3D11/D3D12/GL producer on Windows
            // hands an NT handle, a GL/dma-buf producer hands an fd, and an fd producer can exist on Windows too.
            // These are NOT required — a machine without them still runs, it just can't import external surfaces;
            // the optional pass in CreateLogicalDevice adds the supported ones without throwing.
            OptionalDeviceExtensions = new ReadOnlyCollection<string>(new List<string>
            {
                Constants.VK_KHR_EXTERNAL_MEMORY_WIN32_EXTENSION_NAME,
                Constants.VK_KHR_EXTERNAL_SEMAPHORE_WIN32_EXTENSION_NAME,
                Constants.VK_KHR_EXTERNAL_MEMORY_FD_EXTENSION_NAME,
                Constants.VK_KHR_EXTERNAL_SEMAPHORE_FD_EXTENSION_NAME,
                // After a device-lost (VK_ERROR_DEVICE_LOST), lets us query the REAL GPU fault (description + faulting
                // address regions) instead of guessing - the diagnostic for the designer's render-time device loss.
                Constants.VK_EXT_DEVICE_FAULT_EXTENSION_NAME,

                // Presentation, asked for in the PROMOTED KHR form first and the original EXT form as the fallback - the
                // pair was promoted, and a driver may advertise either. What they buy, measured on this engine: the frame
                // loop blocks synchronously in AcquireNextImage (0.6-0.8 ms a frame, with the GPU fence wait at 0.00),
                // and with update and render on one thread that back-pressure paces the WHOLE engine. swapchain
                // maintenance gives a fence per present (know when an image is free instead of blocking to find out),
                // present-mode changes WITHOUT recreating the swapchain (the vsync setting stops being a teardown), and
                // defined scaling on resize. surface maintenance answers "how many images does THIS present mode need",
                // which is currently a hardcoded 3.
                Constants.VK_KHR_SWAPCHAIN_MAINTENANCE_1_EXTENSION_NAME,
                Constants.VK_EXT_SWAPCHAIN_MAINTENANCE_1_EXTENSION_NAME,
                // Explicit frame pacing: wait for a NAMED present to complete rather than for an image to come free.
                Constants.VK_KHR_PRESENT_ID_EXTENSION_NAME,
                Constants.VK_KHR_PRESENT_WAIT_EXTENSION_NAME,
                // Present only the rectangles that changed - which this renderer already knows, since its whole cache is
                // built around repainting just those.
                Constants.VK_KHR_INCREMENTAL_PRESENT_EXTENSION_NAME,
            });
        }

        public bool IsInDebugMode
        {
            get => VulkanInstance.IsInDebugMode;
            set => VulkanInstance.IsInDebugMode = value;
        }

        private MainGraphicsDevice(IGraphicsDeviceFactory deviceFactory, uint buffersCount, string name,
            bool enableDebug)
        {
            _graphicsDeviceFactory = deviceFactory;
            graphicsDevices = new List<IGraphicsDevice>();
            deviceMap = new Dictionary<Guid, IGraphicsDevice>();
            UsedGraphicsQueues = new Dictionary<uint, Queue>();
            EnableDynamicRendering = true;
            VulkanInstance = VulkanInstance.Create(name, enableDebug);
            GraphicsAdapter = VulkanInstance.MainGraphicsAdapter;
            QueueFamilyContainer = GraphicsAdapter.FindQueueFamilies();
            BuffersCount = buffersCount;
            CreateLogicalDevice();
            unsafe
            {
                if (LogicalDevice != null)
                {
                    Log.Logger.Debug(
                        $"Main device created. Vulkan Instance addr: {VulkanInstance.NativePointer} LogicalDevice addr: {new IntPtr(LogicalDevice.NativePointer)}");
                }
            }

            // The resource-loader device and the shared descriptor heap live for the whole logical-device lifetime,
            // so the main device owns them. Created here (after the logical device exists); callers just read them.
            ResourceLoaderDevice = CreateResourceLoaderDevice();
            DescriptorHeapManager = _graphicsDeviceFactory.CreateDescriptorHeapManager(ResourceLoaderDevice);
        }

        public void RemoveDevice(IGraphicsDevice device)
        {
            deviceMap.Remove(device.DeviceId);
            graphicsDevices.Remove(device);
            device?.Dispose();
        }

        public void RemoveDeviceById(Guid deviceId)
        {
            if (!deviceMap.TryGetValue(deviceId, out var device)) return;

            device?.Dispose();
            deviceMap.Remove(deviceId);
            graphicsDevices.Remove(device);
        }

        public IGraphicsDevice GetDeviceById(Guid deviceId)
        {
            return graphicsDevices.FirstOrDefault(x => x.DeviceId == deviceId);
        }

        public IGraphicsDevice UpdateDevice(Guid deviceId)
        {
            if (!deviceMap.TryGetValue(deviceId, out var device)) return null;

            device?.Dispose();
            deviceMap.Remove(deviceId);
            graphicsDevices.Remove(device);
            var newDevice = CreateRenderDevice();
            deviceMap.Add(deviceId, newDevice);
            graphicsDevices.Add(newDevice);
            return newDevice;
        }

        private void CreateLogicalDevice()
        {
            var queueFamilies = GraphicsAdapter.Adapter.GetQueueFamilyProperties();

            for (int i = 0; i < queueFamilies.Length; ++i)
            {
                Console.WriteLine(
                    $"Queue family {i}. QueueFlags: {queueFamilies[i].QueueFlags}. Queue count: {queueFamilies[i].QueueCount}");
            }

            var graphicsQueues = queueFamilies.FirstOrDefault(x => x.QueueFlags.HasFlag(QueueFlagBits.GraphicsBit));
            AvailableQueuesCount = graphicsQueues?.QueueCount ?? 0;
            var computeQueues = queueFamilies.FirstOrDefault(x => x.QueueFlags.HasFlag(QueueFlagBits.ComputeBit));
            var computeQueuesCount = computeQueues?.QueueCount ?? 0;

            Console.WriteLine($"{AvailableQueuesCount} queues available for graphics");
            Console.WriteLine($"{computeQueuesCount} queues available for compute");
            var queuePriorities = new float[AvailableQueuesCount];
            for (var i = 0; i < queuePriorities.Length; i++)
            {
                queuePriorities[i] = 1.0f;
            }

            var graphicsFamily = QueueFamilyContainer.GetFamilyInfo(QueueFlagBits.GraphicsBit);

            var queueInfos = new List<DeviceQueueCreateInfo>();
            var queueCreateInfo = new DeviceQueueCreateInfo();
            queueCreateInfo.QueueFamilyIndex = graphicsFamily.FamilyIndex;
            queueCreateInfo.QueueCount = AvailableQueuesCount;
            queueCreateInfo.PQueuePriorities = queuePriorities;
            queueInfos.Add(queueCreateInfo);

            // enumerate all available device extensions
            uint propCount = 0;
            GraphicsAdapter.Adapter.EnumerateDeviceExtensionProperties(null, ref propCount, null);
            var supportedDeviceExtensions = new ExtensionProperties[propCount];
            GraphicsAdapter.Adapter.EnumerateDeviceExtensionProperties(null, ref propCount, supportedDeviceExtensions);

            var availableDeviceExtensions = supportedDeviceExtensions.Select(x => x.ExtensionName).ToArray();
            var finalDeviceExtensions = new List<string>();
            foreach (var extension in DeviceExtensions)
            {
                if (availableDeviceExtensions.Contains(extension))
                {
                    finalDeviceExtensions.Add(extension);
                }
            }

            if (finalDeviceExtensions.Count != DeviceExtensions.Count)
            {
                var diff = DeviceExtensions.Except(finalDeviceExtensions).ToArray();
                throw new ExtensionNotSupportedException(GraphicsAdapter.AdapterProperties.DeviceName, diff);
            }

            // Optional extensions: enable whatever the device supports, never throw on the missing ones.
            foreach (var extension in OptionalDeviceExtensions)
            {
                if (availableDeviceExtensions.Contains(extension) && !finalDeviceExtensions.Contains(extension))
                {
                    finalDeviceExtensions.Add(extension);
                }
            }

            // What was ACTUALLY enabled, asked of the device rather than assumed from a list. Everything built on an
            // optional extension reads this: the same binary runs on a driver that has it and one that does not (a Mac
            // on MoltenVK has none of the presentation set), and "works on my machine" is exactly the failure mode a
            // hardcoded assumption produces here.
            EnabledDeviceExtensions = new HashSet<string>(finalDeviceExtensions, StringComparer.Ordinal);

            var descriptorBufferFeature = new PhysicalDeviceDescriptorBufferFeaturesEXT
            {
                DescriptorBuffer = true,
                DescriptorBufferCaptureReplay = true
            };

            var vulkan11Features = new PhysicalDeviceVulkan11Features
            {
                // SV_VertexID / SV_InstanceID make Slang emit the SPIR-V DrawParameters capability (BaseVertex/
                // BaseInstance correction) used by the instanced sprite/glyph expansion that replaced the geometry
                // shaders; without this feature vkCreateShadersEXT rejects those shaders (and the NV driver AVs).
                ShaderDrawParameters = true,
            };

            var vulkan12Features = new PhysicalDeviceVulkan12Features
            {
                TimelineSemaphore = true,
                BufferDeviceAddress = true,
                BufferDeviceAddressCaptureReplay = true,
                SamplerMirrorClampToEdge = true,
            };

            var vulkan13Features = new PhysicalDeviceVulkan13Features
            {
                Synchronization2 = true,
                DynamicRendering = true,
            };
            
            // Enable host image copy only when the device reports support — turning it on unconditionally would
            // fail vkCreateDevice. Texture.Save uses it when available, otherwise falls back to a staging buffer.
            var features14 = new PhysicalDeviceVulkan14Features
            {
                HostImageCopy = GraphicsAdapter.SupportsHostImageCopy
            };

            var primitiveRestart = new PhysicalDevicePrimitiveTopologyListRestartFeaturesEXT
            {
                PrimitiveTopologyListRestart = true,
            };
            
            var heapFeatures = new PhysicalDeviceDescriptorHeapFeaturesEXT
            {
                DescriptorHeap = true,
                // Enable only if the device actually supports it — otherwise vkCreateDevice will fail.
                DescriptorHeapCaptureReplay = GraphicsAdapter.SupportsDescriptorHeapCaptureReplay
            };

            var deviceFeatures2 = GraphicsAdapter.Adapter.GetPhysicalDeviceFeatures2();

            deviceFeatures2.PNext = vulkan11Features;
            vulkan11Features.PNext = vulkan12Features;
            vulkan12Features.PNext = vulkan13Features;
            vulkan13Features.PNext = features14;
            //vulkan13Features.PNext = descriptorBufferFeature;
            features14.PNext = primitiveRestart;
            primitiveRestart.PNext = descriptorBufferFeature;
            
            if (EnableDynamicRendering &&
                finalDeviceExtensions.Contains(Constants.VK_EXT_SHADER_OBJECT_EXTENSION_NAME))
            {
                var shaderObjectFeatures = new PhysicalDeviceShaderObjectFeaturesEXT
                {
                    ShaderObject = true
                };
                descriptorBufferFeature.PNext = shaderObjectFeatures;
                shaderObjectFeatures.PNext = heapFeatures;
            }
            else
            {
                descriptorBufferFeature.PNext = heapFeatures;
            }

            // VK_EXT_device_fault: turn on deviceFault so a later device-lost can be interrogated for its real cause.
            // heapFeatures is the current tail of the feature chain; extend it.
            if (finalDeviceExtensions.Contains(Constants.VK_EXT_DEVICE_FAULT_EXTENSION_NAME))
            {
                var faultFeatures = new PhysicalDeviceFaultFeaturesEXT { DeviceFault = true };
                heapFeatures.PNext = faultFeatures;
                DeviceFaultSupported = true;
            }

            deviceFeatures2.Features.SamplerAnisotropy = true;
            deviceFeatures2.Features.SampleRateShading = true;
            deviceFeatures2.Features.GeometryShader = true;
            // The GPU stroke expander dereferences BDA pointers (uint64_t) in its compute shader, which declares the
            // SPIR-V Int64 capability - that requires the shaderInt64 device feature.
            deviceFeatures2.Features.ShaderInt64 = true;

            var createInfo = new DeviceCreateInfo();
            createInfo.QueueCreateInfoCount = (uint)queueInfos.Count;
            createInfo.PQueueCreateInfos = queueInfos.ToArray();
            createInfo.EnabledExtensionCount = (uint)finalDeviceExtensions.Count;
            createInfo.PEnabledExtensionNames = finalDeviceExtensions.ToArray();
            createInfo.PNext = deviceFeatures2;

            LogicalDevice = GraphicsAdapter.Adapter.CreateDevice(createInfo);
            var fenceInfo = new FenceCreateInfo();
            fenceInfo.Flags = FenceCreateFlagBits.SignaledBit;
            InFlightFences ??= LogicalDevice.CreateFences(fenceInfo, BuffersCount);
        }

        public Result DeviceWaitIdle()
        {
            return LogicalDevice?.DeviceWaitIdle() ?? Result.Success;
        }

        public IGraphicsDevice CreateRenderDevice()
        {
            var renderDevice = _graphicsDeviceFactory.Create(this, GraphicsDeviceType.Rendering);
            deviceMap.Add(renderDevice.DeviceId, renderDevice);
            graphicsDevices.Add(renderDevice);
            return renderDevice;
        }

        private IGraphicsDevice CreateResourceLoaderDevice()
        {
            return _graphicsDeviceFactory.Create(this, GraphicsDeviceType.ResourceLoader);
        }

        public static MainGraphicsDevice Create(IGraphicsDeviceFactory deviceFactory, uint buffersCount, string name,
            bool enableDebug)
        {
            return new(deviceFactory, buffersCount, name, enableDebug);
        }

        public Queue GetAvailableGraphicsQueue()
        {
            var graphicsFamily = QueueFamilyContainer.GetFamilyInfo(QueueFlagBits.GraphicsBit);
            var queue = LogicalDevice.GetDeviceQueue(graphicsFamily.FamilyIndex, _availableGraphicsQueueIndex);
            UsedGraphicsQueues[_availableGraphicsQueueIndex] = queue;
            _availableGraphicsQueueIndex++;
            if (_availableGraphicsQueueIndex >= graphicsFamily.Count)
            {
                _availableGraphicsQueueIndex = 0;
            }

            return queue;
        }

        public Queue GetAvailableComputeQueue()
        {
            var computeFamily = QueueFamilyContainer.GetFamilyInfo(QueueFlagBits.ComputeBit);

            var queue = LogicalDevice.GetDeviceQueue(computeFamily.FamilyIndex, _availableComputeQueueIndex);
            _availableComputeQueueIndex++;
            if (_availableComputeQueueIndex >= computeFamily.Count)
            {
                _availableComputeQueueIndex = 0;
            }

            return queue;
        }

        public Queue GetAvailableTransferQueue()
        {
            var transferFamily = QueueFamilyContainer.GetFamilyInfo(QueueFlagBits.TransferBit);

            var queue = LogicalDevice.GetDeviceQueue(transferFamily.FamilyIndex, _availableTransferQueueIndex);
            _availableTransferQueueIndex++;
            if (_availableTransferQueueIndex >= transferFamily.Count)
            {
                _availableTransferQueueIndex = 0;
            }

            return queue;
        }

        public void Submit(Queue queue, params SubmitInfo[] submitInfos)
        {
            _submissionSync.WaitOne();

            var renderFence = InFlightFences[CurrentFrame];

            var result = LogicalDevice.ResetFences(1, renderFence);

            if (result != Result.Success)
            {
                throw new Exception($"failed to reset fences. Result: {result}");
            }

            result = queue.QueueSubmit((uint)submitInfos.Length, submitInfos, renderFence);
            LogicalDevice.WaitForFences(1, renderFence, true, ulong.MaxValue);

            if (result != Result.Success)
            {
                throw new Exception($"failed to submit draw command buffer! Result was {result}");
            }

            CurrentFrame = (CurrentFrame + 1) % BuffersCount;

            _submissionSync.ReleaseMutex();
        }

        public static implicit operator PhysicalDevice(MainGraphicsDevice device)
        {
            return device.GraphicsAdapter;
        }

        protected override void Dispose(bool disposeManaged)
        {
            Log.Logger.Debug("Start disposing main device");
            LogicalDevice?.DeviceWaitIdle();
            foreach (var device in graphicsDevices)
            {
                device?.Dispose();
            }

            graphicsDevices.Clear();
            deviceMap.Clear();

            // The main device owns these now; dispose the resource-loader device before the logical device (it frees
            // the shared heap's buffers, which were allocated through it).
            ResourceLoaderDevice?.Dispose();
            ResourceLoaderDevice = null;
            DescriptorHeapManager = null;

            // After every device's buffers have returned their sub-ranges, free the shared blocks (uses the still-alive
            // shared LogicalDevice). Before the logical device itself is destroyed.
            _memoryAllocator?.Dispose();
            _memoryAllocator = null;

            LogicalDevice?.Dispose();
            LogicalDevice = null;
            VulkanInstance?.Dispose();
            VulkanInstance = null;
            _availableTransferQueueIndex = 0;
            _availableComputeQueueIndex = 0;
            _availableGraphicsQueueIndex = 0;
            Log.Logger.Debug("End disposing main device");
        }

        public void OnFrameFinished()
        {
            FrameFinished?.Invoke();
        }

        public event Action FrameFinished;
    }
}