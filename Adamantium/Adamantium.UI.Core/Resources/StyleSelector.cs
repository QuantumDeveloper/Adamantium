using Adamantium.Core.Collections;
using Adamantium.Core.TypeParsing;
using Adamantium.UI.Core.Resources.Triggers;
using Adamantium.UI.Core.TypeParsers;

namespace Adamantium.UI.Core.Resources;

/// <summary>
/// A style's match rule (the <c>Selector="…"</c> of a <see cref="Style"/>) - an AND of type / class / class-group / id
/// facets, CSS/WPF-style. Named StyleSelector (not "Selector") so it does not collide with the
/// <see cref="Adamantium.UI.Controls"/> selecting-items control base of the same concept name.
/// <para>May also carry PROPERTY CONDITIONS (<c>"TabControl[TabStripPlacement=Left]"</c>, Avalonia-style): those do NOT
/// gate attachment (<see cref="Match"/> stays structural) - the style attaches to every type/class/id match and its
/// setters are then applied only WHILE the conditions hold, re-applied as the properties change. See Style.Attach.</para>
/// </summary>
[TypeParser(typeof(SelectorParser))]
public class StyleSelector
{
    public StyleSelector()
    {
        Types = new TypesCollection();
        Classes = new Classes();
        ClassGroups = new TrackingCollection<ClassGroup>();
        Conditions = [];
    }

    public TypesCollection Types { get; }

    public Classes Classes {get;}

    public TrackingCollection<ClassGroup> ClassGroups { get; }

    public string Id { get; set; }

    /// <summary>Property=value tests from a <c>[Prop=Value]</c> selector fragment. Not part of <see cref="Match"/> (which
    /// decides attachment, structurally); they gate the style's setters at runtime - see Style.Attach.</summary>
    public List<Condition> Conditions { get; }

    public bool HasConditions => Conditions.Count > 0;

    // Attachment ("does this style bind to this control") is purely STRUCTURAL - type/class/id. Property conditions are a
    // runtime gate on the setters, not on attachment, so a "TabControl[TabStripPlacement=Left]" style attaches to every
    // TabControl and its setters switch in/out as the placement changes. (A condition-only selector, no structural facet,
    // matches every control and is gated entirely by its conditions.)
    public bool Match(IFundamentalUIComponent control)
    {
        // An empty selector (no facet at all) matches nothing.
        if (Types.Count == 0 && Classes.Count == 0 && ClassGroups.Count == 0 && Id == null && Conditions.Count == 0)
            return false;

        // AND of every SPECIFIED facet (CSS/WPF semantics): "Button.Accent" = type Button AND class Accent; a
        // single-facet selector ("Button") still matches purely on that facet.
        // Type match is IS-A here (a Window selector matches a MainWindow : Window) - it decides only CANDIDACY. The
        // BOUNDARY that stops a base type's implicit style from bleeding onto a derived one is applied in
        // Theme.FindStylesForComponent, which keeps only the NEAREST styled ancestor's type styles (DefaultStyleKey
        // semantics): a control with its own style is a hard boundary (a CheckBox does NOT get the ToggleButton style),
        // while a subclass with NO style of its own (an AUML x:Class MainWindow) still falls back to its base's. Explicit
        // cross-type sharing is via Style.BasedOn. IS-A candidacy (not exact) is also what lets Attach apply that fallback.
        if (Types.Count > 0 && !Types.Any(t => t.IsAssignableFrom(control.GetType()))) return false;
        if (Id != null && control.Id != Id) return false;
        if (Classes.Count > 0 && !HasAllClasses(control)) return false;
        if (ClassGroups.Count > 0 && !ContainsClassGroup(control)) return false;
        return true;
    }

    // How closely this selector's type facet matches a control's runtime type: 0 = the selector targets the EXACT type,
    // N = it targets a type N inheritance steps up (a base). A selector with no type facet (class/id/condition only) is
    // the least type-specific. Used to order overlapping matched styles base-first so the most-derived one applies last.
    public int SpecificityDistance(Type controlType)
    {
        if (Types.Count == 0) return int.MaxValue;
        var best = int.MaxValue;
        foreach (var t in Types)
        {
            var distance = 0;
            for (var cur = controlType; cur != null; cur = cur.BaseType, distance++)
            {
                if (cur == t) { if (distance < best) best = distance; break; }
            }
        }
        return best;
    }

    private bool HasAllClasses(IFundamentalUIComponent control)
    {
        // A component with no classes cannot have all of a non-empty set - answer without building its collection.
        if (!control.HasClassNames) return Classes.Count == 0;

        foreach (var @class in Classes)
        {
            if (!control.ClassNames.Contains(@class)) return false;
        }

        return true;
    }

    private bool ContainsClassGroup(IFundamentalUIComponent control)
    {
        // Answer without building the collection - and answer EXACTLY what the loop below answers for an empty one:
        // `All` over an empty sequence is true, so a class-less component matches the first group there is. That reads
        // like a bug (a group selector matching everything that names no class at all), but changing it is not this
        // change's business, so the shortcut reproduces it rather than quietly fixing it.
        if (!control.HasClassNames) return ClassGroups.Count > 0;

        var classes = control.ClassNames;
        foreach (var group in ClassGroups)
        {
            var result = classes.All(x => group.GetElements().Contains(x));
            if (result)
            {
                return true;
            }
        }

        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Types, Classes, ClassGroups, Id, Conditions.Count);
    }

    public override string ToString()
    {
        var conditions = Conditions.Count == 0
            ? string.Empty
            : " [" + string.Join("][", Conditions.Select(c => $"{c.Property}={c.Value}")) + "]";
        return $"{Types} {Classes} {Id}{conditions}";
    }
}
