using System;
using Adamantium.Engine.Core;

namespace Adamantium.Engine.Compiler.Effects
{
   internal class EffectCompilerLogger: Logger
   {
      public void Error(string errorMessage, SourceSpan span)
      {
         LogMessage(new EffectCompilerMessage(LogMessageType.Error, errorMessage, span));
      }

      public void Error(string errorMessage, SourceSpan span, params object[] parameters)
      {
         if (parameters == null) throw new ArgumentNullException(nameof(parameters));
         Error(string.Format(errorMessage, parameters), span);
      }

      public void Warning(string errorMessage, SourceSpan span)
      {
         LogMessage(new EffectCompilerMessage(LogMessageType.Warning, errorMessage, span));
      }

      public void Warning(string errorMessage, SourceSpan span, params object[] parameters)
      {
         if (parameters == null) throw new ArgumentNullException("parameters");
         Warning(string.Format(errorMessage, parameters), span);
      }

      public void Info(string errorMessage, SourceSpan span)
      {
         LogMessage(new EffectCompilerMessage(LogMessageType.Info, errorMessage, span));
      }

      public void Info(string errorMessage, SourceSpan span, params object[] parameters)
      {
         if (parameters == null) throw new ArgumentNullException("parameters");
         Info(string.Format(errorMessage, parameters), span);
      }
   }
}
