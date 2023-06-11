using System.Collections;
using System.Collections.Generic;
using MessagePack;

namespace Adamantium.Engine.Effects
{
    public sealed partial class EffectData
    {
        [MessagePackObject]
        public sealed class Pipeline : IEnumerable<ShaderLink>
        {
            //public ShaderLink[] Links;
            [Key(0)]
            public Dictionary<EffectShaderType, ShaderLink> Links;

            /// <summary>
            /// Initializes a new instance of the <see cref="Pipeline" /> class.
            /// </summary>
            public Pipeline()
            {
                //Links = new ShaderLink[ShaderLinkCount];
                Links = new Dictionary<EffectShaderType, ShaderLink>();
            }

            /// <summary>
            /// Clones this instance.
            /// </summary>
            /// <returns>Pipeline.</returns>
            public Pipeline Clone()
            {
                var pipeline = (Pipeline)MemberwiseClone();
                pipeline.Links = new Dictionary<EffectShaderType, ShaderLink>();
                foreach (var link in Links)
                {
                    pipeline.Links[link.Key] = link.Value.Clone();
                }
                return pipeline;
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
                get
                {
                    if (Links.ContainsKey(effectShaderType))
                    {
                        return Links[effectShaderType]; 
                    }

                    return null;
                }
                set
                {
                    if (value == null) return;

                    value.ShaderType = effectShaderType;
                    Links[effectShaderType] = value;
                }
            }

            public IEnumerator<ShaderLink> GetEnumerator()
            {
                foreach (var shaderLink in Links)
                    yield return shaderLink.Value;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
