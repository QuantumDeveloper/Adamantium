using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Controls;

/// <summary>
/// The themed card that hosts hover-tooltip content (a string or any UI element), shown by <see cref="ToolTipService"/>.
/// Everything visible - background / border / corner / padding / text colour and size - is driven by the active theme's
/// <c>ToolTip</c> style through {ResourceReference}/{ThemeResource}, so it restyles live on a theme or accent change
/// instead of being hard-coded. A string is rendered by the template's ContentPresenter (which template-binds Foreground
/// and FontSize); a UI element is shown as-is.
/// </summary>
public class ToolTip : ContentControl
{
    static ToolTip()
    {
        FontSizeProperty.OverrideMetadata(typeof(ToolTip),
            new PropertyMetadata(12.0, PropertyMetadataOptions.Inherits | PropertyMetadataOptions.AffectsMeasure));
    }

}
