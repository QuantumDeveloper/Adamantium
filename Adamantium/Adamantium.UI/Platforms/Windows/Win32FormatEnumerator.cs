using System.Runtime.InteropServices.ComTypes;
using Adamantium.Win32.Ole;

namespace Adamantium.UI.Platforms.Windows;

/// <summary>The <c>IEnumFORMATETC</c> a drop target walks to discover what <see cref="Win32DataObject"/> can render.
/// A plain cursor over a fixed list of formats.</summary>
internal sealed class Win32FormatEnumerator : IEnumFORMATETC
{
    private readonly FORMATETC[] _formats;
    private int _index;

    public Win32FormatEnumerator(FORMATETC[] formats, int index = 0)
    {
        _formats = formats;
        _index = index;
    }

    public int Next(int count, FORMATETC[] formats, int[] fetched)
    {
        var taken = 0;
        while (taken < count && _index < _formats.Length)
        {
            formats[taken] = _formats[_index];
            taken++;
            _index++;
        }
        if (fetched is { Length: > 0 }) fetched[0] = taken;
        return taken == count ? OleResult.Ok : OleResult.False;
    }

    public int Skip(int count)
    {
        _index += count;
        if (_index <= _formats.Length) return OleResult.Ok;
        _index = _formats.Length;
        return OleResult.False;
    }

    public int Reset()
    {
        _index = 0;
        return OleResult.Ok;
    }

    public void Clone(out IEnumFORMATETC clone) => clone = new Win32FormatEnumerator(_formats, _index);
}
