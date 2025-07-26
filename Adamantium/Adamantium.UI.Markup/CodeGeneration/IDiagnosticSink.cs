using Adamantium.Core;

namespace Adamantium.UI.Markup.CodeGeneration;

public interface IDiagnosticSink
{
    bool HasErrors { get; }
    
    void ReportError(string hintName, string message);

    void ReportWarning(string hintName, string message);

    void ReportInfo(string hintName, string message);

    void ReportLogMessage(string hintName, LogMessage message);
}