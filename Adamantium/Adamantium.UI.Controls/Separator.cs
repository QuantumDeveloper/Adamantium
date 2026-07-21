using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>A thin, non-interactive divider line between menu rows (and usable in toolbars). Not a focus target and
/// carries no behaviour - the theme draws it as a 1px rule. The menu's hover/click logic only targets
/// <see cref="Primitives.MenuItem"/>, so a Separator is naturally skipped by navigation.</summary>
public class Separator : Control
{
    static Separator()
    {
        FocusableProperty.OverrideMetadata(typeof(Separator), new PropertyMetadata(false));
    }
}
