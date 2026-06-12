using Adamantium.Core;
using Adamantium.UI.Markup.CodeGeneration;

namespace Adamantium.UI.Core.Markup;

/// <summary>Collects the transformer's diagnostics into a flat string list for the loader result.</summary>
internal sealed class ListDiagnosticSink : IDiagnosticSink
{
    private readonly List<string> _messages;
    public ListDiagnosticSink(List<string> messages) => _messages = messages;

    public bool HasErrors { get; private set; }

    public void ReportError(string hintName, string message) { HasErrors = true; _messages.Add($"error: {message}"); }
    public void ReportWarning(string hintName, string message) => _messages.Add($"warning: {message}");
    public void ReportInfo(string hintName, string message) => _messages.Add($"info: {message}");
    public void ReportLogMessage(string hintName, LogMessage message) => _messages.Add(message?.ToString());
}
