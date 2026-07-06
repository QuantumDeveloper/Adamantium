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
        ResourceManager = UIAppContext.Current.ResourceManager;
        MergedStyles = new StyleSet();
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
        AccentFillColorDefault = new SolidColorBrush(seed);
        AccentFillColorSecondary = new SolidColorBrush(Color.Lerp(seed, Black, (float)AccentHoverDarken));   // hover
        AccentFillColorTertiary = new SolidColorBrush(Color.Lerp(seed, Black, (float)AccentPressedDarken));  // pressed
        AccentForegroundColor = new SolidColorBrush(OnAccent(seed));
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

    public Style[] FindStylesForComponent(IFundamentalUIComponent component)
    {
        if (component == null) return [];

        // Base-first ordering: a matched style that targets a BASE type (larger SpecificityDistance) is applied BEFORE
        // one that targets a more-derived type, so the most-specific style's setters land last and win (e.g. Button's
        // template overrides the ContentControl template it now also matches). OrderByDescending is stable, so styles of
        // equal specificity keep their document/include order (the existing base + template + triggers + Accent cascade).
        var type = component.GetType();
        return MergedStyles.Styles
            .Where(x => x.Selector.Match(component))
            .OrderByDescending(x => x.Selector.SpecificityDistance(type))
            .ToArray();
    }

    public object GetResource(string key)
    {
        return ResourceManager.FindResource(key);
    }

    public bool TryGetResource(string key, out object value)
    {
        value = ResourceManager.FindResource(key);
        return value != null;
    }

    public object GetResource(IFundamentalUIComponent requester, string key)
    {
        return ResourceManager.FindResource(requester, key);
    }

    public bool TryGetResource(IFundamentalUIComponent requester, string key, out object value)
    {
        value = ResourceManager.FindResource(requester, key);
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