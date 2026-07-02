namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Base for a gallery tab's view-model. Carries the strip <see cref="Header"/> the TabControl's ItemTemplate
/// binds; each concrete type is mapped to its View for the body by <see cref="TabViewSelector"/>. Composing one of these
/// per tab under <see cref="GalleryViewModel"/> is the "real app" shape - a single root view-model owning child ones.</summary>
public abstract class TabPageViewModel
{
    protected TabPageViewModel(string header) => Header = header;

    /// <summary>Label shown in the tab strip.</summary>
    public string Header { get; }
}
