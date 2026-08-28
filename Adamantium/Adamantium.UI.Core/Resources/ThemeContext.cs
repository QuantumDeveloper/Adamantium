using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Resources;

/// <summary>
/// The theme in force AT a place in the tree. <c>ThemeContext.Theme</c> and <c>ThemeContext.Variant</c> are attached
/// properties, so any element can become the root of a scope without the markup being wrapped in anything, and both
/// CASCADE the way <c>DataContext</c> does - set once, and everything below is under them until some deeper element
/// says otherwise.
/// <code>&lt;Border ThemeContext.Theme="{StaticResource Fluent}" ThemeContext.Variant="Dark"&gt; … &lt;/Border&gt;</code>
/// </summary>
/// <remarks>
/// Named to continue <see cref="ResourceContext"/>, which is the same shape for the same kind of question - a static
/// class of attached properties that bind something to a subtree. A reader who has met <c>ResourceContext.Scope</c>
/// already knows what <c>ThemeContext.Variant</c> is. (<see cref="IThemeEngine"/>, which APPLIES styles, was renamed
/// out of the way so the two could not be confused.)
/// <para>A scope REPLACES the application's theme for its subtree rather than layering over it: a key the scope's
/// theme does not define is not found, exactly as it would not be found if the whole application ran on that theme.
/// A subtree can therefore never come out a mixture of two themes.</para>
/// <para>Cascade by PROPERTY INHERITANCE, not by a walk of our own: the value system already answers "the nearest
/// ancestor that set this" and does it incrementally as the tree changes. A second mechanism would be one more thing
/// to keep in step with re-parenting, and its walk would run per element per re-theme on trees of tens of thousands
/// of nodes.</para>
/// </remarks>
public static class ThemeContext
{
    /// <summary>The theme this element and everything under it wears. Unset: whatever the nearest ancestor that names
    /// one says, and failing that the application's current theme.</summary>
    public static readonly AdamantiumProperty ThemeProperty = AdamantiumProperty.RegisterAttached(
        "Theme", typeof(ITheme), typeof(AdamantiumComponent),
        new PropertyMetadata(null, PropertyMetadataOptions.Inherits, OnScopeChanged));

    /// <summary>Which VARIANT of that theme - <c>Light</c>, <c>Dark</c>, <c>System</c>, or whatever else the theme
    /// declares. Unset means "inherit"; <see cref="ThemeVariant.System"/> means "stop inheriting and follow the OS",
    /// which is a different thing and has to be, or it could never be switched on inside a subtree that names a
    /// variant of its own.</summary>
    public static readonly AdamantiumProperty VariantProperty = AdamantiumProperty.RegisterAttached(
        "Variant", typeof(ThemeVariant), typeof(AdamantiumComponent),
        new PropertyMetadata(default(ThemeVariant), PropertyMetadataOptions.Inherits, OnScopeChanged));

    public static ITheme GetTheme(AdamantiumComponent element) => element?.GetValue(ThemeProperty) as ITheme;

    public static void SetTheme(AdamantiumComponent element, ITheme value) => element?.SetValue(ThemeProperty, value);

    public static ThemeVariant GetVariant(AdamantiumComponent element) =>
        element?.GetValue(VariantProperty) is ThemeVariant variant ? variant : default;

    public static void SetVariant(AdamantiumComponent element, ThemeVariant value) =>
        element?.SetValue(VariantProperty, value);

    // Elements that explicitly asked to FOLLOW THE SYSTEM. When the OS appearance flips, the theme they resolve to
    // becomes a different sibling, so their styles - resolved from the old one - have to be re-applied. Only these:
    // everything else is unaffected, and re-styling whole windows for a scope nobody declared would be the expensive
    // cascade again. Held weakly, so asking to follow the system never keeps an element alive.
    private static readonly System.Collections.Generic.List<System.WeakReference<FundamentalUIComponent>> Followers = new();

    static ThemeContext()
    {
        SystemAppearance.Changed += (_, _) => RestyleSystemFollowers();
    }

    private static void RestyleSystemFollowers()
    {
        lock (Followers)
        {
            for (var i = Followers.Count - 1; i >= 0; i--)
            {
                if (Followers[i].TryGetTarget(out var element)) element.InvalidateStyles();
                else Followers.RemoveAt(i);
            }
        }
    }

    /// <summary>The theme <paramref name="component"/> should be styled and resolved against - the single answer to
    /// "which theme applies here". Everything that used to read <c>ThemeManager.CurrentTheme</c> asks this instead;
    /// that is what makes a scope work everywhere rather than only where somebody remembered to check.
    /// <para>The variant is part of the answer, not a separate question: a variant this theme is not currently showing
    /// resolves to the sibling that is (see <see cref="Theme.SiblingForVariant"/>), because one palette cannot hold
    /// two variants at once and a preview pane beside the thing it previews needs exactly that.</para></summary>
    // How many elements have ever been given a scope. Nearly every application has NONE, and this is asked on every
    // single resource lookup - two reads of an INHERITED property per ask, on a tree of tens of thousands of nodes,
    // during a startup that resolves a resource for practically every element. Measured as a ten-second freeze before
    // anything appeared. When nobody has declared a scope there is nothing to resolve, and one integer says so.
    private static int _scopeCount;

