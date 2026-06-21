namespace Adamantium.UI.Markup.AST;

/// <summary>One AUML <c>x:</c> directive: its local name, the description tooling shows, and whether its value names a
/// CLR type (so tooling completes type names in the value - both the plain <c>prefix:Type</c> form and inside
/// <c>{x:Type ...}</c>). See <see cref="AumlDirectives.All"/>.</summary>
public sealed class AumlDirectiveInfo
{
    public AumlDirectiveInfo(string name, string description, bool isTypeReference = false)
    {
        Name = name;
        Description = description;
        IsTypeReference = isTypeReference;
    }

    public string Name { get; }

    public string Description { get; }

    public bool IsTypeReference { get; }
}
