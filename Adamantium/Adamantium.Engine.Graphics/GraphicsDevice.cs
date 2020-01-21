using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Adamantium.Core;
using Adamantium.Core.Collections;
using Adamantium.Engine.Core;
using Adamantium.Engine.Graphics.Effects;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using AdamantiumVulkan.Core;

namespace Adamantium.Engine.Graphics
{
    public class GraphicsDevice : DisposableObject
    {
        private static PhysicalDevice PhysicalDevice { get; set; }
        
        private Queue graphicsQueue;
        private SurfaceKHR surface;
        private Pipeline graphicsPipeline;
        private CommandBuffer[] commandBuffers;
        
        private SubmitInfo[] submitInfos = new SubmitInfo[1];
        private uint imageIndex;
        private BlendState blendState;
        private RasterizerState rasterizerState;
        private DepthStencilState depthStencilState;
        
        private RenderPass renderPass;
        private Type vertexType;
        private PrimitiveTopology primitiveTopology;
        private EffectPass currentEffectPass;
        
        private TrackingCollection<Viewport> viewports;
        private TrackingCollection<Rect2D> scissors;
        private TrackingCollection<DynamicState> dynamicStates;
        
        private PipelineManager pipelineManager;
        
        internal Device LogicalDevice { get; private set; }
        
        internal bool ShouldChangeGraphicsPipeline { get; private set; }
        
        static GraphicsDevice()
        {
            BlendStates = new BlendStatesCollection();
            RasterizerStates = new RasterizerStateCollection();
            DepthStencilStates = new DepthStencilStatesCollection();
        }
        
        private GraphicsDevice(VulkanInstance instance, PhysicalDevice physicalDevice)
        {
            VulkanInstance = instance;
            PhysicalDevice = physicalDevice;
        }

        private GraphicsDevice(GraphicsDevice main, PresentationParameters presentationParameters)
        {
            surface = VulkanInstance.GetOrCreateSurface(presentationParameters);
            if (!main.IsMain)
            {
                CreateMainDevice();
                main.MainDevice = this;
                main.LogicalDevice = LogicalDevice;
            }
            MaxFramesInFlight = presentationParameters.BuffersCount;
            MainDevice = main.MainDevice;
            LogicalDevice = main.LogicalDevice;
            InitializeRenderDevice(presentationParameters);
            dynamicStates = new TrackingCollection<DynamicState>();
            viewports = new TrackingCollection<Viewport>();
            scissors = new TrackingCollection<Rect2D>();

            BlendState = BlendStates.Default;
            RasterizerState = RasterizerStates.Default;
            DepthStencilState = DepthStencilStates.Default;

            pipelineManager = new PipelineManager(this);
            
            BasicEffect = Effect.CompileFromFile(Path.Combine("Effects", "UIEffect.fx"), this);
        }
        
        public CommandPool CommandPool { get; private set; }

        internal Semaphore[] ImageAvailableSemaphores { get; private set; }
        internal Semaphore[] RenderFinishedSemaphores { get; private set; }
        internal Fence[] InFlightFences { get; private set; }

        public uint CurrentFrame { get; private set; }

        public uint ImageIndex => imageIndex;

        public readonly uint MaxFramesInFlight;

        public GraphicsPipeline CurrentGraphicsPipeline { get; private set; }

        public List<EffectPool> EffectPools { get; private set; }

        public EffectPool DefaultEffectPool { get; private set; }

        public static VulkanInstance VulkanInstance { get; private set; }

        internal GraphicsPresenter Presenter { get; private set; }

        public GraphicsDevice MainDevice { get; private set; }

        public bool IsMain { get; private set; }
        
        public static BlendStatesCollection BlendStates { get; }
        
        public static RasterizerStateCollection RasterizerStates { get; }
        
        public static DepthStencilStatesCollection DepthStencilStates { get; }
        
        public Effect BasicEffect { get; private set; }

        public BlendState BlendState
        {
            get => blendState;
            set
            {
                if (SetProperty(ref blendState, value))
                {
                    blendState = value;
                    ShouldChangeGraphicsPipeline = true;
                }
            }
        }

