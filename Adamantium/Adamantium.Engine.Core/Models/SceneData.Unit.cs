using System;

namespace Adamantium.Engine.Core.Models
{
   public partial class SceneData
   {
      public class Unit
      {
         public Unit()
         {
            UnitType = UnitType.Meter;
            Value = 1.0f;
         }

         public Unit(Unit copy)
         {
            if (copy != null)
            {
               UnitType = copy.UnitType;
               Value = copy.Value;
            }
         }

         public Unit(UnitType unitType, Single value)
         {
            UnitType = unitType;
            Value = value;
         }

         public UnitType UnitType { get; set; }
         public Single Value { get; set; }
      }
   }
}
