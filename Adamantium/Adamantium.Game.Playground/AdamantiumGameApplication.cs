using System.Collections.Generic;
using Adamantium.Game.Core;
using Adamantium.UI.Controls;

namespace Adamantium.Game.Playground;

public class AdamantiumGameApplication : GameApplication
{
    
    public AdamantiumGameApplication()
    {
        
    }
        
    protected override void OnStartup()
    {
        base.OnStartup();
        if (StartupUri == null) return;
            
        var path = StartupUri.OriginalString;
        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}