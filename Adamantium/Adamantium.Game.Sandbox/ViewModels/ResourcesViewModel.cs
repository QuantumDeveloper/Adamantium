using System.Collections.Generic;
using Adamantium.MVVM;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Resources;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Resources tab. Pure view-model: NO controls, NO ResourceManager - everything is declarative. The tab shows
/// the live {ObservableResource} vs the resolve-once {ResourceReference} at two levels:
///   - a THEME palette key: press Up (Dark) / Down (Light) to swap themes - the {ObservableResource} swatch follows, the
///     {ResourceReference} one keeps its old colour;
///   - an INLINE local resource declared via ResourceContext.Resources and cycled by a view-layer CycleResourceBehavior.
/// ...and a THEME SCOPE stand: several panes on a theme of their own, each showing a different variant of it.</summary>
[ViewModel]
public partial class ResourcesViewModel : TabPageViewModel
{
    public ResourcesViewModel() : base("Resources") { }

    /// <summary>The theme the scope stand wears - the merged <c>Fluent</c>, which is NOT the theme the rest of the
    /// window is on (the application opens on <c>FluentDark</c>). So the stand shows both halves at once: a subtree on
    /// a theme of its own, and several of that theme's variants side by side.</summary>
    /// <remarks>
    /// A COMPLETE theme, and that is not a detail. A scope REPLACES the application's theme for its subtree rather than
    /// layering over it, so a scope theme carrying only a palette leaves everything inside it with no styles at all -
    /// including the templates controls are built from. A templated control with no template cannot finish a layout
    /// pass, the pass never settles, and the application hangs with the swap overlay up. A palette-only stand theme did
    /// exactly that here, and the symptom - "the button hangs the app with no visible effect" - said nothing about the
    /// cause.
    /// <para>Resolved lazily rather than in the constructor: view-models are built while the application is still
    /// coming up, and the theme manager has its themes only afterwards.</para>
    /// </remarks>
    public ITheme LocalTheme => UIAppContext.Current?.ThemeManager?["Fluent"];

    /// <summary>Every theme the application has registered, by name - what the stand's theme chooser lists.
    /// <para>Read from the manager each time rather than captured: themes are registered while the application comes
    /// up, and a list captured in the constructor would be the empty one.</para></summary>
    public System.Collections.Generic.IReadOnlyList<string> AvailableThemes
    {
        get
        {
            var themes = UIAppContext.Current?.ThemeManager?.Themes;
            if (themes == null) return [];

            var names = new List<string>(themes.Count);
            foreach (var theme in themes) names.Add(theme.Name);
            return names;
        }
    }

    /// <summary>Which theme the chooser shows. Defaults to the one the application is ON, resolved on FIRST READ rather
    /// than in the constructor: a view-model is built while the application is still coming up, when the theme manager
    /// has no themes yet, so a value taken there would be null for good and the chooser would open on nothing.</summary>
    public string SelectedThemeName
    {
        get => _selectedThemeName ??= UIAppContext.Current?.ThemeManager?.CurrentTheme?.Name;
        set
        {
            if (_selectedThemeName == value) return;
            _selectedThemeName = value;
            RaisePropertyChanged(nameof(SelectedThemeName));
        }
    }

    private string _selectedThemeName;

    /// <summary>Move the whole application onto the chosen theme. A SWAP, not a variant switch: different style sets and
    /// different metrics, so every template is rebuilt - which is exactly what the stand exists to make visible next to
    /// the variant button, whose cost is a colour write per palette key.</summary>
    [Command]
    private void SwitchTheme()
    {
        var manager = UIAppContext.Current?.ThemeManager;
        if (manager == null || string.IsNullOrEmpty(SelectedThemeName)) return;

        var theme = manager[SelectedThemeName];
        if (theme != null && !ReferenceEquals(theme, manager.CurrentTheme)) manager.SetTheme(theme);
    }

    /// <summary>The switchable pane's own light/dark, changed by its own checkbox and by nothing else - not by the
    /// application's theme buttons, and not by the OS.</summary>
    [Bindable] private bool _standIsDark;

    public ThemeVariant StandVariant => StandIsDark ? ThemeVariant.Dark : ThemeVariant.Light;

    partial void OnStandIsDarkChanged(bool value) => RaisePropertyChanged(nameof(StandVariant));
}
