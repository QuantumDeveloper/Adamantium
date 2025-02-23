using Adamantium.Core.DependencyInjection;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.UI.Threading;
using AdamantiumVulkan;

namespace Adamantium.UI;

public static class ApplicationBuilder
{
    public static void Build(IDependencyContainer container)
    {
        container.Register<IGraphicsDeviceFactory, GraphicsDeviceFactory>();
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