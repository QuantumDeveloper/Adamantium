using System;

namespace Adamantium.UI.Resources;

public interface ISetter
{
   string Property { get; set; }
   
   Object Value { get; set; }
   
   void Apply(IFundamentalUIComponent component, Style style, ITheme theme);
   
   void Remove(IFundamentalUIComponent component, Style style, ITheme theme);
}