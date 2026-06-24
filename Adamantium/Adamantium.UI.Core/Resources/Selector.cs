using Adamantium.Core.Collections;
using Adamantium.Core.TypeParsing;
using Adamantium.UI.Core.TypeParsers;

namespace Adamantium.UI.Core.Resources;

[TypeParser(typeof(SelectorParser))]
public class Selector
{
    public Selector()
    {
        Types = new TypesCollection();
        Classes = new Classes();
        ClassGroups = new TrackingCollection<ClassGroup>();
    }
   
    public TypesCollection Types { get; }
   
    public Classes Classes {get;}
    
    public TrackingCollection<ClassGroup> ClassGroups { get; }
   
    public string Id { get; set; }

    public bool Match(IFundamentalUIComponent control)
    {
        // An empty selector matches nothing.
        if (Types.Count == 0 && Classes.Count == 0 && ClassGroups.Count == 0 && Id == null)
            return false;

        // AND of every SPECIFIED facet (CSS/WPF semantics): "Button.Accent" = type Button AND class Accent; a
        // single-facet selector ("Button") still matches purely on that facet.
        if (Types.Count > 0 && !Types.Contains(control.GetType())) return false;
        if (Id != null && control.Id != Id) return false;
        if (Classes.Count > 0 && !HasAllClasses(control)) return false;
        if (ClassGroups.Count > 0 && !ContainsClassGroup(control)) return false;
        return true;
    }

    private bool HasAllClasses(IFundamentalUIComponent control)
    {
        foreach (var @class in Classes)
        {
            if (!control.ClassNames.Contains(@class)) return false;
        }

        return true;
    }
    
    private bool ContainsClassGroup(IFundamentalUIComponent control)
    {
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
        return HashCode.Combine(Types, Classes, Id);
    }

    public override string ToString()
    {
        return $"{Types} {Classes} {Id}";
    }
}