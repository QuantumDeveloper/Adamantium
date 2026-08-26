using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Core.Data;

/// <summary>
/// The live connection behind <c>{ThemeResource Key}</c>: pushes a brush property of the ACTIVE theme onto a target
/// property and refreshes it whenever that theme property changes - so accent/focus edits apply at runtime with no
/// theme reload. Mirrors <see cref="TemplateBindingExpression"/>, but its source is the global current theme (not a
/// templated parent), and it detaches itself when the target unloads.
/// </summary>
public class ThemeResourceExpression : BindingExpressionBase
{
    private readonly string _key;
    private readonly ValuePriority _priority;
    private readonly object _token;
    private AdamantiumComponent _theme;
    private AdamantiumProperty _sourceProperty;

    public ThemeResourceExpression(IAdamantiumComponent target, AdamantiumProperty targetProperty, string key,
        ValuePriority priority = ValuePriority.Template, object token = null)
    {
        Target = target;
        TargetProperty = targetProperty;
        _key = key;
        _priority = priority;
        _token = token;
    }

    public override void EstablishConnection()
    {
        CloseConnection();
        // The theme in force AT THE TARGET, not the application's: an element inside a ThemeScope resolves its
        // {ThemeResource} against that scope's theme, or the whole point of a scope would stop at the styles.
        _theme = (Target is IFundamentalUIComponent element
            ? Resources.ThemeScope.For(element)
            : UIAppContext.Current?.ThemeManager?.CurrentTheme) as AdamantiumComponent;
        if (_theme == null || TargetProperty == null || string.IsNullOrEmpty(_key)) return;

        _sourceProperty = AdamantiumPropertyMap.FindRegistered(_theme.GetType(), _key);
        if (_sourceProperty == null) return;

        UpdateTarget();
        Subscribe(_theme, _sourceProperty, this);
        if (Target is IInputComponent input) input.Unloaded += OnTargetUnloaded;
    }

    public override void UpdateTarget()
    {
        if (_theme == null || _sourceProperty == null || TargetProperty == null) return;
        var value = _theme.GetValue(_sourceProperty);
        if (value == null) return;
        // A trigger {ThemeResource} pushes onto the per-token trigger stack (so it coexists with other triggers on the
        // same property); a base/template one writes its priority slot directly.
        if (_priority == ValuePriority.Trigger && _token != null)
            Target.SetTriggerValue(TargetProperty, value, _token);
        else
            Target.SetValue(TargetProperty, value, _priority);
    }

    public override void CloseConnection()
    {
        if (_theme != null)
        {
            Unsubscribe(_theme, _sourceProperty, this);
            _theme = null;
        }
        if (Target is IInputComponent input) input.Unloaded -= OnTargetUnloaded;
    }

    private void OnTargetUnloaded(object sender, RoutedEventArgs e) => CloseConnection();

    // ---- Routing ---------------------------------------------------------------------------------------------------
    // ONE subscription per theme, and each consumer woken only for the property it actually reads.
    //
    // Every consumer used to sit on the theme's PropertyChanged itself and decide inside the handler whether the change
    // was its own. Since an accent edit assigns half a dozen theme properties, five of every six wake-ups did nothing at
    // all - and there is a consumer for every {ThemeResource} in every LIVE view, parked tabs included. Measured while
    // dragging the picker: 308 453 handler calls in one second, of which 61 723 applied, with the window frozen for
    // 0.6-0.8 s at a time. No binding and no layout counter saw any of it: this is a plain event.
    //
    // Keyed by property, that same drag wakes exactly the consumers that read what changed.
    private sealed class Router
    {
        // A SET, not a list: a consumer leaves when its view does, and a tab holds thousands of them - removing each by
        // scan would make leaving a tab quadratic, which is the trap this fix would otherwise walk straight into.
        //
        // WEAK, because a router lives as long as its THEME - that is, as long as the application - so anything it holds
        // strongly can never be collected. It only ever unsubscribes what calls CloseConnection, and two ordinary cases
        // never do: a target that is not an IInputComponent has no Unloaded event to hook (a brush, a GradientStop, any
        // non-visual an AUML template names), and a component discarded without a detach notification never gets the
        // chance. Measured on the stand: every theme swap left +13 consumers behind, permanently, each holding its target
        // and through it a whole subtree - ~15 MB a swap that a forced full collection could not reclaim.
        //
        // Removal stays O(1) because each expression carries the very handle that was put in here (see _handle), and a
        // HashSet of references compares by reference - so leaving a tab is still linear in what leaves, not quadratic.
        public readonly Dictionary<AdamantiumProperty, HashSet<WeakReference<ThemeResourceExpression>>> ByProperty = new();
    }

