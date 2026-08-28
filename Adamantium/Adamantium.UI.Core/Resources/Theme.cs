using System.Collections.Specialized;
using Adamantium.Mathematics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Resources;

public class Theme : AdamantiumComponent, ITheme
{
    public Theme()
    {
        StyleSets = new StyleSetCollection();
        StyleIncludes = new StyleIncludeCollection();
        Variants = new ThemeVariantCollection();
        // Declared through ONE path whether it came from markup or from code: the collection is what markup fills, and
        // adding to it is what creates the palette brushes. A theme file and a hand-built theme must not differ here.
        Variants.CollectionChanged += (_, e) =>
        {
            if (e.NewItems == null) return;
            foreach (ThemeVariantDefinition definition in e.NewItems) AddVariant(definition);
        };
        ResourceManager = UIAppContext.Current.ResourceManager;
        MergedStyles = new StyleSet();
        // Any change to the merged style set invalidates the per-type match cache below (init, AddStyleSet, hot-reload).
        MergedStyles.Styles.CollectionChanged += (_, _) => _typeStyleCache.Clear();
        // Seed the theme's font so it's a real (non-null) property value from the start: a {ThemeResource FontFamily}
        // binding reads the raw GetValue, and a theme can override it (live) to restyle all text.
        FontFamily = SystemDefaultFontFamily;
    }

    public Theme(string name) : this()
    {
        Name = name;
    }

    public string Name { get; protected set; }

    // The ONE accent seed. Setting it derives the whole ramp below (Default/hover/pressed + the on-accent text colour),
    // so a theme - or a runtime accent swap - specifies a single colour and every accented control stays correct and
    // readable. This is the piece WinUI/Avalonia leave to fixed per-theme tokens (which break on a custom accent).
    public static readonly AdamantiumProperty AccentColorProperty = AdamantiumProperty.Register(
        nameof(AccentColor), typeof(Brush), typeof(Theme), new PropertyMetadata(null, OnAccentColorChanged));

    public Brush AccentColor
    {
        get => GetValue<Brush>(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    private static void OnAccentColorChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        if (a is not Theme theme) return;

        if (e.NewValue is SolidColorBrush seed)
        {
            theme.DeriveAccentPalette(seed.Color);
        }
        else if (e.NewValue is Brush brush)
        {
            // A non-solid accent (gradient/image) has no single colour to darken or measure for contrast. Use it flat
            // for all three fills (no hover/pressed ramp) and default the on-accent text to white - so a non-solid seed
            // degrades gracefully instead of leaving the ramp derived from a PREVIOUS solid seed. Themes use a solid seed.
            theme.AccentFillColorDefault = brush;
            theme.AccentFillColorSecondary = brush;
            theme.AccentFillColorTertiary = brush;
            theme.AccentForegroundColor = new SolidColorBrush(White);
        }
    }

    // How much darker the hover / pressed accents are than the seed (0..1, toward black). Theme-settable so the accent
    // ramp can be tuned per theme; defaults match Fluent's feel. Changing one re-derives the ramp from the current seed.
    public static readonly AdamantiumProperty AccentHoverDarkenProperty = AdamantiumProperty.Register(
        nameof(AccentHoverDarken), typeof(double), typeof(Theme), new PropertyMetadata(0.12, OnAccentRampChanged));

    public static readonly AdamantiumProperty AccentPressedDarkenProperty = AdamantiumProperty.Register(
        nameof(AccentPressedDarken), typeof(double), typeof(Theme), new PropertyMetadata(0.24, OnAccentRampChanged));

    /// <summary>Fraction (0..1, toward black) the hover accent (<see cref="AccentFillColorSecondary"/>) is darkened from
    /// the seed. Default 0.12.</summary>
    public double AccentHoverDarken
    {
        get => GetValue<double>(AccentHoverDarkenProperty);
        set => SetValue(AccentHoverDarkenProperty, value);
    }

    /// <summary>Fraction (0..1, toward black) the pressed accent (<see cref="AccentFillColorTertiary"/>) is darkened from
    /// the seed. Default 0.24.</summary>
    public double AccentPressedDarken
    {
        get => GetValue<double>(AccentPressedDarkenProperty);
        set => SetValue(AccentPressedDarkenProperty, value);
    }

    private static void OnAccentRampChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        // A ramp coefficient changed: re-derive from the current seed so hover/pressed pick up the new factor.
        if (a is Theme { AccentColor: SolidColorBrush seed } theme)
            theme.DeriveAccentPalette(seed.Color);
    }

