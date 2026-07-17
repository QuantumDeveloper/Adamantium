using System;
using System.Linq;
using Adamantium.Graphics.Core;
using Adamantium.UI;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Behaviors;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.EntityServices;

namespace Adamantium.Game.Sandbox.Behaviors;

/// <summary>
/// Declaratively (from AUML) runs <see cref="AdamantiumGame"/> into the <see cref="RenderTargetPanel"/> it is
/// attached to. <see cref="AdamantiumGame"/> renders its frame, exports it as a shared surface and hands the
/// panel the descriptor; the panel imports it zero-copy and samples it during compositing — the full Phase 2/3
/// path. Attach in markup:
/// <code>
/// &lt;RenderTargetPanel.Behaviors&gt;&lt;local:GameHostBehavior/&gt;&lt;/RenderTargetPanel.Behaviors&gt;
/// </code>
/// </summary>
public class GameHostBehavior : Behavior<RenderTargetPanel>
{
    private bool _gameAttached;

    protected override void OnAttached(RenderTargetPanel panel)
    {
        // Design-time game preview is WIP and OFF by default: driving a game per render is expensive, so the
        // interactive designer stays responsive (panel shows its placeholder). Opt in with ADAMANTIUM_DESIGN_GAME=1
        // while working on the feature. The designer (DesignerSession) drives the snapshot and composites it.
        if (Design.IsDesignMode)
        {
            if (Environment.GetEnvironmentVariable("ADAMANTIUM_DESIGN_GAME") == "1")
                AttachDesignTimeGame(panel);
            return;
        }

        // OnAttached fires while the AUML tree is being built, before the panel is in the visual tree. Defer the
        // wiring until it is attached, by which point its window and WindowRenderService exist.
        panel.AttachedToVisualTreeEvent += OnPanelAttachedToVisualTree;
    }

    private void AttachDesignTimeGame(RenderTargetPanel panel)
    {
        if (_gameAttached) return;
        // Guarded: a design-time game-setup failure must degrade to a panel-without-game, never break the whole
        // preview (this runs inside the markup instantiation, where an exception would null the entire tree).
        try
        {
            var app = UIApplication.Current;
            if (app == null) return;

            var gameService = app.UIContext.Resolve<IGameService>();
            var graphicsDeviceService = app.UIContext.Resolve<IGraphicsDeviceService>();

            // Slave-mode game sharing the designer's device service; no render service (the designer drives it
            // directly, not via GameService.RunGames). The GameContext device is only a key, so reuse an existing one.
            var game = gameService.CreateGame<AdamantiumGame>(
                "AdamantiumGame", panel.RootVisual as IWindow, null, graphicsDeviceService, app.EnableGraphicsDebug);
            game.CreateOutputFromContext(panel, graphicsDeviceService.ResourceLoaderDevice);
            _gameAttached = true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[design-game] setup failed: {ex}");
        }
    }

    protected override void OnDetached(RenderTargetPanel panel)
    {
        panel.AttachedToVisualTreeEvent -= OnPanelAttachedToVisualTree;
    }

    private void OnPanelAttachedToVisualTree(object sender, VisualTreeAttachmentEventArgs e)
    {
        if (_gameAttached) return;
        var panel = (RenderTargetPanel)sender;
        var app = UIApplication.Current;
        if (app == null || panel.RootVisual is not IWindow window) return;

        var gameService = app.UIContext.Resolve<IGameService>();
        var graphicsDeviceService = app.UIContext.Resolve<IGraphicsDeviceService>();
        var renderService = app.EntityWorld.ServiceManager.GetServices<UiRenderService>()
            .Cast<WindowRenderService>()
            .FirstOrDefault(x => x.Window == window);

        var game = gameService.CreateGame<AdamantiumGame>(
            "AdamantiumGame", window, renderService, graphicsDeviceService, app.EnableGraphicsDebug);
        // Don't spin up a fresh render device here: GamePlatform already creates one per GameOutput, and the
        // device handed to the GameContext is only used for its hash (GameContext.GetHashCode) — never for
        // rendering. Reuse the window's existing render device so we don't burn another slice of the BAR window.
        game.CreateOutputFromContext(panel, renderService.GraphicsDevice);
        _gameAttached = true;

        // Bridge the live game to the tab's view-model so its menu can load models at runtime. The panel's DataContext is
        // INHERITED from the hosting tab and is usually NOT set yet at attach time (attach fires before the inherited
        // value propagates), so bridge now if it's already the GameViewModel, else the moment it becomes one - otherwise
        // the menu's load commands keep seeing a null game and report "not ready".
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
