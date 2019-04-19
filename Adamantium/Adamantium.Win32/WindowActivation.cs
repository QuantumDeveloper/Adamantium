namespace Adamantium.Win32
{
   public enum WindowActivation
   {
      /// <summary>
      /// Deactivated.
      /// </summary>
      Inactive = 0,

      /// <summary>
      /// Activated by some method other than a mouse click (for example, by a call to the SetActiveWindow function or by use of the keyboard interface to select the window).
      /// </summary>
      Active = 1,

      /// <summary>
      /// Activated by a mouse click.
      /// </summary>
      ClickActive = 2,
   }
}
