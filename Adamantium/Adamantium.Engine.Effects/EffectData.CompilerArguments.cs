using System.Collections.Generic;
using MessagePack;

namespace Adamantium.Engine.Effects
{
   public sealed partial class EffectData
   {
      [MessagePackObject]
      public class CompilerArguments
      {
         /// <summary>
         /// The absolute path to the FX source file used to compile this effect.
         /// </summary>
         [Key(0)]
         public string FilePath;

         /// <summary>
         /// The absolute path to dependency file path generated when compiling this effect.
         /// </summary>
         [IgnoreMember]
         public string DependencyFilePath;

         /// <summary>
         /// The flags used to compile an effect.
         /// </summary>
         [Key(1)]
         public EffectCompilerFlags CompilerFlags;

         /// <summary>
         /// The macros used to compile this effect (may be null).
         /// </summary>
         [Key(2)]
         public List<ShaderMacro> Macros;

         /// <summary>
         /// The list of include directory used to compile this file (may be null)
         /// </summary>
         [Key(3)]
         public List<string> IncludeDirectoryList;

         /// <summary>
         /// Returns a string that represents the current object.
         /// </summary>
         /// <returns>
         /// A string that represents the current object.
         /// </returns>
         /// <filterpriority>2</filterpriority>
         public override string ToString()
         {
            return $"FilePath: {FilePath}, CompilerFlags: {CompilerFlags}";
         }
      }
   }
}
