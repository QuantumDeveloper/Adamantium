using System;
using System.Collections.Generic;
using System.Text;
using Adamantium.Core;
using Adamantium.Engine.Graphics.Effects;
using AdamantiumVulkan.Core;

namespace Adamantium.Engine.Graphics
{
    public abstract class PipelineStorage : DisposableObject
    {
        public RenderPass RenderPass { get; set; }
        
        public PipelineLayout PipelineLayout { get; set; }
        
        public DescriptorSetLayout DescriptorSetLayout { get; set; }
        
        public bool IsDirty { get; set; }
        
        public Pipeline Pipeline { get; set; }
        
        public EffectPass EffectPass { get; set; }

        public abstract void CreatePipeline();
    }

    public class ComputePipelineStorage : PipelineStorage
    {
        public override void CreatePipeline()
        {
            throw new NotImplementedException();
        }
    }

    public class GraphicsPipelineStorage : PipelineStorage
    {
        public PrimitiveTopology PrimitiveTopology { get; set; }
        
        public PipelineRasterizationStateCreateInfo RasterizerState { get; set; }
        
        public Viewport Viewport { get; set; }

        public override void CreatePipeline()
        {
            
        }
    }

    public class GraphicsPipelineStorage<T> : GraphicsPipelineStorage  where T : struct
    {
        internal T VertexType { get; set; }
    }
    
}
