using System.Collections;
using System.Collections.Generic;

namespace Adamantium.Engine.Core.Effects
{
    public sealed partial class EffectData
    {
        public sealed class Pipeline : IEnumerable<ShaderLink>
        {
            private const int ShaderLinkCount = 6;

            public ShaderLink[] Links;

            public List<ShaderLink> Links2;

            /// <summary>
            /// Initializes a new instance of the <see cref="Pipeline" /> class.
            /// </summary>
            public Pipeline()
            {
                Links = new ShaderLink[ShaderLinkCount];
                Links2 = new List<ShaderLink>();
            }

            /// <summary>
            /// Clones this instance.
            /// </summary>
            /// <returns>Pipeline.</returns>
            public Pipeline Clone()
            {
                var pipeline = (Pipeline)MemberwiseClone();
                pipeline.Links = new ShaderLink[Links.Length];
                for (int i = 0; i < Links.Length; i++)
                {
                    var link = Links[i];
                    if (link != null)
                    {
                        pipeline.Links[i] = link.Clone();
                    }
                }
                return pipeline;
            }

            private void OnDeserializationCallback()
            {
                var temp = Links;
                Links = new ShaderLink[ShaderLinkCount];
                for (int i = 0; i < temp.Length; i++)
                {
                    Links[(int)temp[i].ShaderType] = temp[i];
                }
            }

            /// <summary>
            /// Gets or sets the <see cref="ShaderLink" /> with the specified stage type.
            /// </summary>
            /// <param name="effectShaderType">Type of the stage.</param>
            /// <returns>A <see cref="ShaderLink"/></returns>
            /// <remarks>
            /// The return value can be null if there is no shaders associated for this particular stage.
            /// </remarks>
            public ShaderLink this[EffectShaderType effectShaderType]
            {
                get { return Links[(int)effectShaderType]; }
                set
                {
                    if (value != null)
                    {
                        //Added this for 2 reasons:
                        //1 - know which typw of shader is in Links array
                        //2 - restore Links[] at their position after deserialization (protobuf-net does not support writing nulls by default )
                        value.ShaderType = effectShaderType;
                    }
                    Links[(int)effectShaderType] = value;
                }
            }

            public IEnumerator<ShaderLink> GetEnumerator()
            {
                foreach (var shaderLink in Links)
                    yield return shaderLink;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
