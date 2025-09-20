using Adamantium.UI.Markup.AST;

namespace Adamantium.UI.Markup.CodeGeneration;

public class AumlMetadataContainer
{
    public AumlMetadataContainer(ITypeResolver typeResolver)
    {
        NamedElements = new List<NamedElement>();
        Usings = new List<string>();
        NamedElementsMap = new Dictionary<IAumlAstNode, string>();
        SourceText = string.Empty;
        TypeResolver = typeResolver;
        DefaultTypeContainer = DefaultTypeContainer.ResolveFrom(typeResolver);
    }

    public string Namespace { get; set; }

    public string RootClassName { get; set; }

    public List<string> Usings { get; }

    public Dictionary<IAumlAstNode, string> NamedElementsMap { get; set; }

    // All named elements in the hierarchy
    public List<NamedElement> NamedElements { get; }
    
    public DefaultTypeContainer DefaultTypeContainer { get; }

    public IAumlAstNode RootNode { get; set; }
    
    public EntityType RootEntityType { get; set; }
    
    public ITypeResolver TypeResolver { get; }
        
    public string RelativeFilePath { get; set; }

    public string FileName => Path.GetFileNameWithoutExtension(RelativeFilePath);

    public string ClassName { get; set; }
        
    public bool HasSemanticErrors { get; set; }

    public string SourceText { get; set; }
        
    public string AssemblyName { get; set; }
    
    public string FullClassName => $"{Namespace}.{ClassName}";
}