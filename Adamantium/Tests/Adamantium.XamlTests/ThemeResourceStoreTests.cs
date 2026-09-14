using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Resources;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// A theme answers from TWO stores under one flat key space: the palette dictionary, and the theme object's own
/// properties - the accent and focus brushes, which are runtime-mutable and therefore not palette entries. Each marker
/// reads exactly one of them: <c>{ThemeResource}</c> the properties, <c>{ObservableResource}</c> and
/// <c>{ResourceReference}</c> the dictionary.
/// <para>Asking the wrong store is SILENT. The setter receives null and the brush simply never appears - no exception,
/// no warning, and a miss is indistinguishable from the transient one a theme swap causes, which is why the live
/// expression deliberately keeps the last good value instead of complaining. One such line left the canvas's snap mark
/// unpainted in all three themes for as long as it existed, unnoticed because the page that showed it bound over the
/// top.</para>
/// <para>Textual, like the other theme tests here, because what a marker resolves to is decided while the template runs
/// and the miss leaves nothing behind to assert on afterwards.</para>
/// </summary>
[TestFixture]
public class ThemeResourceStoreTests
{
    private static readonly Regex FromDictionary =
        new(@"\{\s*(?:ObservableResource|ResourceReference)\s+([A-Za-z0-9_]+)\s*\}", RegexOptions.Compiled);

    private static readonly Regex FromThemeProperties =
        new(@"\{\s*ThemeResource\s+([A-Za-z0-9_]+)\s*\}", RegexOptions.Compiled);

    [TestCase("EditorProTheme")]
    [TestCase("FluentTheme")]
    [TestCase("MacOsTheme")]
    public void AKeyIsAskedFromTheStoreThatHoldsIt(string themeFolder)
    {
        var properties = ThemeProperties();
        Assert.That(properties, Does.Contain("AccentFillColorDefault"), "the theme's own brush properties");

        var folder = Path.Combine(ThemesRoot(), themeFolder);
        var complaints = new List<string>();

        foreach (var file in Directory.EnumerateFiles(folder, "*.auml"))
        {
            var inComment = false;
            foreach (var (raw, number) in File.ReadLines(file).Select((l, i) => (l, i + 1)))
            {
                var line = StripComments(raw, ref inComment);
                if (line.Length == 0) continue;

                foreach (Match use in FromDictionary.Matches(line))
                {
                    if (!properties.Contains(use.Groups[1].Value)) continue;

                    complaints.Add($"{Path.GetFileName(file)}:{number}: '{use.Groups[1].Value}' is a theme PROPERTY, " +
                                   "so it is not in the palette this asks - use {ThemeResource}. The setter gets null " +
                                   "and the brush never appears, silently");
                }

                foreach (Match use in FromThemeProperties.Matches(line))
                {
                    if (properties.Contains(use.Groups[1].Value)) continue;

                    complaints.Add($"{Path.GetFileName(file)}:{number}: '{use.Groups[1].Value}' is not a theme " +
                                   "property, so {ThemeResource} resolves it to nothing - use {ObservableResource}");
                }
            }
        }

        Assert.That(complaints, Is.Empty, string.Join(Environment.NewLine, complaints));
    }

    private static HashSet<string> ThemeProperties()
    {
        // Reflected rather than listed: {ThemeResource} resolves against the registered properties of the theme TYPE,
        // so a list written out here would go stale the moment one was added and quietly stop guarding it.
        return typeof(Theme)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field.FieldType == typeof(AdamantiumProperty))
            .Select(field => ((AdamantiumProperty)field.GetValue(null))?.Name)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string StripComments(string line, ref bool inComment)
    {
        var kept = new StringBuilder();
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
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("could not find Adamantium.UI.Themes above " + AppContext.BaseDirectory);
    }
}
