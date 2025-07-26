using Adamantium.Graphics.Core;

namespace Adamantium.UI.Core.RoutedEvents;

public class MSAALevelChangedEventArgs : RoutedEventArgs
{
    public MSAALevel MSAALevel { get; }

    public MSAALevelChangedEventArgs(MSAALevel level)
    {
        MSAALevel = level;
    }
}