using Adamantium.Core.Collections;
using Adamantium.Core.DependencyInjection;
using Adamantium.UI.Core.Media.Animation;

namespace Adamantium.UI.Core.Resources;

public class ThemeManager : IThemeManager
{
    private IDependencyResolver dependencyResolver;
    private readonly Dictionary<string, ITheme> _themesMap;
    private Dictionary<StyleSelector, IUIComponent> components;
    private TrackingCollection<ITheme> _themes;
    private IResourceManager _resourceManager;

    public IReadOnlyList<ITheme> Themes => _themes;

    // Set when the application asked for ThemeVariant.System, so a later change of the OS appearance is followed
    // rather than being a one-off resolution that goes stale at sunset.
    private bool _followingSystem;

    public ThemeManager(IDependencyResolver dependencyResolver)
    {
        SystemAppearance.Changed += (_, _) =>
        {
            if (_followingSystem) SetVariant(ThemeVariant.System);
        };

        this.dependencyResolver = dependencyResolver;
        _themes = new TrackingCollection<ITheme>();
        _themesMap = new Dictionary<string, ITheme>();
        components = new Dictionary<StyleSelector, IUIComponent>();
        _resourceManager = UIAppContext.Current.ResourceManager;
    }

    public event EventHandler<ThemeChangedEventArgs> ThemeChanging;
    public event EventHandler<ThemeChangedEventArgs> ThemeChanged;

    public bool IsThemeChanging { get; private set; }

    // The windows whose cascade this swap is waiting on, and the args to report when they have all settled. Non-null only
    // while a swap is in flight.
    private List<IUIComponent> _swapping;
    private ThemeChangedEventArgs _swapArgs;

    /// <summary>A MINIMUM time the busy overlay stays up once a swap begins. 0 = finish the moment the cascade drains (the
    /// default). A UX floor stops a fast swap from flashing the overlay for a few frames; set higher to actually SEE the
    /// swap loader spin (the demo). Independent of the cascade: completion waits for BOTH the cascade to settle AND this to
    /// elapse.</summary>
    public double MinSwapSeconds { get; set; }

    private double _swapElapsed;   // seconds since the swap began, on the loop heartbeat
    private bool _cascadeSettled;  // has every swapping window's layout drained?
    private ITheme _pendingOld;    // outgoing theme whose palette a deferred swap must still deactivate

    /// <summary>Bumped on every theme swap. A cheap way to ask "did the theme change while I was away?" - a parked view
    /// that comes back at the same version needs none of the revalidation a returning view otherwise does.</summary>
    public static int Version { get; private set; }

    /// <summary>Bumped whenever the PALETTE is repainted - a variant switch, and a swap (which repaints it by replacing
    /// it). A variant deliberately leaves styles, templates and property values alone, so <see cref="Version"/> does not
    /// move for one; but the colours in every brush DID change, and anything that was out of the tree at the time was
    /// told nothing. That is the question a returning parked subtree has to ask, and it is not the same question as
    /// "must I be re-styled".</summary>
    public static int PaletteVersion { get; private set; }

    /// <summary>Switch the current theme's variant. See <see cref="IThemeManager.SetVariant"/> for why this is not a
    /// theme swap: everything a swap is expensive FOR - rebuilt templates, re-applied styles, a property write per
    /// element - is exactly what a variant does not touch.</summary>
    public bool SetVariant(ThemeVariant variant)
    {
        if (CurrentTheme is not Theme theme) return false;

        if (variant.FollowsSystem)
        {
            var resolved = theme.ResolveSystemVariant(SystemAppearance.PrefersDark);
            if (resolved.IsUnspecified) return false;   // this theme has no light/dark notion to follow

            // Remembered, so the application KEEPS following: the OS changes its mind at sunset, and an application
            // that resolved "system" once and forgot would be right only until then.
            _followingSystem = true;
            if (!theme.ApplyVariant(resolved)) return false;
            PaletteVersion++;
            UIAppContext.Current?.ResourceManager?.NotifyResourcesChanged();
            return true;
        }

        _followingSystem = false;
        if (!theme.ApplyVariant(variant)) return false;
        PaletteVersion++;

        // A variant rewrites the palette, so everything holding a LIVE reference to a keyed resource has to re-resolve.
        // Solid fills came through without this because the brush OBJECT survives a variant and tells its owners itself;
        // a raw COLOUR has no such thread - a gradient stop is handed a value, and only this tells it there is a new one.
        UIAppContext.Current?.ResourceManager?.NotifyResourcesChanged();

        // Nothing else to do here. A variant writes new colours into the brushes the palette already owns, and a brush
        // tells its owners - which INCLUDES the elements that took the value by inheritance, because an inherited value
        // is materialised on the inheritor and attaches from there. There was a window-wide paint walk in this spot,
        // added on the theory that a TextBlock inheriting Foreground is not an owner and would never be told; the pixel
        // test in InheritedBrushRepaintTests disproves it - the text follows the brush with no walk anywhere in sight.
        // The text that really did stay in the old colour was a STALE SNAPSHOT one layer down (TextRenderUnit.RefreshColors).
        return true;
    }

