using Adamantium.Navigation;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Navigation;

/// <summary>Region on a <see cref="ContentControl"/>: renders the region's single <see cref="IRegion.CurrentViewModel"/>
/// as the control's Content (its built-in ContentTransition animates the page swap). Marks the region single-active so
/// navigating replaces instead of accumulating.</summary>
public sealed class ContentControlRegionAdapter : IRegionAdapter
{
    private readonly IViewLocator _viewLocator;

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
        content.Content = viewModel != null ? _viewLocator.ResolveView(viewModel) : null;
    }
}
