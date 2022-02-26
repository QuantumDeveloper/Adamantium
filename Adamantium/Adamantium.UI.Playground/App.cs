using System.IO;
using Adamantium.UI.Controls;
using Adamantium.UI.Xaml;

namespace Adamantium.UI.Playground
{
    public class App : Application
    {
        public App()
        {
            
        }
        
        protected override void OnStartup()
        {
            var path = StartupUri.OriginalString;
            if (XamlParser.Parse(File.ReadAllText(path)) is IWindow wnd)
            {
                MainWindow = wnd;
                MainWindow.Show();
            }
        }
    }
}