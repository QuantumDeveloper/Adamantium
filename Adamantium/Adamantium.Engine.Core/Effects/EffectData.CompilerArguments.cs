using System.Collections.Generic;
using ProtoBuf;

namespace Adamantium.Engine.Core.Effects
{
   public sealed partial class EffectData
   {
      [ProtoContract]
      public class CompilerArguments
      {
         /// <summary>
         /// The absolute path to the FX source file used to compile this effect.
         /// </summary>
         [ProtoMember(1)]
         public string FilePath;

         /// <summary>
         /// The absolute path to dependency file path generated when compiling this effect.
         /// </summary>
         [ProtoMember(2)]
         public string DependencyFilePath;

         /// <summary>
         /// The flags used to compile an effect.
         /// </summary>
         [ProtoMember(3)]
         public EffectCompilerFlags CompilerFlags;

         /// <summary>
         /// The macros used to compile this effect (may be null).
         /// </summary>
         [ProtoMember(4)]
         public List<ShaderMacro> Macros;

         /// <summary>
         /// The list of include directory used to compile this file (may be null)
         /// </summary>
         [ProtoMember(5)]
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
