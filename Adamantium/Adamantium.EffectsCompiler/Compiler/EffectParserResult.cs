using System;

namespace Adamantium.EffectsCompiler
{
   internal class EffectParserResult
   {
      public String SourceFileName;

      public String PreprocessedSource;

      public FileDependencyList DependencyList;

      public Ast.Shader Shader;
   }
}
