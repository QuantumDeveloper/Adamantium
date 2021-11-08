using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Rendering;
using AdamantiumVulkan.Core;

namespace Adamantium.UI.Processors
{
    public class WindowProcessor : UIProcessor
    {
        //private Mutex mutex = new Mutex(false, "adamantiumMutex");
        
        private IGameTime _gameTime;
        private IWindow window;
        private PresentationParameters parameters;
        private GraphicsDevice graphicsDevice;
        private IWindowRenderer windowRenderer;
        
        public WindowProcessor(EntityWorld world, IWindow window, MainGraphicsDevice mainDevice)
            : base(world)
        {
            this.window = window;
            CreateResources(mainDevice);
        }

        private void CreateResources(MainGraphicsDevice mainDevice)
        {
            parameters = new PresentationParameters(
                PresenterType.Swapchain,
                (uint)window.ClientWidth,
                (uint)window.ClientHeight,
                window.SurfaceHandle,
                window.MSAALevel
            )
            {
                HInstanceHandle = Process.GetCurrentProcess().Handle
            };
            
            graphicsDevice = mainDevice.CreateRenderDevice(@parameters);
            graphicsDevice.ClearColor = Colors.White;
            graphicsDevice.AddDynamicStates(DynamicState.Viewport, DynamicState.Scissor);

            windowRenderer = new WindowRenderer(graphicsDevice);
            var renderer = (WindowRenderer)windowRenderer;
            renderer.Parameters = parameters;
            windowRenderer.SetWindow(window);
        }

        public override void UnloadContent()
        {
        }
        
        public override void Update(IGameTime gameTime)
        {
            window.Update();
        }

        public override bool BeginDraw()
        {
            return IsVisible;
        }

        public override void Draw(IGameTime gameTime)
        {
            _gameTime = gameTime;
            base.Draw(gameTime);
            
            if (windowRenderer == null) return;
            
            if (graphicsDevice.BeginDraw(1, 0))
            {
                windowRenderer.Render();
            }
        }
        
        public override void EndDraw()
        {
            base.EndDraw();
            graphicsDevice.EndDraw();
            parameters.Width = (uint)window.ClientWidth;
            parameters.Height = (uint)window.ClientHeight;
            
            graphicsDevice.Present(parameters);
            if (windowRenderer.IsWindowResized)
            {
                windowRenderer.ResizePresenter(parameters);
            }
        }
    }
}