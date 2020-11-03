namespace Adamantium.Game
{
   /// <summary>
   /// Determines on which condition Game loop will be exited
   /// </summary>
   public enum ShutDownMode
   {
      /// <summary>
      /// When Last window was closed
      /// </summary>
      OnLastWindowClosed,
      
      /// <summary>
      /// When ShutDown() method was called explicitly
      /// </summary>
      OnExplicitShutDown
   }
}
