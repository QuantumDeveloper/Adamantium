using Adamantium.UI.Controls;

namespace Adamantium.UI.RoutedEvents
{
    public class StateChangedEventArgs : RoutedEventArgs
    {
        public StateChangedEventArgs(WindowState state)
        {
            State = state;
        }
        
        public WindowState State { get; }
    }
}