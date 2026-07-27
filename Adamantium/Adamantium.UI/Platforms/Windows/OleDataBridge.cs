using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Adamantium.Graphics.Core;
using Adamantium.Imaging;
using Adamantium.UI.Core.Input;
using Adamantium.Win32;
using Adamantium.Win32.Ole;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace Adamantium.UI.Platforms.Windows;

/// <summary>
/// Translates between the OLE wire format (<see cref="ComTypes.IDataObject"/>, HGLOBAL blocks, DROPEFFECT) and the
/// engine's platform-neutral <see cref="IDataPackage"/> / <see cref="DragDropEffects"/>. This is the ONLY place that
/// knows a "file list" is a <c>CF_HDROP</c> - a view-model just reads <c>DataFormats.Files</c>.
/// </summary>
internal static class OleDataBridge
{
    /// <summary>Read an incoming OLE payload into a managed package, EAGERLY: the source's data object is only
    /// guaranteed valid inside the callback that handed it over, and the drop itself is delivered to the view-model a
    /// frame later on the UI loop thread.
    /// <para>A drag that started in OUR app comes back as our own <see cref="Win32DataObject"/> (the CCW round-trips to
    /// the same managed instance), so the LIVE payload is handed straight through - no serialization, the in-app fast
    /// path survives even when the gesture is running through the OS.</para></summary>
    public static IDataPackage Read(ComTypes.IDataObject data)
    {
        if (data is Win32DataObject ours) return ours.Package;

        var package = new DataPackage();
        if (ReadFiles(data) is { Length: > 0 } files) package.Set(DataFormats.Files, files);
        if (ReadText(data) is { } text) package.Set(DataFormats.Text, text);
        if (ReadHtml(data) is { } html) package.Set(DataFormats.Html, html);
        if (ReadRegisteredString(data, RtfFormat, unicode: false) is { } rtf) package.Set(DataFormats.Rtf, rtf);
        if (ReadImage(data) is { Length: > 0 } image) package.Set(DataFormats.Image, image);
        return package;
    }

    // Registered (not standard) clipboard formats, resolved once. The NAMES are the interop contract - every Windows
    // application that speaks HTML or RTF on the clipboard registers these exact strings.
    private static readonly ushort HtmlFormat = Win32Interop.RegisterClipboardFormat("HTML Format");
    private static readonly ushort RtfFormat = Win32Interop.RegisterClipboardFormat("Rich Text Format");
    private static readonly ushort PngFormat = Win32Interop.RegisterClipboardFormat("PNG");

    /// <summary>The registered clipboard format id for one of our neutral names, or 0 when the name has no standard
    /// Windows counterpart - a custom format then registers under its own name, which is what makes two applications
    /// that agree on a name interoperate.</summary>
    public static ushort RegisteredFormat(string neutralName) => neutralName switch
    {
        DataFormats.Html => HtmlFormat,
        DataFormats.Rtf => RtfFormat,
        _ => Win32Interop.RegisterClipboardFormat(neutralName),
    };

    /// <summary>The clipboard format a picture should be OFFERED under - the one that matches what the bytes actually
    /// are. Naming it correctly is what lets the payload go out verbatim: a target that wants PNG takes the PNG, one
    /// that wants a GIF takes the GIF, and nothing is ever re-encoded to fit a name we chose in advance.
    /// Zero for an encoding the desktop has no clipboard name for (a TGA, a DDS) - those travel as CF_DIB or as a file.</summary>
    public static ushort PictureFormat(byte[] picture) => Input.DragPicture.Extension(picture) switch
    {
        ".png" => PngFormat,
        ".jpg" => Win32Interop.RegisterClipboardFormat("JFIF"),
        ".gif" => Win32Interop.RegisterClipboardFormat("GIF"),
        ".tif" => Win32Interop.RegisterClipboardFormat("TIFF"),
        ".bmp" => Win32Interop.RegisterClipboardFormat("image/bmp"),
        _ => 0,
    };

