namespace Adamantium.UI.Core.Input;

/// <summary>
/// The PLATFORM-NEUTRAL names of the standard payload formats an <see cref="IDataPackage"/> can carry across the OS
/// boundary (level 3 of docs/DRAG_DROP_PLAN.md). Each platform maps them onto its own identifiers - Windows
/// <c>CF_UNICODETEXT</c>/<c>CF_HDROP</c>, macOS <c>NSPasteboardType*</c>, Linux <c>text/plain</c>/<c>text/uri-list</c> -
/// so a view-model writes <c>data.Contains(DataFormats.Files)</c> once and it works everywhere.
/// A live CLR object keeps travelling under its own type name (the fast in-app path); these are only for what crosses to
/// another process.
/// </summary>
public static class DataFormats
{
    /// <summary>Unicode text. Value type: <see cref="string"/>.</summary>
    public const string Text = "Text";

    /// <summary>A file/folder drop. Value type: <c>string[]</c> of absolute paths.</summary>
    public const string Files = "Files";

    /// <summary>HTML markup. Value type: <see cref="string"/> - write the fragment you mean, the platform wraps it in
    /// whatever its own format demands (Windows <c>CF_HTML</c> with its byte-offset header, macOS
    /// <c>NSPasteboardTypeHTML</c>, Linux <c>text/html</c>).</summary>
    public const string Html = "Html";

    /// <summary>Rich Text Format. Value type: <see cref="string"/> holding the RTF source.</summary>
    public const string Rtf = "Rtf";

    /// <summary>
    /// A picture. Value type: <c>byte[]</c> holding it ENCODED - PNG, JPEG, GIF, BMP, TIFF, whatever the source had.
    /// Deliberately not "PNG only": re-encoding is by far the most expensive thing that can happen to a drag (an
    /// animated GIF is a hundred megapixels of frames), so the bytes travel exactly as they came and each side decodes
    /// by CONTENT rather than by the name it arrived under. Read it with an image loader, not by assuming a format.
    /// <para>The platform renders what a target asks for out of the same bytes: on Windows the picture is offered both
    /// as a <c>CF_DIB</c> (decoded, cheap) and under its own registered format, so PNG-takers and bitmap-takers are both
    /// served without a conversion in the middle.</para>
    /// </summary>
    public const string Image = "Image";

    // Anything else is a format of your own: name it what you like and store a byte[]. It crosses as a registered
    // platform format under that same name, so two applications that agree on the name interoperate without the engine
    // knowing anything about the payload.
}
