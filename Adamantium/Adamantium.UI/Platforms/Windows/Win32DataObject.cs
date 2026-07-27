using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Adamantium.UI.Core.Input;
using Adamantium.Win32;
using Adamantium.Win32.Ole;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace Adamantium.UI.Platforms.Windows;

/// <summary>
/// Our payload seen through OLE eyes: a COM <see cref="ComTypes.IDataObject"/> over an <see cref="IDataPackage"/>, so a
/// drag that starts in our app can be dropped into Explorer, an editor, a browser. It renders the package's neutral
/// formats on demand - <c>DataFormats.Text</c> as CF_UNICODETEXT, <c>DataFormats.Files</c> as CF_HDROP.
/// <para>
/// It also keeps the LIVE package, so when the gesture comes back over one of our own windows the drop target unwraps
/// this same instance and gets the real CLR object - the fast in-app path survives a trip through the OS.
/// </para>
/// </summary>
internal sealed class Win32DataObject : ComTypes.IDataObject, IDisposable
{
    // Formats WRITTEN INTO us by someone else: the shell's drag-image helper stores its private blocks here (that is how
    // the OS carries our ghost), and a drop target reports what it did through "Performed DropEffect". We own whatever
    // we store and release it on dispose.
    private readonly Dictionary<short, STGMEDIUM> _stored = new();

    public Win32DataObject(IDataPackage package)
    {
        Package = package;
    }

    public IDataPackage Package { get; }

    /// <summary>Every format this object can render right now, most descriptive first (the order an enumerating target
    /// walks, so it sees files before their text form), followed by whatever was written into it.</summary>
    public FORMATETC[] Formats
    {
        get
        {
            var formats = new List<FORMATETC>();
            if (OleDataBridge.FilesOf(Package) != null) formats.Add(Describe((short)Win32Interop.CF_HDROP, TYMED.TYMED_HGLOBAL));
            if (OleDataBridge.TextOf(Package) != null) formats.Add(Describe((short)Win32Interop.CF_UNICODETEXT, TYMED.TYMED_HGLOBAL));
            foreach (var (format, medium) in _stored)
            {
                if (!formats.Exists(f => f.cfFormat == format)) formats.Add(Describe(format, medium.tymed));
            }
            return [.. formats];
        }
    }

    private static FORMATETC Describe(short format, TYMED tymed) => new()
    {
        cfFormat = format,
        ptd = IntPtr.Zero,
        dwAspect = DVASPECT.DVASPECT_CONTENT,
        lindex = -1,
        tymed = tymed
    };

    public void GetData(ref FORMATETC format, out STGMEDIUM medium)
    {
        medium = default;
        if (_stored.TryGetValue(format.cfFormat, out var stored))
        {
            if ((format.tymed & stored.tymed) == 0) throw new COMException(null, OleResult.TymedNotSupported);
            medium = Share(stored);
            if (medium.tymed == TYMED.TYMED_NULL) throw new COMException(null, OleResult.TymedNotSupported);
            return;
        }

        if ((format.tymed & TYMED.TYMED_HGLOBAL) == 0) throw new COMException(null, OleResult.TymedNotSupported);
        var global = Render(format.cfFormat);
        if (global == IntPtr.Zero) throw new COMException(null, OleResult.FormatNotSupported);

        // pUnkForRelease null = the CALLER frees what it gets, which is why every path above hands out its own reference.
        medium = new STGMEDIUM { tymed = TYMED.TYMED_HGLOBAL, unionmember = global, pUnkForRelease = null };
    }

    private IntPtr Render(short format)
    {
        if (format == (short)Win32Interop.CF_HDROP && OleDataBridge.FilesOf(Package) is { Length: > 0 } files)
            return OleDataBridge.CreateHDropGlobal(files);
        if (format == (short)Win32Interop.CF_UNICODETEXT && OleDataBridge.TextOf(Package) is { } text)
            return OleDataBridge.CreateTextGlobal(text);
        return IntPtr.Zero;
    }

    /// <summary>A handout copy of a stored medium: memory is duplicated, an interface is simply AddRef'd (the caller
    /// releases what it receives either way). TYMED_NULL back = we cannot share that kind of medium.</summary>
    private static STGMEDIUM Share(STGMEDIUM stored)
    {
        if (stored.tymed == TYMED.TYMED_HGLOBAL)
        {
            var copy = OleDataBridge.CopyGlobal(stored.unionmember);
            return copy == IntPtr.Zero ? default : new STGMEDIUM { tymed = TYMED.TYMED_HGLOBAL, unionmember = copy };
        }
        if (stored.tymed is TYMED.TYMED_ISTREAM or TYMED.TYMED_ISTORAGE && stored.unionmember != IntPtr.Zero)
        {
            Marshal.AddRef(stored.unionmember);
            return new STGMEDIUM { tymed = stored.tymed, unionmember = stored.unionmember };
        }
        return default;
    }

    public void GetDataHere(ref FORMATETC format, ref STGMEDIUM medium) => throw new COMException(null, OleResult.NotImplemented);

    public int QueryGetData(ref FORMATETC format)
    {
        if (format.dwAspect != DVASPECT.DVASPECT_CONTENT) return OleResult.AspectNotSupported;
        foreach (var offered in Formats)
        {
            if (offered.cfFormat != format.cfFormat) continue;
            return (format.tymed & offered.tymed) != 0 ? OleResult.Ok : OleResult.TymedNotSupported;
        }
        return OleResult.FormatNotSupported;
    }

    public int GetCanonicalFormatEtc(ref FORMATETC formatIn, out FORMATETC formatOut)
    {
        formatOut = formatIn;
        formatOut.ptd = IntPtr.Zero;
        return OleResult.False;   // DATA_S_SAMEFORMATETC: nothing to canonicalize
    }

    // Accepting arbitrary formats is not optional: the shell's drag-image helper stores its private state HERE, and
    // refusing it is what makes an OS drag fall back to a bare cursor with no ghost.
    public void SetData(ref FORMATETC format, ref STGMEDIUM medium, bool release)
    {
        // release = we are handed ownership; otherwise take a reference of our own, since the caller frees its copy.
        var owned = release ? medium : Share(medium);
        if (owned.tymed == TYMED.TYMED_NULL) throw new COMException(null, OleResult.TymedNotSupported);

        Free(format.cfFormat);
        _stored[format.cfFormat] = owned;
    }

    public IEnumFORMATETC EnumFormatEtc(DATADIR direction)
    {
        if (direction != DATADIR.DATADIR_GET) throw new COMException(null, OleResult.NotImplemented);
        return new Win32FormatEnumerator(Formats);
    }

    public int DAdvise(ref FORMATETC format, ADVF advf, IAdviseSink sink, out int connection)
    {
        connection = 0;
        return OleResult.AdviseNotSupported;
    }

    public void DUnadvise(int connection) => throw new COMException(null, OleResult.AdviseNotSupported);

    public int EnumDAdvise(out IEnumSTATDATA enumAdvise)
    {
        enumAdvise = null;
        return OleResult.AdviseNotSupported;
    }

    private void Free(short format)
    {
        if (!_stored.Remove(format, out var medium)) return;
        Win32Interop.ReleaseStgMedium(ref medium);
    }

    public void Dispose()
    {
        foreach (var format in new List<short>(_stored.Keys))
        {
            Free(format);
        }
    }
}