    /// <summary>
    /// A picture from whatever the source happened to offer, handed on as the neutral PNG. Sources differ wildly - a
    /// browser gives PNG, an older editor JPEG or TIFF, Paint only a raw CF_DIB - so everything but PNG is decoded and
    /// re-encoded rather than refused. PNG itself is passed through untouched: it is already the neutral form, and a
    /// needless decode/encode round trip would only cost time and (for a palette image) fidelity.
    /// </summary>
    private static byte[] ReadImage(ComTypes.IDataObject data)
    {
        // Verbatim, every one of them: the payload is "an encoded picture", not "a PNG", so nothing here decodes or
        // re-encodes anything. That matters - re-encoding an incoming animated GIF would cost seconds on a drop.
        foreach (var format in new[] { PngFormat }.Concat(
                     new[] { "JFIF", "JPEG", "image/jpeg", "GIF", "image/gif", "TIFF", "image/bmp" }
                         .Select(Win32Interop.RegisterClipboardFormat)))
        {
            if (ReadRegisteredBytes(data, format) is { Length: > 0 } encoded) return encoded;
        }

        // CF_DIB is a .bmp with its 14-byte file header sliced off - putting one back makes it a picture file again,
        // which is a header write rather than a conversion.
        if (ReadRegisteredBytes(data, (ushort)Win32Interop.CF_DIB) is { Length: > 40 } dib) return BmpFileFromDib(dib);
        return null;
    }

    /// <summary>A packed DIB wrapped back into a BMP file. The pixel offset has to account for what sits between the
    /// header and the pixels: the 12-byte mask table of a BI_BITFIELDS image, or a palette for the low bit depths.</summary>
    private static byte[] BmpFileFromDib(byte[] dib)
    {
        const int fileHeaderSize = 14;
        var headerSize = BitConverter.ToInt32(dib, 0);
        var bitCount = BitConverter.ToInt16(dib, 14);
        var compression = BitConverter.ToInt32(dib, 16);
        var paletteEntries = BitConverter.ToInt32(dib, 32);
        if (paletteEntries == 0 && bitCount <= 8) paletteEntries = 1 << bitCount;
        var extra = (compression == 3 ? 12 : 0) + paletteEntries * 4;

        var file = new byte[fileHeaderSize + dib.Length];
        var writer = new System.IO.BinaryWriter(new System.IO.MemoryStream(file));
        writer.Write((byte)'B'); writer.Write((byte)'M');
        writer.Write(file.Length);
        writer.Write(0);                                              // reserved
        writer.Write(fileHeaderSize + headerSize + extra);            // offset to the pixels
        Buffer.BlockCopy(dib, 0, file, fileHeaderSize, dib.Length);
        return file;
    }

    /// <summary>CF_HTML in, markup out. The wire format is UTF-8 bytes behind a header of BYTE offsets; what a consumer
    /// actually wants is the fragment those offsets delimit, so that is what is handed over (falling back to the whole
    /// document when a source left the fragment markers out).</summary>
    private static string ReadHtml(ComTypes.IDataObject data)
    {
        if (ReadRegisteredBytes(data, HtmlFormat) is not { Length: > 0 } bytes) return null;
        var text = System.Text.Encoding.UTF8.GetString(bytes).TrimEnd('\0');
        var start = OffsetHeader(text, "StartFragment:");
        var end = OffsetHeader(text, "EndFragment:");
        if (start < 0 || end < 0 || end <= start || end > bytes.Length) return text;
        // The offsets count BYTES, not chars - slice the byte array, then decode.
        return System.Text.Encoding.UTF8.GetString(bytes, start, end - start);
    }

