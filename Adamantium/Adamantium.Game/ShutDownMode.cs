namespace Adamantium.Game
{
   /// <summary>
   /// Determines on which condition Run loop will be exited
   /// </summary>
   public enum ShutDownMode
   {
      /// <summary>
      /// When Main window is closed
      /// </summary>
      OnMainWindowClosed,
      
      /// <summary>
      /// When Last window is closed
      /// </summary>
      OnLastWindowClosed,
      
      /// <summary>
      /// When ShutDown() method is called explicitly
      /// </summary>
      OnExplicitShutDown
   }
}
