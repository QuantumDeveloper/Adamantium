namespace Adamantium.UI.Designer.Host;

/// <summary>
/// Result of a designer hit-test: the markup position (1-based line/column) of the authored element under the
/// queried point, plus its rectangle in the frame's design space (for the hover frame). Returned by
/// <see cref="DesignerSession.HitTest"/>.
/// </summary>
public sealed record HitTestResult(int Line, int Position, double X, double Y, double Width, double Height);