    private static int OffsetHeader(string text, string key)
    {
        var at = text.IndexOf(key, StringComparison.Ordinal);
        if (at < 0) return -1;
        var end = text.IndexOf('\r', at);
        if (end < 0) end = text.IndexOf('\n', at);
        if (end < 0) return -1;
        return int.TryParse(text.AsSpan(at + key.Length, end - at - key.Length).Trim(), out var value) ? value : -1;
    }

    private static string ReadRegisteredString(ComTypes.IDataObject data, ushort format, bool unicode)
    {
        if (ReadRegisteredBytes(data, format) is not { Length: > 0 } bytes) return null;
        var encoding = unicode ? System.Text.Encoding.Unicode : System.Text.Encoding.ASCII;
        return encoding.GetString(bytes).TrimEnd('\0');
    }

    private static byte[] ReadRegisteredBytes(ComTypes.IDataObject data, ushort format)
    {
        if (format == 0 || !TryGetMedium(data, unchecked((short)format), out var medium)) return null;
        try
        {
            var size = (int)Win32Interop.GlobalSize(medium.unionmember);
            if (size <= 0) return null;
            var pointer = Win32Interop.GlobalLock(medium.unionmember);
            if (pointer == IntPtr.Zero) return null;
            try
            {
                var bytes = new byte[size];
                Marshal.Copy(pointer, bytes, 0, size);
                return bytes;
            }
            finally
            {
                Win32Interop.GlobalUnlock(medium.unionmember);
            }
        }
        finally
        {
            Win32Interop.ReleaseStgMedium(ref medium);
        }
    }

    private static string[] ReadFiles(ComTypes.IDataObject data)
    {
        if (!TryGetMedium(data, (short)Win32Interop.CF_HDROP, out var medium)) return null;
        try
        {
            // The HDROP is the HGLOBAL HANDLE itself - DragQueryFile locks it internally. Handing it a locked pointer
            // instead works only by accident (fixed memory) and reads garbage from a moveable block.
            var drop = medium.unionmember;
            var count = Win32Interop.DragQueryFile(drop, 0xFFFFFFFF, null, 0);
            var files = new string[count];
            var buffer = new char[260 + 1];
            for (uint i = 0; i < count; i++)
            {
                // Ask for the length first: a path can exceed MAX_PATH (extended-length paths, deep OneDrive trees).
                var length = Win32Interop.DragQueryFile(drop, i, null, 0);
                if (length + 1 > buffer.Length) buffer = new char[length + 1];
                var written = Win32Interop.DragQueryFile(drop, i, buffer, (uint)buffer.Length);
                files[i] = new string(buffer, 0, (int)written);
            }
            return files;
        }
        finally
        {
            Win32Interop.ReleaseStgMedium(ref medium);
        }
    }

    private static string ReadText(ComTypes.IDataObject data)
    {
        if (TryGetMedium(data, (short)Win32Interop.CF_UNICODETEXT, out var unicode))
        {
            try { return ReadGlobalString(unicode.unionmember, true); }
            finally { Win32Interop.ReleaseStgMedium(ref unicode); }
        }
        if (TryGetMedium(data, (short)Win32Interop.CF_TEXT, out var ansi))
        {
            try { return ReadGlobalString(ansi.unionmember, false); }
            finally { Win32Interop.ReleaseStgMedium(ref ansi); }
        }
        return null;
    }

    private static bool TryGetMedium(ComTypes.IDataObject data, short format, out STGMEDIUM medium)
    {
        medium = default;
        var request = new FORMATETC
        {
            cfFormat = format,
            dwAspect = DVASPECT.DVASPECT_CONTENT,
            lindex = -1,
            tymed = TYMED.TYMED_HGLOBAL
        };
        if (data.QueryGetData(ref request) != OleResult.Ok) return false;
        try
        {
            data.GetData(ref request, out medium);
        }
        catch (Exception)
        {
            return false;   // a source that advertises a format it can't actually render must not break the drop
        }
        return medium.unionmember != IntPtr.Zero;
    }

