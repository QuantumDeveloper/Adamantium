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
        private readonly List<GraphicsDevice> graphicsDevices;
        private readonly Dictionary<string, GraphicsDevice> deviceMap;

        public MainGraphicsDevice MainGraphicsDevice
        {
            get => mainGraphicsDevice;
            private set => mainGraphicsDevice = value;
        }

        public GraphicsDevice ResourceLoaderDevice { get; private set; }

        public IReadOnlyCollection<GraphicsDevice> GraphicsDevices => graphicsDevices.AsReadOnly();
        
        public bool DeviceUpdateNeeded { get; set; }

        public GraphicsDeviceService(bool isInDebug)
        {
            graphicsDevices = new List<GraphicsDevice>();
            deviceMap = new Dictionary<string, GraphicsDevice>();
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

        public bool IsReady => MainGraphicsDevice != null;

        public void ChangeOrCreateDevice(string name, bool forceUpdate)
        {
            if (DeviceUpdateNeeded || forceUpdate)
            {
                MainGraphicsDevice.DeviceWaitIdle();
                OnDeviceChangeBegin();
                ResourceLoaderDevice?.Dispose();
                foreach (var device in graphicsDevices)
                {
                    device?.Dispose();
                }
                graphicsDevices.Clear();
                deviceMap.Clear();
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