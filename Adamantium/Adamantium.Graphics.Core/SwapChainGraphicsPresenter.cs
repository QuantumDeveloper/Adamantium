using System;
using Adamantium.Graphics.Core.Extensions;
using Adamantium.Graphics.Core.Presentation;
using Adamantium.Vulkan.Core;
using Serilog;

namespace Adamantium.Graphics.Core
{
    public class SwapChainGraphicsPresenter : GraphicsPresenter
    {
        private SwapchainKHR swapchain;
        private SurfaceKHR surface;
        private ITexture[] swapchainTextures;
        private readonly Queue presentQueue;
        private readonly SwapchainKHR[] _swapchains;
        private Semaphore[] _waitSemaphores;
        private uint[] _imageIndicesArray;
        
        private Semaphore[] imageAvailableSemaphores;
        private Semaphore[] renderFinishedSemaphores;

        public SwapChainGraphicsPresenter(
            IGraphicsDevice graphicsDevice, 
            PresentationParameters description,
            string name = "") : base(graphicsDevice, description, name)
        {
            BackBuffers = new ITexture[BuffersCount];
            presentQueue = graphicsDevice.GraphicsQueue;
            _swapchains = new SwapchainKHR[1];
            _waitSemaphores = new Semaphore[1];
            _imageIndicesArray = new uint[1];
            
            CreateSurface();
            CreateSwapchain();
            CreateRenderTarget();
        }

        class SwapChainSupportDetails
        {
            public SurfaceCapabilitiesKHR Capabilities;
            public SurfaceFormatKHR[] Formats;
            public PresentModeKHR[] PresentModes;
        };
        
        public Semaphore CurrentRenderFinishedSemaphore => renderFinishedSemaphores[currentImageIndex];
        public Semaphore CurrentImageAvailableSemaphore => imageAvailableSemaphores[currentImageIndex];

        private void CreateSurface()
        {
            surface = GraphicsDevice.GetOrCreateSurface(Description);
        }

        private void CreateRenderTarget()
        {
            renderTargets = new IRenderTarget[FrameCopies];
            for (var i = 0; i < renderTargets.Length; i++)
            {
                renderTargets[i] = ToDispose(GraphicsDevice.CreateRenderTarget(Width, Height, MSAALevel, SurfaceFormat,
                    name: $"{Name}+RenderTarget{i}"));
            }
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

            var createInfo = new SwapchainCreateInfoKHR();
            createInfo.Surface = surface;

            createInfo.MinImageCount = imageCount;
            createInfo.ImageFormat = surfaceFormat.Format;
            createInfo.ImageColorSpace = surfaceFormat.ColorSpace;
            createInfo.ImageExtent = extent;
            createInfo.ImageArrayLayers = 1;
            createInfo.ImageUsage = ImageUsageFlagBits.ColorAttachmentBit | ImageUsageFlagBits.TransferDstBit;

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

            // What the SURFACE says it can do, not what we assume: composite alpha is a property of the surface, and in
            // one measured run two surfaces in the SAME process answered differently - one offered pre-multiplied, the
            // other opaque only. So it is asked for, checked, and fallen back on out loud.
            var supportedAlpha = swapChainSupport.Capabilities.SupportedCompositeAlpha;
            var wantsAlpha = Description.TransparentComposition;
            var canDoAlpha = supportedAlpha.HasFlag(CompositeAlphaFlagBitsKHR.PreMultipliedBitKhr);

            createInfo.CompositeAlpha = wantsAlpha && canDoAlpha
                ? CompositeAlphaFlagBitsKHR.PreMultipliedBitKhr
                : CompositeAlphaFlagBitsKHR.OpaqueBitKhr;

            if (wantsAlpha && !canDoAlpha)
            {
                Console.WriteLine("[Swapchain] transparency requested but this surface only offers " +
                                  $"{supportedAlpha} - the window will be opaque.");
            }
            createInfo.PresentMode = presentMode;
            createInfo.Clipped = true;

            swapchain = logicalDevice.CreateSwapchainKHR(createInfo);
            Description.Width = extent.Width;
            Description.Height = extent.Height;
            Description.ImageFormat = surfaceFormat.Format;

            var images = logicalDevice.GetSwapchainImagesKHR(swapchain);
            swapchainTextures = new ITexture[images.Length];
            for (int i = 0; i < images.Length; i++)
            {
                swapchainTextures[i] =
                    GraphicsDevice.CreateTextureFromImage(
                        images[i], 
                        Width, 
                        Height, 
                        Description.MSAALevel,
                        SurfaceFormat,
                        desiredLayout: ImageLayout.Undefined,
                        name:$"SwapchainImage_{i}");
            }
            CreateImageViews();
            _swapchains[0] = swapchain;
            
            var semaphoreInfo = new SemaphoreCreateInfo();
            imageAvailableSemaphores = new Semaphore[swapchainTextures.Length];
            renderFinishedSemaphores = new Semaphore[swapchainTextures.Length];

            for (int i = 0; i < swapchainTextures.Length; i++)
            {
                imageAvailableSemaphores[i] = logicalDevice.CreateSemaphore(semaphoreInfo);
                renderFinishedSemaphores[i] = logicalDevice.CreateSemaphore(semaphoreInfo);
            }
        }

