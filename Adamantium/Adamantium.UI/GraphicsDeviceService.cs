using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Core;
using Adamantium.Engine.Graphics;

namespace Adamantium.UI
{
    public sealed class GraphicsDeviceService : PropertyChangedBase, IGraphicsDeviceService, IDisposable
    {
        private MainGraphicsDevice mainGraphicsDevice;

        private bool isInDebugMode;

        public MainGraphicsDevice MainGraphicsDevice
        {
            get => mainGraphicsDevice;
            private set => mainGraphicsDevice = value;
        }

        public GraphicsDevice ResourceLoaderDevice { get; private set; }

        public IReadOnlyList<GraphicsDevice> GraphicsDevices => MainGraphicsDevice.GraphicsDevices;
        
        public bool DeviceUpdateNeeded { get; set; }

        public GraphicsDeviceService(bool isInDebug)
        {
            IsInDebugMode = isInDebug;
        }

        void IGraphicsDeviceService.CreateMainDevice(string name, bool enableDynamicRendering)
        {
            CreateMainDevice(name, enableDynamicRendering, IsInDebugMode);
            DeviceUpdateNeeded = false;
            DeviceCreated?.Invoke(this, EventArgs.Empty);
        }

        public GraphicsDevice CreateRenderDevice(PresentationParameters parameters)
        {
            return MainGraphicsDevice.CreateRenderDevice(parameters);
        }

        public void RaiseFrameFinished()
        {
            MainGraphicsDevice?.OnFrameFinished();
        }

        public bool IsReady => MainGraphicsDevice != null;

        public void ChangeOrCreateMainDevice(string name, bool forceUpdate)
        {
            if (DeviceUpdateNeeded || forceUpdate)
            {
                MainGraphicsDevice.DeviceWaitIdle();
                OnDeviceChangeBegin();
                ResourceLoaderDevice?.Dispose();

                CreateMainDevice(name, IsInDebugMode);
                
                DeviceUpdateNeeded = false;
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

        public bool IsInDebugMode
        {
            get => isInDebugMode;
            set
            {
                if (SetProperty(ref isInDebugMode, value))
                {
                    DeviceUpdateNeeded = true;
                }
            }
        }

        internal void CreateMainDevice(string name, bool enableDynamicRendering, bool debugEnabled = true)
        {
            MainGraphicsDevice?.Dispose();
            MainGraphicsDevice = MainGraphicsDevice.Create(name, enableDynamicRendering, debugEnabled);
           
            ResourceLoaderDevice = MainGraphicsDevice.CreateResourceLoaderDevice();

            MainGraphicsDevice.ResourceLoaderDevice = ResourceLoaderDevice;
            MainGraphicsDevice.Disposing += GraphicsDeviceDisposing;
            
            OnDeviceCreated();
        }

        private void GraphicsDeviceDisposing(object sender, EventArgs e)
        {
            OnDeviceDisposing();
        }

        public void Dispose()
        {
            mainGraphicsDevice?.Dispose();
            mainGraphicsDevice = null;
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