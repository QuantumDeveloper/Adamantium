using System;
using Adamantium.Core;
using Adamantium.Engine.Graphics;

namespace Adamantium.Game
{
    public sealed class GraphicsDeviceManager : IGraphicsDeviceManager, IGraphicsDeviceService, IDisposable
    {
        private GameBase gameBase;
        private GraphicsDevice graphicsDevice;

        private GraphicsDeviceParameters graphicsDeviceParameters;
        private Boolean deviceUpdateNeeded;
        private Boolean applyChanges;

        public GraphicsDevice GraphicsDevice
        {
            get { return graphicsDevice; }
            internal set { graphicsDevice = value; }
        }

        public GraphicsDeviceManager(GameBase gameBase)
        {
            this.gameBase = gameBase;
            this.gameBase.Services.Add(typeof(IGraphicsDeviceManager), this);
            this.gameBase.Services.Add(typeof(IGraphicsDeviceService), this);
            graphicsDeviceParameters = new GraphicsDeviceParameters();
            //Adapter = GraphicsAdapter.Default;
            //D2DSupportEnabled = true;
            //VideoSupportEnabled = false;
            //DebugModeEnabled = true;
        }

        void IGraphicsDeviceManager.CreateDevice()
        {
            CreateDevice(graphicsDeviceParameters);
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
                CreateDevice(graphicsDeviceParameters);
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

        //public bool D2DSupportEnabled
        //{
        //   get { return graphicsDeviceParameters.D2DSupportEnabled; }
        //   set
        //   {
        //      if (value != graphicsDeviceParameters.D2DSupportEnabled)
        //      {
        //         graphicsDeviceParameters.D2DSupportEnabled = value;
        //         deviceUpdateNeeded = true;
        //      }
        //   }
        //}

        //public bool VideoSupportEnabled
        //{
        //   get { return graphicsDeviceParameters.VideoSupportEnabled; }
        //   set
        //   {
        //      if (value != graphicsDeviceParameters.VideoSupportEnabled)
        //      {
        //         graphicsDeviceParameters.VideoSupportEnabled = value;
        //         deviceUpdateNeeded = true;
        //      }
        //   }
        //}

        //public bool DebugModeEnabled
        //{
        //    get { return graphicsDeviceParameters.DebugModeEnabled; }
        //    set
        //    {
        //        if (value != graphicsDeviceParameters.DebugModeEnabled)
        //        {
        //            graphicsDeviceParameters.DebugModeEnabled = value;
        //            deviceUpdateNeeded = true;
        //        }
        //    }
        //}

        //public GraphicsAdapter Adapter
        //{
        //   get
        //   {
        //      return graphicsDeviceParameters.Adapter;
        //   }
        //   set
        //   {
        //      if (graphicsDeviceParameters.Adapter?.Description.Luid != value?.Description.Luid)
        //      {
        //         graphicsDeviceParameters.Adapter = value;
        //         deviceUpdateNeeded = true;
        //      }
        //   }
        //}

        #region Events

        public event EventHandler<EventArgs> DeviceCreated;

        public event EventHandler<EventArgs> DeviceChangeBegin;

        public event EventHandler<EventArgs> DeviceChangeEnd;

        public event EventHandler<EventArgs> DeviceLost;

        public event EventHandler<EventArgs> DeviceDisposing;

        #endregion

        internal void CreateDevice(GraphicsDeviceParameters parameters)
        {
            //Utilities.Dispose(ref graphicsDevice);
            //GraphicsDevice = Graphics.GraphicsDevice.Create(parameters);

            //GraphicsDevice.Disposing += GraphicsDeviceDisposing;
            //OnDeviceCreated();
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
    }
}