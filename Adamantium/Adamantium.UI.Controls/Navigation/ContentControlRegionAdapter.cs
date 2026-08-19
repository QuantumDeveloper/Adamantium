using Adamantium.Navigation;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Navigation;

/// <summary>Region on a <see cref="ContentControl"/>: renders the region's single <see cref="IRegion.CurrentViewModel"/>
/// as the control's Content (its built-in ContentTransition animates the page swap). Marks the region single-active so
/// navigating replaces instead of accumulating.</summary>
public sealed class ContentControlRegionAdapter : IRegionAdapter
{
    private readonly IViewLocator _viewLocator;

    // What is currently shown, so leaving it can park it. Read off the ContentControl instead and a transition that has
    // not finished swapping would hand back the wrong one.
    private object _currentViewModel;

    public ContentControlRegionAdapter(IViewLocator viewLocator)
    {
        _viewLocator = viewLocator;
    }

    public void Attach(IRegion region, IUIComponent host)
    {
        if (host is not ContentControl content) return;
        region.SingleActiveView = true;
        region.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(IRegion.CurrentViewModel)) Render(region, content);
        };
        Render(region, content);
    }

    private void Render(IRegion region, ContentControl content)
    {
        var viewModel = region.CurrentViewModel;
        if (ReferenceEquals(viewModel, _currentViewModel)) return;

        // Leaving: a view that asked to be kept is handed to the framework's store, which parks it - so the detach that
        // follows reads as "coming back" and the renderer keeps what it built. Anything else is dropped, as before. The
        // view here is the CONTENT itself (a resolved view element), so the presenter cannot keep it for us - whoever
        // supplied it has to.
        if (_currentViewModel != null && content.Content is IUIComponent leaving && ParkedVisuals.ShouldKeep(leaving))
        {
            ParkedVisuals.Keep(content, _currentViewModel, leaving);
        }

        _currentViewModel = viewModel;
        if (viewModel == null)
        {
            content.Content = null;
            return;
        }

        // Returning: the parked view goes back in as it was - the rebuild it avoids is the pause this exists for.
        if (ParkedVisuals.TryTake(content, viewModel, content, out var parked, out _, out _, out _))
        {
            content.Content = parked;
            ParkedSubtree.Unpark(parked);
            return;
        }

        content.Content = _viewLocator.ResolveView(viewModel);
    }
}