        public RasterizerState RasterizerState
        {
            get => rasterizerState;
            set
            {
                if (SetProperty(ref rasterizerState, value))
                {
                    rasterizerState = value;
                    ShouldChangeGraphicsPipeline = true;
                }
            }
        }

        public DepthStencilState DepthStencilState
        {
            get => depthStencilState;
            set
            {
                if (SetProperty(ref depthStencilState, value))
                {
                    depthStencilState = value;
                    ShouldChangeGraphicsPipeline = true;
                }
            }
        }

        public RenderPass RenderPass
        {
            get => renderPass;
            set
            {
                if (SetProperty(ref renderPass, value))
                {
                    Presenter.RenderPass = value;
                    ShouldChangeGraphicsPipeline = true;
                }
            }
        }

        public Type VertexType
        {
            get => vertexType;
            set
            {
                if (SetProperty(ref vertexType, value))
                {
                    vertexType = value;
                    ShouldChangeGraphicsPipeline = true;
                }
            } 
        }

        public PrimitiveTopology PrimitiveTopology
        {
            get => primitiveTopology;
            set
            {
                if (SetProperty(ref primitiveTopology, value))
                {
                    primitiveTopology = value;
                    ShouldChangeGraphicsPipeline = true;
                }
            }
        }

        public EffectPass CurrentEffectPass
        {
            get => currentEffectPass;
            set
            {
                if (SetProperty(ref currentEffectPass, value))
                {
                    currentEffectPass = value;
                    ShouldChangeGraphicsPipeline = true;
                }
            }
        }

        public ReadOnlyCollection<Viewport> Viewports => viewports.AsReadOnly();

        public ReadOnlyCollection<Rect2D> Scissors => scissors.AsReadOnly();

        public bool IsDynamic => dynamicStates.Count > 0;

        public void ApplyViewports(params Viewport[] viewports)
        {
            this.viewports.Clear();
            this.viewports.AddRange(viewports);
            
            if (viewports == null) return;

            if (!IsDynamic || !dynamicStates.Contains(DynamicState.Viewport))
            {
                ShouldChangeGraphicsPipeline = true;
            }
        }

        public void ApplyScissors(params Rect2D[] scissors)
        {
            this.scissors.Clear();
            this.scissors.AddRange(scissors);
            
            if (!IsDynamic || !dynamicStates.Contains(DynamicState.Viewport))
            {
                ShouldChangeGraphicsPipeline = true;
            }
        }

        public void AddDynamicStates(params DynamicState[] states)
        {
            foreach (var state in states)
            {
                if (!dynamicStates.Contains(state))
                {
                    dynamicStates.Add(state);
                }
            }
        }

        public void RemoveDynamicStates(params DynamicState[] states)
        {
            foreach (var state in states)
            {
                dynamicStates.Remove(state);
            }
        }

        public void ClearDynamicStates()
        {
            dynamicStates.Clear();
        }

        public DynamicState[] DynamicStates => dynamicStates.ToArray();
        
        public CommandBuffer CurrentCommandBuffer => commandBuffers[ImageIndex]; 

        private void InitializeRenderDevice(PresentationParameters presentationParameters)
        {
            CreateCommandPool();
            CreateGraphicsPresenter(presentationParameters);
            CreateCommandBuffers();
            //CreateGraphicsPipeline();
            CreateSyncObjects();
        }

        public RenderPass CreateRenderPass(RenderPassCreateInfo createInfo)
        {
            return LogicalDevice.CreateRenderPass(createInfo);
        }

        public DescriptorPool CreateDescriptorPool(DescriptorPoolCreateInfo info)
        {
            return LogicalDevice.CreateDescriptorPool(info);
        }

        public static GraphicsDevice Create(VulkanInstance instance, PhysicalDevice physicalDevice)
        {
            return new GraphicsDevice(instance, physicalDevice);
        }

        public GraphicsDevice CreateRenderDevice(PresentationParameters parameters)
        {
            return new GraphicsDevice(this, parameters);
        }

        private void CreateMainDevice()
        {
            CreateLogicalDevice();
            MainDevice = this;
            IsMain = true;
        }

