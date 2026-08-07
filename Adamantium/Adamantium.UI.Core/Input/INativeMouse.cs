using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Input;

/// <summary>
/// Live pointer state straight from the OS, for the two things our own message-driven tracking cannot answer: where the
/// pointer is when no move has reached us (another application owns it, or nothing moved since we last looked), and
/// warping it somewhere. A platform registers its implementation on <see cref="Mouse.Platform"/> at startup.
/// </summary>
public interface INativeMouse
{
    /// <summary>Where the pointer is on the DESKTOP - see <see cref="PixelPoint"/> for why that has a type of its own.</summary>
    PixelPoint Position { get; set; }
}
