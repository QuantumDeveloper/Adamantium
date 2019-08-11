using System;

namespace Adamantium.Engine.Graphics
{
   /// <summary>
   /// Interface of GraphicsDeviceService
   /// </summary>
   public interface IGraphicsDeviceService
   {
      /// <summary>
      /// Event raising after <see cref="GraphicsDevice"/> was created
      /// </summary>
      event EventHandler<EventArgs> DeviceCreated;

      /// <summary>
      /// Event raising before <see cref="GraphicsDevice"/> is going to be recreated
      /// </summary>
      event EventHandler<EventArgs> DeviceChangeBegin;

      /// <summary>
      /// Event raising after <see cref="GraphicsDevice"/> was created again
      /// </summary>
      event EventHandler<EventArgs> DeviceChangeEnd;

      /// <summary>
      /// Event raising when something in <see cref="GraphicsDevice"/> went wrong and device could not work properly anymore
      /// </summary>
      event EventHandler<EventArgs> DeviceLost;

      /// <summary>
      /// Event raising when <see cref="GraphicsDevice"/> is disposing
      /// </summary>
      event EventHandler<EventArgs> DeviceDisposing;

      /// <summary>
      /// Returns D3DGraphicsDevice instance of GraphicsDevice manager
      /// </summary>
      GraphicsDevice GraphicsDevice { get;}

   }
}
