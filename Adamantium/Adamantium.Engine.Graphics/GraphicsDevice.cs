using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Adamantium.Core;
using Adamantium.Core.Collections;
using Adamantium.Engine.Core.Effects;
using Adamantium.Engine.Graphics.Effects;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using AdamantiumVulkan.Core;
using Semaphore = AdamantiumVulkan.Core.Semaphore;

namespace Adamantium.Engine.Graphics
{
    public class GraphicsDevice : DisposableObject
    {
        public string DeviceId { get; private set; }

        private SurfaceKHR surface;
        private Pipeline graphicsPipeline;
        private CommandBuffer[] commandBuffers;
        internal Queue GraphicsQueue { get; private set; }
        private Queue resourceQueue;
        private Queue computeQueue;
        
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

        internal Device LogicalDevice => MainDevice?.LogicalDevice;

        internal VulkanInstance VulkanInstance => MainDevice?.VulkanInstance;
        
        internal bool ShouldChangeGraphicsPipeline { get; private set; }
        
        private Mutex mutex;
        private static string MutextGuid = Guid.NewGuid().ToString();
        
        static GraphicsDevice()
        {
            BlendStates = new BlendStatesCollection();
            RasterizerStates = new RasterizerStateCollection();
            DepthStencilStates = new DepthStencilStatesCollection();
            
        }

        private GraphicsDevice(MainGraphicsDevice mainDevice)
        {
            InitializeMutex();
            MainDevice = mainDevice;
            MaxFramesInFlight = 1;
            IsResourceLoaderDevice = true;
            InitializeResourceLoadingDevice();
        }

        private GraphicsDevice(MainGraphicsDevice mainDevice, PresentationParameters presentationParameters)
        {
            CreateDevice(mainDevice, presentationParameters);
        }

        private void CreateDevice(MainGraphicsDevice mainDevice, PresentationParameters presentationParameters)
        {
            InitializeMutex();
            DeviceId = Guid.NewGuid().ToString();
            MainDevice = mainDevice;
            
            surface = MainDevice.VulkanInstance.GetOrCreateSurface(presentationParameters);

            EffectPools = new List<EffectPool>();
            DefaultEffectPool = EffectPool.New(this);
            
            MaxFramesInFlight = presentationParameters.BuffersCount;
            InitializeRenderDevice(presentationParameters);
            dynamicStates = new TrackingCollection<DynamicState>();
            viewports = new TrackingCollection<Viewport>();
            scissors = new TrackingCollection<Rect2D>();

            BlendState = BlendStates.Fonts;
            //RasterizerState = RasterizerStates.CullBackClipDisabled;
            RasterizerState = RasterizerStates.CullNoneClipDisabled;
            DepthStencilState = DepthStencilStates.Default;
            SamplerStates = new SamplerStateCollection(this);
            Sampler = SamplerStates.Default;
            
            ClearColor = Colors.CornflowerBlue;

            pipelineManager = new PipelineManager(this);

            BasicEffect = Effect.Load(Path.Combine("Effects", "BasicEffect.fx.compiled"), this);
            //BasicEffect = Effect.Load(Path.Combine("HLSL", "BasicEffect.fx.compiled"), this);
        }
        
        public bool IsResourceLoaderDevice { get; }
        
        public CommandPool CommandPool { get; private set; }

        internal Semaphore[] ImageAvailableSemaphores { get; private set; }
        internal Semaphore[] RenderFinishedSemaphores { get; private set; }
        internal Fence[] InFlightFences { get; private set; }

        public event Action FrameFinished;

        public uint CurrentFrame { get; private set; }

        public uint ImageIndex => imageIndex;

        public uint MaxFramesInFlight { get; private set; }

        public GraphicsPipeline CurrentGraphicsPipeline { get; private set; }

        public List<EffectPool> EffectPools { get; private set; }

        public EffectPool DefaultEffectPool { get; private set; }

        internal GraphicsPresenter Presenter { get; private set; }

        public MainGraphicsDevice MainDevice { get; private set; }

