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
        if (Types.Contains(control.GetType()))
        {
            return true;
        }
        else if (control.Id == Id)
        {
            return true;
        }
        else if (ContainsClass(control))
        {
            return true;
        }
        else if (ContainsClassGroup(control))
        {
            return true;
        }
        return false;
    }

    private bool ContainsClass(IFundamentalUIComponent control)
    {
        var classes = control.ClassNames;
        foreach (var @class in classes)
        {
            if (Classes.Contains(@class)) return true;
        }

        return false;
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