    // One seed -> the ramp: hover/pressed a notch darker (by the AccentHover/PressedDarken factors), and the on-accent
    // TEXT chosen for contrast against the FILL (white on a dark accent, black on a light one) so a checked control reads
    // for ANY accent. Disabled/focus stay theme-authored (they're neutral, not accent-derived).
    private void DeriveAccentPalette(Color seed)
    {
        Recolour(AccentFillColorDefaultProperty, seed);
        Recolour(AccentFillColorSecondaryProperty, Color.Lerp(seed, Black, (float)AccentHoverDarken));   // hover
        Recolour(AccentFillColorTertiaryProperty, Color.Lerp(seed, Black, (float)AccentPressedDarken));  // pressed
        Recolour(AccentForegroundColorProperty, OnAccent(seed));
    }

    // Change the brush's COLOUR, not the theme's brush. The two look alike and cost nothing alike: a theme property that
    // changes IDENTITY has to be pushed onto every {ThemeResource} consumer, and a list realizes one container per row -
    // each of which reads the accent for its selected and hovered states. Measured on a 9 000-tile grid: ~18 000 property
    // writes per step of a colour drag, about a second of them, and the window dead for forty seconds while the steps
    // piled up. Layout and rendering were idle throughout; it was all property writes.
    //
    // Keeping the identity, nobody has to be told: every consumer already holds this brush, and the paint change travels
    // the path built for exactly that (Color is AffectsPaint), which repaints the units that actually paint with it - for
    // an accent, the selected row and the one under the cursor.
    private void Recolour(AdamantiumProperty property, Color color)
    {
        if (GetValue(property) is SolidColorBrush brush)
        {
            brush.Color = color;
            return;
        }

        // First time, or a theme that put something other than a solid brush there: there is no identity to keep yet.
        SetValue(property, new SolidColorBrush(color));
    }

    private static readonly Color Black = Color.FromRgba(0, 0, 0);
    private static readonly Color White = Color.FromRgba(255, 255, 255);

    // Perceptual luminance (Rec. 601): white text on a dark fill, black on a light one.
    private static Color OnAccent(Color c) => 0.299 * c.R + 0.587 * c.G + 0.114 * c.B < 140 ? White : Black;

    private static FontFamily _systemDefaultFontFamily;

    /// <summary>The per-platform system UI font - the single place the platform font choice lives, used as the default
    /// for any theme that doesn't pick its own.</summary>
    public static FontFamily SystemDefaultFontFamily => _systemDefaultFontFamily ??= new FontFamily(
        OperatingSystem.IsWindows() ? "Segoe UI" : OperatingSystem.IsMacOS() ? "Helvetica" : "DejaVu Sans");

    // The font is the theme's runtime-mutable identity, like the accent brushes above: it's an AdamantiumProperty so a
    // change raises PropertyChanged and every consumer (a {ThemeResource FontFamily} binding in a style) refreshes live -
    // no theme reload.
    public static readonly AdamantiumProperty FontFamilyProperty = AdamantiumProperty.Register(
        nameof(FontFamily), typeof(FontFamily), typeof(Theme), new PropertyMetadata(null));

