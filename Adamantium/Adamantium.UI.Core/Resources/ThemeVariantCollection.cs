using Adamantium.Core.Collections;

namespace Adamantium.UI.Core.Resources;

/// <summary>The variants a theme declares, as markup writes them:
/// <code>
/// &lt;Theme.Variants&gt;
///   &lt;ThemeVariantDefinition Key="Light"&gt;
///     &lt;PaletteColor Key="SolidBackgroundFillColorBase" Color="#F3F3F3"/&gt;
///   &lt;/ThemeVariantDefinition&gt;
/// &lt;/Theme.Variants&gt;
/// </code>
/// A collection rather than a fixed pair of light/dark slots: how many variants a theme has, and what they are called,
/// is the theme's business. A HUD theme has no light variant at all and wants three signal colours instead.</summary>
[MarkupItem(ItemType = typeof(ThemeVariantDefinition), ItemProperty = nameof(ThemeVariantDefinition.Key))]
public class ThemeVariantCollection : TrackingCollection<ThemeVariantDefinition>
{
}
