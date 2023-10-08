using System;
using System.Collections.Generic;

namespace Adamantium.UI.Resources;

public class ClassGroup
{
    private List<string> _classes;

    public ClassGroup()
    {
        _classes = new List<string>();
    }
    
    public void AddClass(string name)
    {
        _classes.Add(name);
    }

    public void AddClasses(IEnumerable<string> classes)
    {
        _classes.AddRange(classes);
    }

    public void RemoveClass(string name)
    {
        _classes.Remove(name);
    }

    public IEnumerable<string> GetElements() => _classes;

    public static ClassGroup Parse(string group)
    {
        var classes = group.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var item = new ClassGroup();
        item.AddClasses(classes);
        return item;
    }
}