using Adamantium.Core.Events;
using Adamantium.UI.Controls;
using Adamantium.UI.Platforms;

namespace Adamantium.Game.Core;

/// <summary>
/// The UI's windows and the UI's message loop.
/// </summary>
public class UIWindowingPlatform : IWindowingPlatform
{
    private readonly IEventAggregator eventAggregator;
    private readonly IApplicationPlatform applicationPlatform;

    public UIWindowingPlatform(IEventAggregator eventAggregator, IApplicationPlatform applicationPlatform)
    {
        this.eventAggregator = eventAggregator;
        this.applicationPlatform = applicationPlatform;
    }

    public UniverseOutput CreateWindow(uint width, uint height)
    {
        var window = new Window();
        window.Width = width;
        window.Height = height;
        return new WindowUniverseOutput(eventAggregator, new OutputContext(window));
    }

    public void Run(CancellationToken token)
    {
        applicationPlatform.Run(token);
    }
}