        private void CreateLogicalDevice()
        {
            var indices = PhysicalDevice.FindQueueFamilies(surface);

            var queueInfos = new List<DeviceQueueCreateInfo>();
            HashSet<uint> uniqueQueueFamilies = new HashSet<uint>() { indices.graphicsFamily.Value, indices.presentFamily.Value };
            float queuePriority = 1.0f;
            foreach (var queueFamily in uniqueQueueFamilies)
            {
                var queueCreateInfo = new DeviceQueueCreateInfo();
                queueCreateInfo.QueueFamilyIndex = queueFamily;
                queueCreateInfo.QueueCount = 1;
                queueCreateInfo.PQueuePriorities = queuePriority;
                queueInfos.Add(queueCreateInfo);
            }

            var deviceFeatures = PhysicalDevice.GetPhysicalDeviceFeatures();
            deviceFeatures.SamplerAnisotropy = true;
            deviceFeatures.SampleRateShading = true;

            var createInfo = new DeviceCreateInfo();
            createInfo.QueueCreateInfoCount = (uint)queueInfos.Count;
            createInfo.PQueueCreateInfos = queueInfos.ToArray();
            createInfo.PEnabledFeatures = deviceFeatures;
            createInfo.EnabledExtensionCount = (uint)VulkanInstance.DeviceExtensions.Count;
            createInfo.PpEnabledExtensionNames = VulkanInstance.DeviceExtensions.ToArray();

            if (VulkanInstance.IsInDebugMode)
            {
                createInfo.EnabledLayerCount = (uint)VulkanInstance.ValidationLayers.Count;
                createInfo.PpEnabledLayerNames = VulkanInstance.ValidationLayers.ToArray();
            }

            LogicalDevice = PhysicalDevice.CreateDevice(createInfo);

            EffectPools = new List<EffectPool>();
            DefaultEffectPool = EffectPool.New(this);

            createInfo.Dispose();

            graphicsQueue = LogicalDevice.GetDeviceQueue(indices.graphicsFamily.Value, 0);
        }

        

        

        private void CreateDefaultDescriptorSetLayout()
        {
            var bindings = new List<DescriptorSetLayoutBinding>();

            //DescriptorSetLayoutBinding uboLayoutBinding = new DescriptorSetLayoutBinding();
            //uboLayoutBinding.Binding = 0;
            //uboLayoutBinding.DescriptorCount = 1;
            //uboLayoutBinding.DescriptorType = DescriptorType.UniformBuffer;
            //uboLayoutBinding.PImmutableSamplers = null;
            //uboLayoutBinding.StageFlags = (uint)ShaderStageFlagBits.VertexBit;

            DescriptorSetLayoutBinding samplerLayoutBinding = new DescriptorSetLayoutBinding();
            samplerLayoutBinding.Binding = 0;
            samplerLayoutBinding.DescriptorCount = 1;
            samplerLayoutBinding.DescriptorType = DescriptorType.CombinedImageSampler;
            samplerLayoutBinding.PImmutableSamplers = null;
            samplerLayoutBinding.StageFlags = (uint)ShaderStageFlagBits.FragmentBit;

            bindings.Add(samplerLayoutBinding);

            DescriptorSetLayoutCreateInfo layoutInfo = new DescriptorSetLayoutCreateInfo();
            layoutInfo.BindingCount = 1;
            layoutInfo.PBindings = bindings.ToArray();

            //descriptorSetLayout = LogicalDevice.CreateDescriptorSetLayout(layoutInfo);
        }

        public DescriptorSetLayout CreateDescriptorSetLayout(DescriptorSetLayoutCreateInfo layoutCreateInfo)
        {
            return LogicalDevice.CreateDescriptorSetLayout(layoutCreateInfo);
        }

        public PipelineLayout CreatePipelineLayout(PipelineLayoutCreateInfo createInfo)
        {
            return LogicalDevice.CreatePipelineLayout(createInfo);
        }

        public Pipeline CreateGraphicsPipeline(GraphicsPipelineCreateInfo info)
        {
            return LogicalDevice.CreateGraphicsPipelines(null, 1, info)[0];
        }

