using System.Text.RegularExpressions;

namespace Adamantium.UI.LanguageServer;

/// <summary>
/// Scans xmlns declarations out of a raw AUML buffer (no full parse needed, so it works while
/// editing). Maps a prefix to its URI; the empty-string key is the default xmlns.
/// </summary>
public static class AumlNamespaces
{
    private static readonly Regex Declaration =
        new(@"xmlns(?::(?<prefix>[\w.\-]+))?\s*=\s*""(?<uri>[^""]*)""", RegexOptions.Compiled);

    public static IReadOnlyDictionary<string, string> Scan(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in Declaration.Matches(text))
            result[match.Groups["prefix"].Value] = match.Groups["uri"].Value;   // "" = default xmlns
        return result;
    }
}
