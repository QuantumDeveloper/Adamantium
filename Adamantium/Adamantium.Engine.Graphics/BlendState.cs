using Adamantium.Core;
using AdamantiumVulkan.Core;
using System;

namespace Adamantium.Engine.Graphics
{
    public class BlendState : NamedObject
    {
        private PipelineColorBlendStateCreateInfo info;

        private BlendState(String name, PipelineColorBlendStateCreateInfo info)
        {
            Name = name;
            this.info = (PipelineColorBlendStateCreateInfo)info.Clone();

            LogicOpEnable = info.LogicOpEnable;
            LogicOp = info.LogicOp;
            AttachmentCount = info.AttachmentCount;
            PAttachments = this.info.PAttachments;
            BlendConstants = this.info.BlendConstants;

            hashCode = CalculateHashCode();
        }

        private readonly int hashCode;
        public bool LogicOpEnable { get; }
        public LogicOp LogicOp { get; }
        public uint AttachmentCount { get; }
        public PipelineColorBlendAttachmentState[] PAttachments { get; }
        public float[] BlendConstants { get; }

        private int CalculateHashCode()
        {
            int hash = LogicOpEnable.GetHashCode();
            hash = (hash * 397) ^ LogicOp.GetHashCode();
            hash = (hash * 397) ^ AttachmentCount.GetHashCode();
            foreach(var attachment in PAttachments)
            {
                hash = (hash * 397) ^ attachment.GetHashCode();
            }

            foreach (var constant in BlendConstants)
            {
                hash = (hash * 397) ^ constant.GetHashCode();
            }

            return hash;
        }

        public override int GetHashCode()
        {
            return hashCode;
        }

        public static implicit operator PipelineColorBlendStateCreateInfo (BlendState state)
        {
            return state.info;
        }

        public static BlendState New(String name, PipelineColorBlendStateCreateInfo info)
        {
            return new BlendState(name, info);
        }
        
        public static BlendState New(
            String name, 
            bool blendEnable, 
            BlendFactor sourceBlend, 
            BlendFactor destinationBlend, 
            BlendOp colorBlendOp, 
            ColorComponentFlagBits colorWriteMask)
        {
            var state = PipelineColorBlendStateCreateInfo.Default();
            var colorAttachment = state.PAttachments[0];
            colorAttachment.BlendEnable = blendEnable;
            colorAttachment.SrcColorBlendFactor = sourceBlend;
            colorAttachment.SrcAlphaBlendFactor = sourceBlend;
            colorAttachment.DstColorBlendFactor = destinationBlend;
            colorAttachment.DstAlphaBlendFactor = destinationBlend;
            colorAttachment.ColorBlendOp = colorBlendOp;
            colorAttachment.AlphaBlendOp = colorBlendOp;
            colorAttachment.ColorWriteMask = colorWriteMask;
            
            return new BlendState(name, state);
        }
    }
}
