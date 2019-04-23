using System;

namespace Adamantium.UI.Controls
{
   public struct GridLength:IEquatable<GridLength>
   {
      public GridLength(double value, GridUnitType unitType)
      {
         if (value < 0 || double.IsNaN(value) || double.IsInfinity(value))
         {
            throw new ArgumentException("Invalid value", nameof(value));
         }
         Value = value;
         this.unitType = unitType;
      }

      public GridLength(double value):this(value, GridUnitType.Pixel)
      {
      }

      private GridUnitType unitType;

      public GridUnitType GridUnitType=>unitType;

      public Double Value { get; private set; }

      public static GridLength Auto => new GridLength(0, GridUnitType.Auto);

      public Boolean IsAbsolute => unitType == GridUnitType.Pixel;

      public Boolean IsAuto => unitType == GridUnitType.Auto;

      public Boolean IsStar => unitType == GridUnitType.Star;

      public static bool operator ==(GridLength left, GridLength right)
      {
         return (left.IsAuto && right.IsAuto) || (left.Value == right.Value && left.unitType == right.unitType);
      }

      public static bool operator !=(GridLength left, GridLength right)
      {
         return !(left == right);
      }

      public bool Equals(GridLength other)
      {
         return this == other;
      }

      public override bool Equals(object obj)
      {
         if (!(obj is GridLength))
         {
            return false;
         }

         return this == (GridLength) obj;
      }

      public override string ToString()
      {
         if (IsAuto)
         {
            return "Auto";
         }

         return IsStar ? Value + "*" : Value.ToString();
      }
   }
}
