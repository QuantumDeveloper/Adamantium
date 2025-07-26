using Adamantium.UI.Markup.Parsers;

namespace Adamantium.UI.Markup.CodeGeneration;

public interface IAumlSourceGenerator
{
    void GenerateSourceCode(AumlMetadataContainer container, ICodeOutputSink output, IDiagnosticSink diagnostics);
}