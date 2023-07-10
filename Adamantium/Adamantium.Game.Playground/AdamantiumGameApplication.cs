using System.Collections.Generic;
using Adamantium.Game.Core;
using Adamantium.Imaging;
using Adamantium.UI.Controls;
using Image = Adamantium.Imaging.Image;

namespace Adamantium.Game.Playground;

public class AdamantiumGameApplication : GameApplication
{
    
    public AdamantiumGameApplication()
    {
        EnableGraphicsDebug = true;
        var img = Image.Load(@"Textures\infinity.gif");
        img.Save(@"Textures\infinity2.gif", ImageFileType.Gif);
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