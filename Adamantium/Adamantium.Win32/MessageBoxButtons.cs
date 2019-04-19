namespace Adamantium.Win32
{
   public enum MessageBoxButtons:uint
   {
      /// <summary>
      /// The message box contains three push buttons: Abort, Retry, and Ignore.
      /// </summary>
      ABORTRETRYIGNORE = (uint)0x00000002L,

      /// <summary>
      /// The message box contains three push buttons: Cancel, Try Again, Continue. Use this message box type instead of MB_ABORTRETRYIGNORE.
      /// </summary>
      CANCELTRYCONTINUE = (uint)0x00000006L,

      /// <summary>
      /// Adds a Help button to the message box. When the user clicks the Help button or presses F1, the system sends a WM_HELP message to the owner.
      /// </summary>
      HELP = (uint)0x00004000L,

      /// <summary>
      /// The message box contains one push button: OK. This is the default.
      /// </summary>
      OK = (uint)0x00000000L,

      /// <summary>
      /// The message box contains two push buttons: OK and Cancel.
      /// </summary>
      OKCANCEL = (uint)0x00000001L,

      /// <summary>
      /// The message box contains two push buttons: Retry and Cancel.
      /// </summary>
      RETRYCANCEL = (uint)0x00000005L,

      /// <summary>
      /// The message box contains two push buttons: Yes and No.
      /// </summary>
      YESNO = (uint)0x00000004L,

      /// <summary>
      /// The message box contains three push buttons: Yes, No, and Cancel.
      /// </summary>
      YESNOCANCEL = (uint)0x00000003L
   }
}
