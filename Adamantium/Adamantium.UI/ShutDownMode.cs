namespace Adamantium.UI
{
   /// <summary>
   /// Shutdown mode for Application
   /// </summary>
   public enum ShutDownMode
   {
      /// <summary>
      /// App will be shutted down only when ShutDown() method will called
      /// </summary>
      OnExplicitShutDown,

      /// <summary>
      /// App will be shutted down when Last window will be closed
      /// </summary>
      OnLastWindowClosed,

      /// <summary>
      /// App will be shutted down when Main window will closed
      /// </summary>
      OnMainWindowClosed
   }
}
