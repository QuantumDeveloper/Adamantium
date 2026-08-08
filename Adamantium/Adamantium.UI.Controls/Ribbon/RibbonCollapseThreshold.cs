namespace Adamantium.UI.Controls;

/// <summary>When a command takes a smaller size as its GROUP steps down. The group steps as one - Large, then Medium,
/// then Small - and each command says which of those steps it follows, so a command that is unreadable as a bare icon
/// can sit out the last step while everything around it takes it.
/// <para>This is the knob Telerik's ribbon spells <c>CollapseToMedium</c>/<c>CollapseToSmall</c>, and it is worth having
/// for the same reason: a range alone says how small a command MAY get, never WHEN.</para></summary>
public enum RibbonCollapseThreshold
{
    /// <summary>Follow the group at this step - the ordinary case.</summary>
    WhenGroupIsMedium,

    /// <summary>Hold out until the group's last step.</summary>
    WhenGroupIsSmall,

    /// <summary>Never take this size, however narrow the tab gets.</summary>
    Never
}
