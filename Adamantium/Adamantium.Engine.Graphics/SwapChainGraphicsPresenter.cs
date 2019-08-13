using Adamantium.Imaging;
using System;

namespace Adamantium.Engine.Graphics
{
    public class SwapChainGraphicsPresenter : GraphicsPresenter
    {
        //private SwapChain2 swapChain;
        //PresentParameters parameters = new PresentParameters();
        public SwapChainGraphicsPresenter(GraphicsDevice graphicsDevice, PresentationParameters description, string name = "") : base(graphicsDevice, description, name)
        {
            //Dispose swapchain before creation to be 100% sure we avoid memory leak
            //RemoveAndDispose(ref swapChain);
            //SwapChainDescription1 swapChainDescription1 = new SwapChainDescription1()
            //{
            //   BufferCount = Description.BuffersCount,
            //   Format = Description.PixelFormat,
            //   Width = Description.BackBufferWidth,
            //   Height = Description.BackBufferHeight,
            //   SwapEffect = Description.SwapEffect,
            //   SampleDescription = new SampleDescription((Int32)Description.MSAALevel, 0),
            //   Usage = Description.Usage,
            //   Scaling = description.Scaling,
            //   Flags = Description.Flags,
            //   AlphaMode = description.AlphaMode
            //};

            //SwapChainFullScreenDescription fullScreenDescription = new SwapChainFullScreenDescription
            //{
            //   RefreshRate = Description.RefreshRate,
            //   ScanlineOrdering = DisplayModeScanlineOrder.Progressive,
            //   Windowed = Description.IsWindowed
            //};

            //var factory2 = new SharpDX.DXGI.Factory2();
            //var swapChain1 = new SwapChain1(factory2, GraphicsDevice, Description.OutputHandle, ref swapChainDescription1, fullScreenDescription);
            //swapChain = ToDispose(swapChain1.QueryInterface<SwapChain2>());
            //swapChain1.Dispose();
            //factory2.Dispose();

            ////Create RenderTargetView from backbuffer
            //backbuffer = ToDispose(RenderTarget2D.New(GraphicsDevice, swapChain.GetBackBuffer<SharpDX.Direct3D11.Texture2D>(0)));
        }


        /// <summary>
        /// Present rendered image on screen
        /// </summary>
        public override void Present()
        {
            //var result = swapChain.Present((int)PresentInterval, PresentFlags, parameters);
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

        public void SetFullScreen(bool fullscreen/*, Output display*/)
        {
            //swapChain.SetFullscreenState(fullscreen, display);
            IsFullScreen = fullscreen;
        }

        //public static implicit operator SwapChain2(SwapChainGraphicsPresenter presenter)
        //{
        //    return presenter.swapChain;
        //}
    }
}
