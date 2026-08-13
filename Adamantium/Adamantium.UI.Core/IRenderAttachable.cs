namespace Adamantium.UI.Core;

/// <summary>A value that needs to KNOW which element draws with it. Implemented by the value; asked by the property
/// system when an <c>AffectsRender</c> property takes one or gives one up.
/// <para>An interface rather than type checks: that spot runs on EVERY render-property write, and a chain of "is it a
/// brush, is it a this" there only grows. A new kind of value implements this instead of editing the property
/// system.</para></summary>
public interface IRenderAttachable
{
    /// <summary>An element just took this value for a render property.</summary>
    void AttachTo(AdamantiumComponent owner);

    /// <summary>An element just gave it up.</summary>
    void DetachFrom(AdamantiumComponent owner);
}
