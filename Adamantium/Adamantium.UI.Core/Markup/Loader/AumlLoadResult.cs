namespace Adamantium.UI.Core.Markup;

public class AumlLoadResult
{
    public object Root { get; set; }
    public List<string> Diagnostics { get; } = new();
}
