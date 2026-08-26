namespace Adamantium.UI.Core;

/// <summary>
/// Where a visual stands between being built and being released. The question every teardown has to answer is not
/// "is it in the tree" - a great many live visuals are temporarily out of it - but "may its subscriptions be undone",
/// and only one of these states says yes.
/// </summary>
/// <remarks>
/// This was a bool (<c>IsDiscarded</c>), and a bool cannot tell apart the two ways a visual leaves the tree: gone for
/// good, and gone on purpose and coming back. Worse, a bool that is only ever set is permanent - a keep-alive view
/// returned still carrying the mark it was given when its template was last torn down, so every sweep went on
/// answering "destroyed" for content that was on screen, and released it under the user.
/// <para>Ordering matters as much as the states themselves. Something may be told it is going long before it has
/// stopped taking part in the rebuild that is replacing it, which is why <see cref="Detaching"/> exists as its own
/// state: it says the teardown has STARTED, and nothing may be released yet.</para>
/// </remarks>
public enum VisualLifecycle
{
    /// <summary>In use. Either in the tree, or briefly out of it and coming straight back (a re-parent).</summary>
    Live,

    /// <summary>A teardown has begun on it, and it may still be taking part in the rebuild that replaces it. Nothing
    /// may be released here - this is the state that says "marked, but not yet finished with".</summary>
    Detaching,

    /// <summary>Deliberately out of the tree and coming back through the SAME host - a view that asked to be kept
    /// (<c>x:KeepAlive</c>). Releasing it is a bug: it is not dead, it is waiting. See <c>ParkedVisuals</c>.</summary>
    Parked,

    /// <summary>A generated item container sitting in its generator's pool, to be REUSED for another item. Like
    /// <see cref="Parked"/> it is not dead, but it comes back a different way - re-bound to a new item rather than
    /// resumed where it left off.</summary>
    Recycled,

    /// <summary>Destroyed for good. The ONLY state in which what holds this visual may let go of it, and the only one
    /// this enum treats as final.</summary>
    Discarded,
}