    private static string ReadGlobalString(IntPtr global, bool unicode)
    {
        var pointer = Win32Interop.GlobalLock(global);
        if (pointer == IntPtr.Zero) return null;
        try
        {
            return unicode ? Marshal.PtrToStringUni(pointer) : Marshal.PtrToStringAnsi(pointer);
        }
        finally
        {
            Win32Interop.GlobalUnlock(global);
        }
    }

    /// <summary>The payload's text, if it carries any: an explicit <c>DataFormats.Text</c> entry, else a bare string
    /// payload (so <c>DragData="{Binding SomeString}"</c> is draggable into Notepad with no extra ceremony), else a
    /// dragged multi-selection - one line per item, which is what an editor expects from a list of things.</summary>
    public static string TextOf(IDataPackage package)
    {
        if (package?.Get(DataFormats.Text) as string is { } explicitText) return explicitText;
        if (package?.Get<string>() is { } single) return single;
        if (package?.Get<IEnumerable<string>>() is { } many) return string.Join(Environment.NewLine, many);
        return null;
    }

    /// <summary>The payload's file paths, if it carries any (<c>DataFormats.Files</c> as a string[] or any string
    /// sequence).</summary>
    public static string[] FilesOf(IDataPackage package) => package?.Get(DataFormats.Files) switch
    {
        string[] paths => paths,
        IEnumerable<string> paths => [.. paths],
        string single => [single],
        _ => null,
    };

    /// <summary>An HGLOBAL holding a NUL-terminated UTF-16 string, as CF_UNICODETEXT expects. We publish text ONLY as
    /// Unicode (every app that matters takes it); the ANSI CF_TEXT is read on the way IN, never offered on the way out,
    /// so nothing has to guess a codepage.</summary>
    public static IntPtr CreateTextGlobal(string text)
    {
        return CreateGlobal(System.Text.Encoding.Unicode.GetBytes((text ?? string.Empty) + '\0'));
    }

