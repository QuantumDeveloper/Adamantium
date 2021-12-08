using AdamantiumVulkan.Core;
using System;
using System.Collections.Generic;
using System.Text;
using AdamantiumVulkan.Windows;

namespace Adamantium.Engine.Graphics
{
    public static class PhysicalDeviceExtension
    {
        public static QueueFamilyIndices FindQueueFamilies(this PhysicalDevice device, SurfaceKHR surface)
        {
            QueueFamilyIndices indices = new QueueFamilyIndices();

            var queueFamilies = device.GetQueueFamilyProperties();

            uint i = 0;
            foreach (var queueFamily in queueFamilies)
            {
                if ((queueFamily.QueueFlags & (uint)QueueFlagBits.GraphicsBit) > 0)
                {
                    indices.graphicsFamily = i;
                }

                bool presentSupport = false;
                //presentSupport = device.GetPhysicalDeviceWin32PresentationSupportKHR(i);
                if (surface != null)
                {
                    device.GetPhysicalDeviceSurfaceSupportKHR(i, surface, ref presentSupport);
                }
                else
                {
                    presentSupport = true;
                }

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

        public static UInt32 FindMemoryIndex(this PhysicalDevice physicalDevice, UInt32 memoryTypeBits, MemoryPropertyFlags propertyFlags)
        {
            var memProperties = physicalDevice.GetPhysicalDeviceMemoryProperties();
            for (uint i = 0; i < memProperties.MemoryTypeCount; i++)
            {
                if (((memoryTypeBits >> (int)i) & 1) == 1 &&
                    ((MemoryPropertyFlags)memProperties.MemoryTypes[i].PropertyFlags).HasFlag(propertyFlags))
                {
                    return i;
                }
            }

            return 0;
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
