using System;
using AdamantiumVulkan.Core;
using AdamantiumVulkan.Core.Interop;
using QuantumBinding.Utils;

namespace Adamantium.Graphics.Core;

public unsafe class GraphicsAdapter
{
    private PhysicalDevice _physicalDevice;
    private VulkanInstance _vkInstance;

    public GraphicsAdapter(PhysicalDevice device, VulkanInstance vkInstance)
    {
        _physicalDevice = device;
        _vkInstance = vkInstance;
        Adapter = device;
        device.GetPhysicalDeviceProperties(out var properties);
        AdapterProperties = properties;
        UpdateData();
    }

    public void UpdateData()
    {
        var heapPropertiesNative = new VkPhysicalDeviceDescriptorHeapPropertiesEXT();
        heapPropertiesNative.sType = StructureType.PhysicalDeviceDescriptorHeapPropertiesExt;
        var heapPtr = (IntPtr)NativeUtils.StructOrEnumToPointer(heapPropertiesNative);
        var descriptorBufferProperties = new VkPhysicalDeviceDescriptorBufferPropertiesEXT();
        descriptorBufferProperties.sType = StructureType.PhysicalDeviceDescriptorBufferPropertiesExt;
        descriptorBufferProperties.pNext = (void*)heapPtr;
        var properties2 = new PhysicalDeviceProperties2();
        var bufferPtr = (IntPtr)NativeUtils.StructOrEnumToPointer(descriptorBufferProperties);
        properties2.PNext = bufferPtr;
        properties2.Properties = new PhysicalDeviceProperties();

        _physicalDevice.GetPhysicalDeviceProperties2(ref properties2);
        var bufferProperties = *(VkPhysicalDeviceDescriptorBufferPropertiesEXT*)bufferPtr;
        var heapProperties = *(VkPhysicalDeviceDescriptorHeapPropertiesEXT*)heapPtr;
        AdapterProperties = properties2.Properties;
        DeviceBufferProperties = new PhysicalDeviceDescriptorBufferPropertiesEXT(bufferProperties);
        DeviceHeapProperties = new PhysicalDeviceDescriptorHeapPropertiesEXT(heapProperties);
    }
    
    public PhysicalDeviceProperties AdapterProperties { get; private set; }
    
    public PhysicalDeviceDescriptorBufferPropertiesEXT DeviceBufferProperties { get; private set; }
    
    public PhysicalDeviceDescriptorHeapPropertiesEXT DeviceHeapProperties { get; private set; }

    public PhysicalDeviceType DeviceType => AdapterProperties.DeviceType;
    
    public PhysicalDevice Adapter { get; }
    
    public static implicit operator PhysicalDevice(GraphicsAdapter adapter)
    {
        return adapter._physicalDevice;
    }
}