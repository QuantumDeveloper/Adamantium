using Adamantium.MVVM;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Resources tab. Pure view-model: NO controls, NO ResourceManager - everything is declarative. The tab shows
/// the live {ObservableResource} vs the resolve-once {ResourceReference} at two levels:
///   - a THEME palette key: press Up (Dark) / Down (Light) to swap themes - the {ObservableResource} swatch follows, the
///     {ResourceReference} one keeps its old colour;
///   - an INLINE local resource declared via ResourceContext.Resources and cycled by a view-layer CycleResourceBehavior.</summary>
[ViewModel]
public partial class ResourcesViewModel : TabPageViewModel
{
    public ResourcesViewModel() : base("Resources") { }
}
