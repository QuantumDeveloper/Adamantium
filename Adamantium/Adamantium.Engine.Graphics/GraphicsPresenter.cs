using System;
using System.Threading.Tasks;
using Adamantium.Core;
using Adamantium.Engine.Core;
using Adamantium.Imaging;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics
{
    public abstract class GraphicsPresenter : DisposableObject
    {
        public GraphicsDevice GraphicsDevice { get; private set; }

        public PresentationParameters Description { get; private set; }

        //public RenderTarget2D BackBuffer => backbuffer;

        //public DepthStencilBuffer DepthBuffer => depthbuffer;

        public ViewportF Viewport { get; protected set; }

        //protected RenderTarget2D backbuffer = null;
        //protected DepthStencilBuffer depthbuffer = null;

        private PresentInterval presentInterval = 0;
        //private PresentFlags presentFlags = PresentFlags.None;

        public PresentInterval PresentInterval
        {
            get { return presentInterval; }
            set
            {
                presentInterval = value;
                RaisePropertyChanged();
            }
        }

        public Texture[] Backbuffers { get; protected set; }

        //public PresentFlags PresentFlags
        //{
        //   get { return presentFlags; }
        //   set
        //   {
        //      presentFlags = value;
        //      RaisePropertyChanged();
        //   }
        //}


        protected GraphicsPresenter(GraphicsDevice graphicsDevice, PresentationParameters description, String name = "")
        {
            Name = name;
            GraphicsDevice = graphicsDevice;
            Description = description.Clone();
            CreateDepthBuffer();
            CreateViewPort();
        }

        private void CreateDepthBuffer()
        {
            // Create the depth buffer/stencil view
            //depthbuffer = ToDispose(DepthStencilBuffer.New(GraphicsDevice, Description.BackBufferWidth, Description.BackBufferHeight,
            //      Description.MSAALevel, Description.DepthFormat));
        }

        private void CreateViewPort()
        {
            Viewport = new ViewportF(0, 0, Description.Width, Description.Height, 0.0f, 1.0f);
        }

        /// <summary>
        /// Resize graphics presenter bacbuffer according to width and height
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="buffersCount"></param>
        /// <param name="pixelFormat"></param>
        /// <param name="depthFormat"></param>
        /// <param name="flags"></param>
        public virtual bool Resize(UInt32 width, UInt32 height, uint buffersCount, SurfaceFormat pixelFormat, DepthFormat depthFormat/*, SwapChainFlags flags = SwapChainFlags.None*/)
        {
            bool updateDepthStencil = false;
            if (Description.DepthFormat != depthFormat || (Description.Width != width || Description.Height != height))
            {
                Description.DepthFormat = depthFormat;
                updateDepthStencil = true;
            }

            //if (Description.BackBufferWidth == width && Description.BackBufferHeight == height &&
            //    Description.BuffersCount == buffersCount && Description.PixelFormat == pixelFormat
            //    && Description.Flags == flags)
            //{
            //   return false;
            //}

            //Description.BackBufferWidth = width;
            //Description.BackBufferHeight = height;
            //Description.PixelFormat = pixelFormat;
            //Description.Flags = flags;

            //if (updateDepthStencil)
            //{
            //   RemoveAndDispose(ref depthbuffer);
            //   CreateDepthBuffer();
            //}

            CreateViewPort();

            return true;
        }

        /// <summary>
        /// Present rendered image on screen
        /// </summary>
        public abstract void Present();

        public virtual void OnPresentFinished()
        {
            GraphicsDevice.UpdateCurrentFrameNumber();
        }

        /// <summary>
        /// Takes screenshot from current backbuffer frame
        /// </summary>
        /// <param name="fileName">File path for image to save</param>
        /// <param name="filetype">Type of the saving image</param>
        public void TakeScreenshot(String fileName, ImageFileType filetype)
        {
            Task.Factory.StartNew(() =>
            {
             //using (var image = backbuffer.GetDataAsImage())
             //{
             //   image.Save(fileName, filetype);
             //}
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
            if (parameters.PresenterType == PresenterType.Swapchain)
            {
                return new SwapChainGraphicsPresenter(device, parameters, name);
            }
            else if (parameters.PresenterType == PresenterType.RenderTarget)
            {
                return new RenderTargetGraphicsPresenter(device, parameters, name);
            }
            throw new NotSupportedException("Presenter type - " + parameters.PresenterType + " is not supported");
        }
    }
}
