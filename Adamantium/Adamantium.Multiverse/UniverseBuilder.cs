using System;
using Adamantium.Core.DependencyInjection;
using Adamantium.Multiverse.Input;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;

namespace Adamantium.Multiverse;

public static class UniverseBuilder
{
    /// <summary>
    /// Adds what the engine itself needs and the host has not registered. The surfaces and the windows are the host's.
    /// </summary>
    public static void Build(IDependencyContainer container)
    {
        if (!container.IsRegistered<IGraphicsDeviceFactory>())
        {
            container.RegisterSingleton<IGraphicsDeviceFactory, GraphicsDeviceFactory>();
        }

        if (OperatingSystem.IsWindows() && !container.IsRegistered<IGamepadFactory>())
        {
            container.RegisterSingleton<IGamepadFactory, XBoxGamepadFactory>();
        }
    }
}