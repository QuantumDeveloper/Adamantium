using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Adamantium.Core;
using AdamantiumVulkan.Core;
using AdamantiumVulkan.Core.Interop;
using QuantumBinding.Utils;
using Serilog;

namespace Adamantium.Engine.Graphics
{
    public class MainGraphicsDevice : DisposableBase
    {
        public bool EnableDynamicRendering { get; }
        public VulkanInstance VulkanInstance { get; private set; }
        
        public PhysicalDevice PhysicalDevice { get; private set; }
        
        internal Device LogicalDevice { get; private set; }
        
        public uint AvailableQueuesCount { get; private set; }
        
        //internal Queue GraphicsQueue { get; private set; }
        
        internal CommandPool CommandPool { get; private set; }
        
        public static ReadOnlyCollection<string> DeviceExtensions { get; private set; }

        static MainGraphicsDevice()
        {
            var deviceExt = new List<string>();
            deviceExt.Add(Constants.VK_KHR_SWAPCHAIN_EXTENSION_NAME);
            deviceExt.Add(Constants.VK_KHR_MAINTENANCE_4_EXTENSION_NAME);
            deviceExt.Add(Constants.VK_GOOGLE_HLSL_FUNCTIONALITY_1_EXTENSION_NAME);
            deviceExt.Add(Constants.VK_GOOGLE_USER_TYPE_EXTENSION_NAME);
            deviceExt.Add(Constants.VK_KHR_DYNAMIC_RENDERING_EXTENSION_NAME);
            DeviceExtensions = new ReadOnlyCollection<string>(deviceExt);
        }

        public bool IsInDebugMode
        {
            get => VulkanInstance.IsInDebugMode;
            set => VulkanInstance.IsInDebugMode = value;
        }

        private MainGraphicsDevice(string name, bool enableDynamicRendering, bool enableDebug)
        {
            EnableDynamicRendering = enableDynamicRendering;
            VulkanInstance = VulkanInstance.Create(name, enableDebug);
            PhysicalDevice = VulkanInstance.CurrentDevice;
            CreateLogicalDevice();
            CreateCommandPool();
            unsafe
            {
                if (LogicalDevice != null)
                {
                    Log.Logger.Debug(
                        $"Main device created. Vulkan Instance addr: {VulkanInstance.NativePointer} LogicalDevice addr: {new IntPtr(LogicalDevice.NativePointer)}");
                }
            }
        }
        
