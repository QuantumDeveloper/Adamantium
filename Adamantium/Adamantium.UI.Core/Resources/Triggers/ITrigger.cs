namespace Adamantium.UI.Core.Resources.Triggers;

public interface ITrigger
{
   SetterCollection Setters { get; set; }
   void Apply(IFundamentalUIComponent uiComponent, ITheme theme);
   void Remove(IFundamentalUIComponent uiComponent);
}