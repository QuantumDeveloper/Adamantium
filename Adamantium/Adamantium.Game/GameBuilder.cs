using Adamantium.Core.DependencyInjection;
using Adamantium.UI;
using Adamantium.UI.Threading;

namespace Adamantium.Game;

public static class GameBuilder
{
    public static void Build(IDependencyContainer container)
    {
        switch (Configuration.Platform)
        {
            case Platform.Windows:
                WindowsPlatform.Initialize(container);
                break;
            case Platform.OSX:
                MacOSPlatform.Initialize(container);
                break;
        }
    }
}