        private unsafe void CreateLogicalDevice()
        {
            var indices = PhysicalDevice.FindQueueFamilies(null);

            var queueInfos = new List<DeviceQueueCreateInfo>();
            var uniqueQueueFamilies = new HashSet<uint>() { indices.graphicsFamily.Value, indices.presentFamily.Value };
            float queuePriority = 1.0f;
            var queueFamilies = PhysicalDevice.GetQueueFamilyProperties();

            for (int i = 0; i < queueFamilies.Length; ++i)
            {
                Console.WriteLine($"Queue family {i}. Queue count: {queueFamilies[i].QueueCount}");
            }
            
            AvailableQueuesCount = (uint)queueFamilies.Count(x => x.QueueFlags.HasFlag(QueueFlagBits.GraphicsBit));

            Console.WriteLine($"Only {AvailableQueuesCount} queues available for graphics queue family");
            
            foreach (var queueFamily in uniqueQueueFamilies)
            {
                var queueCreateInfo = new DeviceQueueCreateInfo();
                queueCreateInfo.QueueFamilyIndex = queueFamily;
                queueCreateInfo.QueueCount = AvailableQueuesCount;
                queueCreateInfo.PQueuePriorities = queuePriority;
                queueInfos.Add(queueCreateInfo);
            }

            var deviceFeatures = PhysicalDevice.GetPhysicalDeviceFeatures();
            deviceFeatures.SamplerAnisotropy = true;
            deviceFeatures.SampleRateShading = true;

            var createInfo = new DeviceCreateInfo();
            createInfo.QueueCreateInfoCount = (uint)queueInfos.Count;
            createInfo.PQueueCreateInfos = queueInfos.ToArray();
            createInfo.EnabledExtensionCount = (uint)DeviceExtensions.Count;
            createInfo.PEnabledExtensionNames = DeviceExtensions.ToArray();

            var maintenance4Features = new PhysicalDeviceMaintenance4Features();
            maintenance4Features.SType = StructureType.PhysicalDeviceMaintenance4Features;
            maintenance4Features.Maintenance4 = VkBool32.TRUE;
            
            if (EnableDynamicRendering)
            {
                var dynamicRendering = new PhysicalDeviceDynamicRenderingFeatures();
                dynamicRendering.DynamicRendering = VkBool32.TRUE;

                var vulkan12Features = new PhysicalDeviceVulkan12Features();
                var dynamicRenderingPtr = NativeUtils.StructOrEnumToPointer(dynamicRendering.ToNative());
                vulkan12Features.PNext = dynamicRenderingPtr;
                
                var vulkan11Features = new PhysicalDeviceVulkan11Features();
                var vulkan12FeaturesPtr = NativeUtils.StructOrEnumToPointer(vulkan12Features.ToNative());
                vulkan11Features.PNext = vulkan12FeaturesPtr;

                var features2 = new PhysicalDeviceFeatures2();
                features2.Features = new PhysicalDeviceFeatures();
                features2.Features.SamplerAnisotropy = VkBool32.TRUE;
                features2.Features.SampleRateShading = VkBool32.TRUE;
                
                var vulkan11FeaturesPtr = NativeUtils.StructOrEnumToPointer(vulkan11Features.ToNative());
                features2.PNext = vulkan11FeaturesPtr;

                var features2Ptr = NativeUtils.StructOrEnumToPointer(features2.ToNative());

                maintenance4Features.PNext = features2Ptr;
                var maintenance4FeaturesPtr = NativeUtils.StructOrEnumToPointer(maintenance4Features.ToNative());
                
                createInfo.PNext = maintenance4FeaturesPtr;
            }
            else
            {
                var maintenance4FeaturesPtr = NativeUtils.StructOrEnumToPointer(maintenance4Features.ToNative());
                createInfo.PNext = maintenance4FeaturesPtr;
                createInfo.PEnabledFeatures = deviceFeatures;
            }

            if (VulkanInstance.IsInDebugMode)
            {
                createInfo.EnabledLayerCount = (uint)VulkanInstance.ValidationLayers.Count;
                createInfo.PEnabledLayerNames = VulkanInstance.ValidationLayers.ToArray();
            }

            LogicalDevice = PhysicalDevice.CreateDevice(createInfo);
            
            createInfo.Dispose();

            //GraphicsQueue = LogicalDevice.GetDeviceQueue(indices.graphicsFamily.Value, 0);
        }
        
        private void CreateCommandPool()
        {
            var queueFamilyIndices = PhysicalDevice.FindQueueFamilies(null);

            var poolInfo = new CommandPoolCreateInfo();
            poolInfo.QueueFamilyIndex = queueFamilyIndices.graphicsFamily.Value;
            poolInfo.Flags = CommandPoolCreateFlagBits.ResetCommandBufferBit;
            CommandPool = LogicalDevice?.CreateCommandPool(poolInfo);
        }
        
        public Result DeviceWaitIdle()
        {
            return LogicalDevice?.DeviceWaitIdle() ?? Result.Success;
        }

        public GraphicsDevice CreateRenderDevice(PresentationParameters parameters)
        {
            return GraphicsDevice.Create(this, parameters);
        }

        public GraphicsDevice CreateResourceLoaderDevice()
        {
            return GraphicsDevice.Create(this);
        }

        public static MainGraphicsDevice Create(string name, bool enableDynamicRendering, bool enableDebug)
        {
            return new(name, enableDynamicRendering, enableDebug);
        }

        public static implicit operator PhysicalDevice(MainGraphicsDevice device)
        {
            return device.PhysicalDevice;
        }

        protected override void Dispose(bool disposeManaged)
        {
            Log.Logger.Debug("Start disposing main device");
            LogicalDevice?.DeviceWaitIdle();
            LogicalDevice?.DestroyCommandPool(CommandPool);
            CommandPool = null;
            LogicalDevice?.Dispose();
            LogicalDevice = null;
            VulkanInstance?.Dispose();
            VulkanInstance = null;
            Log.Logger.Debug("End disposing main device");
        }
    }
}