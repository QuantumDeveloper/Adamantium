using Adamantium.UI.Markup.AST;

namespace Adamantium.UI.Core.Markup;

public class AumlLoadResult
{
    public object Root { get; set; }
    public List<string> Diagnostics { get; } = new();

    /// <summary>Maps each authored element instance to its position in the AUML buffer (designer go-to-source /
    /// hover highlight). Reference-keyed; template-generated children are absent (walk up to the nearest entry).</summary>
    public IReadOnlyDictionary<object, AumlSourceSpan> SourceMap { get; set; }

    /// <summary>The parsed AST root of this buffer. The live designer keeps it so the NEXT edit can be reconciled
    /// (diffed) against it instead of rebuilding the tree.</summary>
    public AumlAstObjectNode Ast { get; set; }

    /// <summary>True when a <see cref="AumlLoader.Reconcile"/> actually patched the live tree in place. False means it
    /// declined (e.g. the root element type changed) and the caller should do a full rebuild instead.</summary>
    public bool Reconciled { get; set; }
}
