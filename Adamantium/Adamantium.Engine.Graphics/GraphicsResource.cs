using System;
using Adamantium.Core;

namespace Adamantium.Engine.Graphics
{
   /// <summary>
   /// Base class for all <see cref="GraphicsResource"/>.
   /// </summary>
   public abstract class GraphicsResource: DisposableObject
   {
      /// <summary>
      /// <see cref="GraphicsDevice"/>
      /// </summary>
      public GraphicsDevice GraphicsDevice { get; internal set; }

      internal GraphicsResource()
      {}

      /// <summary>
      /// Constaructor for <see cref="GraphicsResource"/>
      /// </summary>
      /// <param name="graphicsDevice"></param>
      /// <exception cref="ArgumentNullException"></exception>
      protected GraphicsResource(GraphicsDevice graphicsDevice)
      {
         if (graphicsDevice == null)
         {
            throw new ArgumentNullException(nameof(graphicsDevice));
         }
         GraphicsDevice = graphicsDevice;
      }
   }
}
