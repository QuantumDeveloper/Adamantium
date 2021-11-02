using System.Collections.Generic;
using System.Diagnostics;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.UI.Controls;
using Adamantium.UI.Rendering;
using AdamantiumVulkan.Core;

namespace Adamantium.UI.Processors
{
    public class WindowProcessor : UIProcessor
    {
        private IGameTime _gameTime;
        private IWindow window;
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
            var @params = new PresentationParameters(
                PresenterType.Swapchain,
                (uint)window.ClientWidth,
                (uint)window.ClientHeight,
                window.SurfaceHandle,
                MSAALevel.X4
            )
            {
                HInstanceHandle = Process.GetCurrentProcess().Handle
            };
            
            graphicsDevice = mainDevice.CreateRenderDevice(@params);
            graphicsDevice.AddDynamicStates(DynamicState.Viewport, DynamicState.Scissor);

            windowRenderer = new WindowRenderer(graphicsDevice);
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

        public override void EndDraw()
        {
            base.EndDraw();
            graphicsDevice.EndDraw();
            graphicsDevice.Present();
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
    }
}