        private void CreateImageViews()
        {
            Device logicalDevice = GraphicsDevice.LogicalDevice;

            for (int i = 0; i < swapchainTextures.Length; i++)
            {
                var createInfo = new ImageViewCreateInfo();
                createInfo.Image = swapchainTextures[i].GetImage();
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

                swapchainTextures[i].SetImageView(logicalDevice.CreateImageView(createInfo));
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

        public override ITexture GetImageByIndex(uint index)
        {
            return swapchainTextures[index];
        }

        public override ITexture GetCurrentImage()
        {
            return swapchainTextures[CurrentImageIndex];
        }

        public override bool AcquireNextImage(Fence fence, Semaphore semaphore)
        {
            // Finite timeout (1s), NOT UINT64_MAX: the spec forbids an infinite timeout when forward progress cannot be
            // guaranteed (VUID-vkAcquireNextImageKHR-surface-07783), and an infinite wait turned a transient swapchain
            // stall into a HARD freeze - the render thread blocked in AcquireNextImage forever.
            var result =
                GraphicsDevice.LogicalDevice.AcquireNextImageKHR(
                    this,
                    1_000_000_000UL,   // 1 second, in nanoseconds
                    semaphore, null,
                    ref currentImageIndex);

            if (result == Result.ErrorOutOfDateKhr)
            {
                LastPresenterState = ConvertState(result);
                CanPresent = false;
                return false;
            }

            if (result != Result.Success && result != Result.SuboptimalKhr)
            {
                // Timeout or a real error: flag the swapchain OutOfDate so the render service recreates it next frame
                // (the existing self-heal at WindowRenderService.BeginDraw), instead of the render thread spinning on a
                // permanently-failing acquire.
                Log.Logger.Error($"Failed to acquire swap chain image. Result: {result}. Flagging OutOfDate to self-heal.");
                LastPresenterState = PresenterState.OutOfDate;
                CanPresent = false;
                return false;
            }

            CanPresent = true;
            return CanPresent;
        }

        /// <summary>
        /// Present rendered image on screen
        /// </summary>
        public override PresenterState Present()
        {
            //var fence = GraphicsDevice.GetCurrentFence();
            // GraphicsDevice.LogicalDevice.WaitForFences(1U, fence, true, ulong.MaxValue);
            // GraphicsDevice.LogicalDevice.ResetFences(1u, fence);

            if (!CanPresent) return LastPresenterState;
            // One present per acquire. CanPresent is set by AcquireNextImage; consume it here so a later frame that did
            // NOT re-acquire (a skip/recreate during resize churn) can't re-present this already-presented, not-
            // reacquired image. That was the "image not acquired since last present" + "semaphore has no way to be
            // signaled" pair that lost the device on drag-resize.
            CanPresent = false;

            var presenterImage = GetCurrentImage();

            if (presenterImage.ImageLayout != ImageLayout.PresentSrcKhr)
            {
                presenterImage.TransitionImageLayout(ImageLayout.PresentSrcKhr);
            }
            
            //GraphicsDevice.LogicalDevice.WaitForFences(1U, fence, true, ulong.MaxValue);
            var presentInfo = FillPresentInfo(CurrentImageIndex);
            var result = presentQueue.QueuePresentKHR(presentInfo);
            
            if (result != Result.Success && result != Result.SuboptimalKhr)
            {
                Log.Logger.Error($"Failed to present swap chain image. Operation result was: {result}");
            }
            
            LastPresenterState = ConvertState(result);

            return LastPresenterState;
        }
        
        private PresentInfoKHR FillPresentInfo(uint imageIndex)
        {
            _waitSemaphores[0] = renderFinishedSemaphores[imageIndex];
            _imageIndicesArray[0] = imageIndex;
            
            var presentInfo = new PresentInfoKHR();
            presentInfo.WaitSemaphoreCount = 1;
            presentInfo.PWaitSemaphores = _waitSemaphores;

            presentInfo.SwapchainCount = (uint)_swapchains.Length;
            presentInfo.PSwapchains = _swapchains;
            presentInfo.PImageIndices = _imageIndicesArray;
        
            return presentInfo;
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

        private void RecreateSwapchain()
        {
            GraphicsDevice.LogicalDevice.DeviceWaitIdle();
            CleanupSwapChain();
            CreateSwapchain();
            CreateRenderTarget();
            CreateDepthBuffer();
        }

        protected override void CleanupSwapChain()
        {
            foreach (var texture in swapchainTextures)
            {
                texture?.Dispose();
            }

            DisposeFrameSurfaces();

            if (imageAvailableSemaphores != null)
            {
                for (int i = 0; i < imageAvailableSemaphores.Length; i++)
                {
                    GraphicsDevice.Destroy(imageAvailableSemaphores[i]);
                    GraphicsDevice.Destroy(renderFinishedSemaphores[i]);
                }
            }

            GraphicsDevice.Destroy(swapchain);
        }

        public static implicit operator SwapchainKHR(SwapChainGraphicsPresenter presenter)
        {
            return presenter.swapchain;
        }
    }
}
