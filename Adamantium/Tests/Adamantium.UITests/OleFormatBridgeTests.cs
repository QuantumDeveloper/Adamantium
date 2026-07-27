using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Platforms.Windows;
using Adamantium.Win32;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// The Windows wire format for the payloads beyond text and files. CF_HTML is the one that actually bites: its header
/// carries five BYTE offsets into the block, so a single char-vs-byte slip makes every consumer paste garbage - and the
/// bug only shows up with non-ASCII content. So the header is parsed back the way Explorer, a browser or Word would.
/// </summary>
[TestFixture]
public class OleFormatBridgeTests
{
    private static byte[] ReadGlobal(IntPtr global)
    {
        var size = (int)Win32Interop.GlobalSize(global);
        var pointer = Win32Interop.GlobalLock(global);
        try
        {
            var bytes = new byte[size];
            Marshal.Copy(pointer, bytes, 0, size);
            return bytes;
        }
        finally
        {
            Win32Interop.GlobalUnlock(global);
            Win32Interop.GlobalFree(global);
        }
    }

    private static int Header(string text, string key)
    {
        var at = text.IndexOf(key, StringComparison.Ordinal);
        var end = text.IndexOf('\r', at);
        return int.Parse(text.AsSpan(at + key.Length, end - at - key.Length));
    }

    [TestCase("<b>plain ascii</b>")]
    [TestCase("<b>кириллица и emoji 🚀</b>")]
    public void HtmlGlobal_HeaderOffsetsPointAtTheFragmentInBYTES(string fragment)
    {
        var bytes = ReadGlobal(OleDataBridge.CreateHtmlGlobal(fragment));
        var text = Encoding.UTF8.GetString(bytes).TrimEnd('\0');

        var startFragment = Header(text, "StartFragment:");
        var endFragment = Header(text, "EndFragment:");
        var startHtml = Header(text, "StartHTML:");
        var endHtml = Header(text, "EndHTML:");

        Assert.That(Encoding.UTF8.GetString(bytes, startFragment, endFragment - startFragment), Is.EqualTo(fragment),
            "StartFragment/EndFragment must delimit exactly the markup that was handed in");
        Assert.That(Encoding.UTF8.GetString(bytes, startHtml, 6), Is.EqualTo("<html>"));
        Assert.That(endHtml, Is.LessThanOrEqualTo(bytes.Length));
        Assert.That(text, Does.StartWith("Version:0.9"));
    }

    [Test]
    public void AnsiGlobal_RoundTripsRtfSource()
    {
        const string rtf = @"{\rtf1\ansi Hello\par}";
        var bytes = ReadGlobal(OleDataBridge.CreateAnsiGlobal(rtf));

        Assert.That(Encoding.ASCII.GetString(bytes).TrimEnd('\0'), Is.EqualTo(rtf));
    }

    // A drag advertises what it CAN produce. A promised format must appear in that list without being produced - the
    // shell enumerates formats as soon as the drag starts, so redeeming here would defeat the whole mechanism.
    [Test]
    public void DataObject_AdvertisesADeferredFileList_WithoutProducingIt()
    {
        var produced = 0;
        var package = new DataPackage();
        package.SetDeferred(DataFormats.Files, () => { produced++; return new[] { @"C:\exported.txt" }; });

        using var data = new Win32DataObject(package);
        var formats = data.Formats;

        Assert.That(Array.Exists(formats, f => f.cfFormat == (short)Win32Interop.CF_HDROP), Is.True);
        Assert.That(produced, Is.Zero, "enumerating formats must not render the payload");
    }

