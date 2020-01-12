using AdamantiumVulkan.Core;
using System;

namespace Adamantium.Engine.Graphics
{
    /// <summary>
    /// Blend state collection
    /// </summary>
    public class BlendStatesCollection : StateCollectionBase<BlendState>
    {
        /// <summary>
        /// A built-in state object with settings for additive blend, that is adding the destination data to the source data without using alpha.
        /// </summary>
        public readonly BlendState Additive;

        /// <summary>
        /// A built-in state object with settings for lightmap blend, that is adding the destination data to the source data using alpha.
        /// </summary>
        public readonly BlendState LightMap;

        /// <summary>
        /// A built-in state object with settings for alpha blend, that is blending the source and destination data using alpha.
        /// </summary>
        public readonly BlendState AlphaBlend;

        /// <summary>
        /// A built-in state object with settings for blending with non-premultiplied alpha, that is blending source and destination data using alpha while assuming the color data contains no alpha information.
        /// </summary>
        public readonly BlendState NonPremultiplied;

        /// <summary>
        /// A built-in state object with settings for opaque blend, that is overwriting the source with the destination data.
        /// </summary>
        public readonly BlendState Opaque;

        /// <summary>
        /// A built-in state object with settings for opaque blend, that is overwriting the source with the destination data and not writing to color channels at all.
        /// </summary>
        public readonly BlendState DoNotWriteToColorChannels;

        /// <summary>
        /// A built-in default state object (no blending).
        /// </summary>
        public readonly BlendState Default;

        internal BlendStatesCollection()
        {
            Additive = Add(CreateBlendState("Additive", BlendOption.SourceAlpha, BlendOption.One));
            LightMap = Add(CreateBlendState("LightMap", BlendOption.One, BlendOption.One));
            AlphaBlend = Add(CreateBlendState("AlphaBlend", BlendOption.One, BlendOption.InverseSourceAlpha));
            NonPremultiplied = Add(CreateBlendState("NonPremultiplied", BlendOption.SourceAlpha, BlendOption.InverseSourceAlpha));
            Opaque = Add(CreateBlendState("Opaque", BlendOption.One, BlendOption.Zero));
            Default = Add(CreateBlendState("Default", BlendStateDescription.Default()));
            DoNotWriteToColorChannels = Add(CreateBlendState("DoNotWriteToColorChannels", BlendOption.One, BlendOption.Zero, 0));
        }

        private BlendState CreateBlendState(String name, BlendOption sourceBlend, BlendOption destinationBlend, ColorWriteMaskFlags colorWriteMask = ColorWriteMaskFlags.All)
        {
            var description = PipelineColorBlendStateCreateInfo.Default();

            description.RenderTarget[0].IsBlendEnabled = true;
            description.RenderTarget[0].SourceBlend = sourceBlend;
            description.RenderTarget[0].DestinationBlend = destinationBlend;
            description.RenderTarget[0].SourceAlphaBlend = sourceBlend;
            description.RenderTarget[0].DestinationAlphaBlend = destinationBlend;
            description.RenderTarget[0].RenderTargetWriteMask = colorWriteMask;

            return BlendState.New(name, description);
        }

        private BlendState CreateBlendState(string name, PipelineColorBlendStateCreateInfo description)
        {
            return BlendState.New(name, description);
        }
    }
}
