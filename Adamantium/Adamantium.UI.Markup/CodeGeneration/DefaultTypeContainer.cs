namespace Adamantium.UI.Markup.CodeGeneration;

public class DefaultTypeContainer
{
    private DefaultTypeContainer(ITypeResolver typeResolver)
    {
        TypeParser = typeResolver.ResolveByShortName("TypeParser");
        ResourceReference = typeResolver.ResolveByShortName("ResourceReference");
        ResourceDictionary = typeResolver.ResolveByShortName("ResourceDictionary");
        StyleSet =  typeResolver.ResolveByShortName("StyleSet");
        ITheme = typeResolver.ResolveByShortName("ITheme");
    }
    
    public IResolvedType TypeParser { get; }
    
    public IResolvedType ResourceReference { get; }
    
    public IResolvedType ResourceDictionary { get; }
    
    public IResolvedType StyleSet { get; }
    
    public IResolvedType ITheme { get; }

    public static DefaultTypeContainer ResolveFrom(ITypeResolver typeResolver)
    {
        return new DefaultTypeContainer(typeResolver);
    }
}