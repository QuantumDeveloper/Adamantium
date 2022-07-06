using System;
using Adamantium.Core;

namespace Adamantium.Engine.Effects
{
   public partial class EffectData
   {
      public sealed class ValueTypeParameter : Parameter, IEquatable<ValueTypeParameter>
      {
         /// <summary>
         /// Offset in bytes into the <see cref="ConstantBuffer"/>.
         /// </summary>
         public uint Offset;

         /// <summary>
         /// Number of elements.
         /// </summary>
         public uint Count;

         /// <summary>
         /// Size in bytes in the <see cref="ConstantBuffer"/>.
         /// </summary>
         public int Size;

         /// <summary>
         /// Number of rows for this element.
         /// </summary>
         public byte RowCount;

         /// <summary>
         /// Number of columns for this element.
         /// </summary>
         public byte ColumnCount;

         /// <summary>
         /// The default value.
         /// </summary>
         public byte[] DefaultValue;

         public bool Equals(ValueTypeParameter other)
         {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) 
               && Offset == other.Offset 
               && Count == other.Count 
               && Size == other.Size 
               && RowCount == other.RowCount 
               && ColumnCount == other.ColumnCount 
               && Utilities.Compare(DefaultValue, other.DefaultValue);
         }

         public override bool Equals(object obj)
         {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is ValueTypeParameter && Equals((ValueTypeParameter)obj);
         }

         public override int GetHashCode()
         {
            unchecked
            {
               int hashCode = base.GetHashCode();
               hashCode = (hashCode * 397) ^ (int)Offset;
               hashCode = (hashCode * 397) ^ (int)Count;
               hashCode = (hashCode * 397) ^ Size;
               hashCode = (hashCode * 397) ^ RowCount.GetHashCode();
               hashCode = (hashCode * 397) ^ ColumnCount.GetHashCode();
               hashCode = (hashCode * 397) ^ ((DefaultValue == null) ? 0 : DefaultValue.Length);
               return hashCode;
            }
         }

         public static bool operator ==(ValueTypeParameter left, ValueTypeParameter right)
         {
            return Equals(left, right);
         }

         public static bool operator !=(ValueTypeParameter left, ValueTypeParameter right)
         {
            return !Equals(left, right);
         }
      }
   }
}
