using Adamantium.Navigation;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Navigation;

/// <summary>The named-region binding: <c>&lt;ContentControl nav:RegionManager.RegionName="Main"/&gt;</c>. Resolves (or
/// creates) the region by name from the app <see cref="IRegionManager"/> and attaches it to this control; the view model
/// navigates via the injected manager (<c>Regions["Main"]</c>).</summary>
public static class RegionManager
{
    public static readonly AdamantiumProperty RegionNameProperty =
        AdamantiumProperty.RegisterAttached("RegionName", typeof(string), typeof(UIComponent),
            new PropertyMetadata(null, OnRegionNameChanged));

    public static string GetRegionName(IUIComponent element) => element.GetValue<string>(RegionNameProperty);
    public static void SetRegionName(IUIComponent element, string value) => element.SetValue(RegionNameProperty, value);

    private static void OnRegionNameChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not IUIComponent host || e.NewValue is not string name || string.IsNullOrEmpty(name)) return;
        var manager = UIAppContext.Current?.UIContext?.Resolve<IRegionManager>();
        var region = manager?.GetOrCreateRegion(name);
        if (region != null) RegionHost.Attach(region, host);
    }
}
