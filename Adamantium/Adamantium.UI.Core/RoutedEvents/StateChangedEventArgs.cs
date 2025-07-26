namespace Adamantium.UI.Core.RoutedEvents;

public class StateChangedEventArgs : RoutedEventArgs
{
    public StateChangedEventArgs(WindowState state)
    {
        State = state;
    }
        
    public WindowState State { get; }
}