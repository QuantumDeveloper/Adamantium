using System;
using System.Linq;
using Adamantium.Graphics.Core;
using Adamantium.UI;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Behaviors;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.EntityServices;
using Adamantium.UI.Universes;

namespace Adamantium.UI.Sandbox.Behaviors;

/// <summary>
/// Runs <see cref="DemoUniverse"/> into the <see cref="RenderTargetPanel"/> it is attached to; pauses it while the
/// panel is out of the tree and removes it when the panel is discarded. Attach in markup:
/// <code>
/// &lt;RenderTargetPanel.Behaviors&gt;&lt;local:DemoUniverseBehavior/&gt;&lt;/RenderTargetPanel.Behaviors&gt;
/// </code>
/// </summary>
public class DemoUniverseBehavior : Behavior<RenderTargetPanel>
{
    private bool _universeAttached;
    private IUniverseService _universeService;
    private DemoUniverse _universe;

    protected override void OnAttached(RenderTargetPanel panel)
    {
        // Design-time preview is WIP and off: a universe per render is expensive. ADAMANTIUM_DESIGN_UNIVERSE=1 turns it on.
        if (Design.IsDesignMode)
        {
            if (Environment.GetEnvironmentVariable("ADAMANTIUM_DESIGN_UNIVERSE") == "1")
                AttachDesignTimeUniverse(panel);
            return;
        }

        // Wired once in the tree, where its window and render service exist.
        panel.AttachedToVisualTreeEvent += OnPanelAttachedToVisualTree;
        panel.DetachedFromVisualTreeEvent += OnPanelDetachedFromVisualTree;
    }

    private void AttachDesignTimeUniverse(RenderTargetPanel panel)
    {
        if (_universeAttached) return;
        // A failure here would null the whole preview tree: degrade to a panel without a universe.
        try
        {
            var app = UIApplication.Current;
            if (app == null) return;

            var universeService = app.UIContext.Resolve<IUniverseService>();
            var graphicsDeviceService = app.UIContext.Resolve<IGraphicsDeviceService>();

            // No render service: the designer drives it directly.
            var universe = universeService.CreateUniverse<DemoUniverse>(
                "DemoUniverse", panel.RootVisual as IWindow, null, graphicsDeviceService, app.EnableGraphicsDebug, app.Container);
            universe.CreateOutputFromContext(panel);
            _universeAttached = true;
            _universeService = universeService;
            _universe = universe;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[design-universe] setup failed: {ex}");
        }
    }

    // Only on a discard (a kept view is parked): the universe goes with the panel.
    protected override void OnDetached(RenderTargetPanel panel)
    {
        panel.AttachedToVisualTreeEvent -= OnPanelAttachedToVisualTree;
        panel.DetachedFromVisualTreeEvent -= OnPanelDetachedFromVisualTree;
        if (_universe == null)
        {
            return;
        }

        _universeService.RemoveUniverse(_universe);
        _universe = null;
        _universeService = null;
        _universeAttached = false;
    }

    private void OnPanelDetachedFromVisualTree(object sender, VisualTreeAttachmentEventArgs e)
    {
        _universe?.Pause();
    }

    private void OnPanelAttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
    {
        if (_universeAttached)
        {
            _universe?.Resume();
            return;
        }

        var panel = (RenderTargetPanel)sender;
        var app = UIApplication.Current;
        if (app == null || panel.RootVisual is not IWindow window) return;

        var universeService = app.UIContext.Resolve<IUniverseService>();
        var graphicsDeviceService = app.UIContext.Resolve<IGraphicsDeviceService>();
        var renderService = app.EntityWorld.ServiceManager.GetServices<UiRenderService>()
            .Cast<WindowRenderService>()
            .FirstOrDefault(x => x.Window == window);

        var universe = universeService.CreateUniverse<DemoUniverse>(
            "DemoUniverse", window, renderService, graphicsDeviceService, app.EnableGraphicsDebug, app.Container);
        universe.CreateOutputFromContext(panel);
        _universeAttached = true;
        _universeService = universeService;
        _universe = universe;

        // The inherited DataContext usually arrives after attach, so the bridge waits for it.
        BridgeToViewModel(panel, universe);
    }

    private static void BridgeToViewModel(RenderTargetPanel panel, DemoUniverse universe)
    {
        if (panel.DataContext is ViewModels.SceneViewModel vm)
        {
            vm.AttachUniverse(universe);
            return;
        }

        AdamantiumPropertyChangedEventHandler handler = null;
        handler = (_, _) =>
        {
            if (panel.DataContext is not ViewModels.SceneViewModel ready) return;
            ready.AttachUniverse(universe);
            panel.DataContextChanged -= handler;
        };
        panel.DataContextChanged += handler;
    }
}
