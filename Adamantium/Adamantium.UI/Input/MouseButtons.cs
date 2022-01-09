using System;

namespace Adamantium.UI.Input;

/// <summary>
/// 
/// </summary>
[Flags]
public enum MouseButtons
{
   /// <summary>
   /// Null mouse button
   /// </summary>
   None = 0,
   /// <summary>
   /// LeftButton
   /// </summary>
   Left = 1,

   /// <summary>
   /// RightButton
   /// </summary>
   Right = 2,

   /// <summary>
   /// MiddleButton
   /// </summary>
   Middle = 4,

   /// <summary>
   /// XButton1
   /// </summary>
   XButton1 = 8,

   /// <summary>
   /// XButton2
   /// </summary>
   XButton2 = 16
}