        public void CreateGraphicsPipelineForEffectPass(EffectPass effectPass)
        {

        }

        public Result AllocateDescriptorSets(in DescriptorSetAllocateInfo pAllocateInfo, AdamantiumVulkan.Core.DescriptorSet[] descriptorSets)
        {
            return LogicalDevice.AllocateDescriptorSets(pAllocateInfo, descriptorSets);
        }

        private void CreateGraphicsPipeline()
        {
            var vertexContent = File.ReadAllBytes(Path.Combine("Shaders","vert.spv"));
            var fragmentContent = File.ReadAllBytes(Path.Combine("Shaders","frag.spv"));

            var vertexShaderModule = CreateShaderModule(vertexContent);
            var fragmentShaderModule = CreateShaderModule(fragmentContent);

            var vertShaderStageInfo = new PipelineShaderStageCreateInfo();
            vertShaderStageInfo.Stage = ShaderStageFlagBits.VertexBit;
            vertShaderStageInfo.Module = vertexShaderModule;
            vertShaderStageInfo.PName = "main";

            var fragShaderStageInfo = new PipelineShaderStageCreateInfo();
            fragShaderStageInfo.Stage = ShaderStageFlagBits.FragmentBit;
            fragShaderStageInfo.Module = fragmentShaderModule;
            fragShaderStageInfo.PName = "main";

            PipelineShaderStageCreateInfo[] shaderStages = new[] { vertShaderStageInfo, fragShaderStageInfo };

            var bindingDescr = VertexUtils.GetBindingDescription<MeshVertex>();
            var attributesDescriptions = VertexUtils.GetVertexAttributeDescription<MeshVertex>();

            var vertexInputInfo = new PipelineVertexInputStateCreateInfo();
            vertexInputInfo.VertexBindingDescriptionCount = 0;
            vertexInputInfo.VertexAttributeDescriptionCount = 0;

            //vertexInputInfo.VertexBindingDescriptionCount = 1;
            //vertexInputInfo.VertexAttributeDescriptionCount = (uint)attributesDescriptions.Length;
            //vertexInputInfo.PVertexBindingDescriptions = new VertexInputBindingDescription[] { bindingDescr };
            //vertexInputInfo.PVertexAttributeDescriptions = attributesDescriptions;

            var inputAssembly = new PipelineInputAssemblyStateCreateInfo();
            inputAssembly.Topology = PrimitiveTopology.TriangleList;
            inputAssembly.PrimitiveRestartEnable = false;
            
            var viewport = new Viewport();
            viewport.X = 0.0f;
            viewport.Y = 0.0f;
            viewport.Width = Presenter.Width;
            viewport.Height = Presenter.Height;
            viewport.MinDepth = 0.0f;
            viewport.MaxDepth = 1.0f;

            Rect2D scissor = new Rect2D();
            scissor.Offset = new Offset2D();
            scissor.Extent = new Extent2D() { Width = Presenter.Width, Height = Presenter.Height };

            var viewportState = new PipelineViewportStateCreateInfo();
            viewportState.ViewportCount = 1;
            viewportState.PViewports = new Viewport[] { viewport };
            viewportState.ScissorCount = 1;
            viewportState.PScissors = new Rect2D[] { scissor };

            var rasterizer = new PipelineRasterizationStateCreateInfo();
            rasterizer.DepthClampEnable = false;
            rasterizer.RasterizerDiscardEnable = false;
            rasterizer.PolygonMode = PolygonMode.Fill;
            rasterizer.LineWidth = 1.0f;
            rasterizer.CullMode = (uint)CullModeFlagBits.BackBit;
            rasterizer.FrontFace = FrontFace.Clockwise;
            rasterizer.DepthBiasEnable = false;

            var multisampling = new PipelineMultisampleStateCreateInfo();
            multisampling.SampleShadingEnable = true;
            multisampling.MinSampleShading = 0.8f;
            multisampling.RasterizationSamples = (SampleCountFlagBits)Presenter.MSAALevel;

            var depthStencil = new PipelineDepthStencilStateCreateInfo();
            depthStencil.DepthTestEnable = false;
            depthStencil.DepthWriteEnable = true;
            depthStencil.DepthCompareOp = CompareOp.Less;
            depthStencil.DepthBoundsTestEnable = false;
            depthStencil.MinDepthBounds = 0.0f;
            depthStencil.MaxDepthBounds = 1.0f;
            depthStencil.StencilTestEnable = true;
            depthStencil.Front = new StencilOpState() { CompareOp = CompareOp.Always, DepthFailOp = StencilOp.Keep, FailOp = StencilOp.Keep, PassOp = StencilOp.Keep };
            depthStencil.Back = new StencilOpState() { CompareOp = CompareOp.Always, DepthFailOp = StencilOp.Keep, FailOp = StencilOp.Keep, PassOp = StencilOp.Keep };

            var colorBlendAttachment = new PipelineColorBlendAttachmentState();
            colorBlendAttachment.ColorWriteMask = (uint)(ColorComponentFlagBits.RBit | ColorComponentFlagBits.GBit | ColorComponentFlagBits.BBit | ColorComponentFlagBits.ABit);
            colorBlendAttachment.BlendEnable = false;

            var colorBlending = new PipelineColorBlendStateCreateInfo();
            colorBlending.LogicOpEnable = false;
            colorBlending.LogicOp = LogicOp.Copy;
            colorBlending.AttachmentCount = 1;
            colorBlending.PAttachments = new PipelineColorBlendAttachmentState[] { colorBlendAttachment };
            colorBlending.BlendConstants = new float[4];
            colorBlending.BlendConstants[0] = 0.0f;
            colorBlending.BlendConstants[1] = 0.0f;
            colorBlending.BlendConstants[2] = 0.0f;
            colorBlending.BlendConstants[3] = 0.0f;

            var pipelineInfo = new GraphicsPipelineCreateInfo();
            pipelineInfo.StageCount = (uint)shaderStages.Length;
            pipelineInfo.PStages = shaderStages;
            pipelineInfo.PVertexInputState = vertexInputInfo;
            pipelineInfo.PInputAssemblyState = inputAssembly;
            pipelineInfo.PViewportState = viewportState;
            pipelineInfo.PRasterizationState = rasterizer;
            pipelineInfo.PMultisampleState = multisampling;
            pipelineInfo.PColorBlendState = colorBlending;
            //pipelineInfo.Layout = pipelineLayout;
            pipelineInfo.RenderPass = renderPass;
            pipelineInfo.PDepthStencilState = depthStencil;
            pipelineInfo.Subpass = 0;

            var pipelines = LogicalDevice.CreateGraphicsPipelines(null, 1, pipelineInfo);
            graphicsPipeline = pipelines[0];
            //CurrentGraphicsPipeline = graphicsPipeline;

            pipelineInfo.Dispose();
            LogicalDevice.DestroyShaderModule(vertexShaderModule);
            LogicalDevice.DestroyShaderModule(fragmentShaderModule);
        }

