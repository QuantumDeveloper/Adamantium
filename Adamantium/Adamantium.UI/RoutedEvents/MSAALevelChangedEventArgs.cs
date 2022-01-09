using Adamantium.Engine.Graphics;

namespace Adamantium.UI.RoutedEvents;

public class MSAALevelChangedEventArgs : RoutedEventArgs
{
    public MSAALevel MSAALevel { get; }

    public MSAALevelChangedEventArgs(MSAALevel level)
    {
        MSAALevel = level;
    }
}