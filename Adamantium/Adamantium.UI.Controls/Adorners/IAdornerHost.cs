namespace Adamantium.UI.Controls.Adorners;

/// <summary>
/// A window that owns an <see cref="AdornerLayer"/> (tooling overlays - selection / hover frames). Both the real
/// <see cref="WindowBase"/> and the designer's virtual window implement it, so tooling (e.g. the designer) drives the
/// overlay through this one contract instead of casting to a concrete window type.
/// </summary>
/// <remarks>
/// The layer instance still lives on each window (a C# interface can't hold it), and it can't sit on
/// <c>IWindow</c> either: <c>IWindow</c> is in the Core layer, which must not reference the Controls-layer
/// <see cref="AdornerLayer"/> - that's why <c>IWindow</c> exposes only the read-only <c>Adorners</c> list.
/// </remarks>
public interface IAdornerHost
{
    AdornerLayer AdornerLayer { get; }
}