    /// <summary>Which element's scope answers for <paramref name="component"/>. Normally itself - but a TEMPLATE PART
    /// belongs to the control it was built for, and its own inheritance chain need not reach the scope the control sits
    /// in: parts are wired as visual children, and only the template ROOT is given an inheritance parent. A part that
    /// declares nothing of its own therefore asks its templated parent, which is in the ordinary tree and does see the
    /// scope. Measured on the stand: the CheckBox resolved Light and its own PART_ContentPresenter resolved the
    /// application's Dark, so the label came out white on a light panel.</summary>
    private static AdamantiumComponent ScopeAnchor(IFundamentalUIComponent component)
    {
        var element = component as AdamantiumComponent;

        for (var i = 0; i < 8 && element != null; i++)
        {
            if (element.GetValue(ThemeProperty) != null) return element;
            if (GetVariant(element) is { IsUnspecified: false }) return element;
            if ((element as IFundamentalUIComponent)?.TemplatedParent is not AdamantiumComponent host) break;

            element = host;
        }

        return component as AdamantiumComponent;
    }

    public static ITheme For(IFundamentalUIComponent component)
    {
        if (_scopeCount == 0) return UIAppContext.Current?.ThemeManager?.CurrentTheme;

        var element = ScopeAnchor(component);
        var theme = (element?.GetValue(ThemeProperty) as ITheme)
                    ?? UIAppContext.Current?.ThemeManager?.CurrentTheme;

        if (theme is not Theme concrete) return theme;

        var variant = GetVariant(element);
        if (variant.FollowsSystem) variant = concrete.ResolveSystemVariant(SystemAppearance.PrefersDark);

        return variant.IsUnspecified ? concrete : concrete.SiblingForVariant(variant);
    }

    // A scope changing is a theme swap for its subtree, and is put right the same way one is: re-apply styles from the
    // root of the scope down. The inherited value has already reached the subtree by the time this runs - the value
    // system propagates before it calls back - so everything below now answers with the new theme.
    private static void OnScopeChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not FundamentalUIComponent element) return;

        // A scope now exists, so the fast path in For() has to stop being taken. Counted rather than a bool because the
        // count only ever grows - a scope removed is still a tree that once had one, and getting that wrong would mean
        // silently answering with the application's theme for a subtree that has its own.
        if (e.NewValue != null) System.Threading.Interlocked.Increment(ref _scopeCount);

        // An element that asks to follow the OS has to be found again when the OS changes its mind, and the only
        // moment it can be recorded is the one where it says so.
        if (e.NewValue is ThemeVariant { FollowsSystem: true })
        {
            lock (Followers) Followers.Add(new System.WeakReference<FundamentalUIComponent>(element));
        }

        element.InvalidateStyles();

        // ...and the references written straight onto attributes, which no re-theme reaches: they were resolved once,
        // when the element attached, so without this a scope could be SET before anything was shown but never SWITCHED.
        if (element is IUIComponent visual) ResourceResolver.ReResolveSubtree(visual);

        // ...and the LIVE ones, which re-resolve on this event and on nothing else. A scope switch changes which theme
        // answers a key inside the subtree, which is a change of the resource set by any reading - it just never said
        // so, so an {ObservableResource} inside a scope went on showing the theme the scope started under. Announced
        // application-wide rather than per subtree because a re-resolve is a lookup: an expression outside this scope
        // asks and gets the same answer it had. A scope switch is an explicit, rare act.
        UIAppContext.Current?.ResourceManager?.NotifyResourcesChanged();

        // ...and AGAIN when the scope's subtree actually enters the tree. A scope is declared in markup, so it is set
        // while the elements under it are still being built: a template part that resolves a key at that moment walks an
        // ancestor chain that does not reach the scope yet, gets the application's theme, and is never asked again.
        // Measured on the stand - the presenter's chain ended two levels short of the pane, so a checkbox label stayed
        // white on a light panel while the plain text beside it was black.
        if (element is IUIComponent tracked) HookScopeAttach(tracked);
    }

    private static void HookScopeAttach(IUIComponent element)
    {
        element.AttachedToVisualTreeEvent -= OnScopeAttached;
        element.AttachedToVisualTreeEvent += OnScopeAttached;
    }

    private static void OnScopeAttached(object sender, VisualTreeAttachmentEventArgs e) =>
        UIAppContext.Current?.ResourceManager?.NotifyResourcesChanged();
}