    [Test]
    public void DataObject_ProducesTheFileList_OnlyWhenTheTargetAsks()
    {
        var produced = 0;
        var package = new DataPackage();
        package.SetDeferred(DataFormats.Files, () => { produced++; return new[] { @"C:\exported.txt" }; });

        using var data = new Win32DataObject(package);
        var request = new FORMATETC
        {
            cfFormat = (short)Win32Interop.CF_HDROP,
            dwAspect = DVASPECT.DVASPECT_CONTENT,
            lindex = -1,
            tymed = TYMED.TYMED_HGLOBAL
        };
        data.GetData(ref request, out var medium);

        Assert.That(produced, Is.EqualTo(1));
        Assert.That(medium.unionmember, Is.Not.EqualTo(IntPtr.Zero));
        Win32Interop.GlobalFree(medium.unionmember);
    }

    // A format nobody in the engine knows about: named by the source, carried opaquely, and reachable by any application
    // that registers the same name.
    [Test]
    public void DataObject_OffersACustomByteFormatUnderItsOwnName()
    {
        var package = new DataPackage();
        package.Set("application/x-adamantium-test", new byte[] { 1, 2, 3, 4 });

        using var data = new Win32DataObject(package);
        var id = unchecked((short)Win32Interop.RegisterClipboardFormat("application/x-adamantium-test"));

        Assert.That(Array.Exists(data.Formats, f => f.cfFormat == id), Is.True);

        var request = new FORMATETC { cfFormat = id, dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = -1, tymed = TYMED.TYMED_HGLOBAL };
        data.GetData(ref request, out var medium);
        Assert.That(ReadGlobal(medium.unionmember), Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
    }

    // A 2x2 PNG: red, green on the top row; blue, white on the bottom.
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAYAAABytg0kAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7D" +
        "AcdvqGQAAAATSURBVBhXY/jPwPAfDBkY/oMBAEnICfeW3k0uAAAAAElFTkSuQmCC");

    // CF_DIB is where a picture goes wrong silently: rows are stored BOTTOM-UP, channels are BGRA, and the header must
    // be the BI_BITFIELDS spelling with a mask table - byte for byte what Windows' own Clipboard.SetImage produces.
    // The simpler BI_RGB spelling is legal and gets rejected by picky consumers (Paint 3D), which is how this was found.
    [Test]
    public void DibGlobal_MatchesWhatWindowsItselfPublishes()
    {
        var bytes = ReadGlobal(OleDataBridge.CreateDibGlobal(TinyPng));

        Assert.That(BitConverter.ToInt32(bytes, 0), Is.EqualTo(40), "BITMAPINFOHEADER size");
        Assert.That(BitConverter.ToInt32(bytes, 4), Is.EqualTo(2), "width");
        Assert.That(BitConverter.ToInt32(bytes, 8), Is.EqualTo(2), "height, positive = bottom-up");
        Assert.That(BitConverter.ToInt16(bytes, 14), Is.EqualTo(32), "bit count");
        Assert.That(BitConverter.ToInt32(bytes, 16), Is.EqualTo(3), "BI_BITFIELDS");
        Assert.That(BitConverter.ToInt32(bytes, 20), Is.EqualTo(2 * 2 * 4), "biSizeImage");

        Assert.That(BitConverter.ToUInt32(bytes, 40), Is.EqualTo(0x00FF0000u), "red mask");
        Assert.That(BitConverter.ToUInt32(bytes, 44), Is.EqualTo(0x0000FF00u), "green mask");
        Assert.That(BitConverter.ToUInt32(bytes, 48), Is.EqualTo(0x000000FFu), "blue mask");

        // First stored row = the image's LAST row: blue then white, each as B,G,R,A.
        Assert.That(bytes[52..56], Is.EqualTo(new byte[] { 255, 0, 0, 255 }), "bottom-left pixel must be blue in BGRA");
        Assert.That(bytes[56..60], Is.EqualTo(new byte[] { 255, 255, 255, 255 }), "bottom-right pixel must be white");
        // Second stored row = the image's FIRST row: red then green.
        Assert.That(bytes[60..64], Is.EqualTo(new byte[] { 0, 0, 255, 255 }), "top-left pixel must be red in BGRA");
        Assert.That(bytes[64..68], Is.EqualTo(new byte[] { 0, 255, 0, 255 }), "top-right pixel must be green");
        Assert.That(bytes.Length, Is.EqualTo(68), "the exact size Windows produces for a 2x2 image");
    }

