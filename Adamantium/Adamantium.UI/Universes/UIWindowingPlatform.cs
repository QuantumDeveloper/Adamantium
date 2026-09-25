using System.Threading;
using Adamantium.Game;
using Adamantium.UI.Controls;
using Adamantium.UI.Platforms;

namespace Adamantium.UI.Universes;

/// <summary>
/// The UI's windows and the UI's message loop.
/// </summary>
public class UIWindowingPlatform : IWindowingPlatform
{
    private readonly IApplicationPlatform applicationPlatform;

    public UIWindowingPlatform(IApplicationPlatform applicationPlatform)
    {
        this.applicationPlatform = applicationPlatform;
    }

    public UniverseOutput CreateWindow(uint width, uint height, IUniverseEventAggregator events)
    {
        var window = new Window();
        window.Width = width;
        window.Height = height;
        return new WindowUniverseOutput(events, new OutputContext(window));
    }

    public void Run(CancellationToken token)
    {
        applicationPlatform.Run(token);
    }
}
