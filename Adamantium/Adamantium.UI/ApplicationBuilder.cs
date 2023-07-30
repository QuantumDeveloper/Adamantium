using Adamantium.Core.DependencyInjection;
using Adamantium.UI.Threading;

namespace Adamantium.UI;

public static class ApplicationBuilder
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