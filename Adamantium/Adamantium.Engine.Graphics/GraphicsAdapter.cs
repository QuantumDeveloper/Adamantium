using AdamantiumVulkan.Core;

namespace Adamantium.Engine.Graphics;

public class GraphicsAdapter
{
    private PhysicalDevice _physicalDevice;

    public GraphicsAdapter(PhysicalDevice device)
    {
        _physicalDevice = device;
        device.GetPhysicalDeviceProperties(out var proprerties);
        AdapterProperties = proprerties;
    }
    
    public PhysicalDeviceProperties AdapterProperties { get; }

    public PhysicalDeviceType DeviceType => AdapterProperties.DeviceType;
    
    public static implicit operator PhysicalDevice(GraphicsAdapter adapter)
    {
        return adapter._physicalDevice;
    }
}