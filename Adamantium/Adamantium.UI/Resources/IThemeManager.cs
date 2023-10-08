using System.Collections.Generic;

namespace Adamantium.UI.Resources;

public interface IThemeManager
{
    ITheme CurrentTheme { get; }

    void AddTheme(string name, ITheme theme);

    void RemoveTheme(string name);

    void ApplyTheme(ITheme theme);
    
    void ApplyTheme(string name);

    void ApplyStyles(IFundamentalUIComponent component);
    
    void RemoveStyles(IFundamentalUIComponent component);

    IEnumerable<Style> FindStylesForComponent(IFundamentalUIComponent component);

    ITheme this[string name] { get; }
    
    ITheme this[int index] { get; }
}