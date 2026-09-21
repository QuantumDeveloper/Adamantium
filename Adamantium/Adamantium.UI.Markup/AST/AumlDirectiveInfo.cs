namespace Adamantium.UI.Markup.AST;

/// <summary>One AUML <c>x:</c> directive: its local name, the description tooling shows, where it may be written, and
/// whether its value names a CLR type (so tooling completes type names in the value - both the plain
/// <c>prefix:Type</c> form and inside <c>{x:Type ...}</c>). See <see cref="AumlDirectives.All"/>.</summary>
public sealed class AumlDirectiveInfo
{
    public AumlDirectiveInfo(string name, string description, bool isTypeReference = false,
        AumlDirectiveUsage usage = AumlDirectiveUsage.Attribute)
    {
        Name = name;
        Description = description;
        IsTypeReference = isTypeReference;
        Usage = usage;
    }

    public string Name { get; }

    public string Description { get; }

    public bool IsTypeReference { get; }

    public AumlDirectiveUsage Usage { get; }
}
