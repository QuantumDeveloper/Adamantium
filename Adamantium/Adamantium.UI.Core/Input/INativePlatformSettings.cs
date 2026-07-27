using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Input;

/// <summary>
/// User-configured input settings owned by the OS, not by us - honouring them is what makes the app feel native.
/// Registered on <see cref="PlatformSettings.Platform"/> at startup.
/// </summary>
public interface INativePlatformSettings
{
    /// <summary>Longest gap between two clicks that still counts as a double-click, in milliseconds.</summary>
    uint DoubleClickTime { get; }

    /// <summary>How far the pointer must travel, per axis, before a press counts as a DRAG rather than a click.
    /// Every desktop OS exposes this as a user setting (Win32 <c>SM_CXDRAG</c>/<c>SM_CYDRAG</c>, macOS's drag
    /// threshold, the GTK/Qt start-drag distance) - honouring it is the difference between "clicks sometimes
    /// drag by accident" and the app feeling like the rest of the system.</summary>
    Size DragThreshold { get; }
}
