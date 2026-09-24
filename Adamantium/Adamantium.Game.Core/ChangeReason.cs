namespace Adamantium.Game
{
   /// <summary>
   /// Describes reason of <see cref="GraphicsPresenter"/> parameters change reason
   /// </summary>
   public enum ChangeReason
   {
      /// <summary>
      /// The surface or the sample count changed: the output's device and <see cref="GraphicsPresenter"/> are made anew.
      /// </summary>
      FullUpdate,

      /// <summary>
      /// The size or a buffer format changed: the <see cref="GraphicsPresenter"/> rebuilds its buffers.
      /// </summary>
      Resize,
   }
}
