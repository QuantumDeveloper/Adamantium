namespace Adamantium.UI.Core.Input;

/// <summary>
/// Turns a platform-neutral <see cref="Cursor"/> into the pointer the OS actually shows. A platform registers its
/// implementation on <see cref="Cursor.Platform"/> at startup; everything above works in <see cref="CursorType"/>s and
/// never sees a native handle.
/// <para>Implementations cache what they resolve - <see cref="Apply"/> runs on every mouse move during a drag.</para>
/// </summary>
public interface INativeCursors
{
    /// <summary>Make <paramref name="cursor"/> the pointer shown right now. A cursor the platform has no shape for
    /// falls back to the arrow rather than failing - a missing shape must never break input.</summary>
    void Apply(Cursor cursor);
}
