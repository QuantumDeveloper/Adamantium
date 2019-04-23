namespace Adamantium.Engine.Core
{
   /// <summary>
   /// An interface to load and unload content.
   /// </summary>
   public interface IContentable
   {
      /// <summary>
      /// Loads the content.
      /// </summary>
      void LoadContent();

      /// <summary>
      /// Called when graphics resources need to be unloaded. Override this method to unload any game-specific graphics resources.
      /// </summary>
      void UnloadContent();
   }
}
