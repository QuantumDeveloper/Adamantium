namespace Adamantium.UI.LanguageServer;

/// <summary>
/// The <c>x:</c> directives (xmlns <c>http://adamantium/ui/xaml/extensions</c>) and their
/// descriptions, shared by completion and hover.
/// </summary>
internal static class AumlXDirectives
{
    public const string Xmlns = "http://adamantium/ui/xaml/extensions";

    public static readonly IReadOnlyList<(string Name, string Detail)> All = new[]
    {
        ("Name", "Names this element so it is exposed as a field on the generated class."),
        ("Namespace", "Full type name for the generated class (the WPF x:Class analog)."),
        ("Key", "Key under which this entry is stored in a resource dictionary."),
        ("Type", "A reference to a CLR type."),
    };
}
