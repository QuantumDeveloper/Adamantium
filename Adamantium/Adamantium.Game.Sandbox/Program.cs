using Adamantium.UI;
using System;

namespace Adamantium.Game.Sandbox;

public class Program
{
    public static void Main(string[] args)
    {
        var gameApp = new AdamantiumGameApplication();
        gameApp.IsFixedTimeStep = false;
        //gameApp.EnableGraphicsDebug = false;
        gameApp.DesiredFPS = 300;
        gameApp.StartupType = typeof(MainWindow);
        gameApp.Run();
    }
}