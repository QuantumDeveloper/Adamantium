using System;
using System.Threading;
using System.Threading.Tasks;

namespace Adamantium.Navigation;

/// <summary>VM-first, host-agnostic dialog facade: resolves the dialog view model, shows it via the host chosen by
/// <see cref="DialogHostKind"/>, and completes with the <see cref="IDialogResult"/> the dialog closed with.</summary>
public interface IDialogService
{
    Task<IDialogResult> ShowDialogAsync(Type dialogViewModelType, NavigationParameters parameters = null,
        DialogHostKind host = DialogHostKind.Default, CancellationToken cancellationToken = default);

    Task<IDialogResult> ShowDialogAsync<TDialogViewModel>(NavigationParameters parameters = null,
        DialogHostKind host = DialogHostKind.Default, CancellationToken cancellationToken = default);
}
