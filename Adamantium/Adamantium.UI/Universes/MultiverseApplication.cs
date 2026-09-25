using Adamantium.Core;
using Adamantium.Core.DependencyInjection;
using Adamantium.ECS;
using Adamantium.Multiverse;

namespace Adamantium.UI.Universes;

public abstract class MultiverseApplication : UIApplication
{
    public IUniverseService UniverseService { get; private set; }

    public MultiverseApplication()
    {
    }

    protected override void OnInitialize()
    {
        base.OnInitialize();
        UniverseService = new UniverseService();
        EntityWorld.ServiceManager.OnDrawStarted += ServiceManagerOnDrawStarted;
        EntityWorld.ServiceManager.OnDrawFinished += ServiceManagerOnOnDrawFinished;
        UniverseService.OnUniverseAdded += UniverseServiceOnUniverseAdded;
    }

    private void UniverseServiceOnUniverseAdded(IUniverse obj)
    {
    }

    private void ServiceManagerOnDrawStarted(IRenderService service, AppTime time)
    {
        UniverseService.RunUniverses(service, time);
    }

    private void ServiceManagerOnOnDrawFinished(IRenderService arg1, AppTime arg2)
    {
        // Shared-surface publish happens in each universe's own render cycle (RenderingProcessor.EndDraw records the
        // copy into that universe's command buffer and its Submit signals Produce). Driving it from the UI service's
        // OnDrawFinished would record onto the wrong device/queue.
    }

    protected override void RegisterServices(IContainerRegistry containerRegistry)
    {
        base.RegisterServices(containerRegistry);
        containerRegistry.RegisterSingleton<IUniverseService>(UniverseService);
        containerRegistry.RegisterSingleton<IOutputFactory, UIOutputFactory>();
        containerRegistry.RegisterSingleton<IWindowingPlatform, UIWindowingPlatform>();
    }

    protected override void OnBeforeEndScene()
    {
    }

    // protected override void Submit()
    // {
    //     base.Submit();
    //     var graphicQueue = GraphicsDeviceService.MainGraphicsDevice.GetAvailableGraphicsQueue();
    //     var submitInfos = new List<SubmitInfo>();
    //
    //     foreach (var device in GraphicsDeviceService.MainGraphicsDevice.GraphicsDevices)
    //     {
    //         var submitInfo = device.PrepareSubmit();
    //         if (submitInfo != null)
    //         {
    //             submitInfos.Add(submitInfo);
    //         }
    //     }
    //
    //     submitInfos.Reverse();
    //
    //     if (submitInfos.Count > 0)
    //     {
    //         GraphicsDeviceService.MainGraphicsDevice.Submit(graphicQueue, submitInfos.ToArray());
    //     }
    //     
    //     GraphicsDeviceService.MainGraphicsDevice.OnFrameFinished();
    // }
}