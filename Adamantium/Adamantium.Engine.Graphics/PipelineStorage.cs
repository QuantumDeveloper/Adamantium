using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Adamantium.Core;
using Adamantium.Core.Collections;
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

        protected PipelineLayout PipelineLayout { get; set; }

        protected PipelineBase(GraphicsDevice graphicsDevice)
        {
            GraphicsDevice = graphicsDevice;
        }

        public GraphicsDevice GraphicsDevice { get; }

        public abstract PipelineBindPoint BindPoint { get; }

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
    }

    public class GraphicsPipeline : PipelineBase
    {
        private PrimitiveTopology primitiveTopology;
        private RasterizerState rasterizerState;
        private DepthStencilState depthStencilState;
        private BlendState colorBlendState;
        private PipelineVertexInputStateCreateInfo vertexInputInfo;

        public GraphicsPipeline(
            GraphicsDevice graphicsDevice, 
            Type vertexType, 
            EffectPass effectPass,
            RenderPass renderPass,
            PrimitiveTopology primitiveTopology, 
            RasterizerState rasterizer, 
            BlendState blendState,
            DepthStencilState depthStencil,
            params DynamicState[] dynamicStates) : base(graphicsDevice)
        {
            VertexType = vertexType;
            EffectPass = effectPass;
            RenderPass = renderPass;
            PrimitiveTopology = primitiveTopology;
            RasterizerState = rasterizer;
            ColorBlendState = blendState;
            DepthStencilState = depthStencil;
            Viewports = new TrackingCollection<Viewport>();
            Scissors = new TrackingCollection<Rect2D>();
            DynamicStates = new TrackingCollection<DynamicState>(dynamicStates);
            Viewports.CollectionChanged += ViewportsCollectionChanged;
            Scissors.CollectionChanged += ScissorsCollectionChanged;
            DynamicStates.CollectionChanged += DynamicStatesCollectionChanged;

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

        public BlendState ColorBlendState
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

        public TrackingCollection<Viewport> Viewports { get; }

        public TrackingCollection<Rect2D> Scissors { get; }

        public TrackingCollection<DynamicState> DynamicStates { get; }

        public override bool IsDynamic => DynamicStates.IsEmpty;

        public override PipelineBindPoint BindPoint => PipelineBindPoint.Graphics;

        private void ScissorsCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (!DynamicStates.Contains(DynamicState.Scissor))
            {
                IsDirty = true;
            }
        }

        private void ViewportsCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (!DynamicStates.Contains(DynamicState.Viewport))
            {
                IsDirty = true;
            }
        }

        private void DynamicStatesCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            IsDirty = true;
        }

        private void CreateVertexInputInfo()
        {
            var vertexBindingDescription = VertexUtils.GetBindingDescription(VertexType);
            var vertexAttributesDescriptions = VertexUtils.GetVertexAttributeDescription(VertexType);

            vertexInputInfo = new PipelineVertexInputStateCreateInfo();
            vertexInputInfo.VertexBindingDescriptionCount = 1;
            vertexInputInfo.VertexAttributeDescriptionCount = (uint)vertexAttributesDescriptions.Length;
            vertexInputInfo.PVertexBindingDescriptions = new VertexInputBindingDescription[] { vertexBindingDescription };
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
            pipelineInfo.PColorBlendState = ColorBlendState;
            pipelineInfo.Layout = PipelineLayout;
            pipelineInfo.RenderPass = RenderPass;
            pipelineInfo.PDepthStencilState = DepthStencilState;
            pipelineInfo.Subpass = 0;

            if (IsDynamic)
            {
                var dynamicStateInfo = new PipelineDynamicStateCreateInfo();
                dynamicStateInfo.PDynamicStates = DynamicStates.ToArray();
                dynamicStateInfo.DynamicStateCount = (uint)DynamicStates.Count;
                pipelineInfo.PDynamicState = dynamicStateInfo;
            }

            Pipeline = GraphicsDevice.CreateGraphicsPipeline(pipelineInfo);
            IsDirty = false;
        }

        public static GraphicsPipeline New<T>(
            GraphicsDevice graphicsDevice, 
            EffectPass effectPass,
            RenderPass renderPass,
            PrimitiveTopology primitiveTopology,
            RasterizerState rasterizer,
            BlendState blendState,
            DepthStencilState depthStencil, 
            params DynamicState[] dynamicStates) where T : struct
        {
            return new GraphicsPipeline(
                graphicsDevice, 
                typeof(T),
                effectPass,
                renderPass,
                primitiveTopology,
                rasterizer,
                blendState,
                depthStencil,
                dynamicStates);
        }

        protected override void Dispose(bool disposeManagedResources)
        {
            Viewports.CollectionChanged -= ViewportsCollectionChanged;
            Scissors.CollectionChanged -= ScissorsCollectionChanged;
            DynamicStates.CollectionChanged -= DynamicStatesCollectionChanged;

            base.Dispose(disposeManagedResources);
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

        public static ComputePipeline New(GraphicsDevice graphicsDevice)
        {
            return new ComputePipeline(graphicsDevice);
        }
    }
}
