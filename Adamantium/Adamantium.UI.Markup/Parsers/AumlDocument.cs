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

    /// <summary>A copy, for a caller about to change it. The transform resolves types by writing INTO the tree and
    /// short-circuits on <c>IsResolved</c>, while an incremental generator hands back the tree it cached - so a second
    /// run would inherit an older compilation's resolutions and never redo them.</summary>
    public AumlDocument Clone() =>
        new()
        {
            Logger = Logger,
            HasErrors = HasErrors,
            NamespaceMappings = NamespaceMappings,
            NamespaceAliases = new Dictionary<string, string>(NamespaceAliases),
            Root = (AumlAstObjectNode)Root?.Clone(null),
            RelativeFilePath = RelativeFilePath,
            RootNamespace = RootNamespace
        };
}