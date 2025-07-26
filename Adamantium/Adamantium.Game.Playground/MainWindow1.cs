using System;
using System.Linq;
using Adamantium.Graphics.Core;
using Adamantium.ProceduralGeometry.Shapes;
using Adamantium.UI.Controls;
using Adamantium.UI;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Shapes;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.EntityServices;

namespace Adamantium.Game.Playground;

public class MainWindow1 : Window
{
    private Style CreateStyle()
    {
        var style = new Style();
        style.Selector.Types.Add(typeof(Ellipse));
        style.Selector.Classes.Add("Ellipse1");
        var setter = new Setter("Width", 150);
        style.Setters.Add(setter);
        setter = new Setter("Height", "50");
        style.Setters.Add(setter);
        setter = new Setter("Fill", "CornflowerBlue");
        style.Setters.Add(setter);
        setter = new Setter("StartAngle", "90");
        style.Setters.Add(setter);
        setter = new Setter("EllipseType", EllipseType.EdgeToEdge);
        style.Setters.Add(setter);
        return style;
    }
    
    public MainWindow1()
    {
    }

    private void CreateGame()
    {
        RenderTargetPanel rt = null;
        var gameService = UIContext.Resolve<IGameService>();
        var graphicsDeviceService = UIContext.Resolve<IGraphicsDeviceService>();
        // Primary mode
        //var game = gameService.CreateGame<AdamantiumGame>("AdamantiumGame", true, UIApplication.Current.EnableGraphicsDebug);
       
        // Fully slave mode
        var service = UIApplication.Current.EntityWorld.ServiceManager.GetServices<UiRenderService>()
            .Cast<WindowRenderService>().FirstOrDefault(x => x.Window == this);
        var game = gameService.CreateGame<AdamantiumGame>(
            "AdamantiumGame", 
            this, 
            service, 
            graphicsDeviceService, 
            true, 
            UIApplication.Current.EnableGraphicsDebug);
        game.CreateOutputFromContext(rt, UIContext.UIApplication.GraphicsContext.CreateGraphicsDevice());
    }
}