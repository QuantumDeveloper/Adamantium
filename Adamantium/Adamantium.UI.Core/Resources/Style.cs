using System.Runtime.CompilerServices;
using Adamantium.UI.Core.Resources.Triggers;

namespace Adamantium.UI.Core.Resources;

public class Style : AdamantiumComponent
{
    private Dictionary<AdamantiumProperty, ISetter> settersDict;

    // The activators THIS style created per component, so Attach/Detach stay idempotent: a theme swap re-applies
    // WITHOUT detaching first, and each activator carries a live PropertyChanged subscription.
    private readonly ConditionalWeakTable<IFundamentalUIComponent, List<ITriggerActivator>> _activatorsByComponent = new();

    public Style()
    {
        settersDict = new Dictionary<AdamantiumProperty, ISetter>();
        Setters = new SetterCollection();
        Triggers = new TriggerCollection();
        Selector = new StyleSelector();
    }

    internal ITheme Theme { get; set; }

    public StyleSelector Selector { get; set; }

    /// <summary>Explicit style inheritance - the antidote to type matching being EXACT. The bases' setters and triggers
    /// apply FIRST, so this style overrides them; several compose like mixins, left to right. Found by exact type in the
    /// theme, recursively. Same type syntax as <see cref="Selector"/>.</summary>
    public StyleSelector BasedOn { get; set; }

    public SetterCollection Setters { get; }

    public TriggerCollection Triggers { get; }

    public void Add(object child)
    {
        switch (child)
        {
            case Setter setter:
                Setters.Add(setter);
                break;
            case ITrigger trigger:
                Triggers.Add(trigger);
                break;
            default:
                throw new InvalidOperationException(
                    $"Type '{child?.GetType().FullName}' cannot be added to a Style."
                );
        }
    }

    public static void Apply(IFundamentalUIComponent component, params ReadOnlySpan<Style> styles)
    {
        if (styles == null) return;
        
        foreach (var style in styles)
        {
            style.Attach(component);
        }
    }
    
    public static void UnApply(IFundamentalUIComponent component, params ReadOnlySpan<Style> styles)
    {
        if (styles == null) return;
        
        foreach (var style in styles)
        {
            style.Detach(component);
        }
    }

    public void Attach(IFundamentalUIComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);

        if (!Selector.Match(component))
        {
            return;
        }

        // Idempotent: undo a prior attach of THIS style to THIS component first (a theme swap re-applies without a
        // preceding detach), so activators + their subscriptions never accumulate across re-applies.
        ReleaseActivators(component);

        // BasedOn bases FIRST (base-first, mixin order), so this style's own setters/triggers - applied after - override
        // them. A base's contributions are tracked under THIS style, so Detach undoes them together.
        foreach (var baseStyle in ResolveBases())
        {
            baseStyle.ApplyContributions(component, this);
        }

