using Adamantium.UI;

namespace Adamantium.Game.Playground;

class Program
{
    static void Main(string[] args)
    {
        var game = new AdamantiumGameApplication();
        var wnd = new Window();
        wnd.Width = 1280;
        wnd.Height = 720;
        game.Run(wnd);
    }
}