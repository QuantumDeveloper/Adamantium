namespace Adamantium.Engine.Graphics
{
   /// <summary>
   /// Defines flags that describe the relationship between the adapter refresh rate and rate at which Swapvhain.Present() are completed
   /// </summary>
   public enum PresentInterval
   {
      /// <summary>
      /// No presenter synchronization. Presenter will output backbuffer as fast as possible.
      /// </summary>
      Immediate = 0,

      /// <summary>
      /// Wait for vertical retrace period. 
      /// </summary>
      One = 1,

      /// <summary>
      /// Wait for vertical retrace period. Bacbuffer will be displayed every second screen refresh.
      /// </summary>
      Two = 2,

   }
}
