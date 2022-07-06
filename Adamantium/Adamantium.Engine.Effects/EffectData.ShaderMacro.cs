using System;

namespace Adamantium.Engine.Effects
{
   public partial class EffectData
   {
      public struct ShaderMacro : IEquatable<ShaderMacro>
      {
         /// <summary>
         /// The name of the macro.
         /// </summary>
         public string Name;

         /// <summary>
         /// The value of the macro.
         /// </summary>
         public string Value;

         /// <summary>
         /// Initializes a new instance of the <see cref="ShaderMacro" /> struct.
         /// </summary>
         /// <param name="name">The name.</param>
         /// <param name="value">The value.</param>
         public ShaderMacro(string name, object value)
         {
            Name = name;
            Value = value == null ? null : value.ToString();
         }

         public bool Equals(ShaderMacro other)
         {
            return string.Equals(this.Name, other.Name) && string.Equals(this.Value, other.Value);
         }

         public override bool Equals(object obj)
         {
            if (ReferenceEquals(null, obj))
               return false;
            return obj is ShaderMacro && Equals((ShaderMacro)obj);
         }

         public override int GetHashCode()
         {
            unchecked
            {
               return ((this.Name != null ? this.Name.GetHashCode() : 0) * 397) ^ (this.Value != null ? this.Value.GetHashCode() : 0);
            }
         }

         public static bool operator ==(ShaderMacro left, ShaderMacro right)
         {
            return left.Equals(right);
         }

         public static bool operator !=(ShaderMacro left, ShaderMacro right)
         {
            return !left.Equals(right);
         }
      }
   }
}