    private static readonly Dictionary<AdamantiumComponent, Router> Routers = new();
    private static readonly object RoutersLock = new();

    // TEMP (leak hunt): entries the live routers hold, and how many of them are still ALIVE. The two apart are the whole
    // point - a gap between them is swept lazily, a rising ALIVE count is a real leak.
    public static (int Entries, int Alive) RoutedConsumers
    {
        get
        {
            lock (RoutersLock)
            {
                int entries = 0, alive = 0;
                foreach (var router in Routers.Values)
                foreach (var set in router.ByProperty.Values)
                foreach (var handle in set)
                {
                    entries++;
                    if (handle.TryGetTarget(out _)) alive++;
                }

                return (entries, alive);
            }
        }
    }

    // The handle this expression is registered under - kept so Unsubscribe can drop the exact entry rather than hunt for
    // it, and so the router never holds a strong reference back to us.
    private WeakReference<ThemeResourceExpression> _handle;

    private static void Subscribe(AdamantiumComponent theme, AdamantiumProperty property, ThemeResourceExpression e)
    {
        lock (RoutersLock)
        {
            if (!Routers.TryGetValue(theme, out var router))
            {
                Routers[theme] = router = new Router();
                theme.PropertyChanged += OnThemeChanged;
            }

            if (!router.ByProperty.TryGetValue(property, out var consumers))
            {
                router.ByProperty[property] = consumers = new HashSet<WeakReference<ThemeResourceExpression>>();
            }

            e._handle ??= new WeakReference<ThemeResourceExpression>(e);
            consumers.Add(e._handle);
        }
    }

    private static void Unsubscribe(AdamantiumComponent theme, AdamantiumProperty property, ThemeResourceExpression e)
    {
        lock (RoutersLock)
        {
            // The router itself STAYS, with the theme's subscription: a theme lives as long as the application, and
            // dropping the one subscription only to take it again on the next consumer buys nothing.
            if (e._handle != null &&
                Routers.TryGetValue(theme, out var router) &&
                router.ByProperty.TryGetValue(property, out var consumers))
            {
                consumers.Remove(e._handle);
            }
        }
    }

    private static void OnThemeChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        ThemeResourceExpression[] woken;
        lock (RoutersLock)
        {
            if (sender is not AdamantiumComponent theme || !Routers.TryGetValue(theme, out var router)) return;
            if (!router.ByProperty.TryGetValue(e.Property, out var consumers) || consumers.Count == 0) return;

            // A SNAPSHOT: applying a value can re-establish a connection (a style re-applying under it), which would
            // mutate the very set being walked. It is also the sweep - an entry whose expression has been collected is
            // dropped here, which is why nothing has to remember to unsubscribe for the set to stay bounded.
            var alive = new List<ThemeResourceExpression>(consumers.Count);
            List<WeakReference<ThemeResourceExpression>> dead = null;
            foreach (var handle in consumers)
            {
                if (handle.TryGetTarget(out var expression))
                {
                    alive.Add(expression);
                }
                else
                {
                    (dead ??= new List<WeakReference<ThemeResourceExpression>>()).Add(handle);
                }
            }

            if (dead != null)
            {
                foreach (var handle in dead) consumers.Remove(handle);
            }

            woken = alive.ToArray();
        }

        foreach (var expression in woken) expression.UpdateTarget();
    }
}
