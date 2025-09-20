namespace Adamantium.UI.Markup.CodeGeneration;

public class DefaultTypeContainer
{
    private DefaultTypeContainer(ITypeResolver typeResolver)
    {
        TypeParser = typeResolver.ResolveByShortName("TypeParser");
        ResourceReference = typeResolver.ResolveByShortName("ResourceReference");
        ResourceDictionary = typeResolver.ResolveByShortName("ResourceDictionary");
        ResourceResolver = typeResolver.ResolveByShortName("ResourceResolver");
        StyleSet =  typeResolver.ResolveByShortName("StyleSet");
        ITheme = typeResolver.ResolveByShortName("ITheme");
        ControlTemplate = typeResolver.ResolveByShortName("ControlTemplate");
        TemplateBindingExpression = typeResolver.ResolveByShortName("TemplateBindingExpression");
        TemplateResult = typeResolver.ResolveByShortName("TemplateResult");
    }
    
    public IResolvedType TypeParser { get; }
    
    public IResolvedType ResourceReference { get; }
    
    public IResolvedType ResourceResolver { get; }
    
    public IResolvedType ResourceDictionary { get; }
    
    public IResolvedType StyleSet { get; }
    
    public IResolvedType ITheme { get; }
    
    public IResolvedType ControlTemplate { get; }
    
    public IResolvedType TemplateBindingExpression { get; }
    public IResolvedType TemplateResult { get; }

    public static DefaultTypeContainer ResolveFrom(ITypeResolver typeResolver)
    {
        return new DefaultTypeContainer(typeResolver);
    }
}