    [Test]
    public void DataObject_OffersAPictureAsBothPngAndDib()
    {
        var package = new DataPackage();
        package.Set(DataFormats.Image, TinyPng);

        using var data = new Win32DataObject(package);
        var png = unchecked((short)Win32Interop.RegisterClipboardFormat("PNG"));

        Assert.That(Array.Exists(data.Formats, f => f.cfFormat == png), Is.True, "modern applications take PNG");
        Assert.That(Array.Exists(data.Formats, f => f.cfFormat == (short)Win32Interop.CF_DIB), Is.True,
            "classic ones only understand CF_DIB");
    }

    // A picture is offered under the format its bytes actually are - never re-encoded to fit a name we picked. Encoding
    // is the most expensive thing in this subsystem (measured: 15 s to turn a 200-frame GIF into an APNG), so a BMP
    // payload must NOT be advertised as PNG; it travels as a bitmap instead.
    [Test]
    public void DataObject_DoesNotAdvertisePng_ForAPictureThatIsNotOne()
    {
        // A BMP of the same 2x2 picture, built from our own DIB rendering.
        var dib = ReadGlobal(OleDataBridge.CreateDibGlobal(TinyPng));
        var bmp = new byte[14 + dib.Length];
        var w = new BinaryWriter(new MemoryStream(bmp));
        w.Write((byte)'B'); w.Write((byte)'M'); w.Write(bmp.Length); w.Write(0); w.Write(14 + 40 + 12);
        Buffer.BlockCopy(dib, 0, bmp, 14, dib.Length);

        var package = new DataPackage();
        package.Set(DataFormats.Image, bmp);
        using var data = new Win32DataObject(package);
        var png = unchecked((short)Win32Interop.RegisterClipboardFormat("PNG"));

        Assert.That(Array.Exists(data.Formats, f => f.cfFormat == png), Is.False, "not a PNG - do not claim it is one");
        Assert.That(Array.Exists(data.Formats, f => f.cfFormat == (short)Win32Interop.CF_DIB), Is.True,
            "but it must still travel as a bitmap");
    }

    // Order is the contract with the target: it walks the list and takes the FIRST format it understands. Almost
    // everything understands text, so text offered early hijacks a picture drop - it arrives as its own caption.
    [Test]
    public void DataObject_OffersTextLast_SoAPictureWins()
    {
        var package = new DataPackage("A picture (drag into Paint)");   // the live payload doubles as text
        package.Set(DataFormats.Image, TinyPng);

        using var data = new Win32DataObject(package);
        var formats = Array.ConvertAll(data.Formats, f => (int)f.cfFormat);
        var dib = Array.IndexOf(formats, (int)Win32Interop.CF_DIB);
        var text = Array.IndexOf(formats, (int)Win32Interop.CF_UNICODETEXT);

        Assert.That(dib, Is.GreaterThanOrEqualTo(0));
        Assert.That(text, Is.GreaterThanOrEqualTo(0));
        Assert.That(dib, Is.LessThan(text), "the picture must be offered before the plain-text fallback");
    }

    // A live CLR payload is stored under its type name; offering that to other processes would advertise a format
    // nothing outside this process can read.
    [Test]
    public void DataObject_DoesNotOfferALiveObjectAsAnOsFormat()
    {
        var package = new DataPackage(new Uri("https://example.com"));

        using var data = new Win32DataObject(package);
        var id = unchecked((short)Win32Interop.RegisterClipboardFormat(typeof(Uri).FullName!));

        Assert.That(Array.Exists(data.Formats, f => f.cfFormat == id), Is.False);
    }
}
