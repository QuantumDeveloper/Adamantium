using Adamantium.Imaging;
using Adamantium.Win32;
using AdamantiumVulkan.Core;
using AdamantiumVulkan.Windows;
using System;
using VulkanImage = AdamantiumVulkan.Core.Image;

namespace Adamantium.Engine.Graphics
{
    public class SwapChainGraphicsPresenter : GraphicsPresenter
    {
        private SwapchainKHR swapchain;
        private SurfaceKHR surface;
        private VulkanImage[] swapchainImages;
        private ImageView[] swapchainImageViews;
        private Queue presentQueue;
        private Texture[] backbuffers;
        private Framebuffer[] framebuffers;

        public SwapChainGraphicsPresenter(GraphicsDevice graphicsDevice, PresentationParameters description, string name = "") : base(graphicsDevice, description, name)
        {
            //Dispose swapchain before creation to be 100% sure we avoid memory leak
            RemoveAndDispose(ref swapchain);
            CreateSwapchain(graphicsDevice);
            CreateImageViews(graphicsDevice);

            presentQueue = graphicsDevice.GetDeviceQueue(indices.presentFamily.Value, 0);

            //Create RenderTargetView from backbuffer
            //backbuffer = ToDispose(RenderTarget2D.New(GraphicsDevice, swapChain.GetBackBuffer<SharpDX.Direct3D11.Texture2D>(0)));
        }

        class SwapChainSupportDetails
        {
            public SurfaceCapabilitiesKHR Capabilities;
            public SurfaceFormatKHR[] Formats;
            public PresentModeKHR[] PresentModes;
        };

        private void CreateSurface(IntPtr hwnd, IntPtr hInstance, Instance instance)
        {
            var surfaceInfo = new Win32SurfaceCreateInfoKHR();
            surfaceInfo.Hwnd = hwnd;
            surfaceInfo.Hinstance = hInstance;
            surface = instance.CreateWin32Surface(surfaceInfo);
        }

        SwapChainSupportDetails QuerySwapChainSupport(PhysicalDevice device)
        {
            SwapChainSupportDetails details = new SwapChainSupportDetails();
            details.Capabilities = device.GetPhysicalDeviceSurfaceCapabilitiesKHR(surface);
            details.Formats = device.GetPhysicalDeviceSurfaceFormatsKHR(surface);
            details.PresentModes = device.GetPhysicalDeviceSurfacePresentModesKHR(surface);
            return details;
        }

        private void CreateSwapchain(GraphicsDevice device)
        {
            PhysicalDevice physicalDevice = device;
            Device logicalDevice = device;
            var swapChainSupport = QuerySwapChainSupport(physicalDevice);

            SurfaceFormatKHR surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.Formats);
            PresentModeKHR presentMode = ChooseSwapPresentMode(swapChainSupport.PresentModes);
            Extent2D extent = ChooseSwapExtent(swapChainSupport.Capabilities);

            uint imageCount = swapChainSupport.Capabilities.MinImageCount + 1;
            if (swapChainSupport.Capabilities.MaxImageCount > 0 && imageCount > swapChainSupport.Capabilities.MaxImageCount)
            {
                imageCount = swapChainSupport.Capabilities.MaxImageCount;
            }

            SwapchainCreateInfoKHR createInfo = new SwapchainCreateInfoKHR();
            createInfo.Surface = surface;

            createInfo.MinImageCount = imageCount;
            createInfo.ImageFormat = surfaceFormat.Format;
            createInfo.ImageColorSpace = surfaceFormat.ColorSpace;
            createInfo.ImageExtent = extent;
            createInfo.ImageArrayLayers = 1;
            createInfo.ImageUsage = (uint)ImageUsageFlagBits.ColorAttachmentBit;

            QueueFamilyIndices indices = physicalDevice.FindQueueFamilies(surface);
            var queueFamilyIndices = new uint[] { indices.graphicsFamily.Value, indices.presentFamily.Value };

            if (indices.graphicsFamily != indices.presentFamily)
            {
                createInfo.ImageSharingMode = SharingMode.Concurrent;
                createInfo.QueueFamilyIndexCount = (uint)queueFamilyIndices.Length;
                createInfo.PQueueFamilyIndices = queueFamilyIndices;
            }
            else
            {
                createInfo.ImageSharingMode = SharingMode.Exclusive;
            }

            createInfo.PreTransform = swapChainSupport.Capabilities.CurrentTransform;
            createInfo.CompositeAlpha = CompositeAlphaFlagBitsKHR.OpaqueBitKhr;
            createInfo.PresentMode = presentMode;
            createInfo.Clipped = true;

            swapchain = logicalDevice.CreateSwapchainKHR(createInfo);

            createInfo.Dispose();

            swapchainImages = logicalDevice.GetSwapchainImagesKHR(swapchain);

            Description.ImageFormat = surfaceFormat.Format;
            //Description.BackBufferWidth = extent.Width;
            //Description.BackBufferHeight = extent.Height;
        }