    /// <summary>An HGLOBAL holding a CF_HDROP block: a <see cref="DROPFILES"/> header followed by the NUL-terminated
    /// paths and one closing NUL.</summary>
    public static IntPtr CreateHDropGlobal(IReadOnlyList<string> files)
    {
        var header = Marshal.SizeOf<DROPFILES>();
        var list = new System.Text.StringBuilder();
        foreach (var file in files)
        {
            list.Append(file);
            list.Append('\0');
        }
        list.Append('\0');
        var listBytes = System.Text.Encoding.Unicode.GetBytes(list.ToString());

        var bytes = new byte[header + listBytes.Length];
        var drop = new DROPFILES { pFiles = (uint)header, fWide = true };
        var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            Marshal.StructureToPtr(drop, pinned.AddrOfPinnedObject(), false);
        }
        finally
        {
            pinned.Free();
        }
        Buffer.BlockCopy(listBytes, 0, bytes, header, listBytes.Length);
        return CreateGlobal(bytes);
    }

    /// <summary>An HGLOBAL holding CF_HTML: the markup wrapped in the header Windows demands, whose five numbers are
    /// BYTE offsets into the block itself. They can only be filled in once the text is laid out, so the header is
    /// written with placeholders of the right width and then patched - which is also why the digits are zero-padded.</summary>
    public static IntPtr CreateHtmlGlobal(string html)
    {
        const string header = "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";
        const string prefix = "<html><body>\r\n<!--StartFragment-->";
        const string suffix = "<!--EndFragment-->\r\n</body></html>";

        var headerLength = System.Text.Encoding.UTF8.GetByteCount(string.Format(header, 0, 0, 0, 0));
        var startHtml = headerLength;
        var startFragment = startHtml + System.Text.Encoding.UTF8.GetByteCount(prefix);
        var endFragment = startFragment + System.Text.Encoding.UTF8.GetByteCount(html ?? string.Empty);
        var endHtml = endFragment + System.Text.Encoding.UTF8.GetByteCount(suffix);

        var document = string.Format(header, startHtml, endHtml, startFragment, endFragment) + prefix + html + suffix;
        var bytes = System.Text.Encoding.UTF8.GetBytes(document + '\0');
        return CreateGlobal(bytes);
    }

    /// <summary>An HGLOBAL holding a NUL-terminated ANSI string - what the RTF clipboard format expects (RTF is 7-bit
    /// by construction, so nothing is lost and no codepage has to be guessed).</summary>
    public static IntPtr CreateAnsiGlobal(string text)
    {
        return CreateGlobal(System.Text.Encoding.ASCII.GetBytes((text ?? string.Empty) + '\0'));
    }

    /// <summary>An HGLOBAL holding raw bytes - a custom format, carried opaquely.</summary>
    public static IntPtr CreateBytesGlobal(byte[] bytes) => bytes is { Length: > 0 } ? CreateGlobal(bytes) : IntPtr.Zero;

    /// <summary>A PNG rendered as CF_DIB: a BITMAPINFOHEADER followed straight by 32-bit BGRA pixels, BOTTOM-UP (which
    /// is what a positive biHeight means). For the applications that never learned to read PNG off the clipboard -
    /// Paint, older Office. Zero when the picture cannot be decoded, which simply drops that one format from the offer.
    /// <para>The alpha byte rides along but BI_RGB says nothing about it, so a consumer is free to ignore it and
    /// composite on white - the same deal every other toolkit gets from this format.</para></summary>
    public static IntPtr CreateDibGlobal(byte[] png) => CreateBytesGlobal(DibBytes(png));

    /// <summary>The DIB block itself, so a caller can build it ONCE and hand out copies: a target asks for the same
    /// format again and again during a drag, and decoding a picture per request is what turns a drop into a wait.</summary>
    public static byte[] DibBytes(byte[] png)
    {
        if (png is not { Length: > 0 }) return null;
        var bitmap = BitmapLoader.Load(new System.IO.MemoryStream(png));
        if (bitmap == null) return null;

        var pixels = bitmap.GetRawPixels(0);
        int width = (int)bitmap.Width, height = (int)bitmap.Height;
        var stride = width * 4;
        if (pixels == null || pixels.Length < stride * height) return null;

        // The decoder hands back RGBA; the DIB wants BGRA. Anything else we do not claim to understand.
        var swap = bitmap.PixelFormat == SurfaceFormat.R8G8B8A8.UNorm;
        if (!swap && bitmap.PixelFormat != SurfaceFormat.B8G8R8A8.UNorm) return null;

        // BI_BITFIELDS with an explicit mask table, NOT the simpler BI_RGB - because that is byte-for-byte what Windows
        // itself puts on the clipboard for a 32-bit image, and the picky consumers (Paint 3D among them) reject the
        // BI_RGB spelling outright. Verified against Clipboard.SetImage's own block.
        const int headerSize = 40;
        const int maskTableSize = 12;
        var pixelsAt = headerSize + maskTableSize;
        var bytes = new byte[pixelsAt + stride * height];
        var header = new System.IO.BinaryWriter(new System.IO.MemoryStream(bytes));
        header.Write(headerSize);            // biSize
        header.Write(width);                 // biWidth
        header.Write(height);                // biHeight, positive = rows stored bottom-up
        header.Write((short)1);              // biPlanes
        header.Write((short)32);             // biBitCount
        header.Write(3);                     // biCompression = BI_BITFIELDS
        header.Write(stride * height);       // biSizeImage
        header.Write(0); header.Write(0);    // biXPelsPerMeter, biYPelsPerMeter
        header.Write(0); header.Write(0);    // biClrUsed, biClrImportant
        header.Write(0x00FF0000);            // red mask
        header.Write(0x0000FF00);            // green mask
        header.Write(0x000000FF);            // blue mask

        for (var y = 0; y < height; y++)
        {
            var source = y * stride;
            var target = pixelsAt + (height - 1 - y) * stride;   // flip: the DIB's first row is the image's last
            if (!swap)
            {
                Buffer.BlockCopy(pixels, source, bytes, target, stride);
                continue;
            }
            for (var x = 0; x < stride; x += 4)
            {
                bytes[target + x] = pixels[source + x + 2];
                bytes[target + x + 1] = pixels[source + x + 1];
                bytes[target + x + 2] = pixels[source + x];
                bytes[target + x + 3] = pixels[source + x + 3];
            }
        }
        return bytes;
    }

    private static IntPtr CreateGlobal(byte[] bytes)
    {
        var global = Win32Interop.GlobalAlloc(Win32Interop.GMEM_MOVEABLE, (UIntPtr)bytes.Length);
        if (global == IntPtr.Zero) return IntPtr.Zero;
        var target = Win32Interop.GlobalLock(global);
        if (target == IntPtr.Zero)
        {
            Win32Interop.GlobalFree(global);
            return IntPtr.Zero;
        }
        try
        {
            Marshal.Copy(bytes, 0, target, bytes.Length);
        }
        finally
        {
            Win32Interop.GlobalUnlock(global);
        }
        return global;
    }

    /// <summary>A byte-for-byte copy of an HGLOBAL - what <c>GetData</c> must hand out, since the caller releases what
    /// it receives and we keep ours.</summary>
    public static IntPtr CopyGlobal(IntPtr source)
    {
        var size = (int)Win32Interop.GlobalSize(source);
        if (size <= 0) return IntPtr.Zero;
        var pointer = Win32Interop.GlobalLock(source);
        if (pointer == IntPtr.Zero) return IntPtr.Zero;
        try
        {
            var bytes = new byte[size];
            Marshal.Copy(pointer, bytes, 0, size);
            return CreateGlobal(bytes);
        }
        finally
        {
            Win32Interop.GlobalUnlock(source);
        }
    }

    public static DragDropEffects ToEffects(DropEffect effect)
    {
        var result = DragDropEffects.None;
        if ((effect & DropEffect.Copy) != 0) result |= DragDropEffects.Copy;
        if ((effect & DropEffect.Move) != 0) result |= DragDropEffects.Move;
        if ((effect & DropEffect.Link) != 0) result |= DragDropEffects.Link;
        return result;
    }

    public static DropEffect ToDropEffect(DragDropEffects effects)
    {
        var result = DropEffect.None;
        if ((effects & DragDropEffects.Copy) != 0) result |= DropEffect.Copy;
        if ((effects & DragDropEffects.Move) != 0) result |= DropEffect.Move;
        if ((effects & DragDropEffects.Link) != 0) result |= DropEffect.Link;
        return result;
    }

    /// <summary>The drag's modifier state as the engine's own flags. Only what a drag reads (Ctrl/Shift/Alt + the held
    /// button) - the OS gives no left/right distinction here, so both sides are reported.</summary>
    public static InputModifiers ToModifiers(OleKeyState keyState)
    {
        var modifiers = InputModifiers.None;
        if ((keyState & OleKeyState.Control) != 0) modifiers |= InputModifiers.LeftControl | InputModifiers.RightControl;
        if ((keyState & OleKeyState.Shift) != 0) modifiers |= InputModifiers.LeftShift | InputModifiers.RightShift;
        if ((keyState & OleKeyState.Alt) != 0) modifiers |= InputModifiers.LeftAlt | InputModifiers.RightAlt;
        if ((keyState & OleKeyState.LeftButton) != 0) modifiers |= InputModifiers.LeftMouseButton;
        if ((keyState & OleKeyState.RightButton) != 0) modifiers |= InputModifiers.RightMouseButton;
        if ((keyState & OleKeyState.MiddleButton) != 0) modifiers |= InputModifiers.MiddleMouseButton;
        return modifiers;
    }
}
