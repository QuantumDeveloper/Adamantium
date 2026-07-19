namespace Adamantium.Navigation;

/// <summary>Where a dialog is hosted. <see cref="Default"/> is resolved by the host registry (currently to
/// <see cref="Overlay"/>), so callers stay host-agnostic.</summary>
public enum DialogHostKind
{
    Default,
    Overlay,
    Window
}
