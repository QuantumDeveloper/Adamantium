using System;
using Adamantium.MVVM;
using Adamantium.Navigation;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>A confirm dialog view model (IDialogAware): shows a title/message and closes itself with Ok/Cancel via
/// RequestClose. Shown by NavigationDemoViewModel through IDialogService on the overlay host.</summary>
[ViewModel]
public partial class ConfirmDialogViewModel : AdamantiumViewModel, IDialogAware
{
    [Bindable] private string title = "Confirm";
    [Bindable] private string message = "Are you sure?";

    public void OnDialogOpened(NavigationParameters parameters)
    {
        if (parameters == null) return;
        if (parameters.TryGetValue<string>("title", out var t)) Title = t;
        if (parameters.TryGetValue<string>("message", out var m)) Message = m;
    }

    public bool CanCloseDialog() => true;

    public event Action<IDialogResult> RequestClose;

    [Command] private void Ok() => RequestClose?.Invoke(DialogResult.Ok());

    [Command] private void Cancel() => RequestClose?.Invoke(DialogResult.Cancel());
}
