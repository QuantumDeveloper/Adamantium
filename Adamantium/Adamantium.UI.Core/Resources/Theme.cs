using System.Collections.Specialized;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Core.Resources;

public class Theme : AdamantiumComponent, ITheme
{
    public Theme()
    {
        StyleSets = new StyleSetCollection();
        StyleIncludes = new StyleIncludeCollection();
        ResourceManager = UIAppContext.Current.ResourceManager;
        MergedStyles = new StyleSet();
    }

    public Theme(string name) : this()
    {
        Name = name;
    }

    public string Name { get; protected set; }

    public Brush AccentColor { get; set; }

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

        return MergedStyles.Styles.Where(x => x.Selector.Match(component)).ToArray();
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