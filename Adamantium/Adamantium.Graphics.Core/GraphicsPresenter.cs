using System;
using System.Threading.Tasks;
using Adamantium.Core;
using Adamantium.Graphics.Core.Presentation;
using Adamantium.Imaging;
using AdamantiumVulkan.Core;

namespace Adamantium.Graphics.Core
{
    public abstract class GraphicsPresenter : DisposableObject
    {
        protected IRenderTarget renderTarget;
        protected IDepthStencilBuffer depthBuffer;
        
        private PresentInterval presentInterval;
        protected uint currentImageIndex;
        
        public IGraphicsDevice GraphicsDevice { get; private set; }

        public PresentationParameters Description { get; private set; }

        public uint BuffersCount => Description.BuffersCount;

        public uint Width => Description.Width;

        public uint Height => Description.Height;

        public SurfaceFormat SurfaceFormat => Description.ImageFormat;

        public DepthFormat DepthFormat => Description.DepthFormat;

        public MSAALevel MSAALevel => Description.MSAALevel;

        public IRenderTarget RenderTarget => renderTarget;

        public IDepthStencilBuffer DepthBuffer => depthBuffer;
        public Viewport Viewport { get; protected set; }
        
        public PresenterType PresenterType { get; private set; }
        
        public uint CurrentImageIndex => currentImageIndex;
        
        public bool CanPresent { get; protected set; }

        public PresentInterval PresentInterval
        {
            get => presentInterval;
            set
            {
                presentInterval = value;
                RaisePropertyChanged();
            }
        }

        public ITexture[] BackBuffers { get; protected set; }

        protected GraphicsPresenter(IGraphicsDevice graphicsDevice, PresentationParameters description, String name = "")
        {
            Name = name;
            GraphicsDevice = graphicsDevice;
            Description = description.Clone();
            PresenterType = Description.PresenterType;
            CreateDepthBuffer();
            CreateViewPort();
        }

        protected void CreateDepthBuffer()
        {
            depthBuffer = ToDispose(GraphicsDevice.CreateDepthBuffer(Width, Height, DepthFormat, MSAALevel, name: $"{Name}+DepthBuffer"));
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
        
        public abstract ITexture GetImageByIndex(uint index);

        public abstract ITexture GetCurrentImage();

        public virtual bool AcquireNextImage(Fence fence, Semaphore semaphore)
        {
            CanPresent = true;
            return true;
        }

        /// <summary>
        /// Present rendered image on screen
        /// </summary>
        public abstract PresenterState Present();
        
        public PresenterState LastPresenterState { get; protected set; }

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
        
        public static GraphicsPresenter Create(IGraphicsDevice graphicsDevice, PresentationParameters parameters, string name = "")
        {
            switch (parameters.PresenterType)
            {
                case PresenterType.Swapchain:
                    return new SwapChainGraphicsPresenter(graphicsDevice, parameters, name);
                    break;
                case PresenterType.RenderTarget:
                    return new RenderTargetGraphicsPresenter(graphicsDevice, parameters, name);
                    break;
                default:
                    throw new NotSupportedException($"Presenter type: {parameters.PresenterType} is not supported");
            }
        }

        /// <summary>
        /// Takes screenshot from current backbuffer frame
        /// </summary>
        /// <param name="fileName">File path for image to save</param>
        /// <param name="fileType">Type of the saving image</param>
        public async Task TakeScreenshotAsync(String fileName, ImageFileType fileType)
        {
            await Task.Factory.StartNew(() =>
            {
                RenderTarget.Save(fileName, fileType);
            }, TaskCreationOptions.LongRunning);
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
