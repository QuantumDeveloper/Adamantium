using System;

namespace Adamantium.ECS.Components
{
   [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
   public class RequiredComponentAttribute:Attribute
   {
      public Type Component { get; }

      public RequiredComponentAttribute(Type components)
      {
         Component = components;
      }
   }
}
