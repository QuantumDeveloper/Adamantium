using AdamantiumVulkan.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics
{
    public static class PhysicalDeviceExtension
    {
        public static QueueFamilyIndices FindQueueFamilies(this PhysicalDevice device, SurfaceKHR surface)
        {
            QueueFamilyIndices indices = new QueueFamilyIndices();

            uint queueFamilyCount = 0;
            device.GetPhysicalDeviceQueueFamilyProperties(ref queueFamilyCount, null);

            var queueFamilies = new QueueFamilyProperties[queueFamilyCount];
            device.GetPhysicalDeviceQueueFamilyProperties(ref queueFamilyCount, queueFamilies);

            uint i = 0;
            foreach (var queueFamily in queueFamilies)
            {
                if ((queueFamily.QueueFlags & (uint)QueueFlagBits.GraphicsBit) > 0)
                {
                    indices.graphicsFamily = i;

                }

                bool presentSupport = false;
                device.GetPhysicalDeviceSurfaceSupportKHR(i, surface, ref presentSupport);

                if (queueFamily.QueueCount > 0 && presentSupport)
                {
                    indices.presentFamily = i;
                }

                if (indices.isComplete())
                {
                    break;
                }

                i++;
            }

            return indices;
        }
    }

    public class QueueFamilyIndices
    {
        public uint? graphicsFamily;
        public uint? presentFamily;

        public bool isComplete()
        {
            return graphicsFamily.HasValue && presentFamily.HasValue;
        }
    }
}
