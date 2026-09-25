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
/// Runs <see cref="AdamantiumGame"/> into the <see cref="RenderTargetPanel"/> it is attached to; pauses it while the
/// panel is out of the tree and removes it when the panel is discarded. Attach in markup:
/// <code>
/// &lt;RenderTargetPanel.Behaviors&gt;&lt;local:GameHostBehavior/&gt;&lt;/RenderTargetPanel.Behaviors&gt;
/// </code>
/// </summary>
public class GameHostBehavior : Behavior<RenderTargetPanel>
{
    private bool _gameAttached;
    private IUniverseService _gameService;
    private AdamantiumGame _game;

    protected override void OnAttached(RenderTargetPanel panel)
    {
        // Design-time preview is WIP and off: a game per render is expensive. ADAMANTIUM_DESIGN_GAME=1 turns it on.
        if (Design.IsDesignMode)
        {
            if (Environment.GetEnvironmentVariable("ADAMANTIUM_DESIGN_GAME") == "1")
                AttachDesignTimeGame(panel);
            return;
        }

        // Wired once in the tree, where its window and render service exist.
        panel.AttachedToVisualTreeEvent += OnPanelAttachedToVisualTree;
        panel.DetachedFromVisualTreeEvent += OnPanelDetachedFromVisualTree;
    }

    private void AttachDesignTimeGame(RenderTargetPanel panel)
    {
        if (_gameAttached) return;
        // A failure here would null the whole preview tree: degrade to a panel without a game.
        try
        {
            var app = UIApplication.Current;
            if (app == null) return;

            var gameService = app.UIContext.Resolve<IUniverseService>();
            var graphicsDeviceService = app.UIContext.Resolve<IGraphicsDeviceService>();

            // No render service: the designer drives it directly.
            var game = gameService.CreateUniverse<AdamantiumGame>(
                "AdamantiumGame", panel.RootVisual as IWindow, null, graphicsDeviceService, app.EnableGraphicsDebug, app.Container);
            game.CreateOutputFromContext(panel);
            _gameAttached = true;
            _gameService = gameService;
            _game = game;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[design-game] setup failed: {ex}");
        }
    }

    // Only on a discard (a kept view is parked): the game goes with the panel.
    protected override void OnDetached(RenderTargetPanel panel)
    {
        panel.AttachedToVisualTreeEvent -= OnPanelAttachedToVisualTree;
        panel.DetachedFromVisualTreeEvent -= OnPanelDetachedFromVisualTree;
        if (_game == null)
        {
            return;
        }

        _gameService.RemoveUniverse(_game);
        _game = null;
        _gameService = null;
        _gameAttached = false;
    }

    private void OnPanelDetachedFromVisualTree(object sender, VisualTreeAttachmentEventArgs e)
    {
        _game?.Pause();
    }

    private void OnPanelAttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
    {
        if (_gameAttached)
        {
            _game?.Resume();
            return;
        }

        var panel = (RenderTargetPanel)sender;
        var app = UIApplication.Current;
        if (app == null || panel.RootVisual is not IWindow window) return;

        var gameService = app.UIContext.Resolve<IUniverseService>();
        var graphicsDeviceService = app.UIContext.Resolve<IGraphicsDeviceService>();
        var renderService = app.EntityWorld.ServiceManager.GetServices<UiRenderService>()
            .Cast<WindowRenderService>()
            .FirstOrDefault(x => x.Window == window);

        var game = gameService.CreateUniverse<AdamantiumGame>(
            "AdamantiumGame", window, renderService, graphicsDeviceService, app.EnableGraphicsDebug, app.Container);
        game.CreateOutputFromContext(panel);
        _gameAttached = true;
        _gameService = gameService;
        _game = game;

        // The inherited DataContext usually arrives after attach, so the bridge waits for it.
        BridgeToViewModel(panel, game);
    }

    private static void BridgeToViewModel(RenderTargetPanel panel, AdamantiumGame game)
    {
        if (panel.DataContext is ViewModels.GameViewModel vm)
        {
            vm.AttachGame(game);
            return;
        }

        AdamantiumPropertyChangedEventHandler handler = null;
        handler = (_, _) =>
        {
            if (panel.DataContext is not ViewModels.GameViewModel ready) return;
            ready.AttachGame(game);
            panel.DataContextChanged -= handler;
        };
        panel.DataContextChanged += handler;
    }
}
