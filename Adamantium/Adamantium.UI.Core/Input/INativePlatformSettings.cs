
namespace Adamantium.UI.Core.Input;

/// <summary>
/// User-configured input settings owned by the OS, not by us - honouring them is what makes the app feel native.
/// Registered on <see cref="PlatformSettings.Platform"/> at startup.
/// </summary>
public interface INativePlatformSettings
{
    /// <summary>Longest gap between two clicks that still counts as a double-click, in milliseconds.</summary>
    uint DoubleClickTime { get; }
}
