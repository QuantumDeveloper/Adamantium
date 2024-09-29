using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Adamantium.Core;
using AdamantiumVulkan.Core;
using AdamantiumVulkan.Core.Interop;
using QuantumBinding.Utils;
using Serilog;

namespace Adamantium.Engine.Graphics
{
    public class MainGraphicsDevice : DisposableBase
    {
        private uint _availableGraphicsQueueIndex;

        private uint _availableComputeQueueIndex;

        private uint _availableTransferQueueIndex;
        
        private readonly List<GraphicsDevice> graphicsDevices;
        
        private readonly Dictionary<string, GraphicsDevice> deviceMap;
        private Mutex _submissionSync;
        
        public bool EnableDynamicRendering { get; }
        public VulkanInstance VulkanInstance { get; private set; }
        
        public GraphicsAdapter GraphicsAdapter { get; private set; }
        
        public GraphicsDevice ResourceLoaderDevice { get; set; }
        
        internal Device LogicalDevice { get; private set; }
        
        public uint AvailableQueuesCount { get; private set; }
        
        public QueueFamilyContainer QueueFamilyContainer { get; private set; }
        
        public static ReadOnlyCollection<string> DeviceExtensions { get; private set; }
        
        internal Fence[] InFlightFences { get; private set; }
        
        public uint MaxFramesInFlight { get; private set; }
        
        public uint CurrentFrame { get; private set; }
        
        public Dictionary<uint, Queue> UsedGraphicsQueues { get; }

        public IReadOnlyList<GraphicsDevice> GraphicsDevices => graphicsDevices.AsReadOnly();
        
        static MainGraphicsDevice()
        {
            var deviceExt = new List<string>();
            deviceExt.Add(Constants.VK_KHR_SWAPCHAIN_EXTENSION_NAME);
            deviceExt.Add(Constants.VK_KHR_MAINTENANCE_4_EXTENSION_NAME);
            //deviceExt.Add(Constants.VK_GOOGLE_HLSL_FUNCTIONALITY_1_EXTENSION_NAME);
            deviceExt.Add(Constants.VK_GOOGLE_USER_TYPE_EXTENSION_NAME);
            deviceExt.Add(Constants.VK_KHR_DYNAMIC_RENDERING_EXTENSION_NAME);
            deviceExt.Add(Constants.VK_EXT_SHADER_OBJECT_EXTENSION_NAME);
            deviceExt.Add(Constants.VK_EXT_DESCRIPTOR_BUFFER_EXTENSION_NAME);
            DeviceExtensions = new ReadOnlyCollection<string>(deviceExt);
        }

        public bool IsInDebugMode
        {
            get => VulkanInstance.IsInDebugMode;
            set => VulkanInstance.IsInDebugMode = value;
        }

