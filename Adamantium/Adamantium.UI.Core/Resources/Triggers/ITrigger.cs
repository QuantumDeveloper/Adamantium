namespace Adamantium.UI.Core.Resources.Triggers;

public interface ITrigger
{
   SetterCollection Setters { get; set; }
   void Add(ISetter setter);
   void Apply(IFundamentalUIComponent uiComponent, ITheme theme);
   void Remove(IFundamentalUIComponent uiComponent);
}