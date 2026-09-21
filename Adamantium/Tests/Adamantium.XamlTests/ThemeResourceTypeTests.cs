using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// A <c>{ResourceReference}</c> hands a property whatever the metrics filed under that key - and nothing checks that
/// the two are the same KIND of thing. Putting a <c>Double</c> gap into a <c>Padding</c> is not a wrong number, it is a
/// wrong type, and the whole template it sits in stops working: a collapsed ribbon group opened onto nothing at all,
/// and the designer's selection and hover frames drew no border.
/// <para>Which is worse than it sounds, because the theme still LOADS: nothing is missing, nothing logs, and the
/// control simply behaves as though the author had never written that line. So the check is textual and blunt - for
/// every <c>Attribute="{ResourceReference Key}"</c>, does the key's declared type fit the property's?</para>
/// </summary>
[TestFixture]
public class ThemeResourceTypeTests
{
    private static readonly Regex Declaration =
        new(@"<(sys:)?(?<type>\w+)\s+x:Key=""(?<key>[^""]+)""", RegexOptions.Compiled);
    private static readonly Regex Usage =
        new(@"(?<prop>[A-Za-z][A-Za-z0-9]*)\s*=\s*""\{ResourceReference\s+(?<key>[A-Za-z0-9_]+)\s*\}""", RegexOptions.Compiled);

    // What a slot will and will not take. Only the pairs worth policing - the ones where markup reads naturally and the
    // types do not meet.
    private static readonly Dictionary<string, string[]> Expected = new()
    {
        ["Padding"] = ["Thickness"],
        ["Margin"] = ["Thickness"],
        ["BorderThickness"] = ["Thickness"],
        ["CornerRadius"] = ["CornerRadius"],
        ["Width"] = ["Double"],
        ["Height"] = ["Double"],
        ["MinWidth"] = ["Double"],
        ["MinHeight"] = ["Double"],
        ["MaxWidth"] = ["Double"],
        ["MaxHeight"] = ["Double"],
        ["FontSize"] = ["Double"],
        ["StrokeThickness"] = ["Double"],
    };

    [TestCase("EditorProTheme")]
    [TestCase("FluentTheme")]
    public void EveryResourceReferenceFitsTheSlotItIsPutIn(string themeFolder)
    {
        var root = ThemesRoot();

        // Every key this theme declares, and what it was declared AS.
        var declared = new Dictionary<string, string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, themeFolder), "*.auml"))
            foreach (Match match in Declaration.Matches(File.ReadAllText(file)))
                declared[match.Groups["key"].Value] = match.Groups["type"].Value;

        var complaints = new List<string>();

        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, themeFolder), "*.auml"))
        {
            var inComment = false;
            foreach (var (raw, number) in File.ReadLines(file).Select((l, i) => (l, i + 1)))
            {
                var line = StripComments(raw, ref inComment);
                if (line.Length == 0) continue;

                foreach (Match use in Usage.Matches(line))
                {
                    var property = use.Groups["prop"].Value;
                    var key = use.Groups["key"].Value;

                    if (!Expected.TryGetValue(property, out var accepted)) continue;
                    if (!declared.TryGetValue(key, out var type)) continue;   // a key from elsewhere; not this check
                    if (accepted.Contains(type)) continue;

                    complaints.Add($"{Path.GetFileName(file)}:{number} - {property} was given '{key}', declared as {type}");
                }
            }
        }

        Assert.That(complaints, Is.Empty,
            "resources put into slots of another type - the template silently stops working:" + Environment.NewLine +
            string.Join(Environment.NewLine, complaints));
    }

    /// <summary>Comments are not markup, and these files explain their own metrics by name.</summary>
    private static string StripComments(string line, ref bool inComment)
    {
        var kept = new System.Text.StringBuilder();
        var i = 0;

        while (i < line.Length)
        {
            if (inComment)
            {
                var end = line.IndexOf("-->", i, StringComparison.Ordinal);
                if (end < 0) break;
                inComment = false;
                i = end + 3;
                continue;
            }

            var start = line.IndexOf("<!--", i, StringComparison.Ordinal);
            if (start < 0) { kept.Append(line, i, line.Length - i); break; }

            kept.Append(line, i, start - i);
            inComment = true;
            i = start + 4;
        }

        return kept.ToString();
    }

    private static string ThemesRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Adamantium.UI.Themes");
            if (Directory.Exists(Path.Combine(candidate, "FluentTheme"))) return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("could not find Adamantium.UI.Themes above " + AppContext.BaseDirectory);
    }
}
