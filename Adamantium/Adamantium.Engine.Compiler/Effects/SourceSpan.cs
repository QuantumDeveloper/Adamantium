namespace Adamantium.Engine.Compiler.Effects
{
   /// <summary>
   /// Location of a portion of source.
   /// </summary>
   internal struct SourceSpan
   {
      /// <summary>
      /// Path of the file.
      /// </summary>
      public string FilePath;

      /// <summary>
      /// Column of the span.
      /// </summary>
      public int Column;

      /// <summary>
      /// Line of the span.
      /// </summary>
      public int Line;

      /// <summary>
      /// Absolute index in the input string.
      /// </summary>
      public int Index;

      /// <summary>
      /// Length of the source span in the input string.
      /// </summary>
      public int Length;

      public override string ToString()
      {
         return $"{FilePath} ({Line},{Column})";
      }
   }
}
