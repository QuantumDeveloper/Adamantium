using System;
using System.Collections.Generic;
using Adamantium.Core;
using Adamantium.Graphics.Core;

namespace Adamantium.UI.Services;

public sealed class GraphicsDeviceService : PropertyChangedBase, IGraphicsDeviceService
{
    public IGraphicsDeviceFactory GraphicsDeviceFactory { get; }

    private bool isInDebugMode;

    public MainGraphicsDevice MainGraphicsDevice { get; private set; }

    public IGraphicsDevice ResourceLoaderDevice { get; private set; }

    public IReadOnlyList<IGraphicsDevice> GraphicsDevices => MainGraphicsDevice.GraphicsDevices;
        
    public bool DeviceUpdateNeeded { get; set; }

    public GraphicsDeviceService(IGraphicsDeviceFactory deviceFactory, bool isInDebug)
    {
        IsInDebugMode = isInDebug;
        GraphicsDeviceFactory = deviceFactory;
    }

    void IGraphicsDeviceService.CreateMainDevice(string name)
    {
        CreateMainDevice(name, IsInDebugMode);
        DeviceUpdateNeeded = false;
        DeviceCreated?.Invoke(this, EventArgs.Empty);
    }

    public IGraphicsDevice CreateRenderDevice()
    {
        return MainGraphicsDevice.CreateRenderDevice();
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

    internal void CreateMainDevice(string name, bool debugEnabled = true)
    {
        MainGraphicsDevice?.Dispose();
        MainGraphicsDevice = MainGraphicsDevice.Create(GraphicsDeviceFactory, 3, name, debugEnabled);
           
        ResourceLoaderDevice = MainGraphicsDevice.CreateResourceLoaderDevice();

        MainGraphicsDevice.ResourceLoaderDevice = ResourceLoaderDevice;
        MainGraphicsDevice.Disposing += GraphicsDeviceDisposing;
            
        OnDeviceCreated();
    }

    private void GraphicsDeviceDisposing(object sender, EventArgs e)
    {
        OnDeviceDisposing();
    }

    #region Events

    public event EventHandler<EventArgs> DeviceCreated;

    public event EventHandler<EventArgs> DeviceChangeBegin;

    public event EventHandler<EventArgs> DeviceChangeEnd;

    public event EventHandler<EventArgs> DeviceLost;

    public event EventHandler<EventArgs> DeviceDisposing;

    #endregion
}