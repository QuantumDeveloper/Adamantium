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
using Adamantium.Core;
using QuantumBinding.Utils;
using Constants = AdamantiumVulkan.Core.Constants;

namespace Adamantium.Engine.Graphics
{
    public unsafe class VulkanInstance : DisposableBase
    {
        private const string EngineName = "AdamantiumEngine";

        private Instance instance;
        private delegate* unmanaged<DebugUtilsMessageSeverityFlagBitsEXT, DebugUtilsMessageTypeFlagBitsEXT, VkDebugUtilsMessengerCallbackDataEXT*, void*, uint> debugCallback;
        private DebugUtilsMessengerEXT debugMessenger;

        public string ApplicationName { get; set; }

        public bool IsInDebugMode { get; set; }

        public static ReadOnlyCollection<string> ValidationLayers { get; private set; }

        private Dictionary<IntPtr, SurfaceKHR> availableSurfaces;

        static VulkanInstance()
        {
            var validationLayers = new List<string>();
            
            validationLayers.Add("VK_LAYER_KHRONOS_validation");
            //validationLayers.Add("VK_LAYER_LUNARG_monitor");
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
            availableSurfaces = new Dictionary<IntPtr, SurfaceKHR>();
            debugCallback = &DebugCallback;
            ApplicationName = appName;
            IsInDebugMode = enableDebug;
            CreateInstance(appName);
            PhysicalDevices = new AdamantiumCollection<PhysicalDevice>();
            EnumerateDevices();
            CurrentDevice = PhysicalDevices[0];
        }
        
        private void CreateInstance(string appName)
        {
            var appInfo = new ApplicationInfo();
            appInfo.PApplicationName = appName;
            appInfo.ApplicationVersion = AdamantiumVulkan.Core.Constants.VK_MAKE_API_VERSION(1, 0, 0, 0);
            appInfo.PEngineName = EngineName;
            appInfo.EngineVersion = AdamantiumVulkan.Core.Constants.VK_MAKE_API_VERSION(1, 0, 0, 0);
            appInfo.ApiVersion = AdamantiumVulkan.Core.Constants.VK_MAKE_API_VERSION(1, 3, 224, 0);

            var createInfo = new InstanceCreateInfo();
            createInfo.PApplicationInfo = appInfo;

            var layersAvailable = Instance.EnumerateInstanceLayerProperties();
            var extensions = Instance.EnumerateInstanceExtensionProperties();

            //var ext = new string[] { "VK_MVK_macos_surface", "VK_KHR_surface", "VK_KHR_swapchain" };
            //createInfo.EnabledExtensionCount = (uint)ext.Length;
            //createInfo.PpEnabledExtensionNames = ext.ToArray();
            
            createInfo.PEnabledExtensionNames = extensions.Select(x => x.ExtensionName).ToArray();//.Except(new []{"VK_KHR_surface_protected_capabilities"}).ToArray();
            createInfo.EnabledExtensionCount = (uint)createInfo.PEnabledExtensionNames.Length;

            if (IsInDebugMode)
            {
                createInfo.EnabledLayerCount = (uint)ValidationLayers.Count;
                createInfo.PEnabledLayerNames = ValidationLayers.ToArray();
            }

            instance = Instance.Create(createInfo);
            NativePointer = new IntPtr(instance.NativePointer);

            createInfo.Dispose();

            if (IsInDebugMode)
            {
                EnableDebug();
            }
        }

        private void EnableDebug()
        {
            DebugUtilsMessengerCreateInfoEXT debugInfo = new DebugUtilsMessengerCreateInfoEXT();
            debugInfo.MessageSeverity = (DebugUtilsMessageSeverityFlagBitsEXT.InfoBitExt |
                                         DebugUtilsMessageSeverityFlagBitsEXT.WarningBitExt |
                                         DebugUtilsMessageSeverityFlagBitsEXT.ErrorBitExt);
            debugInfo.MessageType = (DebugUtilsMessageTypeFlagBitsEXT.GeneralBitExt |
                                     DebugUtilsMessageTypeFlagBitsEXT.ValidationBitExt |
                                     DebugUtilsMessageTypeFlagBitsEXT.PerformanceBitExt);
            debugInfo.PfnUserCallback = debugCallback;
            CreateDebugUtilsMessenger(debugInfo, out debugMessenger);
        }

        public IntPtr NativePointer { get; private set; }

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
            if (availableSurfaces.TryGetValue(parameters.OutputHandle, out var createSurface))
            {
                return createSurface;
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
            
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var surfaceInfo = new MacOSSurfaceCreateInfoMVK();
                surfaceInfo.PView = parameters.OutputHandle.ToPointer();
                var surface = instance.CreateMacOSSurfaceMVK(surfaceInfo);

                availableSurfaces.Add(parameters.OutputHandle, surface);

                return surface;
            }

            throw new NotSupportedException("Current platform is not supported yet for Surface creation");
        }
        
        private Result CreateDebugUtilsMessenger(DebugUtilsMessengerCreateInfoEXT pCreateInfo, out DebugUtilsMessengerEXT pDebugMessenger)
        {
            pDebugMessenger = null;
            var ptr = instance.GetInstanceProcAddr("vkCreateDebugUtilsMessengerEXT");
            var func = new PFN_vkCreateDebugUtilsMessengerEXT(ptr);
            var infoPtr = NativeUtils.StructOrEnumToPointer(pCreateInfo.ToNative());
            var result = func.Invoke(instance, infoPtr, null, out var pDebugMessenger_t);
            pCreateInfo.Dispose();
            NativeUtils.Free(infoPtr);
            pDebugMessenger = new DebugUtilsMessengerEXT(pDebugMessenger_t);
            return result;
        }

        private void DestroyDebugUtilsMessenger(DebugUtilsMessengerEXT debugMessenger)
        {
            var ptr = instance.GetInstanceProcAddr("vkDestroyDebugUtilsMessengerEXT");
            var func = new PFN_vkDestroyDebugUtilsMessengerEXT(ptr);
            func.Invoke(instance, debugMessenger, null);
        }

        [UnmanagedCallersOnly]
        private static uint DebugCallback(DebugUtilsMessageSeverityFlagBitsEXT messageSeverity, DebugUtilsMessageTypeFlagBitsEXT messageTypes, VkDebugUtilsMessengerCallbackDataEXT* pCallbackData, void* pUserData)
        {
            var data = *pCallbackData;
            Console.WriteLine(new string(data.pMessage));
            return 0;
        }

        public static implicit operator Instance(VulkanInstance vkInstance)
        {
            return vkInstance.instance;
        }

        protected override void Dispose(bool disposeManaged)
        {
            foreach (var surface in availableSurfaces)
            {
                instance?.DestroySurfaceKHR(surface.Value);
            }

            if (IsInDebugMode)
            {
                DestroyDebugUtilsMessenger(debugMessenger);
            }

            instance?.Dispose();
            instance = null;
            PhysicalDevices.Clear();
        }
    }
}
