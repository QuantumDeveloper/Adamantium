namespace Adamantium.UI.Markup.AST.TypeReference;

public interface IAumlAstTypeReference : IAumlAstNode
{
    string Name { get; }
    
    string Namespace { get; }
    
    string Assembly { get; }
    
    bool IsMarkupExtension { get; }
    
    bool IsXmlNamespaceDeclaration { get; }
    
    bool IsResolved { get; }

    bool IsEqual(IAumlAstTypeReference other);

    string GetFullTypeName();

    bool ContainsValidData();
}