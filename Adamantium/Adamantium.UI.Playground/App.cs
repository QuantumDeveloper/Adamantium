using System;
using System.IO;
using Adamantium.UI.Controls;

namespace Adamantium.UI.Playground
{
    public class App : UIApplication
    {
        public App()
        {
            
        }
        
        protected override void OnStartup()
        {
            if (StartupUri == null) return;
            
            var path = StartupUri.OriginalString;
            MainWindow = new MainWindow();
            MainWindow.Show();
        }
    }
}