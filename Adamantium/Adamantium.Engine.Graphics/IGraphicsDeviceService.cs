using System;
using System.Collections.Generic;

namespace Adamantium.Engine.Graphics
{
    /// <summary>
    /// Interface of GraphicsDeviceService
    /// </summary>
    public interface IGraphicsDeviceService
   {
      /// <summary>
      /// Create VulkanInstance, PhysicalDevice and LogicalDevice with certain parameters
      /// </summary>
      void CreateMainDevice(string name);

      GraphicsDevice CreateRenderDevice(PresentationParameters parameters);

      void RemoveDevice(GraphicsDevice device);

      void RemoveDeviceById(string deviceId);

      GraphicsDevice GetDeviceById(string deviceId);

      GraphicsDevice UpdateDevice(string deviceId, PresentationParameters parameters);
      
      bool IsInitialized { get; }
      
      /// <summary>
      /// Event raising after <see cref="MainGraphicsDevice"/> was created
      /// </summary>
      event EventHandler<EventArgs> DeviceCreated;

      /// <summary>
      /// Event raising before <see cref="MainGraphicsDevice"/> is going to be recreated
      /// </summary>
      event EventHandler<EventArgs> DeviceChangeBegin;

      /// <summary>
      /// Event raising after <see cref="MainGraphicsDevice"/> was created again
      /// </summary>
      event EventHandler<EventArgs> DeviceChangeEnd;

      /// <summary>
      /// Event raising when something in <see cref="MainGraphicsDevice"/> went wrong and device could not work properly anymore
      /// </summary>
      event EventHandler<EventArgs> DeviceLost;

      /// <summary>
      /// Event raising when <see cref="MainGraphicsDevice"/> is disposing
      /// </summary>
      event EventHandler<EventArgs> DeviceDisposing;

      /// <summary>
      /// Returns D3DGraphicsDevice instance of GraphicsDevice manager
      /// </summary>
      MainGraphicsDevice MainGraphicsDevice { get;}
      
      GraphicsDevice ResourceLoaderDevice { get; }
      
      IReadOnlyCollection<GraphicsDevice> GraphicsDevices { get; }

   }
}
