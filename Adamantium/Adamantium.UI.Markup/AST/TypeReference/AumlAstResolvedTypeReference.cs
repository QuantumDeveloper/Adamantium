namespace Adamantium.UI.Markup.AST.TypeReference;

public class AumlAstResolvedTypeReference : AumlAstClrTypeReference
{
    public AumlAstResolvedTypeReference(IAumlLineInfo info, string @namespace, string name, string assembly, bool isMarkupExtension) : base(info, @namespace, name, assembly)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentNullException(nameof(name));
        }
        if (string.IsNullOrEmpty(@namespace))
        {
            throw new ArgumentNullException(nameof(@namespace));
        }
        if (string.IsNullOrEmpty(assembly))
        {
            throw new ArgumentNullException(nameof(assembly));
        }
        
        IsMarkupExtension = isMarkupExtension;
    }

    public override bool IsResolved => true;
}