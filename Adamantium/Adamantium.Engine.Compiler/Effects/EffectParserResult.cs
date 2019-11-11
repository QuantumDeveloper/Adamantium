﻿using System;

namespace Adamantium.Engine.Compiler.Effects
{
   internal class EffectParserResult
   {
      public String SourceFileName;

      public String PreprocessedSource;

      public FileDependencyList DependencyList;

      public Ast.Shader Shader;

      public FileIncludeHandler IncludeHandler;
   }
}