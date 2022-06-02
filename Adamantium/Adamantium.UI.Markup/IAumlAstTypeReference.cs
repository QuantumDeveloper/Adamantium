namespace Adamantium.UI.Markup;

public interface IAumlAstTypeReference : IAumlAstNode
{
    string Name { get; }
    
    string Namespace { get; }
    
    bool IsMarkupExtension { get; set; }

    bool IsEqual(IAumlAstTypeReference other);

    string GetFullTypeName();
}