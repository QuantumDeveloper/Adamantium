using System.IO;
using Adamantium.Imaging;

namespace Adamantium.UI.Input;

/// <summary>
/// What a dragged picture has to become before it leaves the application. The neutral payload
/// (<c>DataFormats.Image</c>) is PNG, but a source is free to hand over whatever it had on disk - a JPEG, a GIF, a TGA -
/// so this is the one place that answers "what IS this?" and "make it something the other side can read".
/// <para>
/// Platform-neutral on purpose: the same two questions come up in the OLE bridge (the format advertised as PNG must
/// really be PNG) and in the file fallback (a file nobody can open is not a fallback), and macOS will ask them too.
/// </para>
/// </summary>
internal static class DragPicture
{
    /// <summary>The encodings the world at large opens without a second thought. Anything outside this list is converted
    /// rather than handed over - a .tga or a .dds saved to disk is a file most people cannot use.</summary>
    public static bool IsWidelyReadable(byte[] picture) => Extension(picture) != null;

    /// <summary>The file extension the bytes deserve, or null when nothing common matches (a TGA, a DDS, an ICO...).</summary>
    public static string Extension(byte[] picture) => picture switch
    {
        [0x89, (byte)'P', (byte)'N', (byte)'G', ..] => ".png",
        [0xFF, 0xD8, ..] => ".jpg",
        [(byte)'G', (byte)'I', (byte)'F', ..] => ".gif",
        [(byte)'B', (byte)'M', ..] => ".bmp",
        [0x49, 0x49, 0x2A, 0x00, ..] or [0x4D, 0x4D, 0x00, 0x2A, ..] => ".tif",
        _ => null,
    };

    /// <summary>Is this already the neutral encoding? Asked before offering anything as PNG, because MAKING one is the
    /// single most expensive thing in this whole subsystem - see <see cref="Convert"/>.</summary>
    public static bool IsPng(byte[] picture) => picture is [0x89, (byte)'P', (byte)'N', (byte)'G', ..];

    /// <summary>
    /// Re-encode a picture into <paramref name="format"/>. MEASURED COST, and the reason this is never on a drag's hot
    /// path: decoding anything we support takes single-digit to low-hundreds of milliseconds, but our PNG encoder needs
    /// ~17 SECONDS for a 960x540 image (and throws outright on some 32-bit ones). BMP costs 1-150 ms for the same
    /// pictures because it barely encodes at all - which is why the file fallback prefers it and why a picture that is
    /// not already PNG is simply not advertised as PNG.
    /// </summary>
    public static byte[] Convert(byte[] encoded, ImageFileType format = ImageFileType.Bmp)
    {
        try
        {
            var bitmap = BitmapLoader.Load(new MemoryStream(encoded));
            if (bitmap == null) return null;
            var output = new MemoryStream();
            BitmapLoader.Save(bitmap, output, format);
            return output.Length > 0 ? output.ToArray() : null;
        }
        catch
        {
            return null;
        }
    }
}
