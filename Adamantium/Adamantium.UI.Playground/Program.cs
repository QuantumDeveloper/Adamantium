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
            app.StartupUri = new Uri("MainWindow.xml", UriKind.RelativeOrAbsolute);
            app.Run();
        }
    }
}
