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

    // Inline resources authored right on the element: <X><ResourceContext.Resources>...</ResourceContext.Resources></X>.
    // Always LOCAL scope - the whole point is a private, tree-scoped dictionary that is invisible outside this subtree
    // (no {ResourceReference} from elsewhere can reach it). For a Theme/Global dictionary, link a type via Source instead.
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

        UIAppContext.Current.ResourceManager.AddSource(element, value, ResourceScope.Local);

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
}