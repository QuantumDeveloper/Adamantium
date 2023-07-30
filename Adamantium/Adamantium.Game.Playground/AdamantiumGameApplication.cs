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
        EnableGraphicsDebug = false;
        // var img = BitmapLoader.Load(@"Textures\infinity.gif");
        // BitmapLoader.Save(img, @"Textures\infinity3.gif", ImageFileType.Gif);
        var img = BitmapLoader.Load(@"Textures\luxfon.tga");
        img.Save(@"Textures\luxfon2.tga", ImageFileType.Tga);
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