        public ShaderModule CreateShaderModule(byte[] code)
        {
            ShaderModuleCreateInfo createInfo = new ShaderModuleCreateInfo();
            createInfo.CodeSize = (ulong)code.Length;
            createInfo.PCode = code;

            var shaderModule = LogicalDevice.CreateShaderModule(createInfo);
            createInfo.Dispose();
            return shaderModule;
        }

        private void CreateCommandPool()
        {
            var queueFamilyIndices = PhysicalDevice.FindQueueFamilies(surface);

            var poolInfo = new CommandPoolCreateInfo();
            poolInfo.QueueFamilyIndex = queueFamilyIndices.graphicsFamily.Value;
            poolInfo.Flags = (uint)CommandPoolCreateFlagBits.ResetCommandBufferBit;
            CommandPool = LogicalDevice.CreateCommandPool(poolInfo);
        }

        private void CreateGraphicsPresenter(PresentationParameters parameters)
        {
            Presenter = GraphicsPresenter.Create(this, parameters);
            RenderPass = Presenter.RenderPass;
        }

        private void CreateCommandBuffers()
        {
            var buffersCount = Presenter.BuffersCount;
            
            commandBuffers = new CommandBuffer[buffersCount];

            var allocInfo = new CommandBufferAllocateInfo();
            allocInfo.CommandPool = CommandPool;
            allocInfo.Level = CommandBufferLevel.Primary;
            allocInfo.CommandBufferCount = buffersCount;

            commandBuffers = LogicalDevice.AllocateCommandBuffers(allocInfo);
        }

