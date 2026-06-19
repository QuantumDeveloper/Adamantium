namespace Adamantium.UI.Core.Markup;

public class AumlLoadResult
{
    public object Root { get; set; }
    public List<string> Diagnostics { get; } = new();

    /// <summary>Maps each authored element instance to its position in the AUML buffer (designer go-to-source /
    /// hover highlight). Reference-keyed; template-generated children are absent (walk up to the nearest entry).</summary>
    public IReadOnlyDictionary<object, AumlSourceSpan> SourceMap { get; set; }
}
