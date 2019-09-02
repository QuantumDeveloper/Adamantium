using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using Adamantium.Core;
using Adamantium.Engine.Core;
using Adamantium.Mathematics;
using AdamantiumVulkan.Core;

namespace Adamantium.Engine.Graphics
{
    public class GraphicsDevice : DisposableObject
    {
        private VulkanInstance instance;
        private PhysicalDevice physicalDevice;
        internal Device LogicalDevice { get; private set; }
        private Queue graphicsQueue;
        private RenderPass renderPass;
        private Framebuffer[] framebuffers;
        private SurfaceKHR surface;
        private DescriptorSetLayout samplerDescriptor;
        private DescriptorPool descriptorPool;
        private Pipeline graphicsPipeline;
        private Framebuffer[] swapchainFramebuffers;

        private CommandBuffer[] commandBuffers;
        public CommandPool CommandPool { get; private set; }

        internal Semaphore[] ImageAvailableSemaphores { get; private set; }
        internal Semaphore[] RenderFinishedSemaphores { get; private set; }
        internal Fence[] InFlightFences { get; private set; }

        public uint CurrentFrame { get; private set; }

        public uint CurrentImageIndex => currentImageIndex;

        public readonly uint MaxFramesInFlight;


        private SubmitInfo[] submitInfos = new SubmitInfo[1];
        private uint currentImageIndex;

        private GraphicsDevice(VulkanInstance instance, PhysicalDevice physicalDevice)
        {
            this.instance = instance;
            this.physicalDevice = physicalDevice;
            CreateMainDevice();
        }

        private GraphicsDevice(GraphicsDevice main, PresentationParameters presentationParameters)
        {
            MaxFramesInFlight = presentationParameters.BuffersCount;
            MainDevice = main.MainDevice;
            LogicalDevice = main.LogicalDevice;
            InitializeRenderDevice(presentationParameters);
        }

        public static VulkanInstance Instance { get; private set; }

        public GraphicsPresenter Presenter { get; private set; }

        public GraphicsDevice MainDevice { get; private set; }

        public bool IsMain { get; private set; }

        private void InitializeRenderDevice(PresentationParameters presentationParameters)
        {
            Presenter = GraphicsPresenter.Create(this, presentationParameters);
            CreateRenderPass();
            CreateDefaultFramebuffers();
            CreateCommandPool();
            CreateCommandBuffers();
            CreateDescriptorPool();
            CreateSyncObjects();
            CreateGraphicsPipeline();
        }

        public static GraphicsDevice Create(VulkanInstance instance, PhysicalDevice physicalDevice)
        {
            return new GraphicsDevice(instance, physicalDevice);
        }

        private GraphicsDevice CreateRenderDevice(PresentationParameters parameters)
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
            var indices = physicalDevice.FindQueueFamilies(surface);

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

            var deviceFeatures = physicalDevice.GetPhysicalDeviceFeatures();
            deviceFeatures.SamplerAnisotropy = true;

            var createInfo = new DeviceCreateInfo();
            createInfo.QueueCreateInfoCount = (uint)queueInfos.Count;
            createInfo.PQueueCreateInfos = queueInfos.ToArray();
            createInfo.PEnabledFeatures = deviceFeatures;
            createInfo.EnabledExtensionCount = (uint)VulkanInstance.DeviceExtensions.Count;
            createInfo.PpEnabledExtensionNames = VulkanInstance.DeviceExtensions.ToArray();

            if (instance.IsInDebugMode)
            {
                createInfo.EnabledLayerCount = (uint)VulkanInstance.ValidationLayers.Count;
                createInfo.PpEnabledLayerNames = VulkanInstance.ValidationLayers.ToArray();
            }

            LogicalDevice = physicalDevice.CreateDevice(createInfo);

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
            colorAttachment.FinalLayout = ImageLayout.PresentSrcKhr;

            var colorAttachmentRef = new AttachmentReference();
            colorAttachmentRef.Attachment = 0;
            colorAttachmentRef.Layout = ImageLayout.ColorAttachmentOptimal;

            var subpass = new SubpassDescription();
            subpass.PipelineBindPoint = PipelineBindPoint.Graphics;
            subpass.ColorAttachmentCount = 1;
            subpass.PColorAttachments = colorAttachmentRef;

            var renderPassInfo = new RenderPassCreateInfo();
            renderPassInfo.AttachmentCount = 1;
            renderPassInfo.PAttachments = colorAttachment;
            renderPassInfo.SubpassCount = 1;
            renderPassInfo.PSubpasses = subpass;

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

            if (LogicalDevice.CreateDescriptorPool(poolInfo, null, out descriptorPool) != Result.Success)
            {
                throw new Exception("failed to create descriptor pool!");
            }
        }