        private MainGraphicsDevice(string name, bool enableDynamicRendering, bool enableDebug)
        {
            graphicsDevices = new List<GraphicsDevice>();
            deviceMap = new Dictionary<string, GraphicsDevice>();
            UsedGraphicsQueues = new Dictionary<uint, Queue>();
            EnableDynamicRendering = enableDynamicRendering;
            VulkanInstance = VulkanInstance.Create(name, enableDebug);
            GraphicsAdapter = VulkanInstance.MainGraphicsAdapter;
            QueueFamilyContainer = GraphicsAdapter.Adapter.FindQueueFamilies();
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
        
        public void RemoveDevice(GraphicsDevice device)
        {
            deviceMap.Remove(device.DeviceId);
            graphicsDevices.Remove(device);
            device?.Dispose();
        }

        public void RemoveDeviceById(string deviceId)
        {
            if (!deviceMap.TryGetValue(deviceId, out var device)) return;
            
            device?.Dispose();
            deviceMap.Remove(deviceId);
            graphicsDevices.Remove(device);
        }

        public GraphicsDevice GetDeviceById(string deviceId)
        {
            return graphicsDevices.FirstOrDefault(x => x.DeviceId == deviceId);
        }

        public GraphicsDevice UpdateDevice(string deviceId, PresentationParameters parameters)
        {
            if (!deviceMap.TryGetValue(deviceId, out var device)) return null;
            
            device?.Dispose();
            deviceMap.Remove(deviceId);
            graphicsDevices.Remove(device);
            var newDevice = CreateRenderDevice(parameters);
            deviceMap.Add(deviceId, newDevice);
            graphicsDevices.Add(newDevice);
            return newDevice;
        }
        
        private unsafe void CreateLogicalDevice()
        {
            float queuePriority = 1.0f;
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

            var graphicsFamily = QueueFamilyContainer.GetFamilyInfo(QueueFlagBits.GraphicsBit);
            
            var queueInfos = new List<DeviceQueueCreateInfo>();
            var queueCreateInfo = new DeviceQueueCreateInfo();
            queueCreateInfo.QueueFamilyIndex = graphicsFamily.FamilyIndex;
            queueCreateInfo.QueueCount = AvailableQueuesCount;
            queueCreateInfo.PQueuePriorities = queuePriority;
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

            var maintenance4Features = new PhysicalDeviceMaintenance4Features();
            maintenance4Features.SType = StructureType.PhysicalDeviceMaintenance4Features;
            maintenance4Features.Maintenance4 = VkBool32.TRUE;
            
            var bufferDeviceAddressFeature = new PhysicalDeviceBufferDeviceAddressFeatures();
            bufferDeviceAddressFeature.SType = StructureType.PhysicalDeviceBufferDeviceAddressFeaturesExt;
            bufferDeviceAddressFeature.BufferDeviceAddress = true;
            bufferDeviceAddressFeature.PNext = NativeUtils.StructOrEnumToPointer(maintenance4Features.ToNative());

            var descriptorBufferFeature = new PhysicalDeviceDescriptorBufferFeaturesEXT();
            descriptorBufferFeature.SType = StructureType.PhysicalDeviceDescriptorBufferFeaturesExt;
            descriptorBufferFeature.DescriptorBuffer = true;
            descriptorBufferFeature.PNext = NativeUtils.StructOrEnumToPointer(bufferDeviceAddressFeature.ToNative());
            
            var vulkan11Features = new PhysicalDeviceVulkan11Features();
            vulkan11Features.PNext = NativeUtils.StructOrEnumToPointer(descriptorBufferFeature.ToNative());
            
            var vulkan12Features = new PhysicalDeviceVulkan12Features();
            vulkan12Features.SamplerMirrorClampToEdge = true;
            vulkan12Features.PNext = NativeUtils.StructOrEnumToPointer(vulkan11Features.ToNative());
                
            var features2 = new PhysicalDeviceFeatures2();
            features2.Features = new PhysicalDeviceFeatures
            {
                SamplerAnisotropy = VkBool32.TRUE,
                SampleRateShading = VkBool32.TRUE,
                GeometryShader = true
            };
            features2.PNext = NativeUtils.StructOrEnumToPointer(vulkan12Features.ToNative());
            
            var createInfo = new DeviceCreateInfo();
            createInfo.QueueCreateInfoCount = (uint)queueInfos.Count;
            createInfo.PQueueCreateInfos = queueInfos.ToArray();
            createInfo.EnabledExtensionCount = (uint)finalDeviceExtensions.Count;
            createInfo.PEnabledExtensionNames = finalDeviceExtensions.ToArray();
            
            if (EnableDynamicRendering)
            {
                var dynamicRendering = new PhysicalDeviceDynamicRenderingFeatures();
                dynamicRendering.DynamicRendering = VkBool32.TRUE;
                dynamicRendering.PNext = NativeUtils.StructOrEnumToPointer(features2.ToNative());

                if (finalDeviceExtensions.Contains(Constants.VK_EXT_SHADER_OBJECT_EXTENSION_NAME))
                {
                    var shaderObjectFeatures = new PhysicalDeviceShaderObjectFeaturesEXT();
                    shaderObjectFeatures.ShaderObject = true;
                    shaderObjectFeatures.PNext = NativeUtils.StructOrEnumToPointer(dynamicRendering.ToNative());;
                    createInfo.PNext = NativeUtils.StructOrEnumToPointer(shaderObjectFeatures.ToNative());
                }
                else
                {
                    createInfo.PNext = NativeUtils.StructOrEnumToPointer(dynamicRendering.ToNative());
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

            MaxFramesInFlight = 3;
            LogicalDevice = GraphicsAdapter.Adapter.CreateDevice(createInfo);
            LogicalDevice.InitializeExtensions();
            var fenceInfo = new FenceCreateInfo();
            fenceInfo.Flags = FenceCreateFlagBits.SignaledBit;
            InFlightFences ??= LogicalDevice.CreateFences(fenceInfo, MaxFramesInFlight);
            createInfo.Dispose();
        }
        
        public Result DeviceWaitIdle()
        {
            return LogicalDevice?.DeviceWaitIdle() ?? Result.Success;
        }

        public GraphicsDevice CreateRenderDevice(PresentationParameters parameters)
        {
            var renderDevice = GraphicsDevice.Create(this, parameters);
            deviceMap.Add(renderDevice.DeviceId, renderDevice);
            graphicsDevices.Add(renderDevice);
            return renderDevice;
        }

        public GraphicsDevice CreateResourceLoaderDevice()
        {
            return GraphicsDevice.Create(this);
        }

        public static MainGraphicsDevice Create(string name, bool enableDynamicRendering, bool enableDebug)
        {
            return new(name, enableDynamicRendering, enableDebug);
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
            CurrentFrame = (CurrentFrame + 1) % MaxFramesInFlight;
            
            _submissionSync.ReleaseMutex();
        }
        
        public void UpdateDescriptorSets(uint currentFrame, params WriteDescriptorSet[] writeDescriptorSets)
        {
            if (writeDescriptorSets == null || writeDescriptorSets.Length == 0) return;
            
            // TODO: decide does wait for fences really need here
            var renderFence = InFlightFences[currentFrame];
            var result = LogicalDevice.WaitForFences(1, renderFence, true, ulong.MaxValue);
           
            LogicalDevice.UpdateDescriptorSets((uint)writeDescriptorSets.Length, writeDescriptorSets, 0, out var copySets);
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