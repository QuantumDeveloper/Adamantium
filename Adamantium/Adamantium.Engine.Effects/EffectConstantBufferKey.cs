using System;
using Adamantium.Engine.Core.Effects;

namespace Adamantium.Engine.Graphics
{
   internal class EffectConstantBufferKey : IEquatable<EffectConstantBufferKey>
   {
      public readonly EffectData.ConstantBuffer Description;
      public readonly int HashCode;

      public EffectConstantBufferKey(EffectData.ConstantBuffer description)
      {
         Description = description;
         HashCode = description.GetHashCode();
      }

      public bool Equals(EffectConstantBufferKey other)
      {
         if (ReferenceEquals(null, other)) return false;
         if (ReferenceEquals(this, other)) return true;
         return HashCode == other.HashCode && Description.Equals(other.Description);
      }

      public override bool Equals(object obj)
      {
         if (ReferenceEquals(null, obj)) return false;
         if (ReferenceEquals(this, obj)) return true;
         if (obj.GetType() != this.GetType()) return false;
         return Equals((EffectConstantBufferKey)obj);
      }

      public override int GetHashCode()
      {
         return HashCode;
      }

      public static bool operator ==(EffectConstantBufferKey left, EffectConstantBufferKey right)
      {
         return Equals(left, right);
      }

      public static bool operator !=(EffectConstantBufferKey left, EffectConstantBufferKey right)
      {
         return !Equals(left, right);
      }
   }
}