    /// <summary>The theme's font for text - consume it in styles via <c>{ThemeResource FontFamily}</c> (descendants also
    /// inherit it through UIComponent.FontFamily). Unset falls back to <see cref="SystemDefaultFontFamily"/>; changing it
    /// at runtime refreshes every consumer live (it's an observable AdamantiumProperty).</summary>
    public FontFamily FontFamily
    {
        get => GetValue<FontFamily>(FontFamilyProperty) ?? SystemDefaultFontFamily;
        set => SetValue(FontFamilyProperty, value);
    }

    // Accent/focus brushes are the theme's runtime-mutable identity: change one (theme.AccentFillColorDefault = ...)
    // and every {ThemeResource} binding refreshes live - no theme reload. The static palette stays in the brush
    // dictionary. Plain AdamantiumProperties; the binding engine observes their change notifications.
    public static readonly AdamantiumProperty AccentFillColorDefaultProperty = AdamantiumProperty.Register(
        nameof(AccentFillColorDefault), typeof(Brush), typeof(Theme), new PropertyMetadata(null));

    public static readonly AdamantiumProperty AccentFillColorSecondaryProperty = AdamantiumProperty.Register(
        nameof(AccentFillColorSecondary), typeof(Brush), typeof(Theme), new PropertyMetadata(null));

    public static readonly AdamantiumProperty AccentFillColorTertiaryProperty = AdamantiumProperty.Register(
        nameof(AccentFillColorTertiary), typeof(Brush), typeof(Theme), new PropertyMetadata(null));

    public static readonly AdamantiumProperty AccentFillColorDisabledProperty = AdamantiumProperty.Register(
        nameof(AccentFillColorDisabled), typeof(Brush), typeof(Theme), new PropertyMetadata(null));

    public static readonly AdamantiumProperty AccentForegroundColorProperty = AdamantiumProperty.Register(
        nameof(AccentForegroundColor), typeof(Brush), typeof(Theme), new PropertyMetadata(null));

    public static readonly AdamantiumProperty FocusStrokeColorOuterProperty = AdamantiumProperty.Register(
        nameof(FocusStrokeColorOuter), typeof(Brush), typeof(Theme), new PropertyMetadata(null));

    public static readonly AdamantiumProperty FocusStrokeColorInnerProperty = AdamantiumProperty.Register(
        nameof(FocusStrokeColorInner), typeof(Brush), typeof(Theme), new PropertyMetadata(null));

    public Brush AccentFillColorDefault
    {
        get => GetValue<Brush>(AccentFillColorDefaultProperty);
        set => SetValue(AccentFillColorDefaultProperty, value);
    }

    public Brush AccentFillColorSecondary
    {
        get => GetValue<Brush>(AccentFillColorSecondaryProperty);
        set => SetValue(AccentFillColorSecondaryProperty, value);
    }

    public Brush AccentFillColorTertiary
    {
        get => GetValue<Brush>(AccentFillColorTertiaryProperty);
        set => SetValue(AccentFillColorTertiaryProperty, value);
    }

    public Brush AccentFillColorDisabled
    {
        get => GetValue<Brush>(AccentFillColorDisabledProperty);
        set => SetValue(AccentFillColorDisabledProperty, value);
    }

    public Brush AccentForegroundColor
    {
        get => GetValue<Brush>(AccentForegroundColorProperty);
        set => SetValue(AccentForegroundColorProperty, value);
    }

    public Brush FocusStrokeColorOuter
    {
        get => GetValue<Brush>(FocusStrokeColorOuterProperty);
        set => SetValue(FocusStrokeColorOuterProperty, value);
    }

    public Brush FocusStrokeColorInner
    {
        get => GetValue<Brush>(FocusStrokeColorInnerProperty);
        set => SetValue(FocusStrokeColorInnerProperty, value);
    }

    protected IResourceManager ResourceManager { get; }

    public StyleSet MergedStyles { get; }

    // Cache of the matched-and-ordered style set per runtime type, for components with no id and no classes (see below).
    // Concurrent because styling is no longer a one-thread affair: a subtree can be materialized off the loop thread
    // (deferred tab content) and a virtualizing panel rebinds its tiles across cores, so this memo is written from more
    // than one thread. The value is a pure function of the type, so two threads racing compute the same array.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Style[]> _typeStyleCache = new();

