using Adamantium.Core.DependencyInjection;
using Adamantium.Game.Core;
using Adamantium.Game.Core.Input;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.UI.Core;
using Adamantium.UI.Platforms.MacOS;
using Adamantium.UI.Platforms.Windows;

namespace Adamantium.Game;

public static class UniverseBuilder
{
    public static void Build(IDependencyContainer container)
    {
        container.RegisterSingleton<IGraphicsDeviceFactory, GraphicsDeviceFactory>();
        container.RegisterSingleton<IOutputFactory, UIOutputFactory>();
        container.RegisterSingleton<IWindowingPlatform, UIWindowingPlatform>();
        switch (Configuration.Platform)
        {
            case Platform.Windows:
                WindowsPlatform.Initialize(container);
                container.RegisterSingleton<IGamepadFactory, XBoxGamepadFactory>();
                break;
            case Platform.OSX:
                MacOSPlatform.Initialize(container);
                break;
        }
    }
}