using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Adamantium.Core;
using Adamantium.Engine.Graphics;

namespace Adamantium.Game
{
    public sealed class GraphicsDeviceService : PropertyChangedBase, IGraphicsDeviceService, IDisposable
    {
        private GameBase gameBase;
        private MainGraphicsDevice mainGraphicsDevice;
        private GraphicsDevice resourceLoaderDevice;

        private Boolean deviceUpdateNeeded;
        private Boolean applyChanges;

        private bool enableDebugMode;
        private List<GraphicsDevice> graphicsDevices;
        private Dictionary<string, GraphicsDevice> deviceMap;

        public MainGraphicsDevice MainGraphicsDevice
        {
            get => mainGraphicsDevice;
            private set => mainGraphicsDevice = value;
        }

        public GraphicsDevice ResourceLoaderDevice
        {
            get => resourceLoaderDevice;
            private set => resourceLoaderDevice = value;
        }

        public ReadOnlyCollection<GraphicsDevice> GraphicsDevices => graphicsDevices.AsReadOnly();

        public GraphicsDeviceService(GameBase gameBase)
        {
            this.gameBase = gameBase;
            this.gameBase.Services.RegisterInstance<IGraphicsDeviceService>(this);
            graphicsDevices = new List<GraphicsDevice>();
            deviceMap = new Dictionary<string, GraphicsDevice>();
            EnableDebugMode = true;
        }

        void IGraphicsDeviceService.CreateMainDevice()
        {
            CreateMainDevice(EnableDebugMode);
            deviceUpdateNeeded = false;
            DeviceCreated?.Invoke(this, EventArgs.Empty);
        }

        public GraphicsDevice CreateRenderDevice(PresentationParameters parameters)
        {
            var renderDevice = MainGraphicsDevice.CreateRenderDevice(parameters);
            deviceMap.Add(renderDevice.DeviceId, renderDevice);
            graphicsDevices.Add(renderDevice);
            return renderDevice;
        }

        public void RemoveDevice(GraphicsDevice device)
        {
            deviceMap.Remove(device.DeviceId);
            graphicsDevices.Remove(device);
            device?.Dispose();
        }

        public void RemoveDeviceById(string deviceId)
        {
            if (deviceMap.TryGetValue(deviceId, out var device))
            {
                device?.Dispose();
                deviceMap.Remove(deviceId);
                graphicsDevices.Remove(device);
            }
        }

        public GraphicsDevice GetDeviceById(string deviceId)
        {
            return graphicsDevices.FirstOrDefault(x => x.DeviceId == deviceId);
        }

        public GraphicsDevice UpdateDevice(string deviceId, PresentationParameters parameters)
        {
            if (deviceMap.TryGetValue(deviceId, out var device))
            {
                device?.Dispose();
                deviceMap.Remove(deviceId);
                graphicsDevices.Remove(device);
                var newDevice = MainGraphicsDevice.CreateRenderDevice(parameters);
                deviceMap.Add(deviceId, newDevice);
                graphicsDevices.Add(newDevice);
                return newDevice;
            }

            return null;
        }

        public bool IsInitialized => MainGraphicsDevice != null;

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
                CreateMainDevice(EnableDebugMode);
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

        internal void CreateMainDevice(bool debugEnabled)
        {
            Utilities.Dispose(ref mainGraphicsDevice);
            
            MainGraphicsDevice = MainGraphicsDevice.Create(gameBase.Name, debugEnabled);

            ResourceLoaderDevice = MainGraphicsDevice.CreateResourceLoaderDevice();

            MainGraphicsDevice.Disposing += GraphicsDeviceDisposing;
            OnDeviceCreated();
        }

        private void GraphicsDeviceDisposing(object sender, EventArgs e)
        {
            MainGraphicsDevice = null;
            OnDeviceDisposing();
        }

        public void Dispose()
        {
            Utilities.Dispose(ref mainGraphicsDevice);
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