using Adamantium.Core.DependencyInjection;
using Adamantium.UI.Threading;

namespace Adamantium.UI;

public static class ApplicationBuilder
{
    public static void Build(IDependencyResolver resolver)
    {
        switch (Configuration.Platform)
        {
            case Platform.Windows:
                WindowsPlatform.Initialize(resolver);
                break;
            case Platform.OSX:
                MacOSPlatform.Initialize(resolver);
                break;
        }
    }
}