using Adamantium.Core;
using Microsoft.CodeAnalysis;

namespace Adamantium.UI.Markup.CodeGeneration.Roslyn;

public class RoslynDiagnosticSink : IDiagnosticSink
{
    private SourceProductionContext _context;
    
    public RoslynDiagnosticSink(SourceProductionContext context)
    {
        _context = context;
    }
    
    public bool HasErrors { get; private set; }

    public void ReportError(string hintName, string message)
    {
        CreateDiagnostic(_context, hintName, $"{message}", DiagnosticSeverity.Error);
        HasErrors = true;
    }

    public void ReportWarning(string hintName, string message)
    {
        CreateDiagnostic(_context, hintName, $"{message}", DiagnosticSeverity.Warning);
    }

    public void ReportInfo(string hintName, string message)
    {
        CreateDiagnostic(_context, hintName, $"{message}", DiagnosticSeverity.Info);
    }

    public void ReportLogMessage(string hintName, LogMessage message)
    {
        var severity = message.Type switch
        {
            LogMessageType.Info => DiagnosticSeverity.Info,
            LogMessageType.Warning => DiagnosticSeverity.Warning,
            _ => DiagnosticSeverity.Error
        };
        CreateDiagnostic(_context, hintName, $"{message}", severity);
        HasErrors = true;
    }
    
    private void CreateDiagnostic(SourceProductionContext spc, string className, string diagnosticText, DiagnosticSeverity severity)
    {
        var descriptor = new DiagnosticDescriptor(
            id: "AUM001",
            title: $"Auml {severity}",
            messageFormat: "{0}: {1}",
            category: "Auml",
            severity,
            isEnabledByDefault: true);
        var diagnostic = Diagnostic.Create(descriptor, Location.None, $"{className}.g.cs", diagnosticText);
        spc.ReportDiagnostic(diagnostic);
    }
}