        private void CreateSyncObjects()
        {
            var semaphoreInfo = new SemaphoreCreateInfo();

            var fenceInfo = new FenceCreateInfo();
            fenceInfo.Flags = (uint)FenceCreateFlagBits.SignaledBit;

            ImageAvailableSemaphores = LogicalDevice.CreateSemaphores(semaphoreInfo, MaxFramesInFlight);
            RenderFinishedSemaphores = LogicalDevice.CreateSemaphores(semaphoreInfo, MaxFramesInFlight);
            InFlightFences = LogicalDevice.CreateFences(fenceInfo, MaxFramesInFlight);
        }

        public Queue GetDeviceQueue(uint queueFamilyIndex, uint queueIndex)
        {
            return LogicalDevice.GetDeviceQueue(queueFamilyIndex, queueIndex);
        }

        public Result DeviceWaitIdle()
        {
            return LogicalDevice.DeviceWaitIdle();
        }

        public Framebuffer CreateFramebuffer(FramebufferCreateInfo info)
        {
            return LogicalDevice.CreateFramebuffer(info);
        }

        public bool BeginDraw(Color clearColor, float depth = 1.0f, uint stencil = 0)
        {
            var renderFence = InFlightFences[CurrentFrame];
            var result = LogicalDevice.WaitForFences(1, renderFence, true, ulong.MaxValue);
            result = LogicalDevice.AcquireNextImageKHR((SwapChainGraphicsPresenter) Presenter, ulong.MaxValue, ImageAvailableSemaphores[CurrentFrame], null, ref imageIndex);

            if (result == Result.ErrorOutOfDateKhr)
            {
                return false;
            }
            
            if (result != Result.Success && result != Result.SuboptimalKhr)
            {
                throw new ArgumentException("Failed to acquire swap chain image!");
            }

            var commandBuffer = commandBuffers[ImageIndex];

            var beginInfo = new CommandBufferBeginInfo();
            beginInfo.Flags = (uint) CommandBufferUsageFlagBits.SimultaneousUseBit;

            result = commandBuffer.ResetCommandBuffer(0);
            if (result != Result.Success)
            {
                throw new Exception("failed to begin recording command buffer!");
            }

            result = commandBuffer.BeginCommandBuffer(beginInfo);
            if (result != Result.Success)
            {
                throw new Exception("failed to begin recording command buffer!");
            }

            var renderPassInfo = new RenderPassBeginInfo();
            renderPassInfo.RenderPass = renderPass;
            renderPassInfo.Framebuffer = Presenter.GetFramebuffer(ImageIndex);
            renderPassInfo.RenderArea = new Rect2D();
            renderPassInfo.RenderArea.Offset = new Offset2D();
            renderPassInfo.RenderArea.Extent = new Extent2D()
                {Width = Presenter.Description.Width, Height = Presenter.Description.Height};

            ClearValue clearColorValue = new ClearValue();
            clearColorValue.Color = new ClearColorValue();
            clearColorValue.Color.Float32 = clearColor.ToFloatArray();
            
            ClearValue clearDepthValue = new ClearValue();
            clearDepthValue.DepthStencil = new ClearDepthStencilValue();
            clearDepthValue.DepthStencil.Depth = depth;
            clearDepthValue.DepthStencil.Stencil = stencil;

            ClearValue clearColorValueResolve = new ClearValue();
            clearColorValueResolve.Color = new ClearColorValue();
            clearColorValueResolve.Color.Float32 = clearColor.ToFloatArray();

            renderPassInfo.PClearValues = new [] {clearColorValue, clearDepthValue, clearColorValueResolve };
            renderPassInfo.ClearValueCount = (uint)renderPassInfo.PClearValues.Length;

            commandBuffer.BeginRenderPass(renderPassInfo, SubpassContents.Inline);

            //commandBuffer.BindPipeline(PipelineBindPoint.Graphics, graphicsPipeline);

            return true;
        }

