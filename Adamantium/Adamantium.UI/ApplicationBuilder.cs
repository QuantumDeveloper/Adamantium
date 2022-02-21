using Adamantium.UI.Threading;

namespace Adamantium.UI;

public static class ApplicationBuilder
{
    public static void Build()
    {
        switch (Configuration.Platform)
        {
            case Platform.Windows:
                WindowsPlatform.Initialize();
                break;
            case Platform.OSX:
                MacOSPlatform.Initialize();
                break;
        }
    }
}