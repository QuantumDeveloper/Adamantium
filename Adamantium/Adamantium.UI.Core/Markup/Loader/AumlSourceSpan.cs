namespace Adamantium.UI.Core.Markup;

/// <summary>
/// Source position (1-based line/column, as the AUML parser reports it) of an authored element in the markup
/// buffer. The designer maps a clicked/hovered control back to its markup through these — go-to-source and the
/// hover highlight.
/// </summary>
public readonly record struct AumlSourceSpan(int Line, int Position);