        private void CreateImageViews(Device logicalDevice)
        {
            swapchainImageViews = new ImageView[swapchainImages.Length];

            for (int i = 0; i < swapchainImages.Length; i++)
            {
                var createInfo = new ImageViewCreateInfo();
                createInfo.Image = swapchainImages[i];
                createInfo.ViewType = ImageViewType._2d;
                createInfo.Format = Description.ImageFormat;
                ComponentMapping componentMapping = new ComponentMapping();
                componentMapping.R = ComponentSwizzle.Identity;
                componentMapping.G = ComponentSwizzle.Identity;
                componentMapping.B = ComponentSwizzle.Identity;
                componentMapping.A = ComponentSwizzle.Identity;
                createInfo.Components = componentMapping;
                ImageSubresourceRange subresourceRange = new ImageSubresourceRange();
                subresourceRange.AspectMask = (uint)ImageAspectFlagBits.ColorBit;
                subresourceRange.BaseMipLevel = 0;
                subresourceRange.LevelCount = 1;
                subresourceRange.BaseArrayLayer = 0;
                subresourceRange.LayerCount = 1;
                createInfo.SubresourceRange = subresourceRange;

                swapchainImageViews[i] = logicalDevice.CreateImageView(createInfo);
            }
        }

        SurfaceFormatKHR ChooseSwapSurfaceFormat(SurfaceFormatKHR[] availableFormats)
        {
            if (availableFormats.Length == 1 && availableFormats[0].Format == Format.UNDEFINED)
            {
                return new SurfaceFormatKHR() { Format = Format.B8G8R8A8_UNORM, ColorSpace = ColorSpaceKHR.ColorspaceSrgbNonlinearKhr };
            }

            foreach (var availableFormat in availableFormats)
            {
                if (availableFormat.Format == Description.ImageFormat && availableFormat.ColorSpace == (ColorSpaceKHR)Description.ImageColorSpace)
                {
                    return availableFormat;
                }
            }

            return availableFormats[0];
        }

        PresentModeKHR ChooseSwapPresentMode(PresentModeKHR[] availablePresentModes)
        {
            PresentModeKHR bestMode = PresentModeKHR.FifoKhr;

            foreach (var availablePresentMode in availablePresentModes)
            {
                if (availablePresentMode == PresentModeKHR.MailboxKhr)
                {
                    return availablePresentMode;
                }
                else if (availablePresentMode == PresentModeKHR.ImmediateKhr)
                {
                    bestMode = availablePresentMode;
                }
            }

            return bestMode;
        }

        Extent2D ChooseSwapExtent(SurfaceCapabilitiesKHR capabilities)
        {
            if (capabilities.CurrentExtent.Width != uint.MaxValue)
            {
                return capabilities.CurrentExtent;
            }
            else
            {
                Extent2D actualExtent = new Extent2D() { Width = Description.BackBufferWidth, Height = Description.BackBufferHeight };

                actualExtent.Width = Math.Max(capabilities.MinImageExtent.Width, Math.Min(capabilities.MaxImageExtent.Width, actualExtent.Width));
                actualExtent.Height = Math.Max(capabilities.MinImageExtent.Height, Math.Min(capabilities.MaxImageExtent.Height, actualExtent.Height));

                return actualExtent;
            }
        }

        /// <summary>
        /// Present rendered image on screen
        /// </summary>
        public override void Present()
        {
            var presentInfo = new PresentInfoKHR();

            presentInfo.WaitSemaphoreCount = 1;
            presentInfo.PWaitSemaphores = signalSemaphores;
            SwapchainKHR[] swapchains = { swapchain };
            presentInfo.SwapchainCount = 1;
            presentInfo.PSwapchains = swapchains;
            presentInfo.PImageIndices = new uint[] { imageIndex };

            var result = presentQueue.QueuePresentKHR(presentInfo);
            if (result == Result.ErrorOutOfDateKhr || result == Result.SuboptimalKhr)
            {
                //Console.WriteLine(result);
                RecreateSwapchain();
            }
            else if (result != Result.Success)
            {
                MessageBox.Show("Failed to present swap chain image");
                throw new Exception();
            }
        }


        /// <summary>
        /// Resize graphics presenter bacbuffer according to width and height
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="buffersCount"></param>
        /// <param name="format"></param>
        /// <param name="depthFormat"></param>
        /// <param name="flags"></param>
        public override bool Resize(int width, int height, int buffersCount, SurfaceFormat format, DepthFormat depthFormat/*, SwapChainFlags flags = SwapChainFlags.None*/)
        {
            if (!base.Resize(width, height, buffersCount, format, depthFormat/*, flags*/))
            {
                return false;
            }

            //RemoveAndDispose(ref backbuffer);

            //swapChain.ResizeBuffers(Description.BuffersCount, width, height, format, flags);

            ////Create RenderTargetView from backbuffer
            //backbuffer = ToDispose(RenderTarget2D.New(GraphicsDevice, swapChain.GetBackBuffer<SharpDX.Direct3D11.Texture2D>(0)));

            return true;
        }

        private void RecreateSwapchain()
        {
            //if (WindowState == FormWindowState.Minimized)
            //{
            //    _pauseEvent.WaitOne();
            //}

            GraphicsDevice.DeviceWaitIdle();

            CleanupSwapChain();

            CreateSwapchain(GraphicsDevice);
            CreateRenderPass();
            CreateFramebuffers();
        }

        private void CleanupSwapChain()
        {
            foreach (var buffer in framebuffers)
            {
                buffer.Destroy(GraphicsDevice);
            }

            foreach (var view in swapchainImageViews)
            {
                view.Destroy(GraphicsDevice);
            }

            swapchain?.Destroy(GraphicsDevice);
        }

        public static implicit operator SwapchainKHR(SwapChainGraphicsPresenter presenter)
        {
            return presenter.swapchain;
        }
    }
}
