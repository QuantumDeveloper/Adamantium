using AdamantiumVulkan.Core;
using AdamantiumVulkan.Core.Interop;
using QuantumBinding.Utils;

namespace Adamantium.Engine.Graphics;

public unsafe class GraphicsAdapter
{
    private PhysicalDevice _physicalDevice;
    private VulkanInstance _vkInstance;
    private PFN_vkGetPhysicalDeviceProperties2 _deviceProperties2Delegate;

    public GraphicsAdapter(PhysicalDevice device, VulkanInstance vkInstance)
    {
        _physicalDevice = device;
        _vkInstance = vkInstance;
        Adapter = device;
        device.GetPhysicalDeviceProperties(out var properties);
        AdapterProperties = properties;
        var descriptorBufferProperties = new VkPhysicalDeviceDescriptorBufferPropertiesEXT();
        descriptorBufferProperties.sType = StructureType.PhysicalDeviceDescriptorBufferPropertiesExt;
        var properties2 = new PhysicalDeviceProperties2();
        properties2.PNext = NativeUtils.StructOrEnumToPointer(descriptorBufferProperties);

        _deviceProperties2Delegate = (PFN_vkGetPhysicalDeviceProperties2)vkInstance.VkInstance.GetInstanceProcAddr("vkGetPhysicalDeviceProperties2");
        var nativeStruct = properties2.ToNative();
        var properties2Ptr = NativeUtils.StructOrEnumToPointer(nativeStruct);
        _deviceProperties2Delegate.Invoke(device, properties2Ptr);
        properties2 = new PhysicalDeviceProperties2(*properties2Ptr);
        descriptorBufferProperties = *(VkPhysicalDeviceDescriptorBufferPropertiesEXT*)properties2.PNext;
        AdapterProperties = properties2.Properties;
        DeviceBufferProperties = new PhysicalDeviceDescriptorBufferPropertiesEXT(descriptorBufferProperties);
    }
    
    public PhysicalDeviceProperties AdapterProperties { get; }
    
    public PhysicalDeviceDescriptorBufferPropertiesEXT DeviceBufferProperties { get; }

    public PhysicalDeviceType DeviceType => AdapterProperties.DeviceType;
    
    public PhysicalDevice Adapter { get; }
    
    public static implicit operator PhysicalDevice(GraphicsAdapter adapter)
    {
        return adapter._physicalDevice;
    }
}