    public Style[] FindStylesForComponent(IFundamentalUIComponent component)
    {
        if (component == null) return [];

        var type = component.GetType();

        // Selector.Match is purely STRUCTURAL - type IS-A + id + classes, no instance state. So a component with no id and
        // no classes can be matched ONLY by type selectors, making its matched set a pure function of its runtime type:
        // cache it by type. This collapses the per-element theme scan from O(all theme styles) to O(1) on repeats, so
        // realizing N identical containers (e.g. 40 MenuItems) scans the styles once per DISTINCT type, not once per element.
        // Class/id-bearing components are rare; they take the full scan every time (correctness over their cold path).
        // IsNullOrEmpty, not == null: Id is registered with String.Empty as its default (an unset Id reads as ""), so a
        // null test made EVERY component uncacheable and the scan below ran per element instead of per type. Measured on
        // the Brushes tab: 450 ms of a 2.2 s build, 104 us per element, for a lookup that is meant to be a dictionary hit.
        var cacheable = string.IsNullOrEmpty(component.Id) && !component.HasClassNames;
        if (cacheable && _typeStyleCache.TryGetValue(type, out var cached)) return cached;

        // A styled TYPE is a BOUNDARY (DefaultStyleKey semantics): among the IS-A candidates, keep the type styles of only
        // the NEAREST styled ancestor (smallest inheritance distance). So a derived control does NOT inherit a base type's
        // implicit style - a CheckBox : ToggleButton gets the CheckBox style, not ToggleButton's - which makes matching
        // predictable and kills the accidental-inheritance leaks. But a subclass with NO style of its own (an AUML x:Class
        // MainWindow : Window) still falls back to its base's chrome, because the nearest styled ancestor IS the base.
        // A selector with no type facet (class/id only) is not type-bound and always applies. Cross-type sharing that a
        // control DOES want is explicit via Style.BasedOn. Ordering within the kept set is base-first + stable (document
        // order at equal specificity), so a class style still lands after the type style it refines.
        var matched = MergedStyles.Styles.Where(x => x.Selector.Match(component)).ToArray();
        var nearestDistance = matched
            .Where(x => x.Selector.Types.Count > 0)
            .Select(x => x.Selector.SpecificityDistance(type))
            .DefaultIfEmpty(int.MaxValue)
            .Min();
        var styles = matched
            .Where(x => x.Selector.Types.Count == 0 || x.Selector.SpecificityDistance(type) == nearestDistance)
            .OrderByDescending(x => x.Selector.SpecificityDistance(type))
            .ToArray();

        if (cacheable)
            _typeStyleCache[type] = styles;
        return styles;
    }

    // Expand a BasedOn selector into the base styles a deriving style pulls in - base-first, deduped, and recursive (a
    // base may itself be BasedOn another). `seen` starts with the deriving style so a base can't re-pull it (cycle guard).
    internal void CollectBasedOn(StyleSelector basedOn, List<Style> result, HashSet<Style> seen)
    {
        foreach (var type in basedOn.Types)
            foreach (var s in StylesForType(type))
                AddWithBases(s, result, seen);
    }

    private void AddWithBases(Style s, List<Style> result, HashSet<Style> seen)
    {
        if (!seen.Add(s)) return;
        if (s.BasedOn is { Types.Count: > 0 }) CollectBasedOn(s.BasedOn, result, seen);   // its own bases first
        result.Add(s);
    }

    // The PURE-TYPE styles for a type (no id/class/group facet) - the set an instance of exactly that type would match.
    // BasedOn pulls these in for a derived control that opts into the base look.
    private IEnumerable<Style> StylesForType(Type type) =>
        MergedStyles.Styles.Where(s =>
            s.Selector.Id == null && s.Selector.Classes.Count == 0 && s.Selector.ClassGroups.Count == 0
            && s.Selector.Types.Any(t => t == type));

