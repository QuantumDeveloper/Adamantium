using Adamantium.UI.Input;
using System;
using System.Linq;
using Adamantium.Core.Collections;

namespace Adamantium.UI.Resources;

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

    public static Selector Parse(string selectorString)
    {
        var splitResult = selectorString.Split(new char[] { ',', ' ' },
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var selector = new Selector();

        foreach (var splitItem in splitResult)
        {
            if (splitItem.StartsWith("#"))
            {
                selector.Id = splitItem.Substring(1);
            }
            else if (splitItem.StartsWith("."))
            {
                if (splitItem.Contains('.')) // several chained classes  
                {
                    var group = ClassGroup.Parse(splitItem);
                    selector.ClassGroups.Add(group);
                }
                else  // single class
                {
                    selector.Classes.Add(splitItem.Substring(1));
                }
            }
            else // Control type
            {
                var type = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes()).FirstOrDefault(x => x.Name == splitItem);
                if (type != null)
                {
                    selector.Types.Add(type);
                }
            }
        }
        return selector;
    }
}