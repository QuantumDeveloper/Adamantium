using System;
using System.Collections.Generic;

namespace Adamantium.UI.Core.Input;

/// <summary>
/// The drag payload abstraction: carries EITHER a live CLR object (levels 1-2, the fast in-app path) OR named formats
/// (Text / Files / arbitrary bytes - level 3, OLE). One <c>DropCommand</c> handles both: <c>data.Get&lt;MyItem&gt;()</c>
/// for the live object, <c>data.Contains("Files")</c> for an OS format (docs/DRAG_DROP_PLAN.md, decision 3).
/// </summary>
public interface IDataPackage
{
    /// <summary>The first stored value assignable to <typeparamref name="T"/>, or default.</summary>
    T Get<T>();

    /// <summary>The value stored under a named format, or null.</summary>
    object Get(string format);

    /// <summary>Store a live object under its type's full name.</summary>
    void Set(object data);

    /// <summary>Store a value under a named format.</summary>
    void Set(string format, object data);

    /// <summary>
    /// Advertise a format WITHOUT producing it yet - deferred rendering. The drag offers the format from the moment it
    /// starts, but <paramref name="produce"/> runs only if something actually asks for the value, and only once. That is
    /// the difference between writing a temp file on every drag and writing it on the drop that lands; the same goes for
    /// rendering an image or serializing a large document.
    /// </summary>
    void SetDeferred(string format, Func<object> produce);

    /// <summary>True when this format is still a promise - stored, advertised, not produced. Lets the platform layer
    /// offer it to the OS without paying for it; reading it through <see cref="Get(string)"/> is what pays.</summary>
    bool IsDeferred(string format);

    /// <summary>Is a value stored under this named format?</summary>
    bool Contains(string format);

    /// <summary>Is any stored value assignable to <typeparamref name="T"/>?</summary>
    bool Contains<T>();

    IReadOnlyList<string> GetFormats();
}
