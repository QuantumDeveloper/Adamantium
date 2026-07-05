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

    // A {ResourceReference} used DIRECTLY on an element property (not via a Setter/trigger). The element is not yet in
    // the tree when its properties are assigned during construction, so a Local resource can't be tree-scoped at that
    // moment. Resolve the Theme/Global value immediately (roots and theme references keep working with no delay), then
    // re-resolve tree-scoped once the element attaches to the (rooted) VISUAL tree - which cascades through the whole
    // subtree, so the full ancestor chain (incl. a Local resource's owner) is present - and a nearer Local hit wins.
    public static void SetDeferred(IFundamentalUIComponent element, string property, string key)
    {
        var resourceManager = UIAppContext.Current.ResourceManager;

        var baseline = resourceManager.FindResource(key);
        if (baseline != null)
            element.SetValue(property, baseline);

        // Only visual elements take part in the visual tree; a non-visual target can't be tree-scoped, so the Theme/
        // Global baseline above is all it gets.
        if (element is IUIComponent visual)
        {
            visual.AttachedToVisualTreeEvent += (_, _) =>
            {
                var scoped = resourceManager.FindResource(element, key);
                if (scoped != null)
                    element.SetValue(property, scoped);
            };
        }
    }
}
