using Adamantium.Core.TypeParsing;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Behaviors;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.Game.Sandbox.Behaviors;

/// <summary>
/// View-layer behavior (NOT a view-model): on the button's click it overrides a keyed brush in the given resource SCOPE
/// (Theme palette entry, Global app resource) with a NEW brush via the resource system's runtime override
/// (<see cref="IResourceManager.SetResourceInScope"/>). A live <c>{ObservableResource Key}</c> re-resolves to the new
/// brush; a <c>{ResourceReference Key}</c> keeps the one it resolved once - demonstrating the difference without
/// touching anything else. A Theme-scope override lives in the CURRENT theme's palette dictionary, so a theme swap
/// naturally discards it; a Global-scope one belongs to no theme and survives the swap. Attach in markup:
/// <code>&lt;Button&gt;&lt;Button.Behaviors&gt;&lt;local:CycleScopedResourceBehavior Key="GlobalAccent" Scope="Global"/&gt;&lt;/Button.Behaviors&gt;&lt;/Button&gt;</code>
/// </summary>
public class CycleScopedResourceBehavior : Behavior<Button>
{
    private static readonly string[] Colors = ["#F87171", "#FBBF24", "#34D399", "#60A5FA", "#C084FC"];
    private int _index;

    /// <summary>The resource key to cycle.</summary>
    public string Key { get; set; }

    /// <summary>The scope whose dictionary declares <see cref="Key"/> (Theme for a palette entry, Global for an
    /// app-wide resource).</summary>
    public ResourceScope Scope { get; set; } = ResourceScope.Theme;

    protected override void OnAttached(Button button)
    {
        button.Click += OnClick;
    }

    protected override void OnDetached(Button button)
    {
        button.Click -= OnClick;
    }

    private void OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(Key)) return;
        _index = (_index + 1) % Colors.Length;
        UIAppContext.Current?.ResourceManager?.SetResourceInScope(Key, TypeParser.Parse<Brush>(Colors[_index]), Scope);
    }
}