        public void EndDraw()
        {
            var commandBuffer = commandBuffers[ImageIndex];

            commandBuffer.EndRenderPass();

            var result = commandBuffer.EndCommandBuffer();
            if (result != Result.Success)
            {
                throw new Exception("failed to record command buffer!");
            }

            var submitInfo = new SubmitInfo();

            Semaphore[] waitSemaphores = new[] { ImageAvailableSemaphores[CurrentFrame] };
            uint[] waitStages = new[] { (uint)PipelineStageFlagBits.ColorAttachmentOutputBit };
            submitInfo.WaitSemaphoreCount = 1;
            submitInfo.PWaitSemaphores = waitSemaphores;
            submitInfo.PWaitDstStageMask = waitStages;

            submitInfo.CommandBufferCount = 1;
            submitInfo.PCommandBuffers = new CommandBuffer[] { commandBuffer };

            Semaphore[] signalSemaphores = new[] { RenderFinishedSemaphores[CurrentFrame] };

            submitInfo.SignalSemaphoreCount = 1;
            submitInfo.PSignalSemaphores = signalSemaphores;

            submitInfos[0] = submitInfo;

            var renderFence = InFlightFences[CurrentFrame];

            result = LogicalDevice.ResetFences(1, renderFence);

            result = graphicsQueue.QueueSubmit(1, submitInfos, renderFence);
            if (result != Result.Success)
            {
                throw new Exception($"failed to submit draw command buffer! Result was {result}");
            }
        }

        private void UpdateCurrentFrameNumber()
        {
            CurrentFrame = (CurrentFrame + 1) % MaxFramesInFlight;
        }

        public void SetViewports(params Viewport[] viewports)
        {
            if (viewports == null || viewports.Length == 0) return;
            
            CurrentCommandBuffer.SetViewport(0, (uint)viewports.Length, viewports);
        }
        
        public void SetScissors(params Rect2D[] scissors)
        {
            if (scissors == null || scissors.Length == 0) return;
            
            CurrentCommandBuffer.SetScissor(0, (uint)scissors.Length, scissors);
        }

        public void SetVertexBuffer(Buffer vertexBuffer)
        {
            ulong offset = 0;
            var commandBuffer = commandBuffers[ImageIndex];
            commandBuffer.BindVertexBuffers(0, 1, vertexBuffer, ref offset);
        }

        public void SetVertexBuffers(params Buffer[] vertexBuffers)
        {
            if (vertexBuffers == null || vertexBuffers.Length == 0) return;

            ulong[] offset = new ulong[vertexBuffers.Length];
            var commandBuffer = commandBuffers[ImageIndex];
            var buffers = vertexBuffers.Cast<AdamantiumVulkan.Core.Buffer>().ToArray();
            commandBuffer.BindVertexBuffers(0, (uint)buffers.Length, buffers, offset);
        }

        public void SetIndexBuffer(Buffer indexBuffer)
        {
            var commandBuffer = commandBuffers[ImageIndex];
            commandBuffer.BindIndexBuffer(indexBuffer, 0, IndexType.Uint32);
        }


        public void Draw(uint vertexCount, uint instanceCount, uint firstVertex = 0, uint firstInstance = 0)
        {
            if (CurrentEffectPass == null)
            {
                throw new ArgumentNullException("Effect pass should be applied before executiong draw");
            }
            
            var commandBuffer = commandBuffers[ImageIndex];
            var pipeline = pipelineManager.GetOrCreateGraphicsPipeline(this);

            ShouldChangeGraphicsPipeline = false;

            commandBuffer.BindPipeline(pipeline.BindPoint, pipeline);
            commandBuffer.BindDescriptorSets(
                PipelineBindPoint.Graphics, 
                CurrentEffectPass.PipelineLayout, 
                0, 
                1, 
                CurrentEffectPass.DescriptorSets[(int) ImageIndex], 
                0, 
                0);

            commandBuffer.Draw(vertexCount, instanceCount, firstVertex, firstInstance);
        }