    // ── Variants ──────────────────────────────────────────────────────────────────────────────────────────────────
    //
    // The PALETTE belongs to the theme: one brush per key, shared by every variant. A variant supplies that brush's
    // COLOUR, not a brush of its own. That inversion is the entire point - two dictionaries of separate brush objects
    // under the same keys would mean every element's Background receives a DIFFERENT OBJECT when the variant changes,
    // which is a property write per element (measured at ~18000 on a swap) plus a re-subscribe on every brush. Writing
    // a colour into a brush that is already there is O(palette keys) and touches no element at all.

    private readonly Dictionary<string, SolidColorBrush> _palette = new();
    private readonly Dictionary<ThemeVariant, ThemeVariantDefinition> _variants = new();

    /// <summary>What markup writes: <c>&lt;Theme.Variants&gt;…&lt;/Theme.Variants&gt;</c>. Adding to it declares the
    /// variant, so a theme file and a theme built in code go through exactly the same path.</summary>
    public ThemeVariantCollection Variants { get; }

    /// <summary>The theme's palette brushes by key - created once from the variants' colour tables and never replaced.
    /// Their IDENTITY is what makes a variant switch cheap.</summary>
    public IReadOnlyDictionary<string, SolidColorBrush> Palette => _palette;

    public IReadOnlyDictionary<ThemeVariant, ThemeVariantDefinition> VariantsByKey => _variants;

    public ThemeVariant CurrentVariant { get; private set; }

    /// <summary>The variant used when nothing else is said - the first one declared.</summary>
    public ThemeVariant DefaultVariant { get; private set; }

    public ThemeVariant SystemLightVariant { get; set; }

    public ThemeVariant SystemDarkVariant { get; set; }

    /// <summary>Declare a variant. The palette gains a brush for any colour key it has not seen yet, so the brushes
    /// exist before anything asks for them and never have to be swapped later.</summary>
    public void AddVariant(ThemeVariantDefinition variant)
    {
        if (variant == null || variant.Key.IsUnspecified) return;

        _variants[variant.Key] = variant;
        if (DefaultVariant.IsUnspecified) DefaultVariant = variant.Key;

        RegisterPaletteKeys(variant);

        // ...and keep registering. Markup adds the variant to the theme BEFORE filling in its colours - the loader
        // parents a child and then populates it - so a palette built once, here, would come out empty for every theme
        // read from a file while looking perfectly correct for every theme built in a test. The keys arrive when they
        // arrive; this listens rather than assuming an order.
        variant.Colors.CollectionChanged += (_, _) => RegisterPaletteKeys(variant);
    }

    // Keys served as a raw Color rather than as a brush - a gradient STOP takes a colour. Kept beside the brushes
    // rather than in a second collection on the variant: one palette, two ways of being asked for.
    private readonly Dictionary<string, Color> _rawColors = new();

    /// <summary>Palette entries a variant declares as colours rather than brushes.</summary>
    public IReadOnlyDictionary<string, Color> RawColors => _rawColors;

    private void RegisterPaletteKeys(ThemeVariantDefinition variant)
    {
        foreach (var entry in variant.Colors)
        {
            if (entry.Key == null) continue;

            if (entry.As == PaletteEntryKind.Color)
            {
                if (!_rawColors.ContainsKey(entry.Key)) _rawColors[entry.Key] = entry.Color;
                continue;
            }

            if (_palette.ContainsKey(entry.Key)) continue;

            // The brush is created with the colour of whichever variant is CURRENT, when that variant declares the key
            // - so a palette entry is never briefly the wrong colour on its way to being right.
            var colour = entry.Color;
            if (!CurrentVariant.IsUnspecified && _variants.TryGetValue(CurrentVariant, out var current)
                && current.Colors.TryGet(entry.Key, out var currentColour))
            {
                colour = currentColour;
            }

            _palette[entry.Key] = new SolidColorBrush(colour);
        }
    }

