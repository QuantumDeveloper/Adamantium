using System;
using Adamantium.Core;

namespace Adamantium.Engine.Effects
{
    internal class EffectCompilerLogger : Logger
    {
        public void Error(string errorMessage, SourceSpan span)
        {
            LogMessage(new EffectCompilerMessage(LogMessageType.Error, errorMessage, span));
        }

        public void Errors(params string[] errorMessages)
        {
            foreach(var message in errorMessages)
            {
                if (string.IsNullOrEmpty(message)) continue;

                Error(message);
            }
        }

        public void Error(string errorMessage, SourceSpan span, params object[] parameters)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            Error(string.Format(errorMessage, parameters), span);
        }

        public void Warning(string warningMessage, SourceSpan span)
        {
            LogMessage(new EffectCompilerMessage(LogMessageType.Warning, warningMessage, span));
        }

        public void Warnings(params string[] warningMessages)
        {
            foreach (var message in warningMessages)
            {
                if (string.IsNullOrEmpty(message)) continue;

                Warning(message);
            }
        }

        public void Warning(string warningMessage, SourceSpan span, params object[] parameters)
        {
            if (parameters == null) throw new ArgumentNullException("parameters");
            Warning(string.Format(warningMessage, parameters), span);
        }

        public void Info(string infoMessage, SourceSpan span)
        {
            LogMessage(new EffectCompilerMessage(LogMessageType.Info, infoMessage, span));
        }

        public void Info(string infoMessage, SourceSpan span, params object[] parameters)
        {
            if (parameters == null) throw new ArgumentNullException("parameters");
            Info(string.Format(infoMessage, parameters), span);
        }
    }
}
