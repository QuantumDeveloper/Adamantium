using System;
using ProtoBuf;
using SharpDX.Direct3D11;

namespace Adamantium.Engine.Core.Effects
{
   public sealed partial class EffectData
   {
      [ProtoContract(SkipConstructor = true)]
      public sealed class ShaderLink : IEquatable<ShaderLink>
      {
         public static readonly ShaderLink NullShader = new ShaderLink();

         private int index;
         private string importName;

         /// <summary>
         /// The stream output rasterized stream (-1 if no rasterized stream).
         /// </summary>
         [ProtoMember(1)]
         public int StreamOutputRasterizedStream;

         /// <summary>
         /// The stream output elements only valid for a geometry shader, can be null.
         /// </summary>
         [ProtoMember(2)]
         public StreamOutputElement[] StreamOutputElements;

         /// <summary>
         /// Initializes a new instance of the <see cref="ShaderLink" /> class.
         /// </summary>
         public ShaderLink()
         {
            index = -1;
            StreamOutputRasterizedStream = -1;
         }

         /// <summary>
         /// Gets a value indicating whether this is an import.
         /// </summary>
         /// <value><c>true</c> if this is an import; otherwise, <c>false</c>.</value>
         /// <remarks>
         /// When this is an import, the <see cref="Index"/> is not valid. Only <see cref="ImportName"/> is valid.
         /// </remarks>
         public bool IsImport => importName != null;

         /// <summary>
         /// Gets or sets the index in the shader pool.
         /// </summary>
         /// <value>The index.</value>
         /// <remarks>
         /// This index is a direct reference to the shader in <see cref="EffectData.Shaders"/>.
         /// </remarks>
         [ProtoMember(3)]
         public int Index
         {
            get { return index; }
            set { index = value; }
         }

         /// <summary>
         /// Gets or sets the name of the shader import. Can be null.
         /// </summary>
         /// <value>The name of the import.</value>
         /// <remarks>
         /// This property is not null when there is no shader compiled and this is an import.
         /// </remarks>
         [ProtoMember(4)]
         public string ImportName
         {
            get { return importName; }
            set { importName = value; }
         }

         [ProtoMember(5)]
         public EffectShaderType ShaderType;

         /// <summary>
         /// Gets a value indicating whether this instance is a null shader.
         /// </summary>
         /// <value><c>true</c> if this instance is null shader; otherwise, <c>false</c>.</value>
         public bool IsNullShader => index < 0;

         public ShaderLink Clone()
         {
            return (ShaderLink)MemberwiseClone();
         }

         public bool Equals(ShaderLink other)
         {
            if (ReferenceEquals(null, other))
               return false;
            if (ReferenceEquals(this, other))
               return true;
            return this.index == other.index && string.Equals(this.importName, other.importName);
         }

         public override bool Equals(object obj)
         {
            if (ReferenceEquals(null, obj))
               return false;
            if (ReferenceEquals(this, obj))
               return true;
            return obj is ShaderLink && Equals((ShaderLink)obj);
         }

         public override int GetHashCode()
         {
            unchecked
            {
               return (this.index * 397) ^ (this.importName != null ? this.importName.GetHashCode() : 0);
            }
         }

         public static bool operator ==(ShaderLink left, ShaderLink right)
         {
            return Equals(left, right);
         }

         public static bool operator !=(ShaderLink left, ShaderLink right)
         {
            return !Equals(left, right);
         }
      }
   }
}
