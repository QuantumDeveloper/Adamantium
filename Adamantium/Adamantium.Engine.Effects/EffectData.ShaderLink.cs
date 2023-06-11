using System;
using MessagePack;

namespace Adamantium.Engine.Effects
{
    public sealed partial class EffectData
    {
        [MessagePackObject]
        public sealed class ShaderLink : IEquatable<ShaderLink>
        {
            public static readonly ShaderLink NullShader = new ShaderLink();

            /// <summary>
            /// The stream output rasterized stream (-1 if no rasterized stream).
            /// </summary>
            [IgnoreMember]
            public int StreamOutputRasterizedStream;

            /// <summary>
            /// The stream output elements only valid for a geometry shader, can be null.
            /// </summary>
            //public StreamOutputElement[] StreamOutputElements;

            /// <summary>
            /// Initializes a new instance of the <see cref="ShaderLink" /> class.
            /// </summary>
            public ShaderLink()
            {
                Index = -1;
                StreamOutputRasterizedStream = -1;
            }

            /// <summary>
            /// Gets a value indicating whether this is an import.
            /// </summary>
            /// <value><c>true</c> if this is an import; otherwise, <c>false</c>.</value>
            /// <remarks>
            /// When this is an import, the <see cref="Index"/> is not valid. Only <see cref="ImportName"/> is valid.
            /// </remarks>
            [IgnoreMember]
            public bool IsImport => !String.IsNullOrEmpty(ImportName);

            /// <summary>
            /// Gets or sets the index in the shader pool.
            /// </summary>
            /// <value>The index.</value>
            /// <remarks>
            /// This index is a direct reference to the shader in <see cref="EffectData.Shaders"/>.
            /// </remarks>
            [Key(0)]
            public int Index { get; set; }

            /// <summary>
            /// Gets or sets the entrypoint shader name for further usage.
            /// </summary>
            [Key(1)]
            public string EntryPoint { get; set; }

            /// <summary>
            /// Gets or sets the name of the shader import. Can be null.
            /// </summary>
            /// <value>The name of the import.</value>
            /// <remarks>
            /// This property is not null when there is no shader compiled and this is an import.
            /// </remarks>
            [Key(2)]
            public string ImportName { get; set; }

            [Key(3)]
            public EffectShaderType ShaderType;

            /// <summary>
            /// Gets a value indicating whether this instance is a null shader.
            /// </summary>
            /// <value><c>true</c> if this instance is null shader; otherwise, <c>false</c>.</value>
            [IgnoreMember]
            public bool IsNullShader => Index < 0;

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
                return this.Index == other.Index && string.Equals(this.EntryPoint, other.EntryPoint);
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
                    return (this.Index * 397) ^ (EntryPoint != null ? EntryPoint.GetHashCode() : 0);
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
