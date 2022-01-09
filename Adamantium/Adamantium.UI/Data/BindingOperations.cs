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
      if (o == null)
      {
         throw  new ArgumentNullException(nameof(o));
      }

      if (property == null)
      {
         throw new ArgumentNullException(nameof(property));
      }

      if (binding == null)
      {
         throw new ArgumentNullException(nameof(binding));
      }

      if (binding is Binding)
      {
         var bind = binding as Binding;
         //bind.


         BindingExpression expression = new BindingExpression();
            

      }
      return null;
   }

   private static BindingExpression ParsePathString(Binding binding)
   {
      BindingExpression expression = new BindingExpression();


      return expression;
   }

}