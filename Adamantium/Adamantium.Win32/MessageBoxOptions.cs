using System;

namespace Adamantium.Win32
{
   /// <summary>
   /// Additional options for MessageBox
   /// </summary>
   [Flags]
   public enum MessageBoxOptions:uint
   {
      /// <summary>
      /// The first button is the default button.
      ///MB_DEFBUTTON1 is the default unless MB_DEFBUTTON2, MB_DEFBUTTON3, or MB_DEFBUTTON4 is specified.
      /// </summary>
      DEFBUTTON1 = (uint)0x00000000L,

      /// <summary>
      /// The second button is the default button.
      /// </summary>
      DEFBUTTON2 = (uint)0x00000100L,

      /// <summary>
      /// The third button is the default button.
      /// </summary>
      DEFBUTTON3 = (uint)0x00000200L,

      /// <summary>
      /// The fourth button is the default button.
      /// </summary>
      DEFBUTTON4 = (uint)0x00000300L,



      /// <summary>
      /// The user must respond to the message box before continuing work in the window identified by the hWnd parameter. However, the user can move to the windows of other threads and work in those windows.
      ///Depending on the hierarchy of windows in the application, the user may be able to move to other windows within the thread. All child windows of the parent of the message box are automatically disabled, but pop-up windows are not.
      ///MB_APPLMODAL is the default if neither MB_SYSTEMMODAL nor MB_TASKMODAL is specified.
      /// </summary>
      APPLMODAL = (uint)0x00000000L,

      /// <summary>
      /// Same as MB_APPLMODAL except that the message box has the WS_EX_TOPMOST style. Use system-modal message boxes to notify the user of serious, potentially damaging errors that require immediate attention (for example, running out of memory). This flag has no effect on the user's ability to interact with windows other than those associated with hWnd.
      /// </summary>
      SYSTEMMODAL = (uint)0x00001000L,

      /// <summary>
      /// Same as MB_APPLMODAL except that all the top-level windows belonging to the current thread are disabled if the hWnd parameter is NULL. Use this flag when the calling application or library does not have a window handle available but still needs to prevent input to other windows in the calling thread without suspending other threads.
      /// </summary>
      TASKMODAL = (uint)0x00002000L,


      /// <summary>
      /// Same as desktop of the interactive window station. For more information, see Window Stations.
      /// If the current input desktop is not the default desktop, MessageBox does not return until the user switches to the default desktop.
      /// </summary>
      DEFAULT_DESKTOP_ONLY = (uint)0x00020000L,

      /// <summary>
      /// The text is right-justified.
      /// </summary>
      RIGHT = (uint)0x00080000L,

      /// <summary>
      /// Displays message and caption text using right-to-left reading order on Hebrew and Arabic systems.
      /// </summary>
      RTLREADING = (uint)0x00100000L,

      /// <summary>
      /// The message box becomes the foreground window.Internally, the system calls the SetForegroundWindow function for the message box.
      /// </summary>
      SETFOREGROUND = (uint)0x00010000L,

      /// <summary>
      /// The message box is created with the WS_EX_TOPMOST window style.
      /// </summary>
      TOPMOST = (uint)0x00040000L,

      /// <summary>
      /// The caller is a service notifying the user of an event. The function displays a message box on the current active desktop, even if there is no user logged on to the computer.
      ///Terminal Services: If the calling thread has an impersonation token, the function directs the message box to the session specified in the impersonation token.
      ///If this flag is set, the hWnd parameter must be NULL. This is so that the message box can appear on a desktop other than the desktop corresponding to the hWnd.
      ///For information on security considerations in regard to using this flag, see Interactive Services.In particular, be aware that this flag can produce interactive content on a locked desktop and should therefore be used for only a very limited set of scenarios, such as resource exhaustion.
      /// </summary>
      SERVICE_NOTIFICATION = (uint)0x00200000L,


   }
}
