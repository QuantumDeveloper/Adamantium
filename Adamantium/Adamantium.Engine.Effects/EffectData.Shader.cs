using System.Collections.Generic;
using Adamantium.Core;
using MessagePack;

namespace Adamantium.Engine.Effects
{
    public sealed partial class EffectData
    {
        [MessagePackObject]
        public sealed class Shader
        {
            private List<ConstantBuffer> constantBuffers;
            private List<ResourceParameter> resourceParameters;
            
            /// <summary>
            /// Name of this shader.
            /// </summary>
            [Key(0)]
            public string Name;

            /// <summary>
            /// Entry point of this shader.
            /// </summary>
            [Key(1)]
            public string EntryPoint;

            /// <summary>
            /// Type of this shader.
            /// </summary>
            [Key(2)] 
            public EffectShaderType Type;

            /// <summary>
            /// Compiler flags used to compile this shader.
            /// </summary>
            [Key(3)]
            public EffectCompilerFlags CompilerFlags;

            /// <summary>
            /// Level of this shader.
            /// </summary>
            [Key(4)]
            public string Level;

            /// <summary>
            /// Bytecode of this shader.
            /// </summary>
            [Key(5)] 
            public byte[] Bytecode;

            /// <summary>
            /// Hashcode from the bytecode.
            /// </summary>
            /// <remarks>
            /// Shaders with same bytecode with have same hashcode.
            /// </remarks>
            [Key(6)]
            public int Hashcode;

            /// <summary>
            /// List of constant buffers used by this shader.
            /// </summary>
            [Key(7)]
            public List<ConstantBuffer> ConstantBuffers
            {
                get { return constantBuffers ??= new List<ConstantBuffer>(); }
                set => constantBuffers = value;
            }

            /// <summary>
            /// List of resource parameters used by this shader.
            /// </summary>
            [Key(8)]
            public List<ResourceParameter> ResourceParameters
            {
                get { return resourceParameters ??= new List<ResourceParameter>(); }
                set => resourceParameters = value;
            }

            public override string ToString()
            {
                return $"{(Name == null ? string.Empty : $"Name: {Name},")}Type: {Type} {Level}";
            }

            /// <summary>
            /// Check if this instance is similar to another Shader.
            /// </summary>
            /// <param name="other">The other instance to check against.</param>
            /// <returns>True if this instance is similar, false otherwise.</returns>
            /// <remarks>
            /// Except the name, all fields are checked for deep equality.
            /// </remarks>
            public bool IsSimilar(Shader other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;

                if (!(Hashcode == other.Hashcode && Type.Equals(other.Type) && CompilerFlags.Equals(other.CompilerFlags) && Level.Equals(other.Level)))
                    return false;

                if (!Utilities.Compare(Bytecode, other.Bytecode))
                    return false;

                if (!Utilities.Compare(ConstantBuffers, other.ConstantBuffers))
                    return false;

                if (!Utilities.Compare(ResourceParameters, other.ResourceParameters))
                    return false;

                // Shaders are similar
                return true;
            }
        }
    }
}
