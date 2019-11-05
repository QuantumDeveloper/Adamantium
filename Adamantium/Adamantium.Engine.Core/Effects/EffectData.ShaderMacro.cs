using System;

namespace Adamantium.Engine.Core.Effects
{
   public partial class EffectData
   {
      public struct ShaderMacro : IEquatable<SharpDX.Direct3D.ShaderMacro>
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

         public bool Equals(SharpDX.Direct3D.ShaderMacro other)
         {
            return string.Equals(this.Name, other.Name) && string.Equals(this.Value, other.Definition);
         }

         public override bool Equals(object obj)
         {
            if (ReferenceEquals(null, obj))
               return false;
            return obj is SharpDX.Direct3D.ShaderMacro && Equals((SharpDX.Direct3D.ShaderMacro)obj);
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