        public bool CanPresent { get; private set; }
        
        public static BlendStatesCollection BlendStates { get; }
        
        public static RasterizerStateCollection RasterizerStates { get; }
        
        public static DepthStencilStatesCollection DepthStencilStates { get; }
        
        public SamplerStateCollection SamplerStates { get; internal set; }
        
        public Effect BasicEffect { get; private set; }
        
        public Color ClearColor { get; set; }
        
        public SamplerState Sampler { get; set; }

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

        public Viewport[] Viewports => viewports.ToArray();

        public Rect2D[] Scissors => scissors.ToArray();

        public bool IsDynamic => dynamicStates.Count > 0;

        private void InitializeMutex()
        {
            if (!Mutex.TryOpenExisting(MutextGuid, out mutex))
            {
                mutex = new Mutex(false, MutextGuid);
            }
        }

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
            CreateSyncObjects();
        }

        private void InitializeResourceLoadingDevice()
        {
            CreateCommandPool();
            CreateCommandBuffers();
        }

        public RenderPass CreateRenderPass(RenderPassCreateInfo createInfo)
        {
            return LogicalDevice.CreateRenderPass(createInfo);
        }

        public DescriptorPool CreateDescriptorPool(DescriptorPoolCreateInfo info)
        {
            return LogicalDevice.CreateDescriptorPool(info);
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

        public Result AllocateDescriptorSets(in DescriptorSetAllocateInfo pAllocateInfo, AdamantiumVulkan.Core.DescriptorSet[] descriptorSets)
        {
            return LogicalDevice.AllocateDescriptorSets(pAllocateInfo, descriptorSets);
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
            var queueFamilyIndices = MainDevice.PhysicalDevice.FindQueueFamilies(surface);

            var poolInfo = new CommandPoolCreateInfo
            {
                QueueFamilyIndex = queueFamilyIndices.graphicsFamily.Value,
                Flags = (uint) CommandPoolCreateFlagBits.ResetCommandBufferBit
            };
            CommandPool = LogicalDevice.CreateCommandPool(poolInfo);
            
            GraphicsQueue = LogicalDevice.GetDeviceQueue(queueFamilyIndices.graphicsFamily.Value, 0);
            uint queueIndex = (uint) (MainDevice.AvailableQueuesCount > 1 ? 1 : 0);
            resourceQueue = LogicalDevice.GetDeviceQueue(queueFamilyIndices.graphicsFamily.Value, queueIndex);
            //resourceQueue = LogicalDevice.GetDeviceQueue(queueFamilyIndices.graphicsFamily.Value, 0);
        }

        private void CreateGraphicsPresenter(PresentationParameters parameters)
        {
            Presenter = GraphicsPresenter.Create(this, parameters);
            RenderPass = Presenter.RenderPass;
        }

        private void CreateCommandBuffers()
        {
            var buffersCount = MaxFramesInFlight;
            
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

        public bool BeginDraw(float depth = 1.0f, uint stencil = 0)
        {
            CanPresent = false;
            var renderFence = InFlightFences[CurrentFrame];
            var result = LogicalDevice.WaitForFences(1, renderFence, true, ulong.MaxValue);

            if (result != Result.Success)
            {
                return false;
            }
            
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

            //var backbuffer = ((SwapChainGraphicsPresenter)Presenter).GetImage(ImageIndex);
            //var clearColorValue = new ClearColorValue();
            //clearColorValue.Float32 = clearColor.ToFloatArray();

            //commandBuffer.ClearColorImage(backbuffer, ImageLayout.ColorAttachmentOptimal, clearColorValue, 0, null);

            var renderPassInfo = new RenderPassBeginInfo();
            renderPassInfo.RenderPass = renderPass;
            renderPassInfo.Framebuffer = Presenter.GetFramebuffer(ImageIndex);
            renderPassInfo.RenderArea = new Rect2D();
            renderPassInfo.RenderArea.Offset = new Offset2D();
            renderPassInfo.RenderArea.Extent = new Extent2D()
                {Width = Presenter.Width, Height = Presenter.Height};

            ClearValue clearColorValue = new ClearValue();
            clearColorValue.Color = new ClearColorValue();
            clearColorValue.Color.Float32 = ClearColor.ToFloatArray();

            ClearValue clearDepthValue = new ClearValue();
            clearDepthValue.DepthStencil = new ClearDepthStencilValue();
            clearDepthValue.DepthStencil.Depth = depth;
            clearDepthValue.DepthStencil.Stencil = stencil;

            ClearValue clearColorValueResolve = new ClearValue();
            clearColorValueResolve.Color = new ClearColorValue();
            clearColorValueResolve.Color.Float32 = ClearColor.ToFloatArray();

            renderPassInfo.PClearValues = new[] {clearColorValue, clearDepthValue, clearColorValueResolve};
            renderPassInfo.ClearValueCount = (uint) renderPassInfo.PClearValues.Length;

            commandBuffer.BeginRenderPass(renderPassInfo, SubpassContents.Inline);

            ShouldChangeGraphicsPipeline = true;

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

            Semaphore[] waitSemaphores = new[] {ImageAvailableSemaphores[CurrentFrame]};
            uint[] waitStages = new[] {(uint) PipelineStageFlagBits.ColorAttachmentOutputBit};
            submitInfo.WaitSemaphoreCount = 1;
            submitInfo.PWaitSemaphores = waitSemaphores;
            submitInfo.PWaitDstStageMask = waitStages;

            submitInfo.CommandBufferCount = 1;
            submitInfo.PCommandBuffers = new[] {commandBuffer};

            Semaphore[] signalSemaphores = new[] {RenderFinishedSemaphores[CurrentFrame]};

            submitInfo.SignalSemaphoreCount = 1;
            submitInfo.PSignalSemaphores = signalSemaphores;

            submitInfos[0] = submitInfo;

            var renderFence = InFlightFences[CurrentFrame];

            result = LogicalDevice.ResetFences(1, renderFence);

            if (result != Result.Success)
            {
                throw new Exception($"failed to reset fences. Result: {result}");
            }

            if (MainDevice.AvailableQueuesCount == 1)
            {
                mutex.WaitOne();
            }
            
            result = GraphicsQueue.QueueSubmit(1, submitInfos, renderFence);
            GraphicsQueue.QueueWaitIdle();
            
            if (MainDevice.AvailableQueuesCount == 1)
            {
                mutex.ReleaseMutex();
            }

            if (result != Result.Success)
            {
                throw new Exception($"failed to submit draw command buffer! Result was {result}");
            }

            CanPresent = true;
            FrameFinished?.Invoke();
        }

        private void UpdateCurrentFrameNumber()
        {
            CurrentFrame = (CurrentFrame + 1) % MaxFramesInFlight;
        }

        public void SetViewports(params Viewport[] viewports)
        {
            if (viewports == null || viewports.Length == 0) return;

            this.viewports.Clear();
            this.viewports.AddRange(viewports);
            
            CurrentCommandBuffer.SetViewport(0, (uint)viewports.Length, viewports);
        }
        
        public void SetScissors(params Rect2D[] scissors)
        {
            if (scissors == null || scissors.Length == 0) return;
            
            this.scissors.Clear();
            this.scissors.AddRange(scissors);
            
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
                throw new ArgumentNullException("Effect pass should be applied before executing draw");
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
                CurrentEffectPass.CurrentDescriptors[(int) ImageIndex], 
                0, 
                0);

            commandBuffer.Draw(vertexCount, instanceCount, firstVertex, firstInstance);
        }

        public void DrawIndexed(Buffer vertexBuffer, Buffer indexBuffer)
        {
            ulong offset = 0;
            var commandBuffer = commandBuffers[ImageIndex];

            if (ShouldChangeGraphicsPipeline)
            {
                var pipeline = pipelineManager.GetOrCreateGraphicsPipeline(this);

                ShouldChangeGraphicsPipeline = false;

                commandBuffer.BindPipeline(pipeline.BindPoint, pipeline);
            }

            commandBuffer.BindDescriptorSets(
                PipelineBindPoint.Graphics,
                CurrentEffectPass.PipelineLayout,
                0,
                1,
                CurrentEffectPass.CurrentDescriptors[(int) ImageIndex],
                0,
                0);

            commandBuffer.BindVertexBuffers(0, 1, vertexBuffer, ref offset);

            commandBuffer.BindIndexBuffer(indexBuffer, 0, IndexType.Uint32);

            commandBuffer.DrawIndexed(indexBuffer.ElementCount, 1, 0, 0, 0);
        }

        public void UpdateDescriptorSets(params WriteDescriptorSet[] writeDescriptorSets)
        {
            if (writeDescriptorSets == null || writeDescriptorSets.Length == 0) return;
            
            // TODO: decide does wait for fences really need here
            //var renderFence = InFlightFences[CurrentFrame];
            //var result = LogicalDevice.WaitForFences(1, renderFence, true, ulong.MaxValue);
           
            LogicalDevice.UpdateDescriptorSets((uint)writeDescriptorSets.Length, writeDescriptorSets, 0, out var copySets);
        }

        public CommandBuffer BeginSingleTimeCommands()
        {
            mutex.WaitOne();
            return LogicalDevice.BeginSingleTimeCommand(CommandPool);
        }

        public void EndSingleTimeCommands(CommandBuffer commandBuffer)
        {
            LogicalDevice.EndSingleTimeCommands(resourceQueue, CommandPool, commandBuffer);
            mutex.ReleaseMutex();
        }

        public IntPtr MapMemory(DeviceMemory memory, ulong offset, ulong size, uint flags)
        {
            return LogicalDevice.MapMemory(memory, offset, size, flags);
        }

        public void UnmapMemory(DeviceMemory memory)
        {
            LogicalDevice.UnmapMemory(memory);
        }

        public Sampler CreateSampler(SamplerCreateInfo samplerInfo)
        {
            if (LogicalDevice.CreateSampler(samplerInfo, null, out var sampler) != Result.Success)
            {
                throw new Exception("failed to create texture sampler!");
            }

            return sampler;
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
            if (!CanPresent)
            {
                Console.WriteLine("Cannot call Present() because BeginDraw() was not called");
                return;
            }
            
            var presentResult = Presenter.Present();
            if (presentResult == Result.SuboptimalKhr || presentResult == Result.ErrorOutOfDateKhr)
            {
                //ResizePresenter();
            }
            
            UpdateCurrentFrameNumber();
        }

        public void TakeScreenshot(String fileName, ImageFileType fileType)
        {
            Presenter?.TakeScreenshot(fileName, fileType);
        }
        

        internal Semaphore GetImageAvailableSemaphoreForCurrentFrame()
        {
            return ImageAvailableSemaphores[CurrentFrame];
        }

        internal Semaphore GetRenderFinishedSemaphoreForCurrentFrame()
        {
            return RenderFinishedSemaphores[CurrentFrame];
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

        protected override void Dispose(bool disposeManagedResources)
        {
            base.Dispose(disposeManagedResources);
            
            Presenter?.Dispose();
            LogicalDevice.DestroyRenderPass(renderPass);
            BasicEffect?.Dispose();
            DefaultEffectPool?.Dispose();
            
            for (int i = 0; i < commandBuffers.Length; i++)
            {
                LogicalDevice?.DestroySemaphore(RenderFinishedSemaphores[i]);
                LogicalDevice?.DestroySemaphore(ImageAvailableSemaphores[i]);
                LogicalDevice?.DestroyFence(InFlightFences[i]);
            }
            
            LogicalDevice?.DestroyCommandPool(CommandPool);
        }
        
        internal static GraphicsDevice Create(MainGraphicsDevice device)
        {
            return new(device);
        }

        internal static GraphicsDevice Create(MainGraphicsDevice device, PresentationParameters parameters)
        {
            return new(device, parameters);
        }
    }
}
