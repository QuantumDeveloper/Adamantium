namespace Adamantium.Win32.RawInput
{
   public enum RawInputCommand : uint
   {
      /// <summary>
      /// Get the header information from the RAWINPUT structure.
      /// </summary>
      Header = 0x10000005,

      /// <summary>
      /// Get the raw data from the RAWINPUT structure.
      /// </summary>
      Input = 0x10000003,

   }
}
