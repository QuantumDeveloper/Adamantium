using Adamantium.Core.Collections;
using AdamantiumVulkan.Core;
using AdamantiumVulkan.Core.Interop;
using AdamantiumVulkan.MacOS;
using AdamantiumVulkan.Windows;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using AdamantiumVulkan;
using AdamantiumVulkan.MacOS.Interop;
using Constants = AdamantiumVulkan.Core.Constants;
using DisposableObject = Adamantium.Core.DisposableObject;

namespace Adamantium.Engine.Graphics
{
    public class VulkanInstance : DisposableObject
    {
        private const string EngineName = "AdamantiumEngine";

        private Instance instance;
        private PFN_vkDebugUtilsMessengerCallbackEXT debugCallback;

        public string ApplicationName { get; set; }

        public bool IsInDebugMode { get; private set; }

        public static ReadOnlyCollection<string> DeviceExtensions { get; private set; }

        public static ReadOnlyCollection<string> ValidationLayers { get; private set; }

        private static Dictionary<IntPtr, SurfaceKHR> availableSurfaces;

        static VulkanInstance()
        {
            availableSurfaces = new Dictionary<IntPtr, SurfaceKHR>();
            var deviceExt = new List<string>();
            deviceExt.Add(AdamantiumVulkan.Core.Constants.VK_KHR_SWAPCHAIN_EXTENSION_NAME);
            DeviceExtensions = new ReadOnlyCollection<string>(deviceExt);
            var validationLayers = new List<string>();
            validationLayers.Add("VK_LAYER_LUNARG_standard_validation");
            validationLayers.Add("VK_LAYER_LUNARG_parameter_validation");
            validationLayers.Add("VK_LAYER_LUNARG_monitor");
            ValidationLayers = new ReadOnlyCollection<string>(validationLayers);
        }

        public AdamantiumCollection<PhysicalDevice> PhysicalDevices { get; private set; }

        public PhysicalDevice MainDevice
        {
            get
            {
                var devices = PhysicalDevices;
                if (devices != null && devices.Count >= 1)
                {
                    return PhysicalDevices[0];
                }

                return null;
            }
        }

        public PhysicalDevice CurrentDevice { get; set; }

        private VulkanInstance(string appName, bool enableDebug)
        {
            debugCallback = DebugCallback;
            ApplicationName = appName;
            IsInDebugMode = enableDebug;
            CreateInstance(appName, enableDebug);
            PhysicalDevices = new AdamantiumCollection<PhysicalDevice>();
            EnumerateDevices();
            CurrentDevice = PhysicalDevices[0];
        }

        private void CreateInstance(string appName, bool enableDebug)
        {
            var appInfo = new ApplicationInfo();
            appInfo.PApplicationName = appName;
            appInfo.ApplicationVersion = AdamantiumVulkan.Core.Constants.VK_MAKE_VERSION(1, 0, 0);
            appInfo.PEngineName = EngineName;
            appInfo.EngineVersion = AdamantiumVulkan.Core.Constants.VK_MAKE_VERSION(1, 0, 0);
            appInfo.ApiVersion = AdamantiumVulkan.Core.Constants.VK_MAKE_VERSION(1, 0, 0);

            DebugUtilsMessengerCreateInfoEXT debugInfo = new DebugUtilsMessengerCreateInfoEXT();
            debugInfo.MessageSeverity = (uint)(DebugUtilsMessageSeverityFlagBitsEXT.VerboseBitExt | DebugUtilsMessageSeverityFlagBitsEXT.WarningBitExt | DebugUtilsMessageSeverityFlagBitsEXT.ErrorBitExt);
            debugInfo.MessageType = (uint)(DebugUtilsMessageTypeFlagBitsEXT.GeneralBitExt | DebugUtilsMessageTypeFlagBitsEXT.ValidationBitExt | DebugUtilsMessageTypeFlagBitsEXT.PerformanceBitExt);
            debugInfo.PfnUserCallback = debugCallback;

            var createInfo = new InstanceCreateInfo();
            createInfo.PApplicationInfo = appInfo;

            var layersAvailable = Instance.EnumerateInstanceLayerProperties();
            var extensions = Instance.EnumerateInstanceExtensionProperties();
            
//            var ext = new string[] {"VK_MVK_macos_surface", "VK_KHR_surface", "VK_KHR_swapchain"};
//            createInfo.EnabledExtensionCount = (uint)ext.Length;
//            createInfo.PpEnabledExtensionNames = ext.ToArray();

            createInfo.EnabledExtensionCount = (uint)extensions.Length;
            createInfo.PpEnabledExtensionNames = extensions.Select(x => x.ExtensionName).ToArray();

            if (enableDebug)
            {
                createInfo.EnabledLayerCount = (uint)ValidationLayers.Count;
                createInfo.PpEnabledLayerNames = ValidationLayers.ToArray();
            }

            instance = Instance.Create(createInfo);
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                //var config = instance.GetMoltenVKConfigurationMVK();

                //config.presentWithCommandBuffer = false;
                //config.synchronousQueueSubmits = false;
                //config.debugMode = true;
                //config.displayWatermark = true;
                //instance.SetMoltenVKConfigurationMVK(config);
            }

            createInfo.Dispose();
        }

        public void EnumerateDevices()
        {
            var devices = instance.EnumeratePhysicalDevices();
            PhysicalDevices.Clear();
            PhysicalDevices.AddRange(devices);
        }

        public static VulkanInstance Create(string appName, bool enableDebug)
        {
            return new VulkanInstance(appName, enableDebug);
        }

        public SurfaceKHR GetOrCreateSurface(PresentationParameters parameters)
        {
            if (availableSurfaces.ContainsKey(parameters.OutputHandle))
            {
                return availableSurfaces[parameters.OutputHandle];
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var surfaceInfo = new Win32SurfaceCreateInfoKHR();
                surfaceInfo.Hwnd = parameters.OutputHandle;
                surfaceInfo.Hinstance = parameters.HInstanceHandle;
                var surface = instance.CreateWin32Surface(surfaceInfo);

                availableSurfaces.Add(parameters.OutputHandle, surface);

                return surface;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var surfaceInfo = new MacOSSurfaceCreateInfoMVK();
                surfaceInfo.PView = parameters.OutputHandle;
                var surface = instance.CreateMacOSSurfaceMVK(surfaceInfo);

                availableSurfaces.Add(parameters.OutputHandle, surface);

                return surface;
            }

            throw new NotSupportedException("Current platform is not supported yet for Surface creation");
        }

        private uint DebugCallback(DebugUtilsMessageSeverityFlagBitsEXT messageSeverity, uint messageTypes, AdamantiumVulkan.Core.Interop.VkDebugUtilsMessengerCallbackDataEXT pCallbackData, IntPtr pUserData)
        {
            Console.WriteLine(Marshal.PtrToStringAnsi(pCallbackData.pMessage));
            return 1;
        }

        public static implicit operator Instance(VulkanInstance vkInstance)
        {
            return vkInstance.instance;
        }
    }
}
