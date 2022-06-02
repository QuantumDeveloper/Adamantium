using Adamantium.UI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using Adamantium.UI.Media;

namespace Adamantium.UI;

public class Theme : AdamantiumComponent
{
    private List<StyleRepository> styleRepositories;
    public Theme(string name)
    {
        styleRepositories = new List<StyleRepository>();
        Name = name;
    }

    public string Name { get; }
    
    public Brush AccentColor { get; set; }
    
    public void AddResource(StyleRepository styleDictionary)
    {
        
    }
}
