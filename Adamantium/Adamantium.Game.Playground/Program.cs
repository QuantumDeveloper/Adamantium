using Adamantium.UI;
using System;

namespace Adamantium.Game.Playground;

public class Program
{
    public static void Main(string[] args)
    {
        var game = new AdamantiumGameApplication();
        game.IsFixedTimeStep = false;
        game.DesiredFPS = 300;
        game.StartupType = typeof(MainWindow);
        game.Run();
    }
}