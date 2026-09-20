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

    /// <summary>
    /// A copy of this document, for a caller that is going to change it.
    ///
    /// <para>The transform RESOLVES types by writing back into the tree - it swaps every unresolved type reference for
    /// a resolved one and then short-circuits on <c>IsResolved</c>. That is fine for a tree parsed on the spot, and
    /// wrong for one an incremental generator handed back from its cache: the cache returns the SAME instance, so a
    /// second run would inherit resolutions made against an earlier compilation and, because of the short-circuit,
    /// never redo them. Rename a type in C# and the markup would quietly keep pointing at the old one.</para>
    ///
    /// <para>The nodes copy themselves from here down - see <see cref="IAumlAstNode.Clone"/>.</para>
    /// </summary>
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