using Adamantium.Core;
using Adamantium.UI.Markup.AST;

namespace Adamantium.UI.Markup.Parsers;

public class AumlDocument
{
    public Logger Logger { get; set; }
    
    public bool HasErrors { get; set; }
    
    public NamespaceMapping[]  NamespaceMappings { get; set; }
    
    public Dictionary<string, string> NamespaceAliases { get; set; } = new ();

    public AumlAstObjectNode Root { get; set; }
    
    public string RelativeFilePath { get; set; }

    public string FileName => Path.GetFileNameWithoutExtension(RelativeFilePath);
    
    public string RootNamespace { get; set; }
}