        public void DrawIndexed(Buffer vertexBuffer, Buffer indexBuffer)
        {
            ulong offset = 0;
            var commandBuffer = commandBuffers[ImageIndex];

            var pipeline = pipelineManager.GetOrCreateGraphicsPipeline(this);

            ShouldChangeGraphicsPipeline = false;

            commandBuffer.BindPipeline(pipeline.BindPoint, pipeline);
            //commandBuffer.BindDescriptorSets(
            //    PipelineBindPoint.Graphics,
            //    CurrentEffectPass.PipelineLayout,
            //    0,
            //    1,
            //    CurrentEffectPass.DescriptorSets[(int)ImageIndex],
            //    0,
            //    0);
            

            commandBuffer.BindVertexBuffers(0, 1, vertexBuffer, ref offset);

            commandBuffer.BindIndexBuffer(indexBuffer, 0, IndexType.Uint32);

            commandBuffer.DrawIndexed(indexBuffer.ElementCount, 1, 0, 0, 0);
        }

        public void UpdateDescriptorSets(params WriteDescriptorSet[] writeDescriptorSets)
        {
            if (writeDescriptorSets == null || writeDescriptorSets.Length == 0) return;
           
            LogicalDevice.UpdateDescriptorSets((uint)writeDescriptorSets.Length, writeDescriptorSets, 0, out var copySets);
        }

        public CommandBuffer BeginSingleTimeCommands()
        {
            return LogicalDevice.BeginSingleTimeCommand(CommandPool);
        }

        public void EndSingleTimeCommands(CommandBuffer commandBuffer)
        {
            LogicalDevice.EndSingleTimeCommands(graphicsQueue, CommandPool, commandBuffer);
        }

        public IntPtr MapMemory(DeviceMemory memory, ulong offset, ulong size, uint flags)
        {
            return LogicalDevice.MapMemory(memory, offset, size, flags);
        }

        public void UnmapMemory(DeviceMemory memory)
        {
            LogicalDevice.UnmapMemory(memory);
        }

        public bool ResizePresenter(uint width = 0, uint height = 0)
        {
            bool ResizeFunc() => Presenter.Resize(width, height);
            return ResizePresenter(ResizeFunc);
        }

        public bool ResizePresenter(uint width, uint height, uint buffersCount, SurfaceFormat surfaceFormat, DepthFormat depthFormat)
        {
            bool ResizeFunc() => Presenter.Resize(width, height, buffersCount, surfaceFormat, depthFormat);
            return ResizePresenter(ResizeFunc);
        }

        private bool ResizePresenter(Func<bool> resizeFunc)
        {
            var result = LogicalDevice.DeviceWaitIdle();
            var resizeResult = resizeFunc();
            if (!resizeResult)
            {
                return false;
            }
            // graphicsPipeline?.Destroy(LogicalDevice);
            // CreateGraphicsPipeline();
            OnSurfaceSizeChanged();
            return true;
        }

        public void Present()
        {
            var presentResult = Presenter.Present();
            if (presentResult == Result.SuboptimalKhr || presentResult == Result.ErrorOutOfDateKhr)
            {
                //ResizePresenter();
            }
            
            UpdateCurrentFrameNumber();
        }

        internal Semaphore GetImageAvailableSemaphoreForCurrentFrame()
        {
            return ImageAvailableSemaphores[CurrentFrame];
        }

        internal Semaphore GetRenderFinishedSemaphoreForCurrentFrame()
        {
            return RenderFinishedSemaphores[CurrentFrame];
        }

        public static implicit operator PhysicalDevice (GraphicsDevice device)
        {
            return PhysicalDevice;
        }

        public static implicit operator Device(GraphicsDevice device)
        {
            return device.LogicalDevice;
        }

        public event EventHandler SurfaceSizeChanged;

        protected void OnSurfaceSizeChanged()
        {
            SurfaceSizeChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