    public void SetTheme(ITheme theme)
    {
        if (CurrentTheme == theme) return;

        Version++;
        PaletteVersion++;   // a swap replaces the palette, which is a repaint by any other name
        var oldTheme = CurrentTheme;
        CurrentTheme = theme;

        // Announce the swap BEFORE the cascade so a busy indicator is already up when the tree starts churning.
        _swapArgs = new ThemeChangedEventArgs(oldTheme, theme);
        _pendingOld = oldTheme;
        IsThemeChanging = true;
        ThemeChanging?.Invoke(this, _swapArgs);

        _swapElapsed = 0;
        _cascadeSettled = false;

        // The FIRST theme application (startup) has no overlay to protect and runs before the loop is pumping tickers, so
        // swap the palette + start the cascade synchronously.
        if (oldTheme == null)
        {
            BeginContentCascade();
            if (MinSwapSeconds > 0) AnimationManager.AddTicker(HoldTick);
            TryFinishSwap();
            return;
        }

        // A real swap: this frame ONLY raises the busy overlay (IsThemeChanging above) - a fixed 48x48 that needs no palette.
        // DEFER both the palette swap AND the content re-style by one frame. Both churn the whole tree, and doing them now
        // makes THIS a long frame; the overlay's compositor basis is captured at the START of a frame (before its own layout
        // runs), so a long swap frame keeps the spinner at 0x0 - unrecorded, nothing to animate - for its whole duration:
        // the frozen spinner. Next frame the overlay is already laid out (48x48), so the compositor spins it right through
        // the churn. The opaque scrim hides the single stale-palette content frame.
        var contentStarted = false;
        AnimationManager.AddTicker(dt =>
        {
            _swapElapsed += dt;
            if (!contentStarted)
            {
                contentStarted = true;
                BeginContentCascade();
            }
            TryFinishSwap();
            return _swapArgs == null;   // done once the swap has completed (its args are cleared)
        });
    }

    private bool HoldTick(double dt)
    {
        _swapElapsed += dt;
        TryFinishSwap();
        return _swapArgs == null;
    }

    // Swap the live palette + queue the whole-tree re-style, and begin listening for its settle. Split out of SetTheme so a
    // real swap can defer it one frame (letting the busy overlay lay out first) while the initial application runs it inline.
    private void BeginContentCascade()
    {
        // Palette activation is SYMMETRIC and LAZY: deactivate the outgoing theme's resource sources, then activate the
        // incoming one's - only the CURRENT theme's palette is live in the Theme provider, so N themes cost nothing until
        // chosen. This is the single owner of the "exactly one palette live" invariant.
        if (_pendingOld != null) _resourceManager.RemoveSources(_pendingOld);
        ActivateThemeSources(CurrentTheme);
        _pendingOld = null;

        var windows = UIAppContext.Current.Windows;
        _swapping = [];
        foreach (var window in windows)
        {
            if (window is IUIComponent visual) _swapping.Add(visual);
            window.InvalidateStyles();
        }

        // The restyle + resource re-resolution (brushes) settles over the next few frames through paths that don't all mark
        // the render dirty, so force full render walks until the layout signals the cascade has fully drained - otherwise
        // re-styled controls stay blank until an unrelated mark (a mouse-over) forces a rebuild.
        RenderDirty.ForceStructuralUntilSettled();

        if (_swapping.Count == 0) _cascadeSettled = true;
        else LayoutManager.Quiescent += OnLayoutQuiescent;
    }

