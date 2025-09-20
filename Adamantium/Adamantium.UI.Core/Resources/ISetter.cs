namespace Adamantium.UI.Core.Resources;

public interface ISetter
{
   string Property { get; set; }
   
   Object Value { get; set; }
   
   public string TargetName { get; set; }
   
   void Apply(IFundamentalUIComponent component, Style style, ITheme theme);
   
   void Remove(IFundamentalUIComponent component, Style style, ITheme theme);
}