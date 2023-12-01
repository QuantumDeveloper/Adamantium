using Adamantium.Engine.Core;
using System;
using System.Collections.Generic;

namespace Adamantium.UI.Resources;

public interface IStyleSet : IContainer, IName
{
    public StylesCollection Styles { get; }
    
    public Uri Source { get; set; }

    void Initialize(ITheme theme);

    bool Initialized { get; }

    public void AddStyles(IEnumerable<Style> styles);
    
    public void Add(Style style);
}