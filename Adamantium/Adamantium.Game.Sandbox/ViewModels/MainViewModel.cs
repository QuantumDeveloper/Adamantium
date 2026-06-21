using Adamantium.MVVM;
using Adamantium.Win32;

namespace Adamantium.Game.Sandbox.ViewModels;

[ViewModel]
public partial class MainViewModel
{
    [Command]
    private void ShowMessage()
    {
        MessageBox.Show("Hello World!");
    }
}