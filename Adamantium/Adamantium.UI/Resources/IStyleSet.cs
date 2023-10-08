using Adamantium.Engine.Core;
using System;

namespace Adamantium.UI.Resources;

public interface IStyleSet : IContainer, IName
{
    public StylesCollection Styles { get; }
    
    public Uri Source { get; set; }

    void Initialize(ITheme theme);

    bool Initialized { get; }
}