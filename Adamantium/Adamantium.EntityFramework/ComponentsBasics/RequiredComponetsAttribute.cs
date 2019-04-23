using System;

namespace Adamantium.EntityFramework.ComponentsBasics
{
   [AttributeUsage(AttributeTargets.Class)]
   public class RequiredComponetsAttribute:Attribute
   {
      public Type[] Components { get; }

      public RequiredComponetsAttribute(params Type[] components)
      {
         Components = components;
      }
   }
}
