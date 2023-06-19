using Adamantium.UI;
using System;

namespace Adamantium.Game.Playground;

class Program
{
    static void Main(string[] args)
    {
        var game = new AdamantiumGameApplication();
        game.IsFixedTimeStep = false;
        game.DesiredFPS = 300;
        game.StartupUri = new Uri("MainWindow.xml", UriKind.RelativeOrAbsolute);
        game.Run();
    }
}