using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Adamantium.Core;
using Adamantium.Graphics.Core.Presentation;
using AdamantiumVulkan.Core;
using AdamantiumVulkan.Core.Interop;
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
        public VulkanInstance VulkanInstance { get; private set; }
        
        public GraphicsAdapter GraphicsAdapter { get; private set; }
        
        public IGraphicsDevice ResourceLoaderDevice { get; set; }
        
        public Device LogicalDevice { get; private set; }
        
        public uint AvailableQueuesCount { get; private set; }
        
        public QueueFamilyContainer QueueFamilyContainer { get; private set; }
        
        public static ReadOnlyCollection<string> DeviceExtensions { get; private set; }
        
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
            deviceExt.Add(Constants.VK_EXT_BUFFER_DEVICE_ADDRESS_EXTENSION_NAME);
            deviceExt.Add(Constants.VK_KHR_SYNCHRONIZATION_2_EXTENSION_NAME);
            DeviceExtensions = new ReadOnlyCollection<string>(deviceExt);
        }

        public bool IsInDebugMode
        {
            get => VulkanInstance.IsInDebugMode;
            set => VulkanInstance.IsInDebugMode = value;
        }

        private MainGraphicsDevice(IGraphicsDeviceFactory deviceFactory, uint buffersCount, string name, bool enableDebug)
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
        
        private unsafe void CreateLogicalDevice()
        {
            var queueFamilies = GraphicsAdapter.Adapter.GetQueueFamilyProperties();

            for (int i = 0; i < queueFamilies.Length; ++i)
            {
                Console.WriteLine($"Queue family {i}. QueueFlags: {queueFamilies[i].QueueFlags}. Queue count: {queueFamilies[i].QueueCount}");
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

            var deviceFeatures = GraphicsAdapter.Adapter.GetPhysicalDeviceFeatures();
            deviceFeatures.SamplerAnisotropy = true;
            deviceFeatures.SampleRateShading = true;
            
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

            var bufferDeviceAddressFeature = new PhysicalDeviceBufferDeviceAddressFeatures();
            bufferDeviceAddressFeature.SType = StructureType.PhysicalDeviceBufferDeviceAddressFeaturesExt;
            bufferDeviceAddressFeature.BufferDeviceAddress = true;

            var descriptorBufferFeature = new PhysicalDeviceDescriptorBufferFeaturesEXT();
            descriptorBufferFeature.SType = StructureType.PhysicalDeviceDescriptorBufferFeaturesExt;
            descriptorBufferFeature.DescriptorBuffer = true;
            descriptorBufferFeature.PNext = NativeUtils.StructOrEnumToPointer(bufferDeviceAddressFeature.ToNative());
            
            var vulkan11Features = new PhysicalDeviceVulkan11Features();
            vulkan11Features.PNext = NativeUtils.StructOrEnumToPointer(descriptorBufferFeature.ToNative());
            
            var vulkan12Features = new PhysicalDeviceVulkan12Features();
            vulkan12Features.SamplerMirrorClampToEdge = true;
            vulkan12Features.TimelineSemaphore = true;
            vulkan12Features.PNext = NativeUtils.StructOrEnumToPointer(vulkan11Features.ToNative());
            
            var vulkan13Features = new PhysicalDeviceVulkan13Features();
            vulkan13Features.SType = StructureType.PhysicalDeviceVulkan13Features;
            vulkan13Features.Synchronization2 = true;
            vulkan13Features.DynamicRendering = true;
            vulkan13Features.PNext = NativeUtils.StructOrEnumToPointer(vulkan12Features.ToNative());
                
            var features2 = new PhysicalDeviceFeatures2();
            features2.Features = new PhysicalDeviceFeatures
            {
                SamplerAnisotropy = VkBool32.TRUE,
                SampleRateShading = VkBool32.TRUE,
                GeometryShader = true
            };
            features2.PNext = NativeUtils.StructOrEnumToPointer(vulkan13Features.ToNative());
            
            var createInfo = new DeviceCreateInfo();
            createInfo.QueueCreateInfoCount = (uint)queueInfos.Count;
            createInfo.PQueueCreateInfos = queueInfos.ToArray();
            createInfo.EnabledExtensionCount = (uint)finalDeviceExtensions.Count;
            createInfo.PEnabledExtensionNames = finalDeviceExtensions.ToArray();
            
            if (EnableDynamicRendering)
            {
                if (finalDeviceExtensions.Contains(Constants.VK_EXT_SHADER_OBJECT_EXTENSION_NAME))
                {
                    var shaderObjectFeatures = new PhysicalDeviceShaderObjectFeaturesEXT();
                    shaderObjectFeatures.ShaderObject = true;
                    shaderObjectFeatures.PNext = NativeUtils.StructOrEnumToPointer(features2.ToNative());;
                    createInfo.PNext = NativeUtils.StructOrEnumToPointer(shaderObjectFeatures.ToNative());
                }
                else
                {
                    createInfo.PNext = NativeUtils.StructOrEnumToPointer(features2.ToNative());
                }
            }
            else
            {
                createInfo.PNext = NativeUtils.StructOrEnumToPointer(features2.ToNative());
            }

            if (VulkanInstance.IsInDebugMode)
            {
                createInfo.EnabledLayerCount = (uint)VulkanInstance.ValidationLayers.Count;
                createInfo.PEnabledLayerNames = VulkanInstance.ValidationLayers.ToArray();
            }
            
            LogicalDevice = GraphicsAdapter.Adapter.CreateDevice(createInfo);
            LogicalDevice.InitializeExtensions();
            var fenceInfo = new FenceCreateInfo();
            fenceInfo.Flags = FenceCreateFlagBits.SignaledBit;
            InFlightFences ??= LogicalDevice.CreateFences(fenceInfo, BuffersCount);
            createInfo.Dispose();
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

        public IGraphicsDevice CreateResourceLoaderDevice()
        {
            return _graphicsDeviceFactory.Create(this, GraphicsDeviceType.ResourceLoader);
        }

        public static MainGraphicsDevice Create(IGraphicsDeviceFactory deviceFactory, uint buffersCount, string name, bool enableDebug)
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