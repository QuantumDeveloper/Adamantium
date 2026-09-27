using System;

namespace Adamantium.EffectsCompiler
{
   internal class EffectParserResult
   {
      public String SourceFileName;

      public String PreprocessedSource;

      public Ast.Shader Shader;
   }
}