    private void OnLayoutQuiescent(LayoutManager manager)
    {
        // One window going quiet does not mean the swap is over - every window the swap touched must owe no layout work.
        if (_swapping == null) return;
        foreach (var window in _swapping)
            if (!LayoutManager.For(window).IsSettled) return;

        _cascadeSettled = true;
        TryFinishSwap();
    }

    // Complete only when the cascade has drained AND the minimum overlay time has elapsed.
    private void TryFinishSwap()
    {
        if (_swapArgs == null) return;   // already completed
        if (!_cascadeSettled || _swapElapsed < MinSwapSeconds) return;
        CompleteSwap();
    }

    private void CompleteSwap()
    {
        LayoutManager.Quiescent -= OnLayoutQuiescent;

        // The swap has drained: every template that was going to be rebuilt has been, and the elements it replaced are
        // out of the tree for good. They are still held, though - a brush keeps every element painting with it
        // subscribed, and it is only ever TOLD to let go when that element's property takes a different value, which is
        // exactly what never happens to something discarded. Nothing else in the run knows that a whole application's
        // worth of elements just died; this is the one moment that does. See Brush.SweepEveryBrush.
        Media.Brush.SweepEveryBrush();

        var args = _swapArgs;
        _swapping = null;
        _swapArgs = null;
        IsThemeChanging = false;
        ThemeChanged?.Invoke(this, args);
    }

    // (Re)register the theme's resources into the resource manager: its ResourceContext.Resources block (the palette,
    // the icons - each its own linked dictionary file) and, for a theme that still states one, the single
    // ResourceContext.Source link. Both are kept as attached-property values, so we can re-add them every time the
    // theme becomes current.
    private void ActivateThemeSources(ITheme theme)
    {
        if (theme is not AdamantiumComponent component) return;

        var link = ResourceContext.GetSource(component);
        if (link?.Source != null)
            _resourceManager.AddSource(component, link.Source, link.Scope);

        var resources = ResourceContext.GetResources(component);
        if (resources != null)
            ResourceContext.RegisterResources(component, resources);
    }

    public void ApplyTheme(ITheme theme, IFundamentalUIComponent component)
    {
        theme?.Initialize();
        ApplyStyles(theme, component);
    }

    public void ApplyTheme(string name, IFundamentalUIComponent component)
    {
        if (!_themesMap.TryGetValue(name, out var theme)) return;
        
        ApplyTheme(theme, component);
    }
    
    public void ApplyExternalStyles(IFundamentalUIComponent component, params ReadOnlySpan<Style> styles)
    {
        if (styles.Length == 0) 
            return;
        
        component.AttachStyles(styles);
    }

    public void ApplyStyles(ITheme theme, IFundamentalUIComponent component)
    {
        if (theme == null) return;

        var styles = theme.FindStylesForComponent(component);
        if (styles.Length > 0)
        {
            component.AttachStyles(styles);
        }
    }

    public void ApplyStyles(IFundamentalUIComponent component)
    {
        var styles = FindStylesForComponent(component);
        component.AttachStyles(styles);
    }

    public void RemoveStyles(IFundamentalUIComponent component)
    {
        component.DetachStyles();
    }

    public Style[] FindStylesForComponent(IFundamentalUIComponent component)
    {
        if (CurrentTheme == null) return [];

        var styles = CurrentTheme.FindStylesForComponent(component);

        return styles;
    }

    public ITheme CurrentTheme { get; private set; }

    public void AddTheme(string name, ITheme theme)
    {
        if (!_themesMap.TryAdd(name, theme)) return;

        theme.Initialize();

        _themes.Add(theme);
    }

    public void RemoveTheme(string name)
    {
        _themesMap.Remove(name);
        var theme = _themes.FirstOrDefault(x => x.Name == name);
        if (theme != null)
        {
            _themes.Remove(theme);
        }
    }

    public ITheme this[string name] => !_themesMap.ContainsKey(name) ? null : _themesMap[name];

    public ITheme this[int index] => _themes[index];
    
    /// <summary>Applies the theme that is in force AT <paramref name="control"/> - which is the application's only when
    /// no ancestor declares a scope of its own. See <see cref="ThemeContext"/>.</summary>
    public void ApplyCurrentTheme(IFundamentalUIComponent control)
    {
        ApplyTheme(ThemeContext.For(control) ?? CurrentTheme, control);
    }

}