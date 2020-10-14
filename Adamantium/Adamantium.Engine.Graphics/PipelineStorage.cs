using System;
using System.Collections.ObjectModel;
using System.Linq;
using Adamantium.Core;
using Adamantium.Engine.Graphics.Effects;
using AdamantiumVulkan.Core;

namespace Adamantium.Engine.Graphics
{
    public abstract class PipelineBase : DisposableObject
    {
        private Pipeline pipeline;
        private RenderPass renderPass;
        private DescriptorSetLayout descriptorSetLayout;
        private EffectPass effectPass;

        protected PipelineBase(GraphicsDevice graphicsDevice)
        {
            GraphicsDevice = graphicsDevice;
        }

        public GraphicsDevice GraphicsDevice { get; }

        public abstract PipelineBindPoint BindPoint { get; }
        
        public int HashCode { get; private protected set; }

        public RenderPass RenderPass
        {
            get => renderPass;
            private protected set
            {
                if (SetProperty(ref renderPass, value))
                {
                    IsDirty = true;
                }
            }
        }

        public EffectPass EffectPass
        {
            get => effectPass;
            private protected set
            {
                if (SetProperty(ref effectPass, value))
                {
                    IsDirty = true;
                }
            }
        }

        public virtual bool IsDirty { get; protected set; }

        public virtual bool IsDynamic { get; }
        
        protected Pipeline Pipeline { get => pipeline; set => pipeline = value; }

        public static implicit operator Pipeline(PipelineBase pipelineBase)
        {
            return pipelineBase.Pipeline;
        }

        protected abstract void CreateOrUpdatePipeline();

        protected abstract int ComputeHash();
    }

    public class GraphicsPipeline : PipelineBase
    {
        private PrimitiveTopology primitiveTopology;
        private RasterizerState rasterizerState;
        private DepthStencilState depthStencilState;
        private BlendState colorBlendState;
        private PipelineVertexInputStateCreateInfo vertexInputInfo;

        protected GraphicsPipeline(
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
            params DynamicState[] dynamicStates
            ) : base(graphicsDevice)
        {
            VertexType = vertexType;
            EffectPass = effectPass;
            RenderPass = renderPass;
            PrimitiveTopology = primitiveTopology;
            RasterizerState = rasterizerState;
            BlendState = blendState;
            DepthStencilState = depthStencilState;
            Viewports = new ReadOnlyCollection<Viewport>(viewports);
            Scissors = new ReadOnlyCollection<Rect2D>(scissors);
            DynamicStates = new ReadOnlyCollection<DynamicState>(dynamicStates);

            HashCode = ComputeHash();
            
            CreateVertexInputInfo();
            CreateOrUpdatePipeline();
        }

        public Type VertexType { get; }

        public PrimitiveTopology PrimitiveTopology
        {
            get => primitiveTopology;
            private set
            {
                if (SetProperty(ref primitiveTopology, value))
                {
                    IsDirty = true;
                }
            }
        }

        public RasterizerState RasterizerState
        {
            get => rasterizerState;
            private set
            {
                if (SetProperty(ref rasterizerState, value))
                {
                    IsDirty = true;
                }
            }
        }

        public BlendState BlendState
        {
            get => colorBlendState;
            private set
            {
                if (SetProperty(ref colorBlendState, value))
                {
                    IsDirty = true;
                }
            }
        }

        public DepthStencilState DepthStencilState
        {
            get => depthStencilState;
            private set
            {
                depthStencilState = value;
                IsDirty = true;
            }
        }

        public ReadOnlyCollection<Viewport> Viewports { get; }

        public ReadOnlyCollection<Rect2D> Scissors { get; }

        public ReadOnlyCollection<DynamicState> DynamicStates { get; }

        public override bool IsDynamic => DynamicStates.Count > 0;

        public override PipelineBindPoint BindPoint => PipelineBindPoint.Graphics;

        private void CreateVertexInputInfo()
        {
            var vertexBindingDescription = VertexUtils.GetBindingDescription(VertexType);
            var vertexAttributesDescriptions = VertexUtils.GetVertexAttributeDescription(VertexType);

            vertexInputInfo = new PipelineVertexInputStateCreateInfo();
            vertexInputInfo.VertexBindingDescriptionCount = 1;
            vertexInputInfo.VertexAttributeDescriptionCount = (uint)vertexAttributesDescriptions.Length;
            vertexInputInfo.PVertexBindingDescriptions = new [] { vertexBindingDescription };
            vertexInputInfo.PVertexAttributeDescriptions = vertexAttributesDescriptions;
        }

