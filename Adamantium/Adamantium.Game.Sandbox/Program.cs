using Adamantium.UI;
using System;

namespace Adamantium.Game.Sandbox;

public class Program
{
    // OLE (the OS drag-drop bridge) requires the UI thread to be a single-threaded apartment - the same requirement
    // WPF/WinForms put on their entry point. Without it the app still runs; only drags to/from other applications are off.
    [STAThread]
    public static void Main(string[] args)
    {
        var gameApp = new AdamantiumGameApplication();
        gameApp.IsFixedTimeStep = false;
        gameApp.EnableGraphicsDebug = false;
        gameApp.DesiredFPS = 300;
        gameApp.StartupType = typeof(MainWindow);
        gameApp.Run();
    }
}