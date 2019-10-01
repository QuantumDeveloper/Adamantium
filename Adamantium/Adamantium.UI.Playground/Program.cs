using Adamantium.UI.Windows;
using Adamantium.UI;
using System;

namespace Adamantium.UI.Playground
{
    class Program
    {
        static void Main(string[] args)
        {
            var app = Application.New();
            var mainWindow = Window.New();
            mainWindow.Width = 1280;
            mainWindow.Height = 720;
            app.MainWindow = mainWindow;
            mainWindow.Show();
            app.Run();
        }
    }
}
