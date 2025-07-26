namespace Adamantium.UI.Core.Resources;

public interface IThemeManager : IThemeContext
{
    ITheme CurrentTheme { get; }

    void AddTheme(string name, ITheme theme);

    void RemoveTheme(string name);

    void SetTheme(ITheme theme);

    void ApplyTheme(ITheme theme, IFundamentalUIComponent component);
    
    void ApplyTheme(string name, IFundamentalUIComponent component);

    void RemoveStyles(IFundamentalUIComponent component);

    Style[] FindStylesForComponent(IFundamentalUIComponent component);

    ITheme this[string name] { get; }
    
    ITheme this[int index] { get; }
}