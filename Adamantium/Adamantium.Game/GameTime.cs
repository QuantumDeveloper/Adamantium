using System;
using Adamantium.Engine.Core;

namespace Adamantium.Engine
{
   /// <summary>
   /// Contains game frame time, total time and FPS
   /// </summary>
   public class GameTime:IGameTime
   {
      /// <summary>
      /// Creates instance of GameTime class
      /// </summary>
      public GameTime()
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

      /// <summary>
      /// Updates FrameTime and TotalTime
      /// </summary>
      /// <param name="elapsedTime">Time of current frame</param>
      /// <param name="totalTime">Total time since application startup</param>
      internal void Update(Double elapsedTime, TimeSpan totalTime)
      {
         FrameTime = elapsedTime;
         TotalTime = totalTime;
      }

      /// <summary>
      /// Updates TotaTime
      /// </summary>
      /// <param name="totalGameTime">Total time since application startup</param>
      internal void Update(TimeSpan totalGameTime)
      {
         TotalTime = totalGameTime;
      }
   }
}
