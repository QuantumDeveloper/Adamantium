using System;
using Adamantium.Core.TypeParsing;
using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Core;

/// <summary>Turns <c>Cursor="Hand"</c> in markup into the shared catalogue entry, or into a custom cursor from a file
/// path. Declared ON <see cref="Cursor"/> through <c>[TypeParser]</c>: the parser registry lives a layer below and
/// cannot see a UI type.</summary>
public class CursorParser : ITypeParser<Cursor>
{
    public Cursor Parse(string value) =>
        Enum.TryParse<CursorType>(value, ignoreCase: true, out var type) ? Cursors.Of(type) : new Cursor(value);
}
