using System;
using System.Threading;
using System.Threading.Tasks;
using Adamantium.Core.DependencyInjection;

namespace Adamantium.Navigation;

/// <summary>Default dialog service: resolves the dialog view model through DI, picks the host from
/// <see cref="IDialogHostRegistry"/> by kind, and delegates the show/await to it. UI-free.</summary>
public sealed class DialogService : IDialogService
{
    private readonly IDependencyResolver _resolver;
    private readonly IDialogHostRegistry _hosts;

    public DialogService(IDependencyResolver resolver, IDialogHostRegistry hosts)
    {
        _resolver = resolver;
        _hosts = hosts;
    }

    public Task<IDialogResult> ShowDialogAsync(Type dialogViewModelType, NavigationParameters parameters = null,
        DialogHostKind host = DialogHostKind.Default, CancellationToken cancellationToken = default)
    {
        var dialogHost = _hosts?.Get(host);
        if (dialogHost == null)
            return Task.FromResult<IDialogResult>(new DialogResult(DialogButtonResult.None));

        var dialogViewModel = _resolver.Resolve(dialogViewModelType);
        return dialogHost.ShowAsync(dialogViewModel, parameters, cancellationToken);
    }

    public Task<IDialogResult> ShowDialogAsync<TDialogViewModel>(NavigationParameters parameters = null,
        DialogHostKind host = DialogHostKind.Default, CancellationToken cancellationToken = default)
        => ShowDialogAsync(typeof(TDialogViewModel), parameters, host, cancellationToken);
}
