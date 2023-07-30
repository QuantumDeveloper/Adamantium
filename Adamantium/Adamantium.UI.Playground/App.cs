using System;
using System.Threading;
using Adamantium.Game;
using Adamantium.UI.Controls;
using Serilog;

namespace Adamantium.UI.Playground
{
    public class App : UIApplication
    {
        public App()
        {
            EnableGraphicsDebug = false;
        }

        protected override void OnStartup()
        {
            base.OnStartup();
            if (StartupUri == null) return;

            var path = StartupUri.OriginalString;
            MainWindow = new MainWindow();
            MainWindow.Show();
            Log.Logger.Information("Window is shown");
        }
    }
}