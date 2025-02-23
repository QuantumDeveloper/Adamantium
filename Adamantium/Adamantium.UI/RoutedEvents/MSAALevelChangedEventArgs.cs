using Adamantium.Graphics;
using Adamantium.Graphics.Core;

namespace Adamantium.UI.RoutedEvents;

public class MSAALevelChangedEventArgs : RoutedEventArgs
{
    public MSAALevel MSAALevel { get; }

    public MSAALevelChangedEventArgs(MSAALevel level)
    {
        MSAALevel = level;
    }
}