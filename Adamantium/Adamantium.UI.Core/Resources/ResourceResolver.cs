namespace Adamantium.UI.Core.Resources;

public static class ResourceResolver
{
    public static T Resolve<T>(string literalName)
    {
        var currentTheme = UIAppContext.Current.ThemeManager.CurrentTheme;

        if (currentTheme == null)
        {
            throw new ResourceNotFoundException($"Current theme not found. Please, check correctness of theme initialization");
        }
        
        var resource = currentTheme.GetResource(literalName);

        if (resource == null)
        {
            throw new ResourceNotFoundException(
                $"Resource {literalName} is not found for theme: {currentTheme.Name}");
        }
        
        return (T)resource;
    }
}