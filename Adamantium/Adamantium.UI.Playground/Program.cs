using Adamantium.UI.Windows;
using Adamantium.UI;
using System;
using Adamantium.UI.Threading;

namespace Adamantium.UI.Playground
{
    class Program
    {
        static void Main(string[] args)
        {
            var app = new App();
            var wnd = new MainWindow();
            app.MainWindow = wnd;
            wnd.Show();
            app.Run();
            // mainWindow.Width = 1280;
            // mainWindow.Height = 720;
            //app.Run(mainWindow);
        }
    }
}