        private void CreateGraphicsPipeline()
        {
            //var vertexContent = File.ReadAllBytes(@"shaders\vert.spv");
            //var fragmentContent = File.ReadAllBytes(@"shaders\frag.spv");

            //var vertexShaderModule = CreateShaderModule(vertexContent);
            //var fragmentShaderModule = CreateShaderModule(fragmentContent);

            //var vertShaderStageInfo = new PipelineShaderStageCreateInfo();
            //vertShaderStageInfo.Stage = ShaderStageFlagBits.VertexBit;
            //vertShaderStageInfo.Module = vertexShaderModule;
            //vertShaderStageInfo.PName = "main";

            //var fragShaderStageInfo = new PipelineShaderStageCreateInfo();
            //fragShaderStageInfo.Stage = ShaderStageFlagBits.FragmentBit;
            //fragShaderStageInfo.Module = fragmentShaderModule;
            //fragShaderStageInfo.PName = "main";

            //PipelineShaderStageCreateInfo[] shaderStages = new[] { vertShaderStageInfo, fragShaderStageInfo };

            //var bindingDescr = GetBindingDescription<Vertex>();
            //var attributesDescriptions = GetVertexAttributeDescription<Vertex>();

            //var vertexInputInfo = new PipelineVertexInputStateCreateInfo();
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
            viewport.Width = Presenter.Description.Width;
            viewport.Height = Presenter.Description.Height;
            viewport.MinDepth = 0.0f;
            viewport.MaxDepth = 1.0f;

            Rect2D scissor = new Rect2D();
            scissor.Offset = new Offset2D();
            scissor.Extent = new Extent2D() { Width = Presenter.Description.Width, Height = Presenter.Description.Height };

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
            multisampling.SampleShadingEnable = false;
            multisampling.RasterizationSamples = SampleCountFlagBits._1Bit;

            var colorBlendAttachment = new PipelineColorBlendAttachmentState();
            colorBlendAttachment.ColorWriteMask = (uint)(ColorComponentFlagBits.RBit | ColorComponentFlagBits.GBit | ColorComponentFlagBits.BBit | ColorComponentFlagBits.ABit);
            colorBlendAttachment.BlendEnable = false;

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

            var pipelineLayoutInfo = new PipelineLayoutCreateInfo();
            pipelineLayoutInfo.SetLayoutCount = 0;
            //pipelineLayoutInfo.PSetLayouts = descriptorSetLayout;

            //pipelineLayout = logicalDevice.CreatePipelineLayout(pipelineLayoutInfo);

            var pipelineInfo = new GraphicsPipelineCreateInfo();
            pipelineInfo.StageCount = 0;
            //pipelineInfo.PStages = shaderStages;
            //pipelineInfo.PVertexInputState = vertexInputInfo;
            pipelineInfo.PInputAssemblyState = inputAssembly;
            pipelineInfo.PViewportState = viewportState;
            pipelineInfo.PRasterizationState = rasterizer;
            pipelineInfo.PMultisampleState = multisampling;
            pipelineInfo.PColorBlendState = colorBlending;
            //pipelineInfo.Layout = pipelineLayout;
            pipelineInfo.RenderPass = renderPass;
            pipelineInfo.Subpass = 0;

            var pipelines = LogicalDevice.CreateGraphicsPipelines(null, 1, pipelineInfo);
            graphicsPipeline = pipelines[0];

            pipelineInfo.Dispose();
            //logicalDevice.DestroyShaderModule(vertexShaderModule);
            //logicalDevice.DestroyShaderModule(fragmentShaderModule);
        }

        private void CreateCommandPool()
        {
            var queueFamilyIndices = physicalDevice.FindQueueFamilies(surface);

            var poolInfo = new CommandPoolCreateInfo();
            poolInfo.QueueFamilyIndex = queueFamilyIndices.graphicsFamily.Value;
            poolInfo.Flags = (uint)CommandPoolCreateFlagBits.ResetCommandBufferBit;
            CommandPool = LogicalDevice.CreateCommandPool(poolInfo);
        }

        private void CreateCommandBuffers()
        {
            commandBuffers = new CommandBuffer[swapchainFramebuffers.Length];

            var allocInfo = new CommandBufferAllocateInfo();
            allocInfo.CommandPool = CommandPool;
            allocInfo.Level = CommandBufferLevel.Primary;
            allocInfo.CommandBufferCount = (uint)swapchainFramebuffers.Length;

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
            swapchainFramebuffers = new Framebuffer[Presenter.Description.BuffersCount];

            for (int i = 0; i < swapchainFramebuffers.Length; i++)
            {
                FramebufferCreateInfo framebufferInfo = new FramebufferCreateInfo();
                framebufferInfo.RenderPass = renderPass;
                framebufferInfo.AttachmentCount = 1;
                var swapchainPresenter = (SwapChainGraphicsPresenter)Presenter;
                framebufferInfo.PAttachments = swapchainPresenter.swapchainImageViews[i];
                framebufferInfo.Width = Presenter.Description.Width;
                framebufferInfo.Height = Presenter.Description.Height;
                framebufferInfo.Layers = 1;

                swapchainFramebuffers[i] = LogicalDevice.CreateFramebuffer(framebufferInfo);

                framebufferInfo.Dispose();
            }
        }

