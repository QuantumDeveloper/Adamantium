using System;
using Adamantium.Core.TypeParsing;

namespace Adamantium.UI.Core.Resources;

/// <summary>
/// Which VARIANT of a theme is in force - light, dark, or whatever else that theme declares. A variant changes only
/// what the theme's palette resolves to; the styles and templates are the theme's and are shared by all of its
/// variants, which is what makes switching one cheap.
/// </summary>
/// <remarks>
/// A VALUE with well-known instances, deliberately neither an enum nor a bare string.
/// <list type="bullet">
/// <item>An <b>enum</b> would fix the set of variants in the engine, so a theme could not add one of its own. That is
/// not hypothetical: a HUD theme is dark by nature and has no light variant at all, while it does want variants along
/// a different axis entirely - the signal colour (ally, alert, damage). An enum would force such a theme either to lie
/// ("Light" means amber) or to wait for an engine change.</item>
/// <item>A <b>bare string</b> could not be checked at all. This can: <see cref="Light"/>, <see cref="Dark"/> and
/// <see cref="System"/> are named, so a typo in one of them fails to parse rather than becoming a variant nobody
/// declared.</item>
/// </list>
/// <para>What CANNOT be checked when the file is compiled is whether the variant exists in the theme that ends up
/// being used - the theme is chosen at runtime, so the set of valid keys is not known statically. That is the price of
/// letting a subtree pick its own theme, and it is paid where it is cheapest: a variant a theme does not declare is a
/// visible failure the first time it runs, not a silent substitution.</para>
/// </remarks>
/// <remarks>The parser is named ON the type, not registered in <c>ParserRegistry</c>: that registry is for the core
/// primitives, and a type that carries its own parser cannot be added to markup and then found to have no way of being
/// written there - which is exactly what happened before this attribute existed here (codegen emitted
/// <c>TypeParser.Parse&lt;ThemeVariant&gt;("Dark")</c> quite happily, and it threw the first time a scope was built).</remarks>
[TypeParser(typeof(TypeParsers.ThemeVariantParser))]
public readonly struct ThemeVariant : IEquatable<ThemeVariant>
{
    /// <summary>The light variant, by convention. A theme is free not to have one.</summary>
    public static readonly ThemeVariant Light = new("Light");

    /// <summary>The dark variant, by convention. A theme is free not to have one.</summary>
    public static readonly ThemeVariant Dark = new("Dark");

    /// <summary>Follow the operating system's appearance, and keep following it as it changes.
    /// <para>This is a VALUE, not the absence of one. "Unset" already means "inherit from the nearest ancestor that
    /// says", so if following the system were expressed by leaving the property unset it could never be turned on
    /// INSIDE a subtree that names a variant - the property would simply inherit that variant. A preview pane that
    /// tracks the OS inside a window pinned to dark is an ordinary thing to want.</para>
    /// <para>Which of a theme's variants counts as light and which as dark is the THEME's answer, not this type's -
    /// see <c>ITheme</c>. A theme that gives no answer (a HUD with no light variant) resolves this to its default
    /// variant.</para></summary>
    public static readonly ThemeVariant System = new("System");

    /// <summary>A variant of a theme's own naming - <c>ThemeVariant.Named("Amber")</c>. No engine change needed.</summary>
    public static ThemeVariant Named(string key) => new(key);

    private ThemeVariant(string key) => Key = key;

    /// <summary>The variant's key, as the theme declares it. Null for the default-constructed value, which means
    /// "unspecified" - the state a property is in before anyone sets it.</summary>
    public string Key { get; }

    /// <summary>Nobody has said which variant this is. Distinct from <see cref="System"/>: this one defers to whoever
    /// is asked next (an ancestor, then the theme's default), that one stops and asks the OS.</summary>
    public bool IsUnspecified => string.IsNullOrEmpty(Key);

    /// <summary>Whether this is the follow-the-OS value.</summary>
    public bool FollowsSystem => Equals(System);

    /// <summary>Case-insensitive, so markup may write <c>dark</c> and code <c>ThemeVariant.Dark</c> and mean it.</summary>
    public bool Equals(ThemeVariant other) => string.Equals(Key, other.Key, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object obj) => obj is ThemeVariant other && Equals(other);

    public override int GetHashCode() => Key == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Key);

    public static bool operator ==(ThemeVariant left, ThemeVariant right) => left.Equals(right);

    public static bool operator !=(ThemeVariant left, ThemeVariant right) => !left.Equals(right);

    public override string ToString() => Key ?? "(unspecified)";

    /// <summary>Reads a variant written in markup. An empty string is <see cref="IsUnspecified"/>; anything else is
    /// taken at its word, because a theme may name its variants whatever it likes - the check that it EXISTS belongs
    /// to the theme that is asked for it, which is the only thing that knows.</summary>
    public static ThemeVariant Parse(string text) =>
        string.IsNullOrWhiteSpace(text) ? default : new ThemeVariant(text.Trim());
}
