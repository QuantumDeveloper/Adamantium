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

        /// <summary>The resource-loader device, owned by the main device for its whole lifetime.</summary>
        public IGraphicsDevice ResourceLoaderDevice { get; private set; }

        // Deferred disposal for every GPU resource of this logical device, held until every drawing wrapper has retired
        // the frames in flight at hand-over: the wrappers share one VkDevice and share resources.
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
                    // Each begin waits on the fence of the frame MaxFramesInFlight back.
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

        /// <summary>Disposes everything retired without waiting for frames; the caller must have waited the device idle.
        /// For teardown: what is retired after the last frame is otherwise never freed.</summary>
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

        // A wrapper that is gone cannot be executing anything, so its vote is dropped.
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

        /// <summary>One descriptor heap per logical device, shared by every render-device wrapper.</summary>
        public IDescriptorHeapManager DescriptorHeapManager { get; private set; }

        // Shared like the heap, so no render device grabs its own BAR block. Created on first use.
        private IDeviceMemoryAllocator _memoryAllocator;
        public IDeviceMemoryAllocator MemoryAllocator => _memoryAllocator ??= _graphicsDeviceFactory.CreateMemoryAllocator(ResourceLoaderDevice);

        public Device LogicalDevice { get; private set; }

        public uint AvailableQueuesCount { get; private set; }

        public QueueFamilyContainer QueueFamilyContainer { get; private set; }

        public static ReadOnlyCollection<string> DeviceExtensions { get; private set; }

        /// <summary>Extensions enabled when supported, never required.</summary>
        public static ReadOnlyCollection<string> OptionalDeviceExtensions { get; private set; }

        /// <summary>Device extensions this device actually enabled: the required ones plus the supported optional ones.</summary>
        public IReadOnlySet<string> EnabledDeviceExtensions { get; private set; } = new HashSet<string>();

        public bool IsExtensionEnabled(string extension) => EnabledDeviceExtensions.Contains(extension);

        /// <summary>A present fence per present, present-mode changes without a rebuild, defined scaling on resize. The
        /// extension (KHR or EXT) and the feature both have to be there.</summary>
        public bool SupportsSwapchainMaintenance =>
            GraphicsAdapter.SupportsSwapchainMaintenance1
            && (IsExtensionEnabled(Constants.VK_KHR_SWAPCHAIN_MAINTENANCE_1_EXTENSION_NAME)
                || IsExtensionEnabled(Constants.VK_EXT_SWAPCHAIN_MAINTENANCE_1_EXTENSION_NAME));

        /// <summary>Wait for a named present instead of for an image to come free.</summary>
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

            // Every external memory/semaphore transport the driver has, NT handles and fds alike: the producer dictates
            // the handle, not our OS.
            OptionalDeviceExtensions = new ReadOnlyCollection<string>(new List<string>
            {
                Constants.VK_KHR_EXTERNAL_MEMORY_WIN32_EXTENSION_NAME,
                Constants.VK_KHR_EXTERNAL_SEMAPHORE_WIN32_EXTENSION_NAME,
                Constants.VK_KHR_EXTERNAL_MEMORY_FD_EXTENSION_NAME,
                Constants.VK_KHR_EXTERNAL_SEMAPHORE_FD_EXTENSION_NAME,
                // The real cause of a device-lost.
                Constants.VK_EXT_DEVICE_FAULT_EXTENSION_NAME,

                // Promoted KHR form first, EXT as the fallback: a driver may advertise either.
                Constants.VK_KHR_SWAPCHAIN_MAINTENANCE_1_EXTENSION_NAME,
                Constants.VK_EXT_SWAPCHAIN_MAINTENANCE_1_EXTENSION_NAME,
                Constants.VK_KHR_PRESENT_ID_EXTENSION_NAME,
                Constants.VK_KHR_PRESENT_WAIT_EXTENSION_NAME,
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

            foreach (var extension in OptionalDeviceExtensions)
            {
                if (availableDeviceExtensions.Contains(extension) && !finalDeviceExtensions.Contains(extension))
                {
                    finalDeviceExtensions.Add(extension);
                }
            }

            // What was actually enabled: the same binary runs where the optional ones are missing (MoltenVK has none).
            EnabledDeviceExtensions = new HashSet<string>(finalDeviceExtensions, StringComparer.Ordinal);

            var descriptorBufferFeature = new PhysicalDeviceDescriptorBufferFeaturesEXT
            {
                DescriptorBuffer = true,
                DescriptorBufferCaptureReplay = true
            };

            var vulkan11Features = new PhysicalDeviceVulkan11Features
            {
                // SV_VertexID/SV_InstanceID need DrawParameters; without it vkCreateShadersEXT rejects the instanced shaders.
                ShaderDrawParameters = true,
            };

            var vulkan12Features = new PhysicalDeviceVulkan12Features
            {
                TimelineSemaphore = true,
                BufferDeviceAddress = true,
                BufferDeviceAddressCaptureReplay = true,
                SamplerMirrorClampToEdge = true,
                // Only when reported: requesting a missing feature fails vkCreateDevice.
                ScalarBlockLayout = GraphicsAdapter.SupportsScalarBlockLayout,
                // Byte colors in every instance record; there is no second layout to fall back to.
                StorageBuffer8BitAccess = true,
                ShaderInt8 = true,
            };

            var vulkan13Features = new PhysicalDeviceVulkan13Features
            {
                Synchronization2 = true,
                DynamicRendering = true,
                // Discard before a derivative compiles to demote; used without this it lost the device.
                ShaderDemoteToHelperInvocation = true,
            };

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

            PhysicalDeviceFaultFeaturesEXT faultFeatures = null;
            if (finalDeviceExtensions.Contains(Constants.VK_EXT_DEVICE_FAULT_EXTENSION_NAME))
            {
                faultFeatures = new PhysicalDeviceFaultFeaturesEXT { DeviceFault = true };
                heapFeatures.PNext = faultFeatures;
                DeviceFaultSupported = true;
            }

            // Both this and the fault block are conditional, so the chain's tail is whichever of them landed.
            if (SupportsSwapchainMaintenance)
            {
                var maintenanceFeatures = new PhysicalDeviceSwapchainMaintenance1FeaturesKHR
                {
                    SwapchainMaintenance1 = true
                };

                if (faultFeatures != null)
                {
                    faultFeatures.PNext = maintenanceFeatures;
                }
                else
                {
                    heapFeatures.PNext = maintenanceFeatures;
                }
            }

            deviceFeatures2.Features.SamplerAnisotropy = true;
            deviceFeatures2.Features.SampleRateShading = true;
            deviceFeatures2.Features.GeometryShader = true;
            // The stroke expander's compute shader dereferences BDA pointers (uint64_t).
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
            // A copy: a device takes itself off this list as it is disposed.
            foreach (var device in graphicsDevices.ToArray())
            {
                device?.Dispose();
            }

            graphicsDevices.Clear();
            deviceMap.Clear();

            // The heap's buffers were allocated through the resource-loader device, so they go first.
            DescriptorHeapManager?.Dispose();
            DescriptorHeapManager = null;

            ResourceLoaderDevice?.Dispose();
            ResourceLoaderDevice = null;

            // After every device returned its sub-ranges, before the logical device goes.
            _memoryAllocator?.Dispose();
            _memoryAllocator = null;

            if (InFlightFences != null)
            {
                foreach (var fence in InFlightFences) LogicalDevice?.DestroyFence(fence);
                InFlightFences = null;
            }

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
