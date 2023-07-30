//using Adamantium.Core.Collections;
//using Adamantium.Engine.Graphics;
//using Adamantium.Mathematics;
//using Adamantium.UI.Controls;
//using Adamantium.UI.Input;
//using Adamantium.UI.Media;
//using Adamantium.UI.RoutedEvents;
//using Adamantium.Win32;
//using Polygon = Adamantium.UI.Controls.Polygon;
//using Rectangle = Adamantium.UI.Controls.Rectangle;
//using Shape = Adamantium.UI.Controls.Shape;

using System;
using System.Linq;
using System.Threading.Tasks;
using Adamantium.Engine.Graphics;
using Adamantium.UI.Input;
using Adamantium.UI.Controls;
using Serilog;
using Adamantium.UI;
using Adamantium.UI.Media.Imaging;
using Adamantium.UI.Processors;
using Adamantium.UI.RoutedEvents;

namespace Adamantium.Game.Playground;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        SourceInitialized += OnSourceInitialized;
        KeyDown+= delegate(object sender, KeyEventArgs args)
        {
            Log.Logger.Information($"Key: {args.Key}");
            if (args.Key == Key.A)
            {
                UIApplication.Current.EnableGraphicsDebug = true;
            }
            else if (args.Key == Key.S)
            {
                UIApplication.Current.EnableGraphicsDebug = false;
            }
        };
    }

    private void OnSourceInitialized(object sender, EventArgs e)
    {
        //CreateGame();
    }

    private void CreateGame()
    {
        var rt = Content as RenderTargetPanel;
        var gameService = Resolver.Resolve<IGameService>();
        var graphicsDeviceService = Resolver.Resolve<IGraphicsDeviceService>();
        // Primary mode
        //var game = gameService.CreateGame<AdamantiumGame>("AdamantiumGame", true, UIApplication.Current.EnableGraphicsDebug);
        
        // Fully slave mode
        var service = UIApplication.Current.EntityWorld.ServiceManager.GetServices<UiRenderService>()
            .Cast<WindowRenderService>().FirstOrDefault(x => x.Window == this);
        var game = gameService.CreateGame<AdamantiumGame>(
            "AdamantiumGame", 
            this, 
            service, 
            graphicsDeviceService, true, UIApplication.Current.EnableGraphicsDebug);
        var drawingContext = GetDrawingContext();
        game.CreateOutputFromContext(rt, drawingContext.GraphicsDevice);
    }
}