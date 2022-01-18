using System;
using System.Collections.Generic;
using Adamantium.UI.Controls;

namespace Adamantium.UI.Data;

public static class BindingOperations
{
   private static Dictionary<AdamantiumComponent, List<AdamantiumProperty>> objectToProperty =
      new Dictionary<AdamantiumComponent, List<AdamantiumProperty>>();

   private static Dictionary<AdamantiumComponent, List<BindingExpressionBase>> objectToExpression =
      new Dictionary<AdamantiumComponent, List<BindingExpressionBase>>();

   public static BindingExpressionBase SetBinding(AdamantiumComponent o, AdamantiumProperty property,
      BindingBase binding)
   {
      ArgumentNullException.ThrowIfNull(o);
      ArgumentNullException.ThrowIfNull(property);
      ArgumentNullException.ThrowIfNull(binding);
      
      if (binding is Binding bind)
      {
         var expression = new BindingExpression();
      }
      return null;
   }

   private static BindingExpression ParsePathString(Binding binding)
   {
      BindingExpression expression = new BindingExpression();


      return expression;
   }

}