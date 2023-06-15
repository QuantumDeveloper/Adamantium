using System;
using System.Threading;
using Adamantium.Game;
using Adamantium.UI.Controls;
using Serilog;

namespace Adamantium.UI.Playground
{
    public class App : UIApplication
    {
        private UIGame _game;

        public App()
        {
            _game = new UIGame(GameMode.Slave, true);
        }

        protected override void OnStartup()
        {
            base.OnStartup();
            if (StartupUri == null) return;
            
            var path = StartupUri.OriginalString;
            MainWindow = new MainWindow();
            MainWindow.SourceInitialized += MainWindowOnSourceInitialized;
            Log.Logger.Information("Window is shown");
            MainWindow.Show();
        }

        private void MainWindowOnSourceInitialized(object sender, EventArgs e)
        {
            var panel = MainWindow.Content as RenderTargetPanel;
            Thread t = new Thread(()=>RunGame(panel));
            t.IsBackground = true;
            t.Start();
        }

        private void RunGame(RenderTargetPanel panel)
        {
            _game.Run(panel);
        }
    }
}