    /// <summary>Every variant of a theme must answer the SAME set of keys. A key one variant declares and another does
    /// not would leave the palette holding whatever the previous variant put there - so the subtree's appearance would
    /// depend on which variant it was switched FROM, which is not a thing anyone can reason about. Returns the keys
    /// that are missing somewhere, by variant; empty means the theme is consistent.</summary>
    public IReadOnlyList<string> ValidateVariants()
    {
        var problems = new List<string>();
        if (_variants.Count < 2) return problems;

        foreach (var variant in _variants.Values)
        {
            foreach (var key in _palette.Keys)
            {
                if (!variant.Colors.ContainsKey(key)) problems.Add($"{variant.Key}: no colour for '{key}'");
            }
        }

        return problems;
    }

    public bool ApplyVariant(ThemeVariant variant)
    {
        if (variant.FollowsSystem) return false;   // the caller resolves this one first - see ResolveSystemVariant
        if (variant.IsUnspecified) variant = DefaultVariant;
        if (variant.IsUnspecified || !_variants.TryGetValue(variant, out var definition)) return false;

        CurrentVariant = variant;

        // Colours first: writing into the brushes that already exist, so every element drawing with one keeps drawing
        // with the same object and simply repaints.
        foreach (var entry in definition.Colors)
        {
            if (entry.Key == null) continue;

            if (entry.As == PaletteEntryKind.Color)
            {
                _rawColors[entry.Key] = entry.Color;
                continue;
            }

            if (_palette.TryGetValue(entry.Key, out var brush)) brush.Color = entry.Color;
            else _palette[entry.Key] = new SolidColorBrush(entry.Color);
        }

        // ...then the theme's own properties, which is where {ThemeResource} looks. AccentColor derives the whole ramp
        // on assignment, so setting the seed is enough.
        foreach (var entry in definition.Values)
        {
            if (entry.Property == null) continue;
            var property = AdamantiumPropertyMap.FindRegistered(GetType(), entry.Property);
            if (property != null) SetValue(property, entry.Value);
        }

        return true;
    }

    // ── One theme, several variants AT ONCE ───────────────────────────────────────────────────────────────────────
    //
    // Applying a variant re-colours the palette IN PLACE, and that is what makes an application-wide switch cheap. But
    // the palette is ONE set of brushes, so a single theme object cannot show light in one subtree and dark in another
    // at the same time - and a preview pane beside the thing it previews is exactly that.
    //
    // So a variant that differs from the one this theme is currently showing resolves to a SIBLING: same styles, same
    // templates (literally the same Style objects), its own palette. Which keeps both properties, each where it
    // belongs - the common case (the whole application on one variant) stays a colour write per palette key and costs
    // no element anything, and the rare case (a subtree that wants a different variant) pays a re-style ONCE, when it
    // opts in, instead of making everyone else pay for the possibility.

    private readonly Dictionary<ThemeVariant, Theme> _siblings = new();
    private Theme _variantRoot;   // the theme this one was made from; null on the original

    /// <summary>The theme object that shows <paramref name="variant"/> - this one when it already does, otherwise a
    /// sibling sharing every style with it. Returns this theme unchanged when the variant is not one it declares:
    /// giving back something else would be the silent substitution <see cref="ApplyVariant"/> refuses to make.</summary>
    public Theme SiblingForVariant(ThemeVariant variant)
    {
        if (variant.IsUnspecified || variant.FollowsSystem) return this;
        if (!_variants.ContainsKey(variant)) return this;

        // A named variant ALWAYS gets its own sibling, even when this theme happens to be showing that variant right
        // now. Handing back the theme itself looked like a free optimisation and was a bug: the subtree then held the
        // APPLICATION's brushes, so the moment the application switched variant the pinned subtree switched with it -
        // a pane labelled "Dark" going light because something elsewhere changed. Naming a variant has to mean it
        // cannot be changed by anyone else, and that is only true of brushes nobody else is holding.

        var root = _variantRoot ?? this;
        lock (root._siblings)
        {
            if (root._siblings.TryGetValue(variant, out var existing)) return existing;

            var sibling = new Theme(Name) { _variantRoot = root, FontFamily = FontFamily };
            foreach (var styleSet in StyleSets) sibling.StyleSets.Add(styleSet);
            sibling.MergedStyles.AddStyles(MergedStyles.Styles);

            // The definitions are DATA and are shared: a variant's colour table is read, never written, and having two
            // copies drift apart would be a bug nobody could see.
            foreach (var definition in _variants.Values) sibling.AddVariant(definition);

            sibling.ApplyVariant(variant);
            root._siblings[variant] = sibling;
            return sibling;
        }
    }

