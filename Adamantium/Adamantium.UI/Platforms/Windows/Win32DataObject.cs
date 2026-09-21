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

    // A picture, rendered ONCE per gesture. A target asks for the same format over and over while the drag is in
    // flight - the shell alone did it dozens of times in a single drop - and re-deriving these each time means decoding
    // (and for PNG, re-encoding) a full-size image per request. That is what a "slow" drop actually is. The payload
    // cannot change mid-drag, so caching is simply correct.
    private byte[] _pictureAsDib;

    private byte[] PictureAsDib => _pictureAsDib ??= OleDataBridge.DibBytes(Package.Get(DataFormats.Image) as byte[]);

    /// <summary>Every format this object can render right now, most descriptive first (the order an enumerating target
    /// walks, so it sees files before their text form), followed by whatever was written into it.</summary>
    public FORMATETC[] Formats
    {
        get
        {
            var formats = new List<FORMATETC>();
            // Contains() FIRST, deliberately: it answers without redeeming a deferred format, so advertising a promised
            // payload costs nothing. Only when the package has no such entry do we fall back to reading a live value
            // (a bare string / string list), which is cheap by construction.
            if (Package.Contains(DataFormats.Files) || OleDataBridge.FilesOf(Package) != null)
                formats.Add(Describe((short)Win32Interop.CF_HDROP, TYMED.TYMED_HGLOBAL));
            // A picture goes out twice: as CF_DIB for the applications that only ever learned that one, and as PNG
            // (below, with the other registered formats) for anything modern. Same payload, two renderings.
            if (Package.Contains(DataFormats.Image)) formats.Add(Describe((short)Win32Interop.CF_DIB, TYMED.TYMED_HGLOBAL));
            foreach (var (name, id) in CustomFormats())
            {
                if (!formats.Exists(f => f.cfFormat == id)) formats.Add(Describe(id, TYMED.TYMED_HGLOBAL));
            }
            // Text LAST, and that is not cosmetic: a target takes the first format it understands, and almost everything
            // understands text. Offering it before the picture is how a dropped image arrives as its own caption - our
            // payloads carry the dragged item's string alongside, so this ordering is the difference between working and
            // not. Most descriptive first, plain text as the fallback everybody can read.
            if (Package.Contains(DataFormats.Text) || OleDataBridge.TextOf(Package) != null)
                formats.Add(Describe((short)Win32Interop.CF_UNICODETEXT, TYMED.TYMED_HGLOBAL));
            foreach (var (format, medium) in _stored)
            {
                if (!formats.Exists(f => f.cfFormat == format)) formats.Add(Describe(format, medium.tymed));
            }
            return [.. formats];
        }
    }

    /// <summary>Every REGISTERED format this package offers, paired with the id Windows gave its name: the standard
    /// HTML/RTF pair plus anything the source named itself. A live CLR object is stored under its type's full name and
    /// must NOT be offered to other processes - it is recognised by the value being neither bytes nor a promise, which
    /// also keeps a promise from being redeemed just to answer "what do you have?".</summary>
    private IEnumerable<(string Name, short Id)> CustomFormats()
    {
        foreach (var name in Package.GetFormats())
        {
            if (name is DataFormats.Text or DataFormats.Files) continue;   // rendered as CF_UNICODETEXT / CF_HDROP above
            // A picture is offered under the format its BYTES actually are - a PNG as PNG, a GIF as GIF - so it goes out
            // verbatim. Never re-encoded to fit a name: that is measured in seconds (an animated GIF is a hundred
            // megapixels of frames), which a drag cannot pay. Encodings the clipboard has no name for still travel, as
            // CF_DIB or as a file. Answering this needs the bytes, so a promised picture is redeemed here; the costly
            // parts (the DIB rendering, the file write) stay deferred.
            if (name == DataFormats.Image)
            {
                if (Package.Get(name) is byte[] picture && OleDataBridge.PictureFormat(picture) is var own and not 0)
                {
                    yield return (name, unchecked((short)own));
                }
                continue;
            }

            var crosses = name is DataFormats.Html or DataFormats.Rtf
                          || Package.IsDeferred(name)
                          || Package.Get(name) is byte[];
            if (!crosses) continue;
            var id = OleDataBridge.RegisteredFormat(name);
            if (id != 0) yield return (name, unchecked((short)id));
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

    // Where a deferred format is finally paid for: the target asked, so Get() redeems the promise. Nothing before this
    // point reads the value - which is the whole point of advertising a heavy payload without producing it.
    private IntPtr Render(short format)
    {
        if (format == (short)Win32Interop.CF_HDROP && OleDataBridge.FilesOf(Package) is { Length: > 0 } files)
            return OleDataBridge.CreateHDropGlobal(files);
        if (format == (short)Win32Interop.CF_UNICODETEXT && OleDataBridge.TextOf(Package) is { } text)
            return OleDataBridge.CreateTextGlobal(text);
        if (format == (short)Win32Interop.CF_DIB && Package.Contains(DataFormats.Image))
            return OleDataBridge.CreateBytesGlobal(PictureAsDib);

        foreach (var (name, id) in CustomFormats())
        {
            if (id != format) continue;
            return Package.Get(name) switch
            {
                string value when name == DataFormats.Html => OleDataBridge.CreateHtmlGlobal(value),
                string value when name == DataFormats.Rtf => OleDataBridge.CreateAnsiGlobal(value),
                // Advertised under the format it already is (see CustomFormats), so it goes out untouched.
                byte[] picture when name == DataFormats.Image => OleDataBridge.CreateBytesGlobal(picture),
                byte[] bytes => OleDataBridge.CreateBytesGlobal(bytes),
                string value => OleDataBridge.CreateTextGlobal(value),   // a named format that turned out to be text
                _ => IntPtr.Zero,
            };
        }
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
