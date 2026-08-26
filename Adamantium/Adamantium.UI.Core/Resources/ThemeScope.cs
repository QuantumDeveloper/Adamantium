using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Resources;

/// <summary>
/// Gives one subtree a theme of its own. <c>ThemeScope.Theme</c> is an attached property, so any element can become the
/// root of a scope without wrapping the markup in anything, and it CASCADES the way <c>DataContext</c> does - set it
/// once and everything under it wears that theme until some deeper element says otherwise.
/// <code>&lt;Border ThemeScope.Theme="{StaticResource FluentLight}"&gt; ... &lt;/Border&gt;</code>
/// </summary>
/// <remarks>
/// A scope REPLACES the application's theme for its subtree; it does not layer over it. A key the scope's theme does
/// not define is not found, exactly as it would not be found if the whole application ran on that theme - so what a
/// scope shows is what the application would show, and a subtree can never come out a mixture of two themes.
/// <para>Cascade by PROPERTY INHERITANCE, not by a walk of our own: the value system already answers "the nearest
/// ancestor that set this" for an <c>Inherits</c> property, and it does so incrementally as the tree changes. A second
/// mechanism for the same question would be one more thing to keep in step with re-parenting - and the walk would run
/// per element per re-theme, on a tree of tens of thousands of nodes.</para>
/// <para>WHY this exists beyond the obvious use (a settings page previewing a theme, a test stand switching one panel
/// instead of the application). "Which theme is this subtree wearing" was not expressible at all: there was one theme,
/// and elements merely remembered the values they had resolved from it. Anything out of the tree when the theme
/// changed - a parked keep-alive view - was therefore in an unanswerable state, because the only way to be re-themed
/// was to be reached by a walk that only visits the tree. With a scope the question has an owner and an answer.</para>
/// </remarks>
public static class ThemeScope
{
    /// <summary>The theme this element and everything under it wears. Unset means: whatever the nearest ancestor with a
    /// scope says, and failing that the application's current theme.</summary>
    public static readonly AdamantiumProperty ThemeProperty = AdamantiumProperty.RegisterAttached(
        "Theme", typeof(ITheme), typeof(AdamantiumComponent),
        new PropertyMetadata(null, PropertyMetadataOptions.Inherits, OnThemeChanged));

    public static ITheme GetTheme(AdamantiumComponent element) => element?.GetValue(ThemeProperty) as ITheme;

    public static void SetTheme(AdamantiumComponent element, ITheme value) => element?.SetValue(ThemeProperty, value);

    /// <summary>The theme <paramref name="component"/> should be styled and resolved against: its scope's, or the
    /// application's when no ancestor declares one. The single answer to "which theme applies here" - everything that
    /// used to read <c>ThemeManager.CurrentTheme</c> directly asks this instead, which is what makes a scope work at
    /// all rather than only where somebody remembered to check.</summary>
    public static ITheme For(IFundamentalUIComponent component)
    {
        if (component is AdamantiumComponent element && element.GetValue(ThemeProperty) is ITheme scoped)
        {
            return scoped;
        }

        return UIAppContext.Current?.ThemeManager?.CurrentTheme;
    }

    // A scope changing is a theme swap for its subtree, and is put right the same way one is: re-apply styles from the
    // root of the scope down. The inherited value has already reached the subtree by the time this runs (the value
    // system propagates before it calls back), so every element below now answers with the new theme.
    private static void OnThemeChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        (component as FundamentalUIComponent)?.InvalidateStyles();
    }
}
