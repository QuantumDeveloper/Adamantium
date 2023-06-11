using System;
using MessagePack;

namespace Adamantium.Engine.Effects
{
   public partial class EffectData
   {
      [MessagePackObject]
      public sealed class ResourceParameter : Parameter, IEquatable<ResourceParameter>
      {
         /// <summary>
         /// The slot index register to bind to.
         /// </summary>
         [Key(3)]
         public byte Slot;

         /// <summary>
         /// The number of slots to bind.
         /// </summary>
         [Key(4)]
         public byte Count;

         public bool Equals(ResourceParameter other)
         {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) && Slot == other.Slot && Count == other.Count;
         }

         public override bool Equals(object obj)
         {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is ResourceParameter && Equals((ResourceParameter)obj);
         }

         public override int GetHashCode()
         {
            unchecked
            {
               int hashCode = base.GetHashCode();
               hashCode = (hashCode * 397) ^ Slot.GetHashCode();
               hashCode = (hashCode * 397) ^ Count.GetHashCode();
               return hashCode;
            }
         }

         public static bool operator ==(ResourceParameter left, ResourceParameter right)
         {
            return Equals(left, right);
         }

         public static bool operator !=(ResourceParameter left, ResourceParameter right)
         {
            return !Equals(left, right);
         }
      }
   }
}
