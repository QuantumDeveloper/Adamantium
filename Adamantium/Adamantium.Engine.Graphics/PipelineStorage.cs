using System;
using System.Collections.Generic;
using System.Text;
using Adamantium.Core;
using Adamantium.Engine.Graphics.Effects;
using AdamantiumVulkan.Core;

namespace Adamantium.Engine.Graphics
{
    public class GraphicsPipeline<T> : DisposableObject where T : struct
    {
        public T VertexType { get; }
        public PrimitiveTopology PrimitiveTopology { get; set; }

        public PipelineRasterizationStateCreateInfo RasterizerState { get; set; }

        public Viewport Viewport { get; set; }

        public RenderPass RenderPass { get; set; }
        
        public PipelineLayout PipelineLayout { get; set; }
        
        public DescriptorSetLayout DescriptorSetLayout { get; set; }
        
        public bool IsDirty { get; set; }

        public Pipeline Pipeline { get; set; }

        public EffectPass EffectPass { get; set; }

        public void CreatePipeline()
        {

        }
    }

    public class ComputePipelineStorage : DisposableObject
    {
        public void CreatePipeline()
        {
            
        }
    }
}
