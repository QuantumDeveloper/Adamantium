using Adamantium.MVVM;
using Adamantium.Win32;

namespace Adamantium.Game.Sandbox.ViewModels;

[ViewModel]
public partial class MainViewModel
{
    [Command]
    private void ShowMessage()
    {
        Width += 150;
    }

    [Bindable]
    private double _width = 150;

    // Opens the diagnostics SlidePanel (so the live stats don't cover the window content unless asked for).
    [Bindable] private bool _diagnosticsOpen;
}