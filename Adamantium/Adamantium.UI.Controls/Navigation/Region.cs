using Adamantium.Navigation;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Navigation;

/// <summary>The VM-owned region binding: <c>&lt;TabControl nav:Region.Source="{Binding TabsRegion}"/&gt;</c>. The view
/// model constructs and owns the <see cref="IRegion"/>; setting it here attaches the region to this control via its
/// adapter. No navigation logic in the view.</summary>
public static class Region
{
    public static readonly AdamantiumProperty SourceProperty =
        AdamantiumProperty.RegisterAttached("Source", typeof(IRegion), typeof(UIComponent),
            new PropertyMetadata(null, OnSourceChanged));

    public static IRegion GetSource(IUIComponent element) => element.GetValue<IRegion>(SourceProperty);
    public static void SetSource(IUIComponent element, IRegion value) => element.SetValue(SourceProperty, value);

    private static void OnSourceChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is IUIComponent host && e.NewValue is IRegion region)
            RegionHost.Attach(region, host);
    }
}
