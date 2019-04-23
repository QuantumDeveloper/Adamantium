using Adamantium.Engine.Graphics;
using SharpDX.Direct3D;

namespace Adamantium.Engine
{
   /// <summary>
   /// Interface containing stubs to control <see cref="D3DGraphicsDevice"/> lifecycle inside <see cref="GameBase"/> class
   /// </summary>
   public interface IGraphicsDeviceManager
   {
      /// <summary>
      /// Create D3DGraphicsDevice with received parameters
      /// </summary>
      void CreateDevice();

      /// <summary>
      /// Calling before start of drawing
      /// </summary>
      bool BeginScene();

      /// <summary>
      /// Calling after scene was draw
      /// </summary>
      void EndScene();

      /// <summary>
      /// Called after EndScene to update all devices and resources to avoid resizing issues and black screens
      /// </summary>
      void PrepareForNextFrame();

      /// <summary>
      /// Called to apply new changes to the GraphicsDevice
      /// </summary>
      void ApplyChanges();

      /// <summary>
      /// Enable or disable support for D2D interop
      /// </summary>
      bool D2DSupportEnabled { get; set; }

      /// <summary>
      /// Enable or disable video playback support
      /// </summary>
      bool VideoSupportEnabled { get; set; }

      /// <summary>
      /// Enable or disable debug mode with detao;ed output
      /// </summary>
      bool DebugModeEnabled { get; set; }

      /// <summary>
      /// Adapter on which device will create
      /// </summary>
      GraphicsAdapter Adapter { get; set; }

      /// <summary>
      /// Device feature level
      /// </summary>
      FeatureLevel Profile { get; set; }
   }
}
