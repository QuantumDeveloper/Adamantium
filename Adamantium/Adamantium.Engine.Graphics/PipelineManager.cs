using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Engine.Graphics.Effects;
using AdamantiumVulkan.Core;

namespace Adamantium.Engine.Graphics
{
    public class PipelineManager : IDisposable
    {
        private Dictionary<int, GraphicsPipeline> graphicsPipelines;
        private Dictionary<int, ComputePipeline> computePipelines;

        private readonly GraphicsDevice graphicsDevice;

        private GraphicsPipeline currentGraphicsPipeline;

        public PipelineManager(GraphicsDevice graphicsDevice)
        {
            this.graphicsDevice = graphicsDevice;
            this.graphicsDevice.SurfaceSizeChanged += GraphicsDeviceOnSurfaceSizeChanged;
            graphicsPipelines = new Dictionary<int, GraphicsPipeline>();
            computePipelines = new Dictionary<int, ComputePipeline>();
        }

        private void GraphicsDeviceOnSurfaceSizeChanged(object sender, EventArgs e)
        {
            
        }

        public GraphicsPipeline GetOrCreateGraphicsPipeline(GraphicsDevice graphicsDevice)
        {
            if (!graphicsDevice.ShouldChangeGraphicsPipeline)
            {
                return currentGraphicsPipeline;
            }
            
            currentGraphicsPipeline = GetOrCreateGraphicsPipeline(graphicsDevice,
                graphicsDevice.VertexType,
                graphicsDevice.CurrentEffectPass,
                graphicsDevice.RenderPass,
                graphicsDevice.PrimitiveTopology,
                graphicsDevice.RasterizerState,
                graphicsDevice.BlendState,
                graphicsDevice.DepthStencilState,
                graphicsDevice.Viewports.ToArray(),
                graphicsDevice.Scissors.ToArray(),
                graphicsDevice.DynamicStates);

            return currentGraphicsPipeline;
        }

        public GraphicsPipeline GetOrCreateGraphicsPipeline(
            GraphicsDevice graphicsDevice, 
            Type vertexType, 
            EffectPass effectPass,
            RenderPass renderPass,
            PrimitiveTopology primitiveTopology, 
            RasterizerState rasterizerState, 
            BlendState blendState,
            DepthStencilState depthStencilState,
            Viewport[] viewports,
            Rect2D[] scissors,
            params DynamicState[] dynamicStates)
        {
            var hash = ComputeHash(
                vertexType,
                effectPass,
                renderPass,
                primitiveTopology,
                rasterizerState,
                blendState,
                depthStencilState,
                viewports,
                scissors,
                dynamicStates);

            if (graphicsPipelines.ContainsKey(hash))
            {
                return graphicsPipelines[hash];
            }

            var pipeline = GraphicsPipeline.New(
                graphicsDevice,
                vertexType,
                effectPass,
                renderPass,
                primitiveTopology,
                rasterizerState,
                blendState,
                depthStencilState,
                viewports,
                scissors,
                dynamicStates);
                
            graphicsPipelines.Add(hash, pipeline);
            return pipeline;

        }

        protected int ComputeHash(
            Type vertexType, 
            EffectPass effectPass,
            RenderPass renderPass,
            PrimitiveTopology primitiveTopology, 
            RasterizerState rasterizerState, 
            BlendState blendState,
            DepthStencilState depthStencilState,
            Viewport[] viewports,
            Rect2D[] scissors,
            params DynamicState[] dynamicStates)
        {
            var hashCode = vertexType.GetHashCode();
            hashCode = (hashCode * 397) ^ effectPass.GetHashCode();
            if (renderPass != null)
            {
                hashCode = (hashCode * 397) ^ renderPass.GetHashCode();
            }
            hashCode = (hashCode * 397) ^ primitiveTopology.GetHashCode();
            hashCode = (hashCode * 397) ^ rasterizerState.GetHashCode();
            hashCode = (hashCode * 397) ^ blendState.GetHashCode();
            hashCode = (hashCode * 397) ^ depthStencilState.GetHashCode();

            var isDynamic = dynamicStates != null && dynamicStates.Length > 0;

            if (!isDynamic || !dynamicStates.Contains(DynamicState.Viewport))
            {
                foreach (var viewport in viewports)
                {
                    hashCode = (hashCode * 397) ^ viewport.GetHashCode();
                }
            }
            
            if (!isDynamic || !dynamicStates.Contains(DynamicState.Scissor))
            {
                foreach (var scissor in scissors)
                {
                    hashCode = (hashCode * 397) ^ scissor.GetHashCode();
                }
            }

            return hashCode;
        }

        public void Dispose()
        {
            graphicsPipelines.Clear();
            computePipelines.Clear();
        }
    }
}
