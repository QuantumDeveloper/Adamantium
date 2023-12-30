using System;
using System.Collections.Generic;

namespace Adamantium.Engine.Graphics
{
    /// <summary>
    /// Interface of GraphicsDeviceService
    /// </summary>
    public interface IGraphicsDeviceService
   {
       bool IsInDebugMode { get; set; }
       
      /// <summary>
      /// Create VulkanInstance, PhysicalDevice and LogicalDevice with certain parameters
      /// </summary>
      void CreateMainDevice(string name, bool enableDynamicRendering);

      GraphicsDevice CreateRenderDevice(PresentationParameters parameters);

      void ChangeOrCreateMainDevice(string name, bool forceUpdate);

      void RaiseFrameFinished();
      
      bool IsReady { get; }
      
      bool DeviceUpdateNeeded { get; set; }
      
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
      
      IReadOnlyList<GraphicsDevice> GraphicsDevices { get; }

   }
}
