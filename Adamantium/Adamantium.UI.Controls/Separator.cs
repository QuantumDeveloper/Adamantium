using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>A thin, non-interactive divider line between menu rows and between groups in a toolbar. Not a focus target
/// and carries no behaviour - the theme draws it as a 1px rule. The menu's hover/click logic only targets
/// <see cref="Primitives.MenuItem"/>, so a Separator is naturally skipped by navigation.</summary>
public class Separator : Control
{
    /// <summary>Which way the rule RUNS - across a column of rows, or down between two groups standing side by side.
    /// <para>Horizontal by default, which is the menu. Said and not inferred from the panel it is in: a separator does
    /// not know what is holding it, and one that guessed would guess wrong in every layout that is not a stack.</para>
    /// </summary>
    public static readonly AdamantiumProperty OrientationProperty = AdamantiumProperty.Register(nameof(Orientation),
        typeof(Orientation), typeof(Separator),
        new PropertyMetadata(Panels.Orientation.Horizontal, PropertyMetadataOptions.AffectsMeasure));

    public Orientation Orientation
    {
        get => GetValue<Orientation>(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    static Separator()
    {
        FocusableProperty.OverrideMetadata(typeof(Separator), new PropertyMetadata(false));
    }
}
