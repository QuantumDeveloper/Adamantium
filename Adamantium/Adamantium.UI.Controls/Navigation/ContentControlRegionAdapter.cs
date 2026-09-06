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

    // ...and WHICH VIEW of it. One view-model can be read through several views, and then this is the only thing that
    // changes between two navigations - comparing the model alone would call every one of them "already shown".
    private string _currentViewKey;

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
            if (e.PropertyName is nameof(IRegion.CurrentViewModel) or nameof(IRegion.CurrentViewKey)) Render(region, content);
        };
        Render(region, content);
    }

    private void Render(IRegion region, ContentControl content)
    {
        var viewModel = region.CurrentViewModel;
        var viewKey = region.CurrentViewKey;
        if (ReferenceEquals(viewModel, _currentViewModel) && string.Equals(viewKey, _currentViewKey, StringComparison.Ordinal)) return;

        // Leaving: a view that asked to be kept is handed to the framework's store, which parks it - so the detach that
        // follows reads as "coming back" and the renderer keeps what it built. Anything else is dropped, as before. The
        // view here is the CONTENT itself (a resolved view element), so the presenter cannot keep it for us - whoever
        // supplied it has to.
        if (_currentViewModel != null && content.Content is IUIComponent leaving && ParkedVisuals.ShouldKeep(leaving))
        {
            ParkedVisuals.Keep(content, ParkKey(_currentViewModel, _currentViewKey), leaving);
        }

        _currentViewModel = viewModel;
        _currentViewKey = viewKey;
        if (viewModel == null)
        {
            content.Content = null;
            return;
        }

        // Returning: the parked view goes back in as it was - the rebuild it avoids is the pause this exists for.
        if (ParkedVisuals.TryTake(content, ParkKey(viewModel, viewKey), content, out var parked, out _, out _, out _))
        {
            content.Content = parked;
            ParkedSubtree.Unpark(parked);
            return;
        }

        content.Content = _viewLocator.ResolveView(viewModel, viewKey);
    }

    // Without a key the view-model IS the key, exactly as before - so nothing changes for a region that never names one.
    // With a key the pair is, or two views of the same model would park over each other and come back as the wrong face.
    private static object ParkKey(object viewModel, string viewKey)
        => string.IsNullOrEmpty(viewKey) ? viewModel : (viewModel, viewKey);
}
