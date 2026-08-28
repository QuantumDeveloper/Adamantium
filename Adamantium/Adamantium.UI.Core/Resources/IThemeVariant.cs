namespace Adamantium.UI.Core.Resources;

/// <summary>What makes a markup file a THEME VARIANT rather than a preview fragment. The AUML compiler decides what to
/// emit for a document from the interface its root implements - a window, a style set, a theme - and a variant is the
/// newest member of that list: a file whose root is a <see cref="ThemeVariantDefinition"/> generates a class deriving
/// from it, which the theme then names in <c>&lt;Theme.Variants&gt;</c>.
/// <para>Marker-only on purpose. The contract a variant has to meet is already <see cref="ThemeVariantDefinition"/>'s;
/// this says which files are one, and nothing more.</para></summary>
public interface IThemeVariant
{
}
