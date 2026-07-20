using System;
using System.Threading;
using System.Threading.Tasks;

namespace Adamantium.Navigation;

/// <summary>Shows a view model as a floating, draggable, resizable, pinnable in-window <c>OverlayWindow</c> (VM-first, no
/// UI in the view model). Deliberately separate from <see cref="IDialogService"/>: an overlay is a window, not a modal
/// dialog. Completes with the result the overlay closed with (or null).</summary>
public interface IOverlayService
{
    Task<object> ShowOverlayAsync(Type overlayViewModelType, NavigationParameters parameters = null, CancellationToken cancellationToken = default);

    Task<object> ShowOverlayAsync<TOverlayViewModel>(NavigationParameters parameters = null, CancellationToken cancellationToken = default)
        where TOverlayViewModel : IOverlayAware;
}
