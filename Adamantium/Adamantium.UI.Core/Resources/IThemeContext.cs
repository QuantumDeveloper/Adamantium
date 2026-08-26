namespace Adamantium.UI.Core.Resources;

public interface IThemeContext
{
    void ApplyCurrentTheme(IFundamentalUIComponent control);
    void ApplyStyles(IFundamentalUIComponent component);
    void ApplyExternalStyles(IFundamentalUIComponent control, params ReadOnlySpan<Style> styles);
}