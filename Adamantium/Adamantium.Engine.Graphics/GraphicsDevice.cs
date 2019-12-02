using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Adamantium.Core;
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
        internal Device LogicalDevice { get; private set; }
        private Queue graphicsQueue;
        private RenderPass renderPass;
        private SurfaceKHR surface;
        private DescriptorSetLayout descriptorSetLayout;

        private DescriptorPool descriptorPool;
        private Pipeline graphicsPipeline;
        private Framebuffer[] defaultFramebuffers;

        private PipelineLayout pipelineLayout;

        private CommandBuffer[] commandBuffers;
        public CommandPool CommandPool { get; private set; }

        internal Semaphore[] ImageAvailableSemaphores { get; private set; }
        internal Semaphore[] RenderFinishedSemaphores { get; private set; }
        internal Fence[] InFlightFences { get; private set; }

        public uint CurrentFrame { get; private set; }

        public uint ImageIndex => imageIndex;

        public readonly uint MaxFramesInFlight;

        public Pipeline CurrentGraphicsPipeline { get; internal set; }

        public List<EffectPool> EffectPools { get; private set; }

        public EffectPool DefaultEffectPool { get; private set; }


        private SubmitInfo[] submitInfos = new SubmitInfo[1];
        private uint imageIndex;

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
                main.LogicalDevice = this.LogicalDevice;
            }
            MaxFramesInFlight = presentationParameters.BuffersCount;
            MainDevice = main.MainDevice;
            LogicalDevice = main.LogicalDevice;
            InitializeRenderDevice(presentationParameters);
        }

        public static VulkanInstance VulkanInstance { get; private set; }

        internal GraphicsPresenter Presenter { get; private set; }

        public GraphicsDevice MainDevice { get; private set; }

        public bool IsMain { get; private set; }

        private void InitializeRenderDevice(PresentationParameters presentationParameters)
        {
            CreateCommandPool();
            CreateGraphicsPresenter(presentationParameters);
            CreateRenderPass();
            CreateDefaultFramebuffers();
            CreateCommandBuffers();
            CreateDescriptorPool();
            //CreateDescriptorSetLayout();
            CreatePipelineLayout();
            CreateGraphicsPipeline();
            CreateSyncObjects();
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

        private void CreateRenderPass()
        {
            var colorAttachment = new AttachmentDescription();
            colorAttachment.Format = Presenter.Description.ImageFormat;
            colorAttachment.Samples = (SampleCountFlagBits)Presenter.Description.MSAALevel;
            colorAttachment.LoadOp = AttachmentLoadOp.Clear;
            colorAttachment.StoreOp = AttachmentStoreOp.Store;
            colorAttachment.StencilLoadOp = AttachmentLoadOp.DontCare;
            colorAttachment.StencilStoreOp = AttachmentStoreOp.DontCare;
            colorAttachment.InitialLayout = ImageLayout.Undefined;
            colorAttachment.FinalLayout = ImageLayout.ColorAttachmentOptimal;

            var depthAttachment = new AttachmentDescription();
            depthAttachment.Format = (Format)Presenter.Description.DepthFormat;
            depthAttachment.Samples = (SampleCountFlagBits)Presenter.Description.MSAALevel;
            depthAttachment.LoadOp = AttachmentLoadOp.Clear;
            depthAttachment.StoreOp = AttachmentStoreOp.Store;
            depthAttachment.StencilLoadOp = AttachmentLoadOp.DontCare;
            depthAttachment.StencilStoreOp = AttachmentStoreOp.DontCare;
            depthAttachment.InitialLayout = ImageLayout.Undefined;
            depthAttachment.FinalLayout = Presenter.DepthBuffer.ImageLayout;

            var colorAttachmentResolve = new AttachmentDescription();
            colorAttachmentResolve.Format = Presenter.Description.ImageFormat;
            colorAttachmentResolve.Samples = SampleCountFlagBits._1Bit;
            colorAttachmentResolve.LoadOp = AttachmentLoadOp.Clear;
            colorAttachmentResolve.StoreOp = AttachmentStoreOp.Store;
            colorAttachmentResolve.StencilLoadOp = AttachmentLoadOp.DontCare;
            colorAttachmentResolve.StencilStoreOp = AttachmentStoreOp.DontCare;
            colorAttachmentResolve.InitialLayout = ImageLayout.Undefined;
            colorAttachmentResolve.FinalLayout = ImageLayout.PresentSrcKhr;

            var colorAttachmentRef = new AttachmentReference();
            colorAttachmentRef.Attachment = 0;
            colorAttachmentRef.Layout = ImageLayout.ColorAttachmentOptimal;

            var depthAttachmentRef = new AttachmentReference();
            depthAttachmentRef.Attachment = 1;
            depthAttachmentRef.Layout = Presenter.DepthBuffer.ImageLayout;

            var colorAttachmentResolveRef = new AttachmentReference();
            colorAttachmentResolveRef.Attachment = 2;
            colorAttachmentResolveRef.Layout = ImageLayout.ColorAttachmentOptimal;

            var subpass = new SubpassDescription();
            subpass.PipelineBindPoint = PipelineBindPoint.Graphics;
            subpass.ColorAttachmentCount = 1;
            subpass.PColorAttachments = new[] { colorAttachmentRef };
            subpass.PDepthStencilAttachment = depthAttachmentRef;
            subpass.PResolveAttachments = new[] { colorAttachmentResolveRef };
            
            SubpassDependency subpassDependency = new SubpassDependency();
            subpassDependency.SrcSubpass = Constants.VK_SUBPASS_EXTERNAL;
            subpassDependency.DstSubpass = 0;
            subpassDependency.SrcStageMask = (uint)PipelineStageFlagBits.ColorAttachmentOutputBit;
            subpassDependency.SrcAccessMask = 0;
            subpassDependency.DstStageMask = (uint) PipelineStageFlagBits.ColorAttachmentOutputBit;
            subpassDependency.DstAccessMask = (uint)(AccessFlagBits.ColorAttachmentReadBit | AccessFlagBits.ColorAttachmentWriteBit);

            var attachments = new [] { colorAttachment, depthAttachment, colorAttachmentResolve}; 
            var renderPassInfo = new RenderPassCreateInfo();
            renderPassInfo.AttachmentCount = (uint)attachments.Length;
            renderPassInfo.PAttachments = attachments;
            renderPassInfo.SubpassCount = 1;
            renderPassInfo.PSubpasses = new[] {subpass};
            renderPassInfo.DependencyCount = 1;
            renderPassInfo.PDependencies = new[] {subpassDependency};

            renderPass = LogicalDevice.CreateRenderPass(renderPassInfo);
        }

        private void CreateDescriptorPool()
        {
            DescriptorPoolSize poolSize = new DescriptorPoolSize();
            poolSize.Type = DescriptorType.CombinedImageSampler;
            poolSize.DescriptorCount = Presenter.Description.BuffersCount;

            DescriptorPoolCreateInfo poolInfo = new DescriptorPoolCreateInfo();
            poolInfo.PoolSizeCount = 1;
            poolInfo.PPoolSizes = poolSize;
            poolInfo.MaxSets = Presenter.Description.BuffersCount;

            descriptorPool = LogicalDevice.CreateDescriptorPool(poolInfo);
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

            descriptorSetLayout = LogicalDevice.CreateDescriptorSetLayout(layoutInfo);
        }

        public DescriptorSetLayout CreateDescriptorSetLayout(DescriptorSetLayoutCreateInfo layoutCreateInfo)
        {
            return LogicalDevice.CreateDescriptorSetLayout(layoutCreateInfo);
        }

        public void CreateGraphicsPipelineForEffectPass(EffectPass effectPass, DescriptorSetLayout descriptorSetLayout, PipelineShaderStageCreateInfo[] shaderStages)
        {

        }

        private void CreatePipelineLayout()
        {
            var pipelineLayoutInfo = new PipelineLayoutCreateInfo();
            //pipelineLayoutInfo.SetLayoutCount = 1;
            //pipelineLayoutInfo.PSetLayouts = new DescriptorSetLayout[] { descriptorSetLayout };
            pipelineLayoutInfo.SetLayoutCount = 0;
            pipelineLayoutInfo.PushConstantRangeCount = 0;

            pipelineLayout = LogicalDevice.CreatePipelineLayout(pipelineLayoutInfo);
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

            var bindingDescr = GetBindingDescription<MeshVertex>();
            var attributesDescriptions = GetVertexAttributeDescription<MeshVertex>();

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
            viewportState.PViewports = viewport;
            viewportState.ScissorCount = 1;
            viewportState.PScissors = scissor;

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

            var colorBlendAttachment = new PipelineColorBlendAttachmentState();
            colorBlendAttachment.ColorWriteMask = (uint)(ColorComponentFlagBits.RBit | ColorComponentFlagBits.GBit | ColorComponentFlagBits.BBit | ColorComponentFlagBits.ABit);
            colorBlendAttachment.BlendEnable = false;
            
            var depthStencil = new PipelineDepthStencilStateCreateInfo();
            depthStencil.DepthTestEnable = false;
            depthStencil.DepthWriteEnable = true;
            depthStencil.DepthCompareOp = CompareOp.Less;
            depthStencil.DepthBoundsTestEnable = false;
            depthStencil.MinDepthBounds = 0.0f;
            depthStencil.MaxDepthBounds = 1.0f;
//            depthStencil.StencilTestEnable = true;
//            depthStencil.Front = new StencilOpState();
//            depthStencil.Back = new StencilOpState();

            var colorBlending = new PipelineColorBlendStateCreateInfo();
            colorBlending.LogicOpEnable = false;
            colorBlending.LogicOp = LogicOp.Copy;
            colorBlending.AttachmentCount = 1;
            colorBlending.PAttachments = colorBlendAttachment;
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
            pipelineInfo.Layout = pipelineLayout;
            pipelineInfo.RenderPass = renderPass;
            pipelineInfo.PDepthStencilState = depthStencil;
            pipelineInfo.Subpass = 0;

            var pipelines = LogicalDevice.CreateGraphicsPipelines(null, 1, pipelineInfo);
            graphicsPipeline = pipelines[0];
            CurrentGraphicsPipeline = graphicsPipeline;

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
        }

        private void CreateCommandBuffers()
        {
            commandBuffers = new CommandBuffer[defaultFramebuffers.Length];

            var allocInfo = new CommandBufferAllocateInfo();
            allocInfo.CommandPool = CommandPool;
            allocInfo.Level = CommandBufferLevel.Primary;
            allocInfo.CommandBufferCount = (uint)defaultFramebuffers.Length;

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

        private void CreateDefaultFramebuffers()
        {
            defaultFramebuffers = new Framebuffer[Presenter.Description.BuffersCount];

            for (int i = 0; i < defaultFramebuffers.Length; i++)
            {
                FramebufferCreateInfo framebufferInfo = new FramebufferCreateInfo();
                framebufferInfo.RenderPass = renderPass;
                var swapchainPresenter = (SwapChainGraphicsPresenter)Presenter;
                framebufferInfo.PAttachments = new [] { Presenter.RenderTarget, Presenter.DepthBuffer, swapchainPresenter.swapchainImageViews[i] };
                framebufferInfo.AttachmentCount = (uint)framebufferInfo.PAttachments.Length;
                framebufferInfo.Width = Presenter.Width;
                framebufferInfo.Height = Presenter.Height;
                framebufferInfo.Layers = 1;

                defaultFramebuffers[i] = LogicalDevice.CreateFramebuffer(framebufferInfo);

                framebufferInfo.Dispose();
            }
        }

        public Queue GetDeviceQueue(uint queueFamilyIndex, uint queueIndex)
        {
            return LogicalDevice.GetDeviceQueue(queueFamilyIndex, queueIndex);
        }

        public Result DeviceWaitIdle()
        {
            return LogicalDevice.DeviceWaitIdle();
        }

        public bool BeginDraw(Color clearColor, float depth = 1.0f, uint stencil = 0)
        {
            var renderFence = InFlightFences[CurrentFrame];
            var result = LogicalDevice.WaitForFences(1, renderFence, true, ulong.MaxValue);
            result = LogicalDevice.AcquireNextImageKHR((SwapChainGraphicsPresenter) Presenter, ulong.MaxValue,
                ImageAvailableSemaphores[CurrentFrame], null, ref imageIndex);

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
            renderPassInfo.Framebuffer = defaultFramebuffers[ImageIndex];
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

            commandBuffer.CmdBeginRenderPass(renderPassInfo, SubpassContents.Inline);

            commandBuffer.CmdBindPipeline(PipelineBindPoint.Graphics, graphicsPipeline);

            return true;
        }

        public void EndDraw()
        {
            var commandBuffer = commandBuffers[ImageIndex];

            commandBuffer.CmdEndRenderPass();

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

        public void SetVertexBuffer(Buffer vertexBuffer)
        {
            ulong offset = 0;
            var commandBuffer = commandBuffers[ImageIndex];
            commandBuffer.CmdBindVertexBuffers(0, 1, vertexBuffer, ref offset);
        }

        public void Draw(uint vertexCount, uint instanceCount, uint firstVertex = 0, uint firstInstance = 0)
        {
            var commandBuffer = commandBuffers[ImageIndex];
            //commandBuffer.CmdBindDescriptorSets(PipelineBindPoint.Graphics, pipelineLayout, 0, 1, descriptorSets[CurrentImageIndex], 0, 0);

            commandBuffer.CmdDraw(vertexCount, instanceCount, firstVertex, firstInstance);
        }

        public void DrawIndexed(Buffer vertexBuffer, Buffer indexBuffer)
        {
            ulong offset = 0;
            var commandBuffer = commandBuffers[ImageIndex];
            commandBuffer.CmdBindVertexBuffers(0, 1, vertexBuffer, ref offset);

            commandBuffer.CmdBindIndexBuffer(indexBuffer, 0, IndexType.Uint32);

            //commandBuffer.CmdBindDescriptorSets(PipelineBindPoint.Graphics, pipelineLayout, 0, 1, descriptorSets[CurrentImageIndex], 0, 0);

            commandBuffer.CmdDrawIndexed(indexBuffer.ElementCount, 1, 0, 0, 0);
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
            DestroyFramebuffers();
            var resizeResult = resizeFunc();
            if (!resizeResult)
            {
                return false;
            }
            graphicsPipeline?.Destroy(LogicalDevice);
            CreateGraphicsPipeline();
            CreateDefaultFramebuffers();
            return true;
        }

        public void Present()
        {
            var presentResult = Presenter.Present();
            if (presentResult == Result.SuboptimalKhr || presentResult == Result.ErrorOutOfDateKhr)
            {
                ResizePresenter();
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
            return GraphicsDevice.PhysicalDevice;
        }

        public static implicit operator Device(GraphicsDevice device)
        {
            return device.LogicalDevice;
        }

        private void DestroyFramebuffers()
        {
            for (int i = 0; i< defaultFramebuffers.Length; ++i)
            {
                defaultFramebuffers[i].Destroy(LogicalDevice);
            }
        }


        private VertexInputBindingDescription GetBindingDescription<T>() where T : struct
        {
            var decr = new VertexInputBindingDescription();
            decr.Binding = 0;
            decr.Stride = (uint)Marshal.SizeOf<T>();
            decr.InputRate = VertexInputRate.Vertex;

            return decr;
        }

        private VertexInputAttributeDescription[] GetVertexAttributeDescription<T>()
        {
            var fields = typeof(T).GetFields();

            var attributes = new List<VertexInputAttributeDescription>();
            uint location = 0;

            foreach (var field in fields)
            {
                if (field.IsInitOnly) continue;

                var desc = new VertexInputAttributeDescription();
                desc.Binding = 0;
                desc.Location = location;
                desc.Format = GetFormat(Marshal.SizeOf(field.FieldType));
                desc.Offset = (uint)Marshal.OffsetOf<T>(field.Name).ToInt32();
                location++;
                attributes.Add(desc);
            }

            return attributes.ToArray();
        }

        private Format GetFormat(int size)
        {
            switch (size)
            {
                case 4:
                    return Format.R32_SFLOAT;
                case 8:
                    return Format.R32G32_SFLOAT;
                case 12:
                    return Format.R32G32B32_SFLOAT;
                case 16:
                    return Format.R32G32B32A32_SFLOAT;

                default:
                    throw new Exception($"size {size} is not supported");
            }
        }
    }
}