        public Queue GetDeviceQueue(uint queueFamilyIndex, uint queueIndex)
        {
            return LogicalDevice.GetDeviceQueue(queueFamilyIndex, queueIndex);
        }

        public void DeviceWaitIdle()
        {
            LogicalDevice.DeviceWaitIdle();
        }

        public void BeginDrawCommand()
        {
            var renderFence = InFlightFences[CurrentFrame];
            var result = LogicalDevice.WaitForFences(1, renderFence, true, ulong.MaxValue);
            result = LogicalDevice.AcquireNextImageKHR((SwapChainGraphicsPresenter)Presenter, ulong.MaxValue, ImageAvailableSemaphores[CurrentFrame], null, ref currentImageIndex);

            //if (result == Result.ErrorOutOfDateKhr)
            //{
            //    Presenter.Resize()
            //    return;
            //}
            //else if (result != Result.Success && result != Result.SuboptimalKhr)
            //{
            //    MessageBox.Show("Failed to acquire swap chain image!");
            //    throw new ArgumentException();
            //}

            if (result != Result.Success && result != Result.SuboptimalKhr)
            {
                throw new ArgumentException("Failed to acquire swap chain image!");
            }

            var commandBuffer = commandBuffers[CurrentImageIndex];

            var beginInfo = new CommandBufferBeginInfo();
            beginInfo.Flags = (uint)CommandBufferUsageFlagBits.SimultaneousUseBit;

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
            renderPassInfo.Framebuffer = swapchainFramebuffers[CurrentFrame];
            renderPassInfo.RenderArea = new Rect2D();
            renderPassInfo.RenderArea.Offset = new Offset2D();
            renderPassInfo.RenderArea.Extent = new Extent2D() { Width = Presenter.Description.Width, Height = Presenter.Description.Height };

            ClearValue clearValue = new ClearValue();
            clearValue.Color = new ClearColorValue();
            clearValue.Color.Float32 = new float[4] { 0.5f, 0.7f, 1.0f, 0.0f };

            renderPassInfo.ClearValueCount = 1;
            renderPassInfo.PClearValues = new ClearValue[] { clearValue };

            commandBuffer.CmdBeginRenderPass(renderPassInfo, SubpassContents.Inline);

            commandBuffer.CmdBindPipeline(PipelineBindPoint.Graphics, graphicsPipeline);
        }

        public void EndDrawCommand()
        {
            var commandBuffer = commandBuffers[CurrentImageIndex];
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

        public void Draw(Buffer vertexBuffer)
        {
            ulong offset = 0;
            var commandBuffer = commandBuffers[CurrentFrame];
            commandBuffer.CmdBindVertexBuffers(0, 1, vertexBuffer, ref offset);

            commandBuffer.CmdBindDescriptorSets(PipelineBindPoint.Graphics, pipelineLayout, 0, 1, descriptorSets[CurrentImageIndex], 0, 0);

            commandBuffer.CmdDraw(vertexBuffer.ElementCount, 1, 0, 0);
        }

        public void DrawIndexed(Buffer vertexBuffer, Buffer indexBuffer)
        {
            ulong offset = 0;
            var commandBuffer = commandBuffers[CurrentFrame];
            commandBuffer.CmdBindVertexBuffers(0, 1, vertexBuffer, ref offset);

            commandBuffer.CmdBindIndexBuffer(indexBuffer, 0, IndexType.Uint32);

            commandBuffer.CmdBindDescriptorSets(PipelineBindPoint.Graphics, pipelineLayout, 0, 1, descriptorSets[CurrentImageIndex], 0, 0);

            commandBuffer.CmdDrawIndexed(indexBuffer.ElementCount, 1, 0, 0, 0);
        }

        internal Semaphore GetImageAvailableSemaphoreForCurrentFrame()
        {
            return ImageAvailableSemaphores[CurrentFrame];
        }

        internal Semaphore GetRenderFinishedSemaphoreForCurrentFrame()
        {
            return ImageAvailableSemaphores[CurrentFrame];
        }

        public static implicit operator PhysicalDevice (GraphicsDevice device)
        {
            return device.physicalDevice;
        }

        public static implicit operator Device(GraphicsDevice device)
        {
            return device.LogicalDevice;
        }
    }
}
