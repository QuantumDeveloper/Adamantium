namespace Adamantium.UI.Core.RoutedEvents;

public class ComponentUpdatedEventArgs : EventArgs
{
    public ComponentUpdatedEventArgs(IAdamantiumComponent component)
    {
        Component = component;
    }
    
    public IAdamantiumComponent Component { get; }
}