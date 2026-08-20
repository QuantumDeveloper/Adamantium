using Adamantium.UI.Core.MarkupExtensions;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Resources;

public static class ResourceContext
{
    public static readonly AdamantiumProperty SourceProperty =
        AdamantiumProperty.RegisterAttached<ResourceLink>("Source", typeof(AdamantiumComponent));

    public static ResourceLink GetSource(AdamantiumComponent element)
    {
        return element.GetValue<ResourceLink>(SourceProperty);
    }
    
    public static void SetSource(AdamantiumComponent element, ResourceLink value)
    {
        element.SetValue(SourceProperty, value);

        // A THEME only records its palette link here; the ThemeManager activates it (AddSource) while the theme is
        // current and removes it when it stops being current. That keeps exactly one palette - the current theme's -
        // live, so declaring 20 themes doesn't register 20 palettes. Every non-theme element registers eagerly, below.
        if (element is ITheme) return;

        UIAppContext.Current.ResourceManager.AddSource(element, value.Source, value.Scope );

        // A GLOBAL source is APP-WIDE: it must NOT be torn down when the declaring element unloads. A theme swap unloads
        // and reloads the subtree, which would otherwise RemoveSources -> abandon the (last-owner) dictionary -> lose any
        // runtime override on it (e.g. a cycled GlobalAccent) and re-create a fresh copy. Surviving a theme swap is the
        // whole point of Global scope, so it is never lifecycle-bound to its declaring element. Local sources stay tied to
        // their element (tree-scoped, cleaned up on unload).
        if (value.Scope == ResourceScope.Global) return;

        if (element is IInputComponent inputComponent)
        {
            inputComponent.Unloaded += InputComponentOnUnloaded;
        }

        static void InputComponentOnUnloaded(object sender, RoutedEventArgs e)
        {
            var adamantiumComponent = (IInputComponent)sender;
            adamantiumComponent.Unloaded -= InputComponentOnUnloaded;

            UIAppContext.Current.ResourceManager.RemoveSources(adamantiumComponent);
        }
    }

    // The scope the inline Resources below are published into. LOCAL by default - a private, tree-scoped dictionary that
    // is invisible outside this subtree. Set it to Theme on a theme (icons belong to the theme: another theme may declare
    // the same keys) or to Global for an app-wide set, without having to move the entries into a separate linked type.
    public static readonly AdamantiumProperty ScopeProperty =
        AdamantiumProperty.RegisterAttached("Scope", typeof(ResourceScope), typeof(AdamantiumComponent),
            new PropertyMetadata(ResourceScope.Local));

    public static ResourceScope GetScope(AdamantiumComponent element)
    {
        return element.GetValue<ResourceScope>(ScopeProperty);
    }

    public static void SetScope(AdamantiumComponent element, ResourceScope value)
    {
        element.SetValue(ScopeProperty, value);
    }

    // The element's resources: <X><ResourceContext.Resources>...</ResourceContext.Resources></X>. Two kinds of child,
    // freely mixed - a <ResourceLink> naming a dictionary TYPE (its own .auml file: a palette, an icon set) and a keyed
    // object declared right here. Scoped by ResourceContext.Scope (Local unless said otherwise); a link may state its own
    // Scope to override that. Attributes are applied before property elements, so the scope is already known here
    // however the two are ordered in the markup.
    public static readonly AdamantiumProperty ResourcesProperty =
        AdamantiumProperty.RegisterAttached<ResourceDictionary>("Resources", typeof(AdamantiumComponent));

    public static ResourceDictionary GetResources(AdamantiumComponent element)
    {
        return element.GetValue<ResourceDictionary>(ResourcesProperty);
    }

    public static void SetResources(AdamantiumComponent element, ResourceDictionary value)
    {
        element.SetValue(ResourcesProperty, value);
        if (value == null) return;

        // A THEME publishes nothing until it is the current one - the same rule its palette link follows, so declaring
        // 20 themes doesn't put 20 icon sets into the Theme scope at once. The ThemeManager activates it.
        if (element is ITheme) return;

        var scope = RegisterResources(element, value);

        // A GLOBAL dictionary is app-wide and must outlive the element that declared it (a theme swap unloads and
        // reloads the subtree) - exactly as a Global Source does.
        if (scope == ResourceScope.Global) return;

        if (element is IInputComponent inputComponent)
        {
            inputComponent.Unloaded += InputComponentOnUnloaded;
        }

        static void InputComponentOnUnloaded(object sender, RoutedEventArgs e)
        {
            var adamantiumComponent = (IInputComponent)sender;
            adamantiumComponent.Unloaded -= InputComponentOnUnloaded;

            UIAppContext.Current.ResourceManager.RemoveSources(adamantiumComponent);
        }
    }

    // Publish a Resources block: first the linked dictionary FILES, then the block's own keyed entries. Every one of
    // them is registered under the SAME owner, so RemoveSources(element) takes the whole block back down at once.
    // Returns the scope the block landed in. A link may name its own Scope; left at the default it follows the block.
    internal static ResourceScope RegisterResources(AdamantiumComponent element, ResourceDictionary resources)
    {
        var scope = GetScope(element);
        var manager = UIAppContext.Current.ResourceManager;

        foreach (var include in resources.Includes)
        {
            if (include?.Source == null) continue;
            manager.AddSource(element, include.Source, include.Scope == ResourceScope.Local ? scope : include.Scope);
        }

        manager.AddSource(element, resources, scope);
        return scope;
    }
}