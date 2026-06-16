using System.Linq;
using Adamantium.Graphics.Core;
using Adamantium.UI;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Behaviors;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.EntityServices;

namespace Adamantium.Game.Playground;

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
        // OnAttached fires while the AUML tree is being built, before the panel is in the visual tree. Defer the
        // wiring until it is attached, by which point its window and WindowRenderService exist.
        panel.AttachedToVisualTreeEvent += OnPanelAttachedToVisualTree;
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
    }
}
