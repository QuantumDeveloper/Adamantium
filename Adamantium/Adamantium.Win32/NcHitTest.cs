namespace Adamantium.Win32
{
   /// <summary>
   /// Non-client hit test values, HT*
   /// </summary>
   public enum NcHitTest
   {
      Error = -2,
      Transparent = -1,
      Nowhere = 0,
      Client = 1,
      Caption = 2,
      Sysmenu = 3,
      Growbox = 4,
      Menu = 5,
      Hscroll = 6,
      Vscroll = 7,
      Minbutton = 8,
      Maxbutton = 9,
      Left = 10,
      Right = 11,
      Top = 12,
      Topleft = 13,
      Topright = 14,
      Bottom = 15,
      Bottomleft = 16,
      Bottomright = 17,
      Border = 18,
      Object = 19,
      Close = 20,
      Help = 21
   }
}
