using System;

namespace Adamantium.Navigation;

/// <summary>Lifecycle a dialog view model opts into: it is told when it opens, can veto closing, and closes itself (with
/// a result) by raising <see cref="RequestClose"/>.</summary>
public interface IDialogAware
{
    void OnDialogOpened(NavigationParameters parameters);

    bool CanCloseDialog();

    event Action<IDialogResult> RequestClose;
}
