using System;
using Adamantium.Core;

namespace Adamantium.Graphics.Core;

/// <summary>
/// Base class for all <see cref="GraphicsResource"/>.
/// </summary>
public abstract class GraphicsResource: DisposableObject
{
   /// <summary>
   /// <see cref="GraphicsDevice"/>
   /// </summary>
   public IGraphicsDevice GraphicsDevice { get; }

   internal GraphicsResource()
   {
         
   }

   /// <summary>
   /// Constructor for <see cref="GraphicsResource"/>
   /// </summary>
   /// <param name="graphicsDevice"></param>
   /// <exception cref="ArgumentNullException"></exception>
   protected GraphicsResource(IGraphicsDevice graphicsDevice, string name = "")
   {
      ArgumentNullException.ThrowIfNull(graphicsDevice);
      GraphicsDevice = graphicsDevice;
      GraphicsDevice.AddResource(this);
      Name = name;
   }
}