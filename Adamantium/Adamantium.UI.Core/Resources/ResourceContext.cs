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
        UIAppContext.Current.ResourceManager.AddSource(element, value.Source, value.Scope );

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