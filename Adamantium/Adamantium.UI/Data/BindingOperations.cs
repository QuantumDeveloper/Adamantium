using System;
using System.Collections.Generic;

namespace Adamantium.UI.Data
{
   public static class BindingOperations
   {
      private static Dictionary<DependencyComponent, List<AdamantiumProperty>> objectToProperty =
         new Dictionary<DependencyComponent, List<AdamantiumProperty>>();

      private static Dictionary<DependencyComponent, List<BindingExpressionBase>> objectToExpression =
         new Dictionary<DependencyComponent, List<BindingExpressionBase>>();

      public static BindingExpressionBase SetBinding(DependencyComponent o, AdamantiumProperty property,
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
}
