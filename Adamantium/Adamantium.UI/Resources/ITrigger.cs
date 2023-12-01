namespace Adamantium.UI.Resources;

public interface ITrigger
{
   SetterCollection Setters { get; set; }
   void Apply(IFundamentalUIComponent uiComponent, ITheme theme);
   void Remove(IFundamentalUIComponent uiComponent);
}