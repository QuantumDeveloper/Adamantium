using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// Every <c>{TemplateBinding X}</c> in a theme names a property of the control the template is FOR. When it names one
/// that does not exist, the binding resolves to a null property and the whole template BUILD throws
/// (TemplateBindingExpression.UpdateTarget -> GetValue(null)) - so the control keeps whatever template it had before.
/// <para>What that looks like on screen is the worst part: not a blank control, not an error, but the PREVIOUS theme's
/// look, indistinguishable from "the new style was never written". One such typo (SlidePanel has Header, not Title)
/// cost a session's worth of a whole theme appearing not to apply.</para>
/// <para>The check is TEXTUAL because a built ControlTemplate is a compiled builder with nothing left to inspect - the
/// binding is created while it runs, which is exactly the moment that throws. So the markup is read the way the
/// generator reads it: a stack of enclosing ControlTemplate TargetTypes, since a TemplateBinding always resolves
/// against the NEAREST one (a Popup.ChildTemplate binds to the Popup, not to the control outside it).</para>
/// </summary>
[TestFixture]
public class ThemeTemplateBindingTests
{
    private static readonly Regex TemplateOpen =
        new(@"<ControlTemplate\b[^>]*\bTargetType\s*=\s*""([^""]+)""", RegexOptions.Compiled);
    private static readonly Regex TemplateSelfClosing = new(@"<ControlTemplate\b[^>]*/>", RegexOptions.Compiled);
    private static readonly Regex TemplateClose = new(@"</ControlTemplate>", RegexOptions.Compiled);
    private static readonly Regex Binding = new(@"\{TemplateBinding\s+([A-Za-z0-9_.]+)\s*\}", RegexOptions.Compiled);

    [TestCase("EditorProTheme")]
    [TestCase("FluentTheme")]
    [TestCase("MacOsTheme")]
    public void EveryTemplateBindingNamesARealProperty(string themeFolder)
    {
        var root = ThemesRoot();
        var complaints = new List<string>();

        foreach (var file in Directory.EnumerateFiles(Path.Combine(root, themeFolder), "*.auml"))
        {
            var stack = new Stack<string>();
            var inComment = false;

            foreach (var (raw, number) in File.ReadLines(file).Select((l, i) => (l, i + 1)))
            {
                var line = StripComments(raw, ref inComment);
                if (line.Length == 0) continue;

                // Order matters on a line that both opens and closes; a self-closing tag opens nothing.
                var open = TemplateOpen.Match(line);
                if (open.Success && !TemplateSelfClosing.IsMatch(line)) stack.Push(open.Groups[1].Value);

                foreach (Match binding in Binding.Matches(line))
                {
                    var name = binding.Groups[1].Value;
                    if (name.Contains('.')) continue;             // attached properties resolve elsewhere
                    if (stack.Count == 0) continue;               // outside a template there is no parent to bind to

                    var owner = ResolveType(stack.Peek());
                    if (owner == null) continue;                  // an unknown type is a separate complaint, not this one

                    if (owner.GetProperty(name, BindingFlags.Public | BindingFlags.Instance) == null)
                        complaints.Add($"{Path.GetFileName(file)}:{number} - {stack.Peek()} has no '{name}'");
                }

                foreach (Match _ in TemplateClose.Matches(line))
                    if (stack.Count > 0) stack.Pop();
            }
        }

        Assert.That(complaints, Is.Empty,
            "template bindings that would throw while the template is built:" + Environment.NewLine +
            string.Join(Environment.NewLine, complaints));
    }

    /// <summary>Comments are not markup, and these files EXPLAIN their bindings - a comment quoting the very mistake it
    /// warns about would be reported as that mistake. Multi-line comments carry their state across lines, so the flag
    /// is threaded through rather than decided per line.</summary>
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

    /// <summary>Types are named bare in markup ("SlidePanel"), so they are found by simple name across the loaded
    /// control assemblies - the same way the markup compiler resolves them from the default namespace.</summary>
    private static Type ResolveType(string name)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic || !assembly.FullName!.StartsWith("Adamantium")) continue;

            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray()!; }

            var hit = types.FirstOrDefault(t => t.Name == name);
            if (hit != null) return hit;
        }

        return null;
    }

    /// <summary>The .auml files are not build output, so they are found in the tree rather than beside the assembly.</summary>
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
