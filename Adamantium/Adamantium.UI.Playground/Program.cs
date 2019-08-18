using Adamantium.UI.Windows;
using System;

namespace Adamantium.UI.Playground
{
    class Program
    {
        static void Main(string[] args)
        {
            var app = new WindowsApplication();
            var mainWindow = new Win32Window();
            app.MainWindow = mainWindow;
            mainWindow.Show();
            app.Run();
        }
    }
}
