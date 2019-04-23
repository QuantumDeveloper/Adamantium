using System;

namespace Adamantium.Engine.Core
{
   /// <summary>
   /// Interface for implementing Application Time container
   /// </summary>
   public interface IGameTime
   {
      /// <summary>
      /// Number of frames passed since game start
      /// </summary>
      UInt64 FramesCount { get; }

      /// <summary>
      /// current FPS
      /// </summary>
       Single FpsCount { get; }

      /// <summary>
      /// Total application time
      /// </summary>
      TimeSpan TotalTime { get; }

      /// <summary>
      /// Time of one frame in seconds
      /// </summary>
      Double FrameTime { get; }
   }
}
