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

    // A {ResourceReference} on a plain markup object - a template selector, a converter, anything the author writes as an
    // element but that is not part of the property system. There is no property store to defer into and no place in the
    // tree to be scoped from, so the key is resolved AT ONCE and flat: the Local scope first (the dictionary that
    // declared it is already registered by the time the object is built), then Theme, then Global.
    public static object ResolveNow(string key)
    {
        var resourceManager = UIAppContext.Current.ResourceManager;

        return resourceManager.FindResourceInScope(key, ResourceScope.Local) ?? resourceManager.FindResource(key);
    }

    // A {ResourceReference} used DIRECTLY on a component property (not via a Setter/trigger). The target is any
    // AdamantiumComponent, NOT only a UI element: a resource reference is a property-system feature, so it applies to a
    // GradientStop.Color, a Pen's brush, any animatable component - the same way WPF's Static/DynamicResource work on any
    // DependencyObject, not just UIElements. The target is not yet in the tree when its properties are assigned during
    // construction, so a Local resource can't be tree-scoped at that moment. Resolve the Theme/Global value immediately,
    // then - ONLY for a target that lives in the VISUAL tree - re-resolve tree-scoped once it attaches (the cascade brings
    // the full ancestor chain, incl. a Local resource's owner, and a nearer Local hit wins).
    public static void SetDeferred(IAdamantiumComponent target, string property, string key)
    {
        var resourceManager = UIAppContext.Current.ResourceManager;

        var baseline = resourceManager.FindResource(key);
        if (baseline != null)
            target.SetValue(property, baseline);

        // Only a visual element takes part in the visual tree; a non-visual target (a GradientStop) can't be tree-scoped,
        // so the Theme/Global baseline above is all it gets.
        if (target is IUIComponent visual)
        {
            visual.AttachedToVisualTreeEvent += (_, _) =>
            {
                var scoped = resourceManager.FindResource(visual, key);
                if (scoped != null)
                    visual.SetValue(property, scoped);
            };
        }
    }
}