        ApplyContributions(component, this);
    }

    // Apply THIS style's own setters + triggers to `component`, tracked under `owner` (this, or - for a BasedOn base -
    // the deriving style, so the base's contribution is released together with it).
    private void ApplyContributions(IFundamentalUIComponent component, Style owner)
    {
        if (Selector.HasConditions)
        {
            // The selector carries property conditions ("TabControl[TabStripPlacement=Left]"): its setters apply only
            // WHILE the conditions hold (and re-apply/undo as the properties change), so route them through the very
            // activator a MultiTrigger uses - at Trigger priority, which outranks an unconditional base style's setters.
            var gate = new MultiTrigger { Setters = Setters };
            gate.Conditions.AddRange(Selector.Conditions);
            owner.RecordActivator(component, gate.Apply(new StyleTriggerExecutionContext(component, owner.Theme)));
        }
        else
        {
            foreach (var setter in Setters)
            {
                setter.Apply(component, owner, owner.Theme);
            }
        }

        foreach (var trigger in Triggers)
        {
            owner.RecordActivator(component, trigger.Apply(new StyleTriggerExecutionContext(component, owner.Theme)));
        }
    }

    public void Detach(IFundamentalUIComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);

        if (!Selector.Match(component))
        {
            return;
        }

        // Undo own then base setters (bases were tracked under THIS style); ReleaseActivators tears down every activator
        // this style recorded (its own triggers AND any base triggers).
        RemoveContributions(component, this);
        foreach (var baseStyle in ResolveBases())
        {
            baseStyle.RemoveContributions(component, this);
        }

        ReleaseActivators(component);
    }

    // Undo THIS style's own SETTERS from `component` (tracked under `owner`). Conditioned styles applied their setters
    // through an activator, never unconditionally - those are torn down by ReleaseActivators, not here.
    private void RemoveContributions(IFundamentalUIComponent component, Style owner)
    {
        if (Selector.HasConditions) return;
        foreach (var setter in Setters)
        {
            setter.Remove(component, owner, owner.Theme);
        }
    }

    private List<Style> _resolvedBases;

    // The bases this style is BasedOn, base-first, deduped and recursive; empty when there is no BasedOn, the common
    // case. Memoized because a style's bases are constant for its lifetime - a theme swap builds fresh Style objects -
    // so thousands of identical containers do not each re-scan the theme.
    private IReadOnlyList<Style> ResolveBases()
    {
        if (_resolvedBases != null) return _resolvedBases;
        var result = new List<Style>();
        if (BasedOn is { Types.Count: > 0 } && Theme is Theme theme)
        {
            var seen = new HashSet<Style> { this };   // guard against a self/cyclic BasedOn
            theme.CollectBasedOn(BasedOn, result, seen);
        }
        // Push each style's band down onto its trigger setters - the band itself is decided by the selector, not here.
        foreach (var baseStyle in result) baseStyle.StampBand();
        StampBand();

        return _resolvedBases = result;
    }

    /// <summary>How specific this style is, read the way the web reads it: an id beats any number of classes, a class
    /// beats any depth of type. Packed into one number so the value stack compares with one compare.
    ///
    /// <para>THE SELECTOR DECIDES, AND NOTHING ELSE. Not the BasedOn chain - a control spreads its rules over several
    /// blocks and only one of them says BasedOn, so ranking by position there once put a BASE rule above the derived
    /// rule meant to overrule it. And not the include order either - counting only the type put a class style and a
    /// plain type style in one band, so whichever set a theme listed later won.</para>
    ///
    /// <para>No type facet bands at 0; several types take the SHALLOWEST, since the style speaks for all of them.
    /// Property conditions (<c>[Prop=Value]</c>) count with the classes, as attribute selectors do in CSS.</para>
    /// </summary>
    private int StyleBandOfSelector()
    {
        var shallowest = int.MaxValue;
        foreach (var type in Selector.Types)
        {
            var depth = 0;
            for (var t = type.BaseType; t != null; t = t.BaseType) depth++;
            if (depth < shallowest) shallowest = depth;
        }

        var typeDepth = shallowest == int.MaxValue ? 0 : shallowest;

        // Weights, not tiers, only because the band is one int. Both are far above anything reachable: an inheritance
        // chain is a dozen deep at most, and nobody writes a thousand classes into one selector.
        const int ClassWeight = 1_000;
        const int IdWeight = 1_000_000;

        var narrowing = Selector.Classes.Count + Selector.ClassGroups.Count + Selector.Conditions.Count;
        var identity = string.IsNullOrEmpty(Selector.Id) ? 0 : 1;

        return identity * IdWeight + narrowing * ClassWeight + typeDepth;
    }

    /// <summary>Copy this style's band onto its trigger setters, which is all a trigger setter can be asked. It does
    /// not DECIDE the band - <see cref="Band"/> does, from the selector - so there is one answer to "how specific is
    /// this style" rather than one per caller.</summary>
    private void StampBand()
    {
        foreach (var trigger in Triggers)
        {
            if (trigger?.Setters is not { } setters) continue;
            foreach (var setter in setters) setter.StyleBand = Band;
        }
    }

    private int _band = -1;

    /// <summary>How specific this style is, computed once from the selector alone (see
    /// <see cref="StyleBandOfSelector"/>) and therefore answerable at any time - before attaching, without a BasedOn,
    /// in any order. Read by the value stack.</summary>
    internal int Band => _band >= 0 ? _band : _band = StyleBandOfSelector();

    // Add an activator to both the component's shared list (so a template-change reevaluation sees it) and this style's
    // own per-component record (so Detach/re-Attach can remove exactly the ones it added).
    private void RecordActivator(IFundamentalUIComponent component, ITriggerActivator activator)
    {
        GetOrCreateActiveActivators(component).Add(activator);
        _activatorsByComponent.GetValue(component, static _ => new List<ITriggerActivator>()).Add(activator);
    }

    // Deactivate + drop every activator this style put on the component (from both the shared list and its own record).
    private void ReleaseActivators(IFundamentalUIComponent component)
    {
        if (!_activatorsByComponent.TryGetValue(component, out var mine)) return;
        var all = GetActiveActivators(component);
        foreach (var activator in mine)
        {
            activator?.Deactivate();
            all?.Remove(activator);
        }
        _activatorsByComponent.Remove(component);
    }
    
    // Re-wire this component's style-trigger activators after its template changed: each tears down what it applied to
    // the OLD parts (and its subscriptions) and re-evaluates against the NEW template. Lets a runtime template swap stay
    // leak-free while a style's triggers target named parts of whatever template is currently applied.
    internal static void ReevaluateActivators(IFundamentalUIComponent component)
    {
        var activators = GetActiveActivators(component);
        if (activators == null) return;

        foreach (var activator in activators)
        {
            // Skip template-independent activators (no setter targets a named part): re-pointing them is needless, and
            // for a setter on Template itself it would re-swap the template from inside this very pass and recurse.
            if (activator is not { TargetsTemplateParts: true }) continue;
            activator.Deactivate();
            activator.Activate();
        }
    }

    // The component left / re-entered the visual tree. Style triggers are where a loading indicator's pulse actually
    // comes from (the theme runs it), so this is the half that matters most - see ITriggerActivator.SuspendActions.
    internal static void SuspendActivators(IFundamentalUIComponent component)
    {
        var activators = GetActiveActivators(component);
        if (activators == null) return;

        foreach (var activator in activators) activator?.SuspendActions();
    }

    internal static void ResumeActivators(IFundamentalUIComponent component)
    {
        var activators = GetActiveActivators(component);
        if (activators == null) return;

        foreach (var activator in activators) activator?.ResumeActions();
    }

    // A PLAIN FIELD on the component rather than an attached property, and that is measured: this is read once per node
    // on every attach and answers null for nearly all of them. A property read to be told "nothing here" was the largest
    // remaining item of the attach walk after the IsInitialized latch.
    private static List<ITriggerActivator> GetActiveActivators(IFundamentalUIComponent component)
    {
        return (component as FundamentalUIComponent)?.StyleActivators;
    }

    private static List<ITriggerActivator> GetOrCreateActiveActivators(IFundamentalUIComponent component)
    {
        if (component is not FundamentalUIComponent owner) 
            return new List<ITriggerActivator>();

        return owner.StyleActivators ??= new List<ITriggerActivator>();
    }

    public override string ToString()
    {
        return $"{Selector}";
    }
}