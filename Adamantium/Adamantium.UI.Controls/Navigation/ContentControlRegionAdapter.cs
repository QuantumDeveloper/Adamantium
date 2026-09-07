using Adamantium.Navigation;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Navigation;

/// <summary>Region on a <see cref="ContentControl"/>: renders the region's single <see cref="IRegion.CurrentViewModel"/>
/// as the control's Content (its built-in ContentTransition animates the page swap). Marks the region single-active so
/// navigating replaces instead of accumulating.</summary>
public sealed class ContentControlRegionAdapter : IRegionAdapter
{
    private readonly IViewLocator _viewLocator;

    /// <summary>What one HOST is currently showing. Per host, not per adapter: RegionAdapterMappings hands out a single
    /// adapter INSTANCE for a control type, so every ContentControl region in the app shares this object. Keeping "what
    /// is shown" in its fields meant two such regions overwrote each other's answer, and each then decided a navigation
    /// was "already shown" and drew nothing - which is what a second ContentControl region turned up the moment one
    /// existed.</summary>
    private sealed class Shown
    {
        // Read off the ContentControl instead and a transition that has not finished swapping hands back the wrong one.
        public object ViewModel;

        // ...and WHICH VIEW of it. One view-model can be read through several views, and then this is the only thing
        // that changes between two navigations - comparing the model alone would call every one of them "already shown".
        public string ViewKey;
    }

    public ContentControlRegionAdapter(IViewLocator viewLocator)
    {
        _viewLocator = viewLocator;
    }

    public void Attach(IRegion region, IUIComponent host)
    {
        if (host is not ContentControl content) return;
        region.SingleActiveView = true;
        var shown = new Shown();
        region.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName is nameof(IRegion.CurrentViewModel) or nameof(IRegion.CurrentViewKey)) Render(region, content, shown);
        };
        Render(region, content, shown);
    }

    private void Render(IRegion region, ContentControl content, Shown shown)
    {
        var viewModel = region.CurrentViewModel;
        var viewKey = region.CurrentViewKey;
        if (ReferenceEquals(viewModel, shown.ViewModel) && string.Equals(viewKey, shown.ViewKey, StringComparison.Ordinal)) return;

        // Leaving: a view that asked to be kept is handed to the framework's store, which parks it - so the detach that
        // follows reads as "coming back" and the renderer keeps what it built. Anything else is dropped, as before. The
        // view here is the CONTENT itself (a resolved view element), so the presenter cannot keep it for us - whoever
        // supplied it has to.
        if (shown.ViewModel != null && content.Content is IUIComponent leaving && ParkedVisuals.ShouldKeep(leaving))
        {
            ParkedVisuals.Keep(content, ParkKey(shown.ViewModel, shown.ViewKey), leaving);
        }

        shown.ViewModel = viewModel;
        shown.ViewKey = viewKey;
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
