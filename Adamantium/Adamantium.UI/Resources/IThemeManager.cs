using System.Collections.Generic;

namespace Adamantium.UI.Resources;

public interface IThemeManager
{
    ITheme CurrentTheme { get; }

    void AddTheme(string name, ITheme theme);

    void RemoveTheme(string name);

    void ApplyTheme(ITheme theme, IUIComponent component);
    
    void ApplyTheme(string name, IUIComponent component);

    void ApplyStyles(IUIComponent component, params Style[] styles);

    void ApplyStyles(IUIComponent component);
    
    void RemoveStyles(IUIComponent component);

    IEnumerable<Style> FindStylesForComponent(IUIComponent component);

    ITheme this[string name] { get; }
    
    ITheme this[int index] { get; }
}