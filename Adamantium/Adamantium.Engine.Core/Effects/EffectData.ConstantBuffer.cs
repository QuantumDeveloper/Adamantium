using System;
using System.Collections.Generic;
using Adamantium.Core;
using ProtoBuf;

namespace Adamantium.Engine.Core.Effects
{
   public partial class EffectData
   {
      [ProtoContract]
      public sealed class ConstantBuffer : IEquatable<ConstantBuffer>
      {
         /// <summary>
         /// Name of this constant buffer.
         /// </summary>
         [ProtoMember(1)]
         public string Name;

         /// <summary>
         /// Size in bytes of this constant buffer.
         /// </summary>
         [ProtoMember(2)]
         public int Size;

         /// <summary>
         /// List of parameters in this constant buffer.
         /// </summary>
         [ProtoMember(3)]
         public List<ValueTypeParameter> Parameters;

         public bool Equals(ConstantBuffer other)
         {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Name, other.Name) && Size == other.Size &&
                   Utilities.Compare(Parameters, other.Parameters);
         }

         public override bool Equals(object obj)
         {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is ConstantBuffer && Equals((ConstantBuffer) obj);
         }

         public override int GetHashCode()
         {
            unchecked
            {
               int hashCode = Name.GetHashCode();
               hashCode = (hashCode*397) ^ Size;
               hashCode = (hashCode*397) ^ Parameters.Count;
               return hashCode;
            }
         }

         public static bool operator ==(ConstantBuffer left, ConstantBuffer right)
         {
            return Equals(left, right);
         }

         public static bool operator !=(ConstantBuffer left, ConstantBuffer right)
         {
            return !Equals(left, right);
         }
      }
   }
}
