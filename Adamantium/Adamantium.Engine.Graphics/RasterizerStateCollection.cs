using AdamantiumVulkan.Core;
using System;

namespace Adamantium.Engine.Graphics
{
    /// <summary>
    /// Rasterizer state collection.
    /// </summary>
    public class RasterizerStateCollection : StateCollectionBase<RasterizerState>
    {
        /// <summary>
        /// Built-in rasterizer state object with settings for wireframe rendering.
        /// </summary>
        public readonly RasterizerState WireFrameCullBackClipEnabled;

        /// <summary>
        /// Built-in rasterizer state object with settings for wireframe rendering.
        /// </summary>
        public readonly RasterizerState WireFrameCullNoneClipEnabled;

        /// <summary>
        /// Built-in rasterizer state object with settings for culling primitives with clockwise winding order (front facing).
        /// </summary>
        public readonly RasterizerState CullBackClipEnabled;

        /// <summary>
        /// Built-in rasterizer state object with settings for culling primitives with counter-clockwise winding order (back facing).
        /// </summary>
        public readonly RasterizerState CullFrontClipEnabled;

        /// <summary>
        /// Built-in rasterizer state object with settings for not culling any primitives.
        /// </summary>
        public readonly RasterizerState CullNoneClipEnabled;

        /// <summary>
        /// Built-in rasterizer state object with settings for culling primitives with clockwise winding order (front facing).
        /// DepthClip enabled
        /// </summary>
        public readonly RasterizerState CullBackScissorOnClipEnabled;

        /// <summary>
        /// Built-in rasterizer state object with settings for culling primitives with clockwise winding order (front facing)
        /// and with disabled depth clipping
        /// </summary>
        public readonly RasterizerState CullBackClipDisabled;

        /// <summary>
        /// Built-in rasterizer state object with settings for culling primitives with clockwise winding order (back facing)
        /// and with disabled depth clipping
        /// </summary>
        public readonly RasterizerState CullFrontClipDisabled;

        /// <summary>
        /// Built-in rasterizer state object with settings for culling primitives with clockwise winding order (front facing).
        /// and with disabled depth clipping
        /// </summary>
        public readonly RasterizerState CullNoneClipDisabled;

        /// <summary>
        /// Built-in rasterizer state object with settings for wireframe rendering.
        /// and with disabled depth clipping
        /// </summary>
        public readonly RasterizerState WireFrameCullBackClipDisabled;

        /// <summary>
        /// Built-in rasterizer state object with settings for wireframe rendering.
        /// and with disabled depth clipping
        /// </summary>
        public readonly RasterizerState WireFrameCullNoneClipDisabled;


        /// <summary>
        /// Built-in default rasterizer state object is back facing (see <see cref="CullNoneClipDisabled"/>).
        /// </summary>
        public readonly RasterizerState Default;

        /// <summary>
        /// Initializes a new instance of the <see cref="RasterizerStateCollection" /> class.
        /// </summary>
        /// <param name="graphicsDevice">The device.</param>
        internal RasterizerStateCollection()
        {
            WireFrameCullBackClipEnabled = Add(RasterizerState.New(nameof(WireFrameCullBackClipEnabled), CullModeFlagBits.BackBit, PolygonMode.Line, FrontFace.Clockwise, true));
            WireFrameCullNoneClipEnabled = Add(RasterizerState.New(nameof(WireFrameCullNoneClipEnabled), CullModeFlagBits.None, PolygonMode.Line, FrontFace.Clockwise, true));
            CullBackClipEnabled = Add(RasterizerState.New(nameof(CullBackClipEnabled), CullModeFlagBits.BackBit, PolygonMode.Fill, FrontFace.Clockwise, true));
            CullFrontClipEnabled = Add(RasterizerState.New(nameof(CullFrontClipEnabled), CullModeFlagBits.FrontBit, PolygonMode.Fill, FrontFace.Clockwise, true));
            CullNoneClipEnabled = Add(RasterizerState.New(nameof(CullNoneClipEnabled), CullModeFlagBits.None, PolygonMode.Fill, FrontFace.Clockwise, true));

            WireFrameCullBackClipDisabled = Add(RasterizerState.New(nameof(WireFrameCullBackClipDisabled), CullModeFlagBits.BackBit, PolygonMode.Line));
            WireFrameCullNoneClipDisabled = Add(RasterizerState.New(nameof(WireFrameCullNoneClipDisabled), CullModeFlagBits.None, PolygonMode.Line));
            CullBackClipDisabled = Add(RasterizerState.New(nameof(CullBackClipDisabled), CullModeFlagBits.BackBit, PolygonMode.Fill));
            CullFrontClipDisabled = Add(RasterizerState.New(nameof(CullFrontClipDisabled), CullModeFlagBits.FrontBit, PolygonMode.Fill));
            CullNoneClipDisabled = Add(RasterizerState.New(nameof(CullNoneClipDisabled), CullModeFlagBits.None, PolygonMode.Fill));

            Default = CullNoneClipDisabled;
        }
    }
}
