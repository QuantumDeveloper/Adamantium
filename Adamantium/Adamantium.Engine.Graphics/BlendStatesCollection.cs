using AdamantiumVulkan.Core;
using System;

namespace Adamantium.Engine.Graphics
{
    /// <summary>
    /// Blend state collection
    /// </summary>
    public class BlendStatesCollection : StateCollectionBase<BlendState>
    {
        private const ColorComponentFlagBits DefaultColorWriteMask =
            ColorComponentFlagBits.RBit | 
            ColorComponentFlagBits.GBit | 
            ColorComponentFlagBits.BBit |
            ColorComponentFlagBits.ABit;
        
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

        public readonly BlendState Fonts;

        internal BlendStatesCollection()
        {
            Fonts = Add(BlendState.New(nameof(Fonts), true, BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha, BlendOp.Add, DefaultColorWriteMask));
            Additive = Add(BlendState.New(nameof(Additive), true, BlendFactor.SrcAlpha, BlendFactor.One, BlendOp.Add, DefaultColorWriteMask));
            LightMap = Add(BlendState.New(nameof(LightMap), true, BlendFactor.One, BlendFactor.One, BlendOp.Add, DefaultColorWriteMask));
            AlphaBlend = Add(BlendState.New(nameof(AlphaBlend), true, BlendFactor.One, BlendFactor.OneMinusSrcAlpha, BlendOp.Add, DefaultColorWriteMask));
            NonPremultiplied = Add(BlendState.New(nameof(NonPremultiplied), true, BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha, BlendOp.Add, DefaultColorWriteMask));
            Opaque = Add(BlendState.New(nameof(Opaque), false, BlendFactor.One, BlendFactor.Zero, BlendOp.Add, ColorComponentFlagBits.ABit));
            Default = Add(BlendState.New(nameof(Default), PipelineColorBlendStateCreateInfo.Default()));
            //DoNotWriteToColorChannels = Add(BlendState.New(nameof(DoNotWriteToColorChannels), false, BlendFactor.One, BlendFactor.Zero, BlendOp.Add, DefaultColorWriteMask));
        }
    }
}
