namespace Adamantium.UI.LanguageServer;

public enum AumlCompletionKind { None, ElementName, AttributeName, AttributeValue, MarkupExtensionName, MarkupExtensionArg }

/// <summary>What the caret is positioned to complete, plus the surrounding element/attribute. For a markup
/// extension (<c>{Name arg}</c>) <see cref="MarkupExtension"/> holds the extension name when completing an argument.</summary>
public sealed record AumlCompletionContext(
    AumlCompletionKind Kind,
    string Prefix,
    string? ElementName = null,
    string? AttributeName = null,
    string? MarkupExtension = null);

/// <summary>
/// Lenient, caret-based context detector for AUML completion. Works on partial/malformed
/// buffers (mid-edit) by scanning around the caret instead of parsing the whole document
/// (the strict <c>AumlParser</c> is XDocument-based and throws on incomplete XML).
/// </summary>
public static class AumlCaretContext
{
    public static AumlCompletionContext Detect(string text, int offset)
    {
        if (string.IsNullOrEmpty(text) || offset <= 0)
            return new(AumlCompletionKind.None, "");

        offset = Math.Min(offset, text.Length);
        int lastLt = text.LastIndexOf('<', offset - 1);
        int lastGt = text.LastIndexOf('>', offset - 1);

        // Inside an (unclosed) tag if the nearest '<' is closer than the nearest '>'.
        if (lastLt > lastGt)
            return DetectInsideTag(text.Substring(lastLt, offset - lastLt));

        // In element content / free text: nothing to complete until a '<' is typed.
        return new(AumlCompletionKind.None, "");
    }

    private static AumlCompletionContext DetectInsideTag(string tag)
    {
        // tag begins with '<' (possibly "</" for a closing tag — still complete element names).
        int i = 1;
        if (i < tag.Length && tag[i] == '/') i++;
        int nameStart = i;
        while (i < tag.Length && IsNameChar(tag[i])) i++;
        string elementName = tag.Substring(nameStart, i - nameStart);

        // Still typing the element name (no whitespace after it yet).
        if (i >= tag.Length)
            return new(AumlCompletionKind.ElementName, elementName);

        // Attribute region: an odd number of quotes after the name means we are inside a value.
        bool insideValue = tag.Substring(i).Count(c => c == '"') % 2 == 1;
        if (insideValue)
        {
            int openQuote = tag.LastIndexOf('"');
            string value = tag.Substring(openQuote + 1);
            var attrName = AttributeNameBeforeQuote(tag, openQuote);

            // A markup extension: "{Name arg}". Up to the first space is the extension name; the rest are its args.
            if (value.StartsWith("{"))
                return DetectMarkupExtension(value, elementName, attrName);

            return new(AumlCompletionKind.AttributeValue, value, elementName, attrName);
        }

        // Attribute name: prefix is the current token after the last whitespace.
        int tokenStart = tag.Length;
        while (tokenStart > 0 && !char.IsWhiteSpace(tag[tokenStart - 1])) tokenStart--;
        string token = tag.Substring(tokenStart);
        // A token already containing '=' or '"' means we are between attributes — offer all.
        string namePrefix = token.Contains('=') || token.Contains('"') ? "" : token;
        return new(AumlCompletionKind.AttributeName, namePrefix, elementName);
    }

    private static AumlCompletionContext DetectMarkupExtension(string value, string? elementName, string? attrName)
    {
        var body = value.Substring(1);   // drop the leading '{'
        int space = body.IndexOf(' ');
        if (space < 0)
            return new(AumlCompletionKind.MarkupExtensionName, body.Trim(), elementName, attrName);

        var extName = body.Substring(0, space).Trim();
        var argPart = body.Substring(space + 1);
        // Current argument token: text after the last separator (space / comma) up to the caret.
        int tok = argPart.Length;
        while (tok > 0 && argPart[tok - 1] is not (' ' or ',')) tok--;
        return new(AumlCompletionKind.MarkupExtensionArg, argPart.Substring(tok), elementName, attrName, extName);
    }

    private static string? AttributeNameBeforeQuote(string tag, int openQuote)
    {
        int j = openQuote - 1;
        while (j >= 0 && char.IsWhiteSpace(tag[j])) j--;   // skip whitespace before the quote
        if (j < 0 || tag[j] != '=') return null;
        j--;
        while (j >= 0 && char.IsWhiteSpace(tag[j])) j--;   // skip whitespace before '='
        int end = j + 1;
        while (j >= 0 && IsNameChar(tag[j])) j--;
        return j + 1 < end ? tag.Substring(j + 1, end - (j + 1)) : null;
    }

    private static bool IsNameChar(char c) =>
        char.IsLetterOrDigit(c) || c is '_' or '-' or '.' or ':';
}