        protected override void CreateOrUpdatePipeline()
        {
            if (!IsDirty) return;

            var shaderStages = EffectPass.ShaderStages.ToArray();

            var presenter = GraphicsDevice.Presenter;

            var inputAssembly = new PipelineInputAssemblyStateCreateInfo();
            inputAssembly.Topology = PrimitiveTopology;
            inputAssembly.PrimitiveRestartEnable = false;

            var viewportState = new PipelineViewportStateCreateInfo();
            viewportState.PViewports = Viewports.ToArray();
            viewportState.ViewportCount = (uint)Viewports.Count;
            viewportState.PScissors = Scissors.ToArray();
            viewportState.ScissorCount = (uint)Scissors.Count;

            var multisampling = new PipelineMultisampleStateCreateInfo();
            multisampling.SampleShadingEnable = true;
            multisampling.MinSampleShading = 0.8f;
            multisampling.RasterizationSamples = (SampleCountFlagBits)presenter.MSAALevel;

            var pipelineInfo = new GraphicsPipelineCreateInfo();
            pipelineInfo.StageCount = (uint)shaderStages.Length;
            pipelineInfo.PStages = shaderStages;
            pipelineInfo.PVertexInputState = vertexInputInfo;
            pipelineInfo.PInputAssemblyState = inputAssembly;
            pipelineInfo.PViewportState = viewportState;
            pipelineInfo.PRasterizationState = RasterizerState;
            pipelineInfo.PMultisampleState = multisampling;
            pipelineInfo.PColorBlendState = BlendState;
            pipelineInfo.Layout = EffectPass.PipelineLayout;
            pipelineInfo.RenderPass = RenderPass;
            pipelineInfo.PDepthStencilState = DepthStencilState;
            pipelineInfo.Subpass = 0;

            if (IsDynamic)
            {
                var dynamicStateInfo = new PipelineDynamicStateCreateInfo();
                dynamicStateInfo.PDynamicStates = DynamicStates.ToArray();
                dynamicStateInfo.DynamicStateCount = (uint)DynamicStates.Count;
                pipelineInfo.PDynamicState = dynamicStateInfo;

                viewportState.ViewportCount = 1;
                viewportState.PViewports = new Viewport[]{new Viewport() {}};
                viewportState.ScissorCount = 1;
                viewportState.PScissors = new Rect2D[]{new Rect2D() };
            }

            Pipeline = GraphicsDevice.CreateGraphicsPipeline(pipelineInfo);
            IsDirty = false;
        }

        protected override int ComputeHash()
        {
            var hashCode = VertexType.GetHashCode();
            hashCode = (hashCode * 397) ^ EffectPass.GetHashCode();
            hashCode = (hashCode * 397) ^ RenderPass.GetHashCode();
            hashCode = (hashCode * 397) ^ PrimitiveTopology.GetHashCode();
            hashCode = (hashCode * 397) ^ RasterizerState.GetHashCode();
            hashCode = (hashCode * 397) ^ BlendState.GetHashCode();
            hashCode = (hashCode * 397) ^ DepthStencilState.GetHashCode();

            if (!IsDynamic || !DynamicStates.Contains(DynamicState.Viewport))
            {
                foreach (var viewport in Viewports)
                {
                    hashCode = (hashCode * 397) ^ viewport.GetHashCode();
                }
            }
            
            if (!IsDynamic || !DynamicStates.Contains(DynamicState.Scissor))
            {
                foreach (var scissor in Scissors)
                {
                    hashCode = (hashCode * 397) ^ scissor.GetHashCode();
                }
            }

            return hashCode;
        }

        public override int GetHashCode()
        {
            return HashCode;
        }
        
        public static GraphicsPipeline New(
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
            return new GraphicsPipeline(
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
        }

        public static GraphicsPipeline New<T>(
            GraphicsDevice graphicsDevice, 
            EffectPass effectPass,
            RenderPass renderPass,
            PrimitiveTopology primitiveTopology,
            RasterizerState rasterizerState,
            BlendState blendState,
            DepthStencilState depthStencilState,
            Viewport[] viewports,
            Rect2D[] scissors,
            params DynamicState[] dynamicStates) where T : struct
        {
            return GraphicsPipeline.New(
                graphicsDevice, 
                typeof(T), 
                effectPass, 
                renderPass, 
                primitiveTopology, 
                rasterizerState, 
                blendState, 
                depthStencilState, 
                viewports, 
                scissors, 
                dynamicStates);
        }
    }

    public class ComputePipeline : PipelineBase
    {
        public override PipelineBindPoint BindPoint => PipelineBindPoint.Compute;

        public ComputePipeline(GraphicsDevice graphicsDevice) : base (graphicsDevice)
        {

        }

        protected override void CreateOrUpdatePipeline()
        {
            
        }

        protected override int ComputeHash()
        {
            return 0;
        }

        public static ComputePipeline New(GraphicsDevice graphicsDevice)
        {
            return new ComputePipeline(graphicsDevice);
        }
    }
}
