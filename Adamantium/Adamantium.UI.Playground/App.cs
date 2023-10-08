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
    }
}