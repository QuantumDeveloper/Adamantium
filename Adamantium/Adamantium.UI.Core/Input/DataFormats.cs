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
}
