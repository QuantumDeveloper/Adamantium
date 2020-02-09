using Adamantium.Core;
using Adamantium.Engine.Core;

namespace Adamantium.Engine.Compiler.Effects
{
    class EffectCompilerMessage:LogMessage
   {
      public readonly SourceSpan Span;

      public EffectCompilerMessage(LogMessageType type, string text, SourceSpan span)
         : base(type, text)
      {
         Span = span;
      }

      public override string ToString()
      {
         return string.Format("{0}: {1} X000: {2}", Span, Type.ToString().ToLowerInvariant(), Text);
      }
   }
}
