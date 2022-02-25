using Adamantium.UI.Windows;
using Adamantium.UI;
using System;
using System.IO;
using Adamantium.UI.Controls;
using Adamantium.UI.Threading;
using Adamantium.UI.Xaml;

namespace Adamantium.UI.Playground
{
    class Program
    {
        static void Main(string[] args)
        {
            var app = new App();
            //var wnd = new MainWindow();
            var wnd = XamlParser.Parse(File.ReadAllText("MainWindow.xml")) as IWindow;
            app.MainWindow = wnd;
            wnd.Show();
            app.Run();
            // mainWindow.Width = 1280;
            // mainWindow.Height = 720;
            //app.Run(mainWindow);70028128<
        }
    }
}
