using System;
using System.Threading.Tasks;
using Adamantium.Core;
using Adamantium.Engine.Core;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using AdamantiumVulkan.Core;

namespace Adamantium.Engine.Graphics
{
    public abstract class GraphicsPresenter : DisposableObject
    {
        public GraphicsDevice GraphicsDevice { get; private set; }

        internal PresentationParameters Description { get; set; }

        public uint BuffersCount => Description.BuffersCount;

        public uint Width => Description.Width;

        public uint Height => Description.Height;

        public SurfaceFormat ImageFormat => Description.ImageFormat;

        public DepthFormat DepthFormat => Description.DepthFormat;

        public MSAALevel MSAALevel => Description.MSAALevel;

        public RenderTarget RenderTarget => renderTarget;

        public DepthStencilBuffer DepthBuffer => depthBuffer;

        public RenderPass RenderPass 
        {
            get => renderPass;
            internal set => renderPass = value;
        }

        public Viewport Viewport { get; protected set; }

        protected RenderTarget renderTarget;
        protected DepthStencilBuffer depthBuffer;
        protected RenderPass renderPass;
        
        
        private PresentInterval presentInterval;

        public PresentInterval PresentInterval
        {
            get => presentInterval;
            set
            {
                presentInterval = value;
                RaisePropertyChanged();
            }
        }

        public Texture[] BackBuffers { get; protected set; }

        protected GraphicsPresenter(GraphicsDevice graphicsDevice, PresentationParameters description, String name = "")
        {
            Name = name;
            GraphicsDevice = graphicsDevice;
            Description = description.Clone();
            CreateDepthBuffer();
            CreateRenderPass();
            CreateViewPort();
        }

        protected void CreateDepthBuffer()
        {
            depthBuffer = ToDispose(DepthStencilBuffer.New(GraphicsDevice, Width, Height, DepthFormat, MSAALevel));
        }

        private void CreateViewPort()
        {
            Viewport = new Viewport
            {
                X = 0,
                Y = 0,
                Width = Description.Width,
                Height = Description.Height,
                MinDepth = 0.0f,
                MaxDepth = 1.0f
            };
        }
        
        /// <summary>
        /// Resize graphics presenter backBuffer according to width and height
        /// </summary>
        public bool Resize(UInt32 width = 0, UInt32 height = 0)
        {
            Description.Width = width;
            Description.Height = height;
            return Resize(Description);
        }

        /// <summary>
        /// Resize graphics presenter backbuffer according to width and height
        /// </summary>
        /// <param name="parameters"></param>
        public virtual bool Resize(PresentationParameters parameters)
        {
            Description = parameters.Clone();
            
            CreateViewPort();

            return true;
        }
        
        protected void CreateRenderPass()
        {
            var colorAttachment = new AttachmentDescription();
            colorAttachment.Format = ImageFormat;
            colorAttachment.Samples = (SampleCountFlagBits)MSAALevel;
            colorAttachment.LoadOp = AttachmentLoadOp.Clear;
            colorAttachment.StoreOp = MSAALevel == MSAALevel.None ? AttachmentStoreOp.Store : AttachmentStoreOp.DontCare;
            colorAttachment.StencilLoadOp = AttachmentLoadOp.DontCare;
            colorAttachment.StencilStoreOp = AttachmentStoreOp.DontCare;
            colorAttachment.InitialLayout = ImageLayout.Undefined;
            colorAttachment.FinalLayout = ImageLayout.ColorAttachmentOptimal;

            var depthAttachment = new AttachmentDescription();
            depthAttachment.Format = (Format)DepthFormat;
            depthAttachment.Samples = (SampleCountFlagBits)MSAALevel;
            depthAttachment.LoadOp = AttachmentLoadOp.Clear;
            depthAttachment.StoreOp = MSAALevel == MSAALevel.None ? AttachmentStoreOp.Store : AttachmentStoreOp.DontCare;
            depthAttachment.StencilLoadOp = AttachmentLoadOp.DontCare;
            depthAttachment.StencilStoreOp = AttachmentStoreOp.DontCare;
            depthAttachment.InitialLayout = ImageLayout.Undefined;
            depthAttachment.FinalLayout = DepthBuffer.ImageLayout;

            var colorAttachmentResolve = new AttachmentDescription();
            colorAttachmentResolve.Format = ImageFormat;
            colorAttachmentResolve.Samples = SampleCountFlagBits._1Bit;
            colorAttachmentResolve.LoadOp = AttachmentLoadOp.Clear;
            colorAttachmentResolve.StoreOp = AttachmentStoreOp.Store;
            colorAttachmentResolve.StencilLoadOp = AttachmentLoadOp.DontCare;
            colorAttachmentResolve.StencilStoreOp = AttachmentStoreOp.DontCare;
            colorAttachmentResolve.InitialLayout = ImageLayout.Undefined;
            colorAttachmentResolve.FinalLayout = ImageLayout.PresentSrcKhr;

            var colorAttachmentRef = new AttachmentReference();
            colorAttachmentRef.Attachment = 0;
            colorAttachmentRef.Layout = ImageLayout.ColorAttachmentOptimal;

            var depthAttachmentRef = new AttachmentReference();
            depthAttachmentRef.Attachment = 1;
            depthAttachmentRef.Layout = DepthBuffer.ImageLayout;

            var colorAttachmentResolveRef = new AttachmentReference();
            colorAttachmentResolveRef.Attachment = 2;
            colorAttachmentResolveRef.Layout = ImageLayout.ColorAttachmentOptimal;

            var subpass = new SubpassDescription();
            subpass.PipelineBindPoint = PipelineBindPoint.Graphics;
            subpass.ColorAttachmentCount = 1;
            subpass.PColorAttachments = new[] { colorAttachmentRef };
            subpass.PDepthStencilAttachment = depthAttachmentRef;
            if (MSAALevel != MSAALevel.None)
            {
                subpass.PResolveAttachments = new[] {colorAttachmentResolveRef};
            }

            SubpassDependency subpassDependency = new SubpassDependency();
            subpassDependency.SrcSubpass = Constants.VK_SUBPASS_EXTERNAL;
            subpassDependency.DstSubpass = 0;
            subpassDependency.SrcStageMask = (uint)PipelineStageFlagBits.ColorAttachmentOutputBit;
            subpassDependency.SrcAccessMask = 0;
            subpassDependency.DstStageMask = (uint) PipelineStageFlagBits.ColorAttachmentOutputBit;
            subpassDependency.DstAccessMask = (uint)(AccessFlagBits.ColorAttachmentReadBit | AccessFlagBits.ColorAttachmentWriteBit);

            AttachmentDescription[] attachments = null;
            if (MSAALevel != MSAALevel.None)
            {
                attachments = new[] { colorAttachment, depthAttachment, colorAttachmentResolve };
            }
            else
            {
                attachments = new [] { colorAttachmentResolve, depthAttachment};
            }
            
            var renderPassInfo = new RenderPassCreateInfo();
            renderPassInfo.AttachmentCount = (uint)attachments.Length;
            renderPassInfo.PAttachments = attachments;
            renderPassInfo.SubpassCount = 1;
            renderPassInfo.PSubpasses = new[] {subpass};
            renderPassInfo.DependencyCount = 1;
            renderPassInfo.PDependencies = new[] {subpassDependency};

            RenderPass = GraphicsDevice.CreateRenderPass(renderPassInfo);
        }

        public virtual Framebuffer GetFramebuffer(uint index)
        {
            return null;
        }

        /// <summary>
        /// Present rendered image on screen
        /// </summary>
        public abstract PresenterState Present();
        
        protected PresenterState ConvertState(Result result)
        {
            switch (result)
            {
                case Result.Success:
                    return PresenterState.Success;
                case Result.SuboptimalKhr:
                    return PresenterState.Suboptimal;
                case Result.ErrorDeviceLost:
                    return PresenterState.DeviceLost;
                case Result.ErrorOutOfHostMemory:
                    return PresenterState.OutOfHostMemory;
                case Result.ErrorOutOfDeviceMemory:
                    return PresenterState.OutOfDeviceMemory;
                case Result.ErrorOutOfDateKhr:
                    return PresenterState.OutOfDate;
                case Result.ErrorSurfaceLostKhr:
                    return PresenterState.SurfaceLost;
                case Result.ErrorFullScreenExclusiveModeLostExt:
                    return PresenterState.FullScreenExclusiveModeLost;
                default:
                    return PresenterState.Unknown;
            }
        }

        /// <summary>
        /// Takes screenshot from current backbuffer frame
        /// </summary>
        /// <param name="fileName">File path for image to save</param>
        /// <param name="fileType">Type of the saving image</param>
        public virtual void TakeScreenshot(String fileName, ImageFileType fileType)
        {
            Task.Factory.StartNew(() =>
            {
                RenderTarget.Save(fileName, fileType);
            }, TaskCreationOptions.LongRunning);
        }

        /// <summary>
        /// Creates new GraphicsPresenter and returns corresponding presenter based on its type
        /// </summary>
        /// <param name="device">GraphicsDevice</param>
        /// <param name="parameters">Presentation Parameters</param>
        /// <param name="name">Presenter name</param>
        /// <returns><see cref="SwapChainGraphicsPresenter"/> or <see cref="RenderTargetGraphicsPresenter"/></returns>
        /// <exception cref="NotSupportedException"></exception>
        public static GraphicsPresenter Create(GraphicsDevice device, PresentationParameters parameters, String name = "")
        {
            switch (parameters.PresenterType)
            {
                case PresenterType.Swapchain:
                    return new SwapChainGraphicsPresenter(device, parameters, name);
                case PresenterType.RenderTarget:
                    return new RenderTargetGraphicsPresenter(device, parameters, name);
                default:
                    throw new NotSupportedException($"Presenter type: {parameters.PresenterType} is not supported");
            }
        }

        protected virtual void CleanupSwapChain()
        {
            
        }

        protected override void Dispose(bool disposeManagedResources)
        {
            base.Dispose(disposeManagedResources);
            CleanupSwapChain();
        }
    }
}
