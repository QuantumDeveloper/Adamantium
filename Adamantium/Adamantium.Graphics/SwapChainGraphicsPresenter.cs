using System;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Presentation;
using AdamantiumVulkan.Core;
using Serilog;
using VulkanImage = AdamantiumVulkan.Core.Image;

namespace Adamantium.Graphics
{
    public class SwapChainGraphicsPresenter : GraphicsPresenter
    {
        private SwapchainKHR swapchain;
        private SurfaceKHR surface;
        private VulkanImage[] images;
        private ImageView[] imageViews;
        private Queue presentQueue;
        private SwapchainKHR[] swapchains;

        public SwapChainGraphicsPresenter(
            GraphicsDevice graphicsDevice, 
            PresentationParameters description,
            string name = "") : base(graphicsDevice, description, name)
        {
            BackBuffers = new ITexture[BuffersCount];
            presentQueue = graphicsDevice.GraphicsQueue;
            swapchains = new SwapchainKHR[1];
            
            CreateSurface();
            CreateSwapchain();
            CreateRenderTarget();
            CreateImageViews();
        }

        class SwapChainSupportDetails
        {
            public SurfaceCapabilitiesKHR Capabilities;
            public SurfaceFormatKHR[] Formats;
            public PresentModeKHR[] PresentModes;
        };

        private void CreateSurface()
        {
            surface = GraphicsDevice.GetOrCreateSurface(Description);
        }

        private void CreateRenderTarget()
        {
            renderTarget = ToDispose(Graphics.RenderTarget.New(GraphicsDevice, Width, Height, MSAALevel, SurfaceFormat));
        }

        SwapChainSupportDetails QuerySwapChainSupport(PhysicalDevice device)
        {
            SwapChainSupportDetails details = new SwapChainSupportDetails();
            details.Capabilities = device.GetPhysicalDeviceSurfaceCapabilitiesKHR(surface);
            details.Formats = device.GetPhysicalDeviceSurfaceFormatsKHR(surface);
            details.PresentModes = device.GetPhysicalDeviceSurfacePresentModesKHR(surface);
            return details;
        }

        private void CreateSwapchain()
        {
            PhysicalDevice physicalDevice = GraphicsDevice.MainDevice;
            Device logicalDevice = GraphicsDevice.LogicalDevice;
            var swapChainSupport = QuerySwapChainSupport(physicalDevice);
            SurfaceFormatKHR surfaceFormat = ChooseSwapSurfaceFormat(swapChainSupport.Formats);
            PresentModeKHR presentMode = ChooseSwapPresentMode(swapChainSupport.PresentModes);
            Extent2D extent = ChooseSwapExtent(swapChainSupport.Capabilities);
            uint imageCount = swapChainSupport.Capabilities.MinImageCount;
            if (imageCount < Description.BuffersCount)
            {
                imageCount = Description.BuffersCount;
            }

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
            createInfo.ImageUsage = ImageUsageFlagBits.ColorAttachmentBit;

            var graphicsFamily =
                GraphicsDevice.MainDevice.QueueFamilyContainer.GetFamilyInfo(QueueFlagBits.GraphicsBit);
            var presentFamilyIndex = GraphicsDevice.MainDevice.QueueFamilyContainer.GetPresentFamilyIndex(surface);
            
            if (graphicsFamily.FamilyIndex != presentFamilyIndex)
            {
                var queueFamilyIndices = new[] { graphicsFamily.FamilyIndex, presentFamilyIndex };
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
            Description.Width = extent.Width;
            Description.Height = extent.Height;
            Description.ImageFormat = surfaceFormat.Format;

            createInfo.Dispose();

            images = logicalDevice.GetSwapchainImagesKHR(swapchain);
            swapchains[0] = swapchain;
        }

        private void CreateImageViews()
        {
            Device logicalDevice = GraphicsDevice.LogicalDevice;
            imageViews = new ImageView[images.Length];

            for (int i = 0; i < images.Length; i++)
            {
                var createInfo = new ImageViewCreateInfo();
                createInfo.Image = images[i];
                createInfo.ViewType = ImageViewType._2d;
                createInfo.Format = SurfaceFormat;
                ComponentMapping componentMapping = new ComponentMapping();
                componentMapping.R = ComponentSwizzle.Identity;
                componentMapping.G = ComponentSwizzle.Identity;
                componentMapping.B = ComponentSwizzle.Identity;
                componentMapping.A = ComponentSwizzle.Identity;
                createInfo.Components = componentMapping;
                ImageSubresourceRange subresourceRange = new ImageSubresourceRange();
                subresourceRange.AspectMask = ImageAspectFlagBits.ColorBit;
                subresourceRange.BaseMipLevel = 0;
                subresourceRange.LevelCount = 1;
                subresourceRange.BaseArrayLayer = 0;
                subresourceRange.LayerCount = 1;
                createInfo.SubresourceRange = subresourceRange;

                imageViews[i] = logicalDevice.CreateImageView(createInfo);
            }
        }
        
        SurfaceFormatKHR ChooseSwapSurfaceFormat(SurfaceFormatKHR[] availableFormats)
        {
            if (availableFormats.Length == 1 && availableFormats[0].Format == Format.UNDEFINED)
            {
                return new SurfaceFormatKHR() { Format = Format.B8G8R8A8_UNORM, ColorSpace = ColorSpaceKHR.SrgbNonlinearKhr };
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
            var actualExtent = new Extent2D() { Width = Description.Width, Height = Description.Height };
            
            actualExtent.Width = Math.Max(capabilities.MinImageExtent.Width, Math.Min(capabilities.MaxImageExtent.Width, actualExtent.Width));
            actualExtent.Height = Math.Max(capabilities.MinImageExtent.Height, Math.Min(capabilities.MaxImageExtent.Height, actualExtent.Height));
            
            return actualExtent;
        }

        /// <summary>
        /// Present rendered image on screen
        /// </summary>
        public override PresenterState Present()
        {
            var presentInfo =  GraphicsDevice.FillPresentInfo(swapchains);

            var result = presentQueue.QueuePresentKHR(presentInfo);
            if (result != Result.Success && result != Result.SuboptimalKhr)
            {
                Log.Logger.Information("Failed to present swap chain image");
            }

            return ConvertState(result);
        }

        /// <summary>
        /// Resize graphics presenter backbuffer according to width and height
        /// </summary>
        /// <param name="parameters"></param>
        public override bool Resize(PresentationParameters parameters)
        {
            if (!base.Resize(parameters))
            {
                return false;
            }

            try
            {
                RecreateSwapchain();
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Exception during GraphicsPresenter resizing: {ex}");
                return false;
            }

            return true;
        }

        public override ImageView GetImageView(uint index)
        {
            return imageViews[index];
        }
        
        public override VulkanImage GetImage(uint index)
        {
            return images[index];
        }

        private void RecreateSwapchain()
        {
            CleanupSwapChain();
            CreateSwapchain();
            CreateRenderTarget();
            CreateDepthBuffer();
            CreateImageViews();
        }

        protected override void CleanupSwapChain()
        {
            foreach (var view in imageViews)
            {
                GraphicsDevice.Destroy(view);
            }

            RemoveAndDispose(ref depthBuffer);
            RemoveAndDispose(ref renderTarget);

            GraphicsDevice.Destroy(swapchain);
        }

        public static implicit operator SwapchainKHR(SwapChainGraphicsPresenter presenter)
        {
            return presenter.swapchain;
        }
    }
}