    /// <summary>The theme this one is a variant sibling of - itself when it is the original.</summary>
    public Theme VariantRoot => _variantRoot ?? this;

    public ThemeVariant ResolveSystemVariant(bool osPrefersDark)
    {
        var wanted = osPrefersDark ? SystemDarkVariant : SystemLightVariant;
        return wanted.IsUnspecified || !_variants.ContainsKey(wanted) ? default : wanted;
    }

    /// <summary>The palette's answer for <paramref name="key"/>, or null. Brushes first, then the few keys a variant
    /// declares as raw colours.</summary>
    internal object PaletteValue(string key)
    {
        if (_palette.TryGetValue(key, out var brush)) return brush;
        return _rawColors.TryGetValue(key, out var colour) ? colour : null;
    }

    public object GetResource(string key)
    {
        return PaletteValue(key) ?? ResourceManager.FindResource(key);
    }

    public bool TryGetResource(string key, out object value)
    {
        value = PaletteValue(key) ?? ResourceManager.FindResource(key);
        return value != null;
    }

    // The requester-aware pair asks the tree-scoped chain FIRST and the palette last: a Local dictionary on the
    // requester's own subtree is meant to override the theme, and answering from the palette before looking would make
    // a theme key impossible to shadow locally.
    public object GetResource(IFundamentalUIComponent requester, string key)
    {
        return ResourceManager.FindResource(requester, key) ?? PaletteValue(key);
    }

    public bool TryGetResource(IFundamentalUIComponent requester, string key, out object value)
    {
        value = GetResource(requester, key);
        return value != null;
    }

    public StyleSetCollection StyleSets { get; }

    public StyleIncludeCollection StyleIncludes { get; }

    public void AddStyleSet(StyleSet styleSet)
    {
        StyleSets.Add(styleSet);
        MergedStyles.AddStyles(styleSet.Styles);
    }

    public void Initialize()
    {
        if (Initialized || Initializing) return;
        Initializing = true;

        // A theme with variants must HAVE one from the moment it is usable. Declaring a variant only creates the
        // palette brushes; the accent, the on-accent text colour and the focus strokes are theme PROPERTIES and are set
        // by nothing but ApplyVariant. A theme left on no variant therefore comes up with those properties null - and
        // {ThemeResource AccentForegroundColor} (31 uses) and {ThemeResource AccentFillColorDefault} (72) then resolve
        // to nothing, so the window's title text has no Foreground at all and the render walk throws on it. The screen
        // shows a blank tab and empty fills, which says nothing about the cause.
        if (CurrentVariant.IsUnspecified && !DefaultVariant.IsUnspecified) ApplyVariant(DefaultVariant);

        foreach (var styleInclude in StyleIncludes)
        {
            var styleSet = (StyleSet)Activator.CreateInstance(styleInclude.Source);
            styleSet?.Initialize(this);
            StyleSets.Add(styleSet);
        }

        var styles = StyleSets.SelectMany(x => x.Styles).ToList();
        MergedStyles.AddStyles(styles);
        StyleSets.CollectionChanged += OnStyleSetRepositoriesChanged;
        Initialized = true;
        Initializing = false;
    }

    public bool Initialized { get; private set; }

    public bool Initializing { get; private set; }

    private void OnStyleSetRepositoriesChanged(object sender, NotifyCollectionChangedEventArgs e)
    {

    }
}