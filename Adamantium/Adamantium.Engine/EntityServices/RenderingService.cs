using System;
using System.Threading;
using Adamantium.Core;
using Adamantium.Engine.Core.Content;
using Adamantium.Engine.Graphics;
using Adamantium.Engine.Managers;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Game;
using Adamantium.Game.Core;
using Adamantium.Game.Core.Input;
using Adamantium.Game.Core.Payloads;
using Keys = Adamantium.Game.Core.Input.Keys;

namespace Adamantium.Engine.EntityServices
{
    public class RenderingService : EntityService
    {
        private readonly AutoResetEvent pauseEvent = new AutoResetEvent(false);
        
        public override bool IsUpdateService => false;
        public override bool IsRenderingService => true;
        protected IContentManager Content { get; }
        public GameOutput Window { get; }

        protected LightManager LightManager { get; }
        protected GameInputManager InputManager { get; }
        protected CameraManager CameraManager { get; }
        protected ToolsManager ToolsManager { get; }
        
        //protected SpriteBatch SpriteBatch;

        protected Camera ActiveCamera { get; set; }
        protected bool ShowDebugOutput { get; set; }

        public RenderingService(EntityWorld world, GameOutput window) : base(world)
        {
            GraphicsDeviceService = world.DependencyResolver.Resolve<IGraphicsDeviceService>();
            GraphicsDeviceService.DeviceChangeBegin += DeviceChangeBegin;
            GraphicsDeviceService.DeviceChangeEnd += DeviceChangeEnd;
            GraphicsDevice = window.GraphicsDevice;
            Content = world.DependencyResolver.Resolve<IContentManager>();
            Window = window;
            Window.ParametersChanging += Window_ParametersChanging;
            Window.ParametersChanged += Window_ParametersChanged;
            Window.StateChanged += StateChanged;
            Window.SizeChanged += WindowOnSizeChanged;
            LightManager = world.DependencyResolver.Resolve<LightManager>();
            InputManager = world.DependencyResolver.Resolve<GameInputManager>();
            CameraManager = EntityWorld.DependencyResolver.Resolve<CameraManager>();
            ToolsManager = EntityWorld.DependencyResolver.Resolve<ToolsManager>();
            //SpriteBatch = new SpriteBatch(GraphicsDevice, 80000);
        }

        private void WindowOnSizeChanged(GameOutputSizeChangedPayload obj)
        {
            //Window.UpdatePresenter();
        }

        private void StateChanged(WindowStatePayload obj)
        {
            
        }

        private void Window_ParametersChanged(GameOutputParametersPayload payload)
        {
            OnWindowParametersChanged(payload.Reason);
        }

        private void Window_ParametersChanging(GameOutputParametersPayload payload)
        {
            OnWindowParametersChanging(payload.Reason);
        }

        protected virtual void OnWindowParametersChanging(ChangeReason reason)
        { }

        protected virtual void OnWindowParametersChanged(ChangeReason reason)
        { }

        private void DeviceChangeBegin(object sender, EventArgs e)
        {
            OnDeviceChangeBegin();
        }

        private void DeviceChangeEnd(object sender, EventArgs e)
        {
            OnDeviceChangeEnd();
        }

        protected virtual void OnDeviceChangeBegin()
        {
            //SpriteBatch?.Dispose();
        }

        protected virtual void OnDeviceChangeEnd()
        {
            //SpriteBatch = new SpriteBatch(GraphicsDevice, 25000);
        }

        public virtual void CreateSystemResources()
        { }

        public override bool BeginDraw()
        {
            if (!Window.IsUpToDate() || !GraphicsDevice.BeginDraw())
            {
                return false;
            }

            GraphicsDevice.SetViewports(Window.Viewport);
            GraphicsDevice.SetScissors(Window.Scissor);
            return true;
        }

        public override void Draw(AppTime gameTime)
        {
            if (!Window.IsVisible)
            {
                pauseEvent.WaitOne();
            }
            
            AppTime = gameTime;

            if (InputManager.IsKeyPressed(Keys.P))
            {
                ShowDebugOutput = !ShowDebugOutput;
            }

            ActiveCamera = CameraManager.GetActive(Window);
            
            Processor?.Draw(gameTime);
        }

        protected virtual void Debug() { }

        public override void EndDraw()
        {
            GraphicsDevice.EndDraw();
        }

        public override void Submit()
        {
            GraphicsDevice.Submit();
        }

        public override void DisplayContent()
        {
            GraphicsDevice.Present();
        }
    }
}
