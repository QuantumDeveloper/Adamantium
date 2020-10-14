using Adamantium.Core;
using AdamantiumVulkan.Core;
using System;

namespace Adamantium.Engine.Graphics
{
    public class RasterizerState : NamedObject
    {
        private RasterizerState(string name, PipelineRasterizationStateCreateInfo info)
        {
            Name = name;
            this.info = (PipelineRasterizationStateCreateInfo)info.Clone();

            DepthClampEnable = info.DepthClampEnable;
            RasterizerDiscardEnable = info.RasterizerDiscardEnable;
            PolygonMode = info.PolygonMode;
            CullMode = (CullModeFlagBits)info.CullMode;
            FrontFace = info.FrontFace;
            DepthBiasEnable = info.DepthBiasEnable;
            DepthBiasConstantFactor = info.DepthBiasConstantFactor;
            DepthBiasClamp = info.DepthBiasClamp;
            DepthBiasSlopeFactor = info.DepthBiasSlopeFactor;
            LineWidth = info.LineWidth;

            hashCode = CalculateHashCode();
        }

        private PipelineRasterizationStateCreateInfo info;

        private readonly int hashCode;

        public bool DepthClampEnable { get; }
        public bool RasterizerDiscardEnable { get; }
        public PolygonMode PolygonMode { get; }
        public CullModeFlagBits CullMode { get; }
        public FrontFace FrontFace { get; }
        public bool DepthBiasEnable { get; }
        public float DepthBiasConstantFactor { get; }
        public float DepthBiasClamp { get; }
        public float DepthBiasSlopeFactor { get; }
        public float LineWidth { get; }

        private int CalculateHashCode()
        {
            int hash = DepthClampEnable.GetHashCode();
            hash = (hash * 397) ^ RasterizerDiscardEnable.GetHashCode();
            hash = (hash * 397) ^ PolygonMode.GetHashCode();
            hash = (hash * 397) ^ CullMode.GetHashCode();
            hash = (hash * 397) ^ FrontFace.GetHashCode();
            hash = (hash * 397) ^ DepthBiasEnable.GetHashCode();
            hash = (hash * 397) ^ DepthBiasConstantFactor.GetHashCode();
            hash = (hash * 397) ^ DepthBiasClamp.GetHashCode();
            hash = (hash * 397) ^ DepthBiasSlopeFactor.GetHashCode();
            hash = (hash * 397) ^ LineWidth.GetHashCode();

            return hash;
        }

        public override int GetHashCode()
        {
            return hashCode;
        }


        public static implicit operator PipelineRasterizationStateCreateInfo (RasterizerState state)
        {
            return state.info;
        }

        public static RasterizerState New(string name, PipelineRasterizationStateCreateInfo info)
        {
            return new RasterizerState(name, info);
        }

        public static RasterizerState New(
            String name, 
            CullModeFlagBits cullMode, 
            PolygonMode polygonMode, 
            FrontFace frontFace = FrontFace.Clockwise, 
            Boolean depthClampEnable = false, 
            Boolean rasterizerDiscardEnable = false, 
            Single lineWidth = 1.0f, 
            Boolean depthBiasEnable = false, 
            Single depthBiasConstantFactor = 0, 
            Single depthBiasClamp = 0, 
            Single depthBiasSlopeFactor = 0)
        {
            var rasterizerState = PipelineRasterizationStateCreateInfo.Default();

            rasterizerState.CullMode = (uint)cullMode;
            rasterizerState.PolygonMode = polygonMode;
            rasterizerState.FrontFace = frontFace;
            rasterizerState.DepthClampEnable = depthClampEnable;
            rasterizerState.RasterizerDiscardEnable = rasterizerDiscardEnable;
            rasterizerState.LineWidth = lineWidth;
            rasterizerState.DepthBiasEnable = depthBiasEnable;
            rasterizerState.DepthBiasConstantFactor = depthBiasConstantFactor;
            rasterizerState.DepthBiasClamp = depthBiasClamp;
            rasterizerState.DepthBiasSlopeFactor = depthBiasSlopeFactor;

            return New(name, rasterizerState);
        }
    }
}
