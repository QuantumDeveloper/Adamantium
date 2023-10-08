using Adamantium.UI.Resources;
using System;

namespace Adamantium.UI;

public interface ISetter
{
   string Property { get; set; }
   
   Object Value { get; set; }
   
   void Apply(IFundamentalUIComponent control, ITheme theme);
   
   void UnApply(IFundamentalUIComponent control, ITheme theme);
}