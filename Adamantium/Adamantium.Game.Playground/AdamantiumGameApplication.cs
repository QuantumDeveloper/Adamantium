using Adamantium.Imaging;

namespace Adamantium.Game.Playground;

public class AdamantiumGameApplication : GameApplication
{
    public AdamantiumGameApplication()
    {
        EnableGraphicsDebug = false;
        // var img = BitmapLoader.Load(@"Textures\infinity.gif");
        // BitmapLoader.Save(img, @"Textures\infinity3.gif", ImageFileType.Gif);
        //var img = BitmapLoader.Load(@"Textures\luxfon.tga");
        //img.Save(@"Textures\luxfon2.tga", ImageFileType.Tga);
    }
}