﻿using Adamantium.Imaging;
using Adamantium.Win32;
using AdamantiumVulkan.Core;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using VulkanImage = AdamantiumVulkan.Core.Image;

namespace Adamantium.Engine.Graphics
{
    public class SwapChainGraphicsPresenter : GraphicsPresenter
    {
        private SwapchainKHR swapchain;
        private SurfaceKHR surface;
        private VulkanImage[] images;
        private ImageView[] imageViews;
        private Framebuffer[] framebuffers;
        private Queue presentQueue;
        

        public SwapChainGraphicsPresenter(GraphicsDevice graphicsDevice, PresentationParameters description, string name = "") : base(graphicsDevice, description, name)
        {
            CreateSurface();
            CreateSwapchain();
            CreateRenderTarget();
            CreateImageViews();
            CreateFramebuffers();
            BackBuffers = new Texture[BuffersCount];
            presentQueue = graphicsDevice.GraphicsQueue;
        }

        class SwapChainSupportDetails
        {
            public SurfaceCapabilitiesKHR Capabilities;
            public SurfaceFormatKHR[] Formats;
            public PresentModeKHR[] PresentModes;
        };

        private void CreateSurface()
        {
            surface = GraphicsDevice.VulkanInstance.GetOrCreateSurface(Description);
        }

        private void CreateRenderTarget()
        {
            renderTarget = ToDispose(RenderTarget.New(GraphicsDevice, Width, Height, MSAALevel, ImageFormat));
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
            Device logicalDevice = GraphicsDevice;
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
            createInfo.ImageUsage = (uint)ImageUsageFlagBits.ColorAttachmentBit;

            QueueFamilyIndices indices = physicalDevice.FindQueueFamilies(surface);
            var queueFamilyIndices = new [] { indices.graphicsFamily.Value, indices.presentFamily.Value };

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
            Description.Width = extent.Width;
            Description.Height = extent.Height;
            Description.ImageFormat = surfaceFormat.Format;

            createInfo.Dispose();

            images = logicalDevice.GetSwapchainImagesKHR(swapchain);
        }


        private void CreateImageViews()
        {
            Device logicalDevice = GraphicsDevice;
            imageViews = new ImageView[images.Length];

            for (int i = 0; i < images.Length; i++)
            {
                var createInfo = new ImageViewCreateInfo();
                createInfo.Image = images[i];
                createInfo.ViewType = ImageViewType._2d;
                createInfo.Format = ImageFormat;
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

                imageViews[i] = logicalDevice.CreateImageView(createInfo);
            }
        }
        
        private void CreateFramebuffers()
        {
            framebuffers = new Framebuffer[BuffersCount];

            for (int i = 0; i < framebuffers.Length; i++)
            {
                FramebufferCreateInfo framebufferInfo = new FramebufferCreateInfo();
                framebufferInfo.RenderPass = RenderPass;
                if (MSAALevel != MSAALevel.None)
                {
                    framebufferInfo.PAttachments = new [] { renderTarget, depthBuffer, imageViews[i] };
                }
                else
                {
                    framebufferInfo.PAttachments = new [] { imageViews[i], depthBuffer };
                }
                
                framebufferInfo.AttachmentCount = (uint)framebufferInfo.PAttachments.Length;
                framebufferInfo.Width = Width;
                framebufferInfo.Height = Height;
                framebufferInfo.Layers = 1;

                framebuffers[i] = GraphicsDevice.CreateFramebuffer(framebufferInfo);

                framebufferInfo.Dispose();
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
            Semaphore[] waitSemaphores = { GraphicsDevice.GetRenderFinishedSemaphoreForCurrentFrame() };
            var presentInfo = new PresentInfoKHR();

            presentInfo.WaitSemaphoreCount = 1;
            presentInfo.PWaitSemaphores = waitSemaphores;
            SwapchainKHR[] swapchains = { swapchain };
            presentInfo.SwapchainCount = 1;
            presentInfo.PSwapchains = swapchains;
            presentInfo.PImageIndices = new [] { GraphicsDevice.ImageIndex };

            var result = presentQueue.QueuePresentKHR(presentInfo);
            if (result != Result.Success && result != Result.SuboptimalKhr)
            {
                Debug.WriteLine("Failed to present swap chain image");
            }

            return ConvertState(result);;
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
        
        public override Framebuffer GetFramebuffer(uint index)
        {
            return framebuffers[index];
        }

        public VulkanImage GetImage(uint index)
        {
            return images[index];
        }

        private void RecreateSwapchain()
        {
            var timer = Stopwatch.StartNew();
            CleanupSwapChain();

            CreateSwapchain();
            CreateRenderTarget();
            CreateDepthBuffer();
            CreateImageViews();
            CreateFramebuffers();
            timer.Stop();
            Console.WriteLine($"Resize presenter time: {timer.ElapsedMilliseconds}");
        }

        protected override void CleanupSwapChain()
        {
            foreach (var framebuffer in framebuffers)
            {
                framebuffer.Destroy(GraphicsDevice);
            }
            
            foreach (var view in imageViews)
            {
                view.Destroy(GraphicsDevice);
            }

            RemoveAndDispose(ref depthBuffer);
            RemoveAndDispose(ref renderTarget);

            swapchain?.Destroy(GraphicsDevice);
        }
        
        public override void TakeScreenshot(String fileName, ImageFileType fileType)
        {
            Task.Factory.StartNew(() =>
            {
                // TODO: implement saving backbuffer to file
                //framebuffers[GraphicsDevice.CurrentFrame].Save(fileName, fileType);
            }, TaskCreationOptions.LongRunning);
        }

        public static implicit operator SwapchainKHR(SwapChainGraphicsPresenter presenter)
        {
            return presenter.swapchain;
        }
    }
}
