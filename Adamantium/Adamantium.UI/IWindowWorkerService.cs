using System;
using Adamantium.UI.MacOS;
using Adamantium.UI.Windows;

namespace Adamantium.UI;

public interface IWindowWorkerService
{
    public void SetWindow(WindowBase window);

    public void SetTitle(string title);

    public static IWindowWorkerService GetWorker()
    {
        switch (Configuration.Platform)
        {
            case Platform.Windows:
                return new Win32WindowWorker();
            case Platform.OSX:
                return new MacOSWindowWorker();
            default:
                throw new NotSupportedException($"{Configuration.Platform} does not yet supported for windowing system");
        }
    }
}