using Serilog;

namespace Adamantium.UI.Playground
{
    public class App : UIApplication
    {
        public App()
        {
            
        }
        
        protected override void OnStartup()
        {
            base.OnStartup();
            if (StartupUri == null) return;
            
            var path = StartupUri.OriginalString;
            MainWindow = new MainWindow();
            Log.Logger.Information("Window is shown");
            MainWindow.Show();
        }
    }
}