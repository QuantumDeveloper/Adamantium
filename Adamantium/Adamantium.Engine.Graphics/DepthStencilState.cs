using Adamantium.Core;
using AdamantiumVulkan.Core;
using System;

namespace Adamantium.Engine.Graphics
{
    public class DepthStencilState : NamedObject
    {
        private PipelineDepthStencilStateCreateInfo info;

        private DepthStencilState(string name, PipelineDepthStencilStateCreateInfo info)
        {
            Name = name;
            this.info = (PipelineDepthStencilStateCreateInfo)info.Clone();

            DepthTestEnable = info.DepthTestEnable;
            DepthWriteEnable = info.DepthWriteEnable;
            DepthCompareOp = info.DepthCompareOp;
            DepthBoundsTestEnable = info.DepthBoundsTestEnable;
            StencilTestEnable = info.StencilTestEnable;
            Front = this.info.Front;
            Back = this.info.Back;
            MinDepthBounds = info.MinDepthBounds;
            MaxDepthBounds = info.MaxDepthBounds;

            hashCode = CalculateHashCode();
        }

        private readonly int hashCode;

        public bool DepthTestEnable { get; }
        public bool DepthWriteEnable { get; }
        public CompareOp DepthCompareOp { get; }
        public bool DepthBoundsTestEnable { get; }
        public bool StencilTestEnable { get; }
        public StencilOpState Front { get; }
        public StencilOpState Back { get; }
        public float MinDepthBounds { get; }
        public float MaxDepthBounds { get; }

        private int CalculateHashCode()
        {
            int hash = DepthTestEnable.GetHashCode();
            hash = (hash * 397) ^ DepthWriteEnable.GetHashCode();
            hash = (hash * 397) ^ DepthCompareOp.GetHashCode();
            hash = (hash * 397) ^ DepthBoundsTestEnable.GetHashCode();
            hash = (hash * 397) ^ StencilTestEnable.GetHashCode();
            hash = (hash * 397) ^ Front.GetHashCode();
            hash = (hash * 397) ^ Back.GetHashCode();
            hash = (hash * 397) ^ MinDepthBounds.GetHashCode();
            hash = (hash * 397) ^ MaxDepthBounds.GetHashCode();

            return hash;
        }
        
        public override int GetHashCode()
        {
            return hashCode;
        }

        public static implicit operator PipelineDepthStencilStateCreateInfo(DepthStencilState state)
        {
            return state.info;
        }

        public static DepthStencilState New(String name, PipelineDepthStencilStateCreateInfo info)
        {
            return new DepthStencilState(name, info);
        }

        public static DepthStencilState New(String name, Boolean depthEnable, Boolean depthWriteEnable, Boolean stencilEnable = true, CompareOp depthComparison = CompareOp.Less)
        {
            var description = PipelineDepthStencilStateCreateInfo.Default();
            description.DepthTestEnable = depthEnable;
            description.DepthWriteEnable = depthWriteEnable;
            description.DepthCompareOp = depthComparison;
            description.StencilTestEnable = stencilEnable;

            return New(name, description);
        }
    }
}
