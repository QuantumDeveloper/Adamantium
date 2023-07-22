using AdamantiumVulkan.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AdamantiumVulkan.Windows;

namespace Adamantium.Engine.Graphics
{
    public static class PhysicalDeviceExtension
    {
        public static QueueFamilyContainer FindQueueFamilies(this PhysicalDevice device)
        {
            var container = new QueueFamilyContainer(device);

            var queueFamilies = device.GetQueueFamilyProperties();

            for (uint index = 0; index < queueFamilies.Length; index++)
            {
                var info = new QueueFamilyInfo();
                var queueFamily = queueFamilies[index];
                info.Type = queueFamily.QueueFlags;
                info.FamilyIndex = index;
                info.Count = queueFamily.QueueCount;
                
                container.AddQueueFamily(info);
            }

            return container;
        }

        public static bool CanPresent(this PhysicalDevice device, uint queueFamilyIndex, SurfaceKHR surface)
        {
            device.GetPhysicalDeviceSurfaceSupport(queueFamilyIndex, surface, out var presentSupport);
            return presentSupport;
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

    public class QueueFamilyContainer
    {
        private List<QueueFamilyInfo> _familyInfos;
        
        public PhysicalDevice PhysicalDevice { get; }

        public QueueFamilyContainer(PhysicalDevice physicalDevice)
        {
            PhysicalDevice = physicalDevice;
            _familyInfos = new List<QueueFamilyInfo>();
        }

        public IReadOnlyList<QueueFamilyInfo> FamilyInfos => _familyInfos;

        public void AddQueueFamily(QueueFamilyInfo info)
        {
            _familyInfos.Add(info);
        }
        
        public bool CanPresent(QueueFamilyInfo info, SurfaceKHR surface)
        {
            if (surface == null) return true;
            
            PhysicalDevice.GetPhysicalDeviceSurfaceSupport(info.FamilyIndex, surface, out var presentSupport);
            return presentSupport;
        }

        public uint GetPresentFamilyIndex(SurfaceKHR surface)
        {
            foreach (var familyInfo in _familyInfos)
            {
                PhysicalDevice.GetPhysicalDeviceSurfaceSupport(familyInfo.FamilyIndex, surface, out var presentSupport);
                if (presentSupport) break;
                return familyInfo.FamilyIndex;
            }

            return 0;
        }

        public QueueFamilyInfo GetFamilyInfo(QueueFlagBits flags)
        {
            return _familyInfos.FirstOrDefault(x => x.Type.HasFlag(flags));
        }
    }

    public class QueueFamilyInfo
    {
        public uint FamilyIndex { get; set; }
        
        public uint Count { get; set; }
        
        public QueueFlagBits Type { get; set; }
    }
}
