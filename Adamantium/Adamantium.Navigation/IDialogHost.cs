using System.Threading;
using System.Threading.Tasks;

namespace Adamantium.Navigation;

/// <summary>UI-free abstraction over WHERE a dialog is shown (an in-window overlay, a modal window, ...). The service
/// resolves the view model and hands it here; the host presents the resolved view and completes when the dialog is
/// dismissed. Implementations register themselves in <see cref="IDialogHostRegistry"/>.</summary>
public interface IDialogHost
{
    DialogHostKind Kind { get; }

    Task<IDialogResult> ShowAsync(object dialogViewModel, NavigationParameters parameters, CancellationToken cancellationToken = default);
}
