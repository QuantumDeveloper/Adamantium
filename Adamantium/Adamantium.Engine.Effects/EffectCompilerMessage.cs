using Adamantium.Core;

namespace Adamantium.Engine.Effects
{
   class EffectCompilerMessage : LogMessage
   {
      public readonly SourceSpan Span;

      public EffectCompilerMessage(LogMessageType type, string text, SourceSpan span)
         : base(type, text)
      {
         Span = span;
      }

      public override string ToString()
      {
         return $"{Span}: {Type.ToString().ToLowerInvariant()} X000: {Text}";
      }
   }
}
