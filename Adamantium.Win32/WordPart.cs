namespace Adamantium.Win32
{
   /// <summary>
   /// Describes the word part from which concrete information should be decrypted
   /// </summary>
   public enum WordPart
   {
      /// <summary>
      /// Use whole variable
      /// </summary>
      WholeWord,

      /// <summary>
      /// Use only first 16 bits of the variable
      /// </summary>
      LoWord,

      /// <summary>
      /// Use only last 16 bits of the variable (from 15 to 31 bits)
      /// </summary>
      HiWord,
   }
}
