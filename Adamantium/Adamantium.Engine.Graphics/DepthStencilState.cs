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
        }

        public bool DepthTestEnable { get; }
        public bool DepthWriteEnable { get; }
        public CompareOp DepthCompareOp { get; }
        public bool DepthBoundsTestEnable { get; }
        public bool StencilTestEnable { get; }
        public StencilOpState Front { get; }
        public StencilOpState Back { get; }
        public float MinDepthBounds { get; }
        public float MaxDepthBounds { get; }

        public override int GetHashCode()
        {
            int hashCode = DepthTestEnable.GetHashCode();
            hashCode = (hashCode * 397) ^ DepthWriteEnable.GetHashCode();
            hashCode = (hashCode * 397) ^ DepthCompareOp.GetHashCode();
            hashCode = (hashCode * 397) ^ DepthBoundsTestEnable.GetHashCode();
            hashCode = (hashCode * 397) ^ StencilTestEnable.GetHashCode();
            hashCode = (hashCode * 397) ^ Front.GetHashCode();
            hashCode = (hashCode * 397) ^ Back.GetHashCode();
            hashCode = (hashCode * 397) ^ MinDepthBounds.GetHashCode();
            hashCode = (hashCode * 397) ^ MaxDepthBounds.GetHashCode();

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
