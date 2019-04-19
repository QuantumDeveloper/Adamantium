namespace Adamantium.Win32
{
   public enum MessageBoxIcon:uint
   {
      /// <summary>
      /// An exclamation-point icon appears in the message box.
      /// </summary>
      ICONEXCLAMATION = (uint)0x00000030L,

      /// <summary>
      /// An exclamation-point icon appears in the message box.
      /// </summary>
      ICONWARNING = (uint)0x00000030L,

      /// <summary>
      /// An icon consisting of a lowercase letter i in a circle appears in the message box.
      /// </summary>
      ICONINFORMATION = (uint)0x00000040L,

      /// <summary>
      /// An icon consisting of a lowercase letter i in a circle appears in the message box.
      /// </summary>
      ICONASTERISK = (uint)0x00000040L,

      /// <summary>
      /// A question-mark icon appears in the message box. The question-mark message icon is no longer recommended because it does not clearly represent a specific type of message and because the phrasing of a message as a question could apply to any message type. In addition, users can confuse the message symbol question mark with Help information. Therefore, do not use this question mark message symbol in your message boxes. The system continues to support its inclusion only for backward compatibility.
      /// </summary>
      ICONQUESTION = (uint)0x00000020L,

      /// <summary>
      /// A stop-sign icon appears in the message box.
      /// </summary>
      ICONSTOP = (uint)0x00000010L,

      /// <summary>
      /// A stop-sign icon appears in the message box.
      /// </summary>
      ICONERROR = (uint)0x00000010L,

      /// <summary>
      /// A stop-sign icon appear
      /// </summary>
      ICONHAND = (uint)0x00000010L,
   }
}
