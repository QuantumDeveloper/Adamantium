using System;
using Adamantium.Engine.Core;

namespace Adamantium.UI
{
   /// <summary>
   /// Contains game frame time, total time and FPS
   /// </summary>
   public class ApplicationTime : IGameTime
   {
      /// <summary>
      /// Creates instance of GameTime class
      /// </summary>
      public ApplicationTime()
      {
         FpsCount = 60;
      }

      /// <summary>
      /// Number of frames passed since game start
      /// </summary>
      public UInt64 FramesCount { get; internal set; }

      /// <summary>
      /// current FPS
      /// </summary>
      public Single FpsCount { get; internal set; }

      /// <summary>
      /// Total application time
      /// </summary>
      public TimeSpan TotalTime { get; internal set; }

      /// <summary>
      /// Time of one frame in seconds
      /// </summary>
      public Double FrameTime { get; internal set; }

      internal void Update(Double elapsedTime, TimeSpan totalGameTime)
      {
         FrameTime = elapsedTime;
         TotalTime = totalGameTime;
      }

      internal void Update(TimeSpan totalGameTime)
      {
         TotalTime = totalGameTime;
      }
   }
}
