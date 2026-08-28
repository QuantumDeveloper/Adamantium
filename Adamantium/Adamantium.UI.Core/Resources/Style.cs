using System.Runtime.CompilerServices;
using Adamantium.UI.Core.Resources.Triggers;

namespace Adamantium.UI.Core.Resources;

public class Style : AdamantiumComponent
{
    private Dictionary<AdamantiumProperty, ISetter> settersDict;

    // The activators THIS style created on each component (a subset of the component's shared ActiveActivators list).
    // Kept so Attach/Detach are idempotent: re-applying a style (a theme swap re-themes WITHOUT a prior detach) must
    // undo its previous activators instead of piling up more - each carries a live PropertyChanged subscription.
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

    /// <summary>Explicit style inheritance (the antidote to type matching being EXACT now): the base type(s) this style
    /// builds on. Their setters + triggers are applied FIRST when this style attaches, so this style's own contributions
    /// override them. Multiple bases compose like mixins - listed left-to-right, LATER wins over earlier (own wins over
    /// all). A base type's styles are found by exact type in the theme, recursively (a base may itself be BasedOn another).
    /// Parsed from the same type syntax as <see cref="Selector"/> (e.g. <c>BasedOn="ToggleButton"</c>).</summary>
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

    // The base styles this style is BasedOn, in application order (base-first, deduped, recursive - a base may itself be
    // BasedOn another). Resolved by EXACT type from the theme; empty when there is no BasedOn (the common case). Memoized:
    // a style's bases are constant for its lifetime (a theme swap builds fresh Style objects), so thousands of identical
    // containers don't each re-scan the theme.
    private IReadOnlyList<Style> ResolveBases()
    {
        if (_resolvedBases != null) return _resolvedBases;
        var result = new List<Style>();
        if (BasedOn is { Types.Count: > 0 } && Theme is Theme theme)
        {
            var seen = new HashSet<Style> { this };   // guard against a self/cyclic BasedOn
            theme.CollectBasedOn(BasedOn, result, seen);
        }
        // Band every style in the chain, and this one, by what its selector SELECTS - see StyleBandOfSelector.
        foreach (var baseStyle in result) baseStyle.StampBand(baseStyle.StyleBandOfSelector());
        StampBand(StyleBandOfSelector());

        return _resolvedBases = result;
    }

    /// <summary>How LOCAL this style's rules are: the inheritance depth of the type its selector names. A style on the
    /// control's own type outranks one on a type it derives from, so the deriving control always has the last word.
    /// <para>It is the SELECTOR that says this, not the BasedOn chain, because a control's rules are spread over
    /// SEVERAL style blocks (one concern each, per the small-styles convention) while the base arrives through the one
    /// block that says <c>BasedOn</c>. Banding by position in the collected chain therefore ranked a BASE style above a
    /// derived one whose block simply declared no BasedOn of its own - and every trigger a derived control writes to
    /// un-inherit a base rule lives in exactly such a block. Measured: ToggleButton pulls in three style blocks, so its
    /// checked-label rule landed on band 2 while the ToggleSwitch rule meant to overrule it sat on band 0. The label of
    /// a checked switch, checkbox and radio button came out white on a light panel.</para>
    /// <para>A selector with no type facet bands at 0. Selecting several types takes the SHALLOWEST: the style speaks
    /// for all of them, so it can only claim the specificity of the least specific.</para></summary>
    private int StyleBandOfSelector()
    {
        var shallowest = int.MaxValue;
        foreach (var type in Selector.Types)
        {
            var depth = 0;
            for (var t = type.BaseType; t != null; t = t.BaseType) depth++;
            if (depth < shallowest) shallowest = depth;
        }

        return shallowest == int.MaxValue ? 0 : shallowest;
    }

    /// <summary>Mark every trigger setter of THIS style with its band (see <see cref="StyleBandOfSelector"/>), so the
    /// trigger value stack can prefer the more local rule. Idempotent: the band is a property of the style alone.</summary>
    private void StampBand(int band)
    {
        if (_band == band) return;
        _band = band;

        foreach (var trigger in Triggers)
        {
            if (trigger?.Setters is not { } setters) continue;
            foreach (var setter in setters) setter.StyleBand = band;
        }
    }

    private int _band = -1;

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

    // A PLAIN FIELD on the component, not an attached property, and the difference is measured: this is read once per
    // node on every attach - ResumeActivators is called unconditionally for the whole subtree - and the answer is null
    // for nearly all of them, because most controls carry no style triggers at all. A property read to be told "nothing
    // here" was the largest remaining item of the attach walk after the IsInitialized latch. Element triggers were
    // already a plain field next to this one (`_triggerActivators`); style triggers now live the same way.
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