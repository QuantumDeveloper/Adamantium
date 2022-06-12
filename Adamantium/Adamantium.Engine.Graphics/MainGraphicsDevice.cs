using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Core;
using Adamantium.Engine.Graphics.Effects;
using AdamantiumVulkan.Core;

namespace Adamantium.Engine.Graphics
{
    public class MainGraphicsDevice : DisposableBase
    {
        public VulkanInstance VulkanInstance { get; private set; }
        
        public PhysicalDevice PhysicalDevice { get; private set; }
        
        internal Device LogicalDevice { get; private set; }
        
        public uint AvailableQueuesCount { get; private set; }
        
        //internal Queue GraphicsQueue { get; private set; }
        
        internal CommandPool CommandPool { get; private set; }

        private MainGraphicsDevice(string name, bool enableDebug)
        {
            VulkanInstance = VulkanInstance.Create(name, enableDebug);
            PhysicalDevice = VulkanInstance.CurrentDevice;
            CreateLogicalDevice();
            CreateCommandPool();
        }
        
        private void CreateLogicalDevice()
        {
            var indices = PhysicalDevice.FindQueueFamilies(null);

            var queueInfos = new List<DeviceQueueCreateInfo>();
            HashSet<uint> uniqueQueueFamilies = new HashSet<uint>() { indices.graphicsFamily.Value, indices.presentFamily.Value };
            float queuePriority = 1.0f;
            var queueFamilies = PhysicalDevice.GetQueueFamilyProperties();

            for (int i = 0; i < queueFamilies.Length; ++i)
            {
                Console.WriteLine($"Queue family {i}. Queue count: {queueFamilies[i].QueueCount}");
            }
            
            AvailableQueuesCount = 2;

            if (queueFamilies[0].QueueCount < 2)
            {
                Console.WriteLine($"There are only {queueFamilies[0].QueueCount} queues for queue family 0");
                AvailableQueuesCount = 1;
            }
            
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
            createInfo.PEnabledFeatures = deviceFeatures;
            createInfo.EnabledExtensionCount = (uint)VulkanInstance.DeviceExtensions.Count;
            createInfo.PpEnabledExtensionNames = VulkanInstance.DeviceExtensions.ToArray();

            if (VulkanInstance.IsInDebugMode)
            {
                createInfo.EnabledLayerCount = (uint)VulkanInstance.ValidationLayers.Count;
                createInfo.PpEnabledLayerNames = VulkanInstance.ValidationLayers.ToArray();
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
            poolInfo.Flags = (uint)CommandPoolCreateFlagBits.ResetCommandBufferBit;
            CommandPool = LogicalDevice.CreateCommandPool(poolInfo);
        }
        
        public Result DeviceWaitIdle()
        {
            return LogicalDevice.DeviceWaitIdle();
        }

        public GraphicsDevice CreateRenderDevice(PresentationParameters parameters)
        {
            return GraphicsDevice.Create(this, parameters);
        }

        public GraphicsDevice CreateResourceLoaderDevice()
        {
            return GraphicsDevice.Create(this);
        }

        public static MainGraphicsDevice Create(string name, bool enableDebug)
        {
            return new(name, enableDebug);
        }

        public static implicit operator PhysicalDevice(MainGraphicsDevice device)
        {
            return device.PhysicalDevice;
        }

        protected override void Dispose(bool disposeManaged)
        {
            LogicalDevice?.DeviceWaitIdle();
            LogicalDevice?.DestroyCommandPool(CommandPool);
            LogicalDevice?.Dispose();
            VulkanInstance?.Dispose();
        }
    }
}