using System.Collections.Generic;
using Adamantium.Core;

namespace Adamantium.Engine.Core.Effects
{
    public sealed partial class EffectData
    {
        public sealed class Shader
        {
            /// <summary>
            /// Name of this shader, only valid for public shaders, else null.
            /// </summary>
            public string Name;

            /// <summary>
            /// Type of this shader.
            /// </summary>
            public EffectShaderType Type;

            /// <summary>
            /// Compiler flags used to compile this shader.
            /// </summary>
            public EffectCompilerFlags CompilerFlags;

            /// <summary>
            /// Level of this shader.
            /// </summary>
            public ShaderModel Level;

            /// <summary>
            /// Shader language HLSL/GLSL
            /// </summary>
            public ShaderLanguage Language;

            /// <summary>
            /// Bytecode of this shader.
            /// </summary>
            public byte[] Bytecode;

            /// <summary>
            /// Hashcode from the bytecode.
            /// </summary>
            /// <remarks>
            /// Shaders with same bytecode with have same hashcode.
            /// </remarks>
            public int Hashcode;

            /// <summary>
            /// Description of the input <see cref="Signature"/>.
            /// </summary>
            public Signature InputSignature;

            /// <summary>
            /// Description of the output <see cref="Signature"/>.
            /// </summary>
            public Signature OutputSignature;

            /// <summary>
            /// List of constant buffers used by this shader.
            /// </summary>
            public List<ConstantBuffer> ConstantBuffers
            {
                get { return constantBuffers ??= new List<ConstantBuffer>(); }
                set => constantBuffers = value;
            }

            /// <summary>
            /// List of resource parameters used by this shader.
            /// </summary>
            public List<ResourceParameter> ResourceParameters
            {
                get { return resourceParameters ??= new List<ResourceParameter>(); }
                set => resourceParameters = value;
            }

            private List<ConstantBuffer> constantBuffers;
            private List<ResourceParameter> resourceParameters;

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

                if (!(Hashcode == other.Hashcode && Type.Equals(other.Type) && CompilerFlags.Equals(other.CompilerFlags) && Level.Equals(other.Level) && InputSignature.Equals(other.InputSignature) && OutputSignature.Equals(other.OutputSignature)))
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
