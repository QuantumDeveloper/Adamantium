using Adamantium.UI.Markup.Parsers;

namespace Adamantium.UI.Markup.CodeGeneration;

public interface IAumlTransformer
{
    AumlMetadataContainer Transform(
        AumlDocument document,
        ITypeResolver typeResolver,
        IDiagnosticSink diagnostics);
}