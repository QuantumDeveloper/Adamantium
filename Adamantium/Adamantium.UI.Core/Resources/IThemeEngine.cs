namespace Adamantium.UI.Core.Resources;

/// <summary>What APPLIES a theme's styles to an element. Named for what it does, and named as a subsystem beside
/// <c>BindingEngine</c> - the point of the rename was to stop it sitting next to <see cref="ThemeContext"/>, which is
/// the theme in force AT a place in the tree. One applies, the other answers "which theme applies here"; two names an
/// apart would have been a trap for every later reader.</summary>
public interface IThemeEngine
{
    void ApplyCurrentTheme(IFundamentalUIComponent control);
    void ApplyStyles(IFundamentalUIComponent component);
    void ApplyExternalStyles(IFundamentalUIComponent control, params ReadOnlySpan<Style> styles);
}