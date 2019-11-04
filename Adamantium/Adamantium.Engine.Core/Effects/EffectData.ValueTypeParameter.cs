using System;
using ProtoBuf;
using SharpDX;

namespace Adamantium.Engine.Core.Effects
{
   public partial class EffectData
   {
      [ProtoContract]
      public sealed class ValueTypeParameter : Parameter, IEquatable<ValueTypeParameter>
      {
         /// <summary>
         /// Offset in bytes into the <see cref="ConstantBuffer"/>.
         /// </summary>
         [ProtoMember(1)]
         public int Offset;

         /// <summary>
         /// Number of elements.
         /// </summary>
         [ProtoMember(2)]
         public int Count;

         /// <summary>
         /// Size in bytes in the <see cref="ConstantBuffer"/>.
         /// </summary>
         [ProtoMember(3)]
         public int Size;

         /// <summary>
         /// Number of rows for this element.
         /// </summary>
         [ProtoMember(4)]
         public byte RowCount;

         /// <summary>
         /// Number of columns for this element.
         /// </summary>
         [ProtoMember(5)]
         public byte ColumnCount;

         /// <summary>
         /// The default value.
         /// </summary>
         [ProtoMember(6)]
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
               hashCode = (hashCode * 397) ^ Offset;
               hashCode = (hashCode * 397) ^ Count;
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
