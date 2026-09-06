using System;
using Adamantium.Vulkan.Core;

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
        // ONE call per kind, with everything wanted chained onto it - which is how Vulkan is meant to be asked. This
        // used to be a call PER STRUCTURE, each one hand-marshalled into unmanaged memory and read back with a raw
        // cast, because the binding handed an output chain back as a bare address and dropped the typed objects. It
        // marshals them into the objects the caller supplied now, so the chain can simply be built and read.
        var heapProperties = new PhysicalDeviceDescriptorHeapPropertiesEXT();
        var bufferProperties = new PhysicalDeviceDescriptorBufferPropertiesEXT { PNext = heapProperties };
        var properties2 = new PhysicalDeviceProperties2
        {
            Properties = new PhysicalDeviceProperties(),
            PNext = bufferProperties
        };

        _physicalDevice.GetPhysicalDeviceProperties2(ref properties2);

        AdapterProperties = properties2.Properties;
        DeviceBufferProperties = bufferProperties;
        DeviceHeapProperties = heapProperties;

        // descriptor-heap capture/replay is what tools like NSight need; host image copy (Vulkan 1.4) lets a render
        // target be read back straight to host memory, skipping the staging buffer + queue submit (used by
        // Texture.Save, which falls back when absent); swapchainMaintenance1 is a FEATURE and having the extension is
        // not the same thing - passing one of its structures with the feature off is invalid use, not a no-op, so it
        // is asked here and device creation enables it before anything may use it.
        var maintenance = new PhysicalDeviceSwapchainMaintenance1FeaturesKHR();
        var hostCopy = new PhysicalDeviceVulkan14Features { PNext = maintenance };
        var heapFeatures = new PhysicalDeviceDescriptorHeapFeaturesEXT { PNext = hostCopy };
        // scalarBlockLayout: a buffer member then aligns to its COMPONENT, not to its vector - a float4 to 4 instead
        // of 16 - which is how C#'s sequential layout already lays these records out. It is a FEATURE of Vulkan 1.2,
        // not a guarantee, so it is asked for rather than assumed.
        var layout12 = new PhysicalDeviceVulkan12Features();
        heapFeatures.PNext = layout12;
        layout12.PNext = hostCopy;
        var features2 = new PhysicalDeviceFeatures2 { PNext = heapFeatures };

        _physicalDevice.GetPhysicalDeviceFeatures2(ref features2);

        SupportsDescriptorHeapCaptureReplay = heapFeatures.DescriptorHeapCaptureReplay;
        SupportsHostImageCopy = hostCopy.HostImageCopy;
        SupportsSwapchainMaintenance1 = maintenance.SwapchainMaintenance1;
        SupportsScalarBlockLayout = layout12.ScalarBlockLayout;
        Supports8BitStorage = layout12.StorageBuffer8BitAccess && layout12.ShaderInt8;

        Console.WriteLine($"DescriptorHeap supported={(bool)heapFeatures.DescriptorHeap}, " +
                          $"captureReplay supported={SupportsDescriptorHeapCaptureReplay}");
        Console.WriteLine($"HostImageCopy supported={SupportsHostImageCopy}");
        Console.WriteLine($"SwapchainMaintenance1 supported={SupportsSwapchainMaintenance1}");
        Console.WriteLine($"ScalarBlockLayout supported={SupportsScalarBlockLayout}");
        Console.WriteLine($"8-bit storage supported={Supports8BitStorage} " +
                          $"(storageBuffer8BitAccess={(bool)layout12.StorageBuffer8BitAccess}, shaderInt8={(bool)layout12.ShaderInt8})");
    }
    
    public PhysicalDeviceProperties AdapterProperties { get; private set; }
    
    public PhysicalDeviceDescriptorBufferPropertiesEXT DeviceBufferProperties { get; private set; }
    
    public PhysicalDeviceDescriptorHeapPropertiesEXT DeviceHeapProperties { get; private set; }

    public bool SupportsDescriptorHeapCaptureReplay { get; private set; }

    public bool SupportsHostImageCopy { get; private set; }

    /// <summary>Whether a buffer member may align to its COMPONENT rather than to its vector. Without it a float4 in a
    /// storage buffer aligns to 16 while C# aligns the same field to 4 - invisible while every field is a float4, and
    /// a silent divergence the moment one is not.</summary>
    public bool SupportsScalarBlockLayout { get; private set; }

    /// <summary>Whether a storage buffer may hold 8-bit members and a shader may compute with them - the pair the
    /// instance records need to carry colour as four BYTES rather than as a packed word.</summary>
    public bool Supports8BitStorage { get; private set; }

    /// <summary>Whether the device supports the swapchainMaintenance1 FEATURE, not merely the extension.</summary>
    public bool SupportsSwapchainMaintenance1 { get; private set; }

    public PhysicalDeviceType DeviceType => AdapterProperties.DeviceType;
    
    public PhysicalDevice Adapter { get; }
    
    public static implicit operator PhysicalDevice(GraphicsAdapter adapter)
    {
        return adapter._physicalDevice;
    }
}