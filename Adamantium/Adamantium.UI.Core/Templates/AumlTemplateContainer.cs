using Adamantium.UI.Markup;
using Adamantium.UI.Markup.AST;

namespace Adamantium.UI.Core.Templates;

public class AumlTemplateContainer
{
    public AumlTemplateContainer()
    {
        NamedElementsMap = new Dictionary<IAumlAstNode, string>();
        NamedElements = new List<NamedElement>();
        TypesMap = new Dictionary<string, Type>();
    }
    
    public Dictionary<IAumlAstNode, string> NamedElementsMap { get; set; }

    // All named elements in the hierarchy
    public List<NamedElement> NamedElements { get; }

    public Dictionary<string, Type> TypesMap { get; }

    public IAumlAstNode RootNode { get; set; }
}