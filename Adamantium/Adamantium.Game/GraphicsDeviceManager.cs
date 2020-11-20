using System;
using Adamantium.Core;
using Adamantium.Engine.Graphics;

namespace Adamantium.Game
{
    public sealed class GraphicsDeviceManager : PropertyChangedBase, IGraphicsDeviceManager, IGraphicsDeviceService, IDisposable
    {
        private GameBase gameBase;
        private GraphicsDevice graphicsDevice;
        private VulkanInstance vulkanInstance;
        
        private Boolean deviceUpdateNeeded;
        private Boolean applyChanges;

        private bool enableDebugMode;

        public GraphicsDevice GraphicsDevice
        {
            get => graphicsDevice;
            internal set => graphicsDevice = value;
        }

        public GraphicsDeviceManager(GameBase gameBase)
        {
            this.gameBase = gameBase;
            this.gameBase.Services.RegisterInstance<IGraphicsDeviceManager>(this);
            this.gameBase.Services.RegisterInstance<IGraphicsDeviceService>(this);
            EnableDebugMode = true;
        }

        void IGraphicsDeviceManager.CreateDevice()
        {
            CreateDevice(EnableDebugMode);
            deviceUpdateNeeded = false;
            DeviceCreated?.Invoke(this, EventArgs.Empty);
        }

        bool IGraphicsDeviceManager.BeginScene()
        {
            if (GraphicsDevice == null)
            {
                return false;
            }

            //switch (GraphicsDevice.DeviceStatus)
            //{
            //      case GraphicsDeviceStatus.Normal:
            //      GraphicsDevice.ClearState();
            //      break;

            //   default:
            //      Thread.Sleep(20);
            //      try
            //      {
            //         OnDeviceLost();
            //         ChangeOrCreateDevice(true);
            //      }
            //      catch (Exception)
            //      {
            //         return false;
            //      }
            //      break;
            //}
            return true;
        }

        //private void SetDefaultRenderTargets(GraphicsPresenter presenter)
        //{
        //   GraphicsDevice.SetRenderTargets(presenter.DepthBuffer, presenter.BackBuffer);
        //   GraphicsDevice.SetViewport(presenter.Viewport);
        //}


        void IGraphicsDeviceManager.EndScene()
        { }

        void IGraphicsDeviceManager.PrepareForNextFrame()
        {
            if (deviceUpdateNeeded && applyChanges)
            {
                ChangeOrCreateDevice(false);
                applyChanges = false;
            }
        }

        public void ApplyChanges()
        {
            applyChanges = true;
        }

        private void ChangeOrCreateDevice(bool forceUpdate)
        {
            if (forceUpdate)
            {
                //GraphicsAdapter.Reset();
                //graphicsDeviceParameters.Adapter = GraphicsAdapter.Default;
            }

            if (deviceUpdateNeeded || forceUpdate)
            {
                OnDeviceChangeBegin();
                CreateDevice(EnableDebugMode);
                deviceUpdateNeeded = false;
                OnDeviceChangeEnd();
            }
        }

        private void OnDeviceCreated()
        {
            RaiseEvent(DeviceCreated, this, EventArgs.Empty);
        }

        private void OnDeviceChangeBegin()
        {
            RaiseEvent(DeviceChangeBegin, this, EventArgs.Empty);
        }

        private void OnDeviceChangeEnd()
        {
            RaiseEvent(DeviceChangeEnd, this, EventArgs.Empty);
        }

        private void OnDeviceDisposing()
        {
            RaiseEvent(DeviceDisposing, this, EventArgs.Empty);
        }

        private void OnDeviceLost()
        {
            RaiseEvent(DeviceLost, this, EventArgs.Empty);
        }

        private void RaiseEvent<T>(EventHandler<T> handler, object sender, T args) where T : EventArgs
        {
            handler?.Invoke(sender, args);
        }

        public bool EnableDebugMode
        {
            get => enableDebugMode;
            set
            {
                if (SetProperty(ref enableDebugMode, value))
                {
                    deviceUpdateNeeded = true;
                }
            }
        }

        internal void CreateDevice(bool debugEnabled)
        {
            Utilities.Dispose(ref graphicsDevice);
            vulkanInstance = VulkanInstance.Create(gameBase.Name, debugEnabled);
            GraphicsDevice = GraphicsDevice.Create(vulkanInstance, vulkanInstance.CurrentDevice);

            GraphicsDevice.Disposing += GraphicsDeviceDisposing;
            OnDeviceCreated();
        }

        private void GraphicsDeviceDisposing(object sender, EventArgs e)
        {
            GraphicsDevice = null;
            OnDeviceDisposing();
        }

        public void Dispose()
        {
            Utilities.Dispose(ref graphicsDevice);
        }
        
        #region Events

        public event EventHandler<EventArgs> DeviceCreated;

        public event EventHandler<EventArgs> DeviceChangeBegin;

        public event EventHandler<EventArgs> DeviceChangeEnd;

        public event EventHandler<EventArgs> DeviceLost;

        public event EventHandler<EventArgs> DeviceDisposing;

        #endregion
    }
}