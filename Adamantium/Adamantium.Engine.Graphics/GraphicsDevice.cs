using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Adamantium.Core;
using Adamantium.Core.Collections;
using Adamantium.Engine.Graphics.Effects;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using AdamantiumVulkan.Core;
using Semaphore = AdamantiumVulkan.Core.Semaphore;
using Adamantium.Engine.Graphics.Effects.Generated;
using AdamantiumVulkan.Core.Interop;
using Serilog;
using Image = AdamantiumVulkan.Core.Image;
using Logger = Serilog.Core.Logger;

namespace Adamantium.Engine.Graphics
{
    public class GraphicsDevice : DisposableObject
    {
        public string DeviceId { get; private set; }

        private SurfaceKHR surface;
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
        
        private readonly PipelineStageFlagBits[] waitStages = {PipelineStageFlagBits.ColorAttachmentOutputBit};
        
        private PipelineManager pipelineManager;
        
        private List<GraphicsDevice> _secondaryDevices;

        internal Device LogicalDevice => MainDevice?.LogicalDevice;

        internal VulkanInstance VulkanInstance => MainDevice?.VulkanInstance;
        
        internal bool ShouldChangeGraphicsPipeline { get; private set; }
        
        private Mutex mutex;
        private static string MutextGuid = Guid.NewGuid().ToString();
        
        private Semaphore[] waitSemaphoresArray = new Semaphore[1];
        private Semaphore[] signalSemaphoresArray = new Semaphore[1];
        private CommandBuffer[] commandBuffersArray = new CommandBuffer[1];

        private List<GraphicsResource> _graphicsResources = new List<GraphicsResource>();  

        static GraphicsDevice()
        {
            BlendStates = new BlendStatesCollection();
            RasterizerStates = new RasterizerStateCollection();
            DepthStencilStates = new DepthStencilStatesCollection();
        }

        private GraphicsDevice(MainGraphicsDevice mainDevice)
        {
            CreateResourceLoadingDevice(mainDevice);
        }

        private GraphicsDevice(MainGraphicsDevice mainDevice, PresentationParameters presentationParameters)
        {
            CreatePrimaryDevice(mainDevice, presentationParameters);
        }
        
        private GraphicsDevice(GraphicsDevice primary, PresentationParameters presentationParameters)
        {
            CreateSecondaryDevice(primary, presentationParameters);
            Log.Logger.Debug($"Render device created. Id: {DeviceId}");
        }

        private void CreateResourceLoadingDevice(MainGraphicsDevice mainDevice)
        {
            DeviceType = GraphicsDeviceType.ResourceLoader;
            InitializeMutex();
            DeviceId = Guid.NewGuid().ToString();
            MainDevice = mainDevice;
            MaxFramesInFlight = 1;
            InitializeResourceLoadingDevice();
            Log.Logger.Debug($"Resource loader device created. Id: {DeviceId}");
        }

        private void CreatePrimaryDevice(MainGraphicsDevice mainDevice, PresentationParameters presentationParameters)
        {
            DeviceType = GraphicsDeviceType.Primary;
            InitializeMutex();
            var timer = Stopwatch.StartNew();
            var timer2 = Stopwatch.StartNew();
            DeviceId = Guid.NewGuid().ToString();
            MainDevice = mainDevice;
            _secondaryDevices = new List<GraphicsDevice>();

            EnableDynamicRendering = mainDevice.EnableDynamicRendering;
            surface = MainDevice.VulkanInstance.GetOrCreateSurface(presentationParameters);

            EffectPools = new List<EffectPool>();
            DefaultEffectPool = EffectPool.New(this);
            
            MaxFramesInFlight = presentationParameters.BuffersCount;
            InitializePrimaryRenderDevice(presentationParameters);
            InitializePipeline();

            timer.Stop();
            BasicEffect = new BasicEffect(this);
            timer2.Stop();
            var diff = timer2.ElapsedMilliseconds - timer.ElapsedMilliseconds;
            Log.Logger.Information($"Effect initialization time: {diff}");
            
            Log.Logger.Debug($"Primary render device created. Id: {DeviceId}");
        }

        private void CreateSecondaryDevice(GraphicsDevice primary, PresentationParameters presentationParameters)
        {
            DeviceType = GraphicsDeviceType.Secondary;
            DeviceId = Guid.NewGuid().ToString();
            MainDevice = primary.MainDevice;
            PrimaryDevice = primary;
            
            EnableDynamicRendering = MainDevice.EnableDynamicRendering;
            
            EffectPools = new List<EffectPool>();
            DefaultEffectPool = EffectPool.New(this);
            
            MaxFramesInFlight = presentationParameters.BuffersCount;
            InitializeSecondaryRenderingDevice(primary, presentationParameters);
            InitializePipeline();
            
            BasicEffect = new BasicEffect(this);
            
            PrimaryDevice.AddSecondaryDevice(this);
        }

        private void AddSecondaryDevice(GraphicsDevice secondary)
        {
            if (!_secondaryDevices.Contains(secondary))
            {
                _secondaryDevices.Add(secondary);
            }
        }

        private void InitializePipeline()
        {
            dynamicStates = new TrackingCollection<DynamicState>();
            viewports = new TrackingCollection<Viewport>();
            scissors = new TrackingCollection<Rect2D>();

            BlendState = BlendStates.Fonts;
            RasterizerState = RasterizerStates.CullNoneClipDisabled;
            DepthStencilState = DepthStencilStates.Default;
            SamplerStates = new SamplerStateCollection(this);
            Sampler = SamplerStates.Default;
            
            ClearColor = Colors.CornflowerBlue;
            
            pipelineManager = new PipelineManager(this);
        }
        
        public GraphicsDeviceType DeviceType { get; private set; }

        public bool IsPrimaryDevice => DeviceType == GraphicsDeviceType.Primary;
        
        public GraphicsDevice PrimaryDevice { get; private set; }

        public IReadOnlyList<GraphicsDevice> SecondaryDevices => _secondaryDevices.AsReadOnly();

        public bool IsResourceLoaderDevice => DeviceType == GraphicsDeviceType.ResourceLoader;
        
        public bool EnableDynamicRendering { get; private set; }
        
        public CommandPool CommandPool { get; private set; }

        internal Semaphore[] ImageAvailableSemaphores { get; private set; }
        internal Semaphore[] RenderFinishedSemaphores { get; private set; }
        internal Fence[] InFlightFences { get; private set; }
        
        internal Fence[] SyncFences { get; private set; }
        
        public PresenterState LastPresenterState { get; private set; }

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
        
        public BasicEffect BasicEffect { get; private set; }
        
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

        private void InitializePrimaryRenderDevice(PresentationParameters presentationParameters)
        {
            CreateCommandPool();
            CreateGraphicsPresenter(presentationParameters);
            CreateCommandBuffers();
            CreateSyncObjects();
            
            var fenceInfo = new FenceCreateInfo();
            fenceInfo.Flags = FenceCreateFlagBits.SignaledBit;

            SyncFences = LogicalDevice.CreateFences(fenceInfo, MaxFramesInFlight);
        }

        private void InitializeSecondaryRenderingDevice(GraphicsDevice primaryDevice, PresentationParameters presentationParameters)
        {
            CommandPool = primaryDevice.CommandPool;
            var queueFamilyIndices = MainDevice.PhysicalDevice.FindQueueFamilies(surface);
            uint queueIndex = (uint) (MainDevice.AvailableQueuesCount > 1 ? 1 : 0);
            resourceQueue = LogicalDevice.GetDeviceQueue(queueFamilyIndices.graphicsFamily.Value, queueIndex);
            
            CreateGraphicsPresenter(presentationParameters);
            CreateCommandBuffers();
            CreateSyncObjects();
        }

        private void InitializeResourceLoadingDevice()
        {
            CreateCommandPool();
            CreateCommandBuffers();
        }

        internal void AddResource(GraphicsResource resource)
        {
            _graphicsResources.Add(resource);
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
                Flags = CommandPoolCreateFlagBits.ResetCommandBufferBit
            };
            CommandPool = LogicalDevice.CreateCommandPool(poolInfo);
            
            GraphicsQueue = LogicalDevice.GetDeviceQueue(queueFamilyIndices.graphicsFamily.Value, 0);
            uint queueIndex = (uint) (MainDevice.AvailableQueuesCount > 1 ? 1 : 0);
            resourceQueue = LogicalDevice.GetDeviceQueue(queueFamilyIndices.graphicsFamily.Value, queueIndex);
        }

        private void CreateGraphicsPresenter(PresentationParameters parameters)
        {
            Presenter = GraphicsPresenter.Create(this, parameters);
            if (!EnableDynamicRendering)
            {
                RenderPass = Presenter.RenderPass;
            }
        }

        private void CreateCommandBuffers()
        {
            var buffersCount = MaxFramesInFlight;
            
            commandBuffers = new CommandBuffer[buffersCount];

            var allocInfo = new CommandBufferAllocateInfo();
            allocInfo.CommandPool = CommandPool;
            allocInfo.Level = IsPrimaryDevice ? CommandBufferLevel.Primary : CommandBufferLevel.Secondary;
            allocInfo.CommandBufferCount = buffersCount;

            commandBuffers = LogicalDevice.AllocateCommandBuffers(allocInfo);
        }

        private void CreateSyncObjects()
        {
            var semaphoreInfo = new SemaphoreCreateInfo();
            
            var fenceInfo = new FenceCreateInfo();
            fenceInfo.Flags = FenceCreateFlagBits.SignaledBit;

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
        
        private void InsertImageMemoryBarrier(CommandBuffer commandBuffer,
            Image image,
            AccessFlagBits sourceAccessMask,
            AccessFlagBits destinationAccessMask,
            ImageLayout oldLayout,
            ImageLayout newLayout,
            PipelineStageFlagBits sourceStageMask,
            PipelineStageFlagBits destinationStageMask,
            ImageSubresourceRange subresourceRange)
        {
            ImageMemoryBarrier barrier = new ImageMemoryBarrier();
            barrier.SrcQueueFamilyIndex = (~0U);
            barrier.DstQueueFamilyIndex = (~0U);
            barrier.SrcAccessMask = sourceAccessMask;
            barrier.DstAccessMask = destinationAccessMask;
            barrier.OldLayout = oldLayout;
            barrier.NewLayout = newLayout;
            barrier.Image = image;
            barrier.SubresourceRange = subresourceRange;

            commandBuffer.PipelineBarrier(
                (uint)sourceStageMask,
                (uint)destinationStageMask,
                0,
                0,
                null,
                0,
                null,
                1,
                barrier);
        }

        public bool BeginDraw(float depth = 1.0f, uint stencil = 0)
        {
            CanPresent = false;
            Result result;
            if (IsPrimaryDevice)
            {
                var renderFence = InFlightFences[CurrentFrame];
                result = LogicalDevice.WaitForFences(1, renderFence, true, ulong.MaxValue);

                if (result != Result.Success)
                {
                    return false;
                }
            }

            if (Presenter is SwapChainGraphicsPresenter swapchain)
            {
                result = LogicalDevice.AcquireNextImageKHR(swapchain, ulong.MaxValue,
                    ImageAvailableSemaphores[CurrentFrame], null, ref imageIndex);

                if (result == Result.ErrorOutOfDateKhr)
                {
                    return false;
                }

                if (result != Result.Success && result != Result.SuboptimalKhr)
                {
                    throw new ArgumentException("Failed to acquire swap chain image!");
                }
            }
            else
            {
                imageIndex = 0;
            }

            var commandBuffer = commandBuffers[ImageIndex];

            var beginInfo = new CommandBufferBeginInfo();
            beginInfo.Flags = CommandBufferUsageFlagBits.SimultaneousUseBit;
            if (DeviceType == GraphicsDeviceType.Secondary)
            {
                beginInfo.PInheritanceInfo = new CommandBufferInheritanceInfo();
                if (!EnableDynamicRendering)
                {
                    beginInfo.PInheritanceInfo.Framebuffer = PrimaryDevice.Presenter.GetFramebuffer(PrimaryDevice.ImageIndex);
                    beginInfo.PInheritanceInfo.RenderPass = PrimaryDevice.RenderPass;
                }
            }

            result = commandBuffer.ResetCommandBuffer(0);

            if (result != Result.Success)
            {
                throw new Exception("failed to begin recording command buffer!");
            }
            
            Log.Logger.Information($"Begin Command buffer on {DeviceType} device");
            result = commandBuffer.BeginCommandBuffer(beginInfo);
            if (result != Result.Success)
            {
                throw new Exception("failed to begin recording command buffer!");
            }

            BeginRendering(commandBuffer, depth, stencil);
            
            return true;
        }

        public void EndDraw()
        {
            var commandBuffer = commandBuffers[ImageIndex];

            if (EnableDynamicRendering)
            {
                commandBuffer.EndRendering();

                ImageSubresourceRange range = new ImageSubresourceRange();
                range.AspectMask = ImageAspectFlagBits.ColorBit;
                range.BaseMipLevel = 0;
                range.LevelCount = (~0U);
                range.BaseArrayLayer = 0;
                range.LayerCount = (~0U);

                InsertImageMemoryBarrier(commandBuffer,
                    Presenter.GetImage(imageIndex),
                    AccessFlagBits.ColorAttachmentWriteBit,
                    0,
                    ImageLayout.ColorAttachmentOptimal,
                    ImageLayout.PresentSrcKhr,
                    PipelineStageFlagBits.ColorAttachmentOutputBit,
                    PipelineStageFlagBits.BottomOfPipeBit,
                    range);
            }
            else
            {
                if (DeviceType == GraphicsDeviceType.Primary)
                {
                    commandBuffer.EndRenderPass();
                }
            }

            if (DeviceType == GraphicsDeviceType.Primary && SecondaryDevices.Count > 0)
            {
                var syncFence = InFlightFences[CurrentFrame];
                var result1 = LogicalDevice.WaitForFences(1, syncFence, true, ulong.MaxValue);
            }

            Log.Logger.Information($"End Command buffer on {DeviceType} device");
            var result = commandBuffer.EndCommandBuffer();
            if (result != Result.Success)
            {
                throw new Exception("failed to record command buffer!");
            }

            if (DeviceType == GraphicsDeviceType.Secondary)
            {
                ExecuteCommands();
                var syncFence = InFlightFences[CurrentFrame];
                var result1 = LogicalDevice.ResetFences(1, syncFence);
                return;
            }

            commandBuffersArray[0] = commandBuffer;

            var submitInfo = new SubmitInfo();

            waitSemaphoresArray[0] = ImageAvailableSemaphores[CurrentFrame];
            
            submitInfo.WaitSemaphoreCount = (uint)waitSemaphoresArray.Length;
            submitInfo.PWaitSemaphores = waitSemaphoresArray;
            submitInfo.PWaitDstStageMask = waitStages;

            submitInfo.CommandBufferCount = (uint)commandBuffersArray.Length;
            submitInfo.PCommandBuffers = commandBuffersArray;

            signalSemaphoresArray[0] = RenderFinishedSemaphores[CurrentFrame];

            submitInfo.SignalSemaphoreCount = (uint)signalSemaphoresArray.Length;
            submitInfo.PSignalSemaphores = signalSemaphoresArray;

            submitInfos[0] = submitInfo;

            var renderFence = InFlightFences[CurrentFrame];

            result = LogicalDevice.ResetFences(1, renderFence);

            if (result != Result.Success)
            {
                throw new Exception($"failed to reset fences. Result: {result}");
            }

            if (MainDevice.AvailableQueuesCount == 1)
            {
                Log.Debug($"Thread id: {Thread.CurrentThread.ManagedThreadId} Wait one");
                //mutex?.WaitOne();
            }

            Log.Debug($"Thread id: {Thread.CurrentThread.ManagedThreadId} Queue submit");
            result = GraphicsQueue.QueueSubmit(1, submitInfos, renderFence);
            Log.Debug($"Thread id: {Thread.CurrentThread.ManagedThreadId} wait for fences");
            LogicalDevice.WaitForFences(1, renderFence, true, ulong.MaxValue);
            
            if (MainDevice.AvailableQueuesCount == 1)
            {
                Log.Debug($"Thread id: {Thread.CurrentThread.ManagedThreadId} release mutex");
                //mutex?.ReleaseMutex();
            }

            if (result != Result.Success)
            {
                throw new Exception($"failed to submit draw command buffer! Result was {result}");
            }

            CanPresent = true;
            FrameFinished?.Invoke();
        }

        private void BeginRendering(CommandBuffer commandBuffer, float depth = 1.0f, uint stencil = 0)
        {
            var clearColorValue = new ClearValue
            {
                Color = new ClearColorValue
                {
                    Float32 = ClearColor.ToFloatArray()
                }
            };

            var clearDepthValue = new ClearValue
            {
                DepthStencil = new ClearDepthStencilValue
                {
                    Depth = depth,
                    Stencil = stencil
                }
            };
            if (EnableDynamicRendering)
            {
                var colorAttachmentInfo = new RenderingAttachmentInfo();
                colorAttachmentInfo.SType = StructureType.RenderingAttachmentInfo;
                colorAttachmentInfo.ImageLayout = ImageLayout.ColorAttachmentOptimal;
                colorAttachmentInfo.ResolveMode = ResolveModeFlagBits.None;
                colorAttachmentInfo.LoadOp = AttachmentLoadOp.Clear;
                colorAttachmentInfo.StoreOp = AttachmentStoreOp.Store;
                colorAttachmentInfo.ClearValue = clearColorValue;
                if (Presenter.Description.MSAALevel != MSAALevel.None)
                {
                    colorAttachmentInfo.ImageView = Presenter.RenderTarget;
                    colorAttachmentInfo.ResolveImageView = Presenter.GetImageView(imageIndex);
                }
                else
                {
                    colorAttachmentInfo.ImageView = Presenter.GetImageView(imageIndex);
                }

                var depthAttachmentInfo = new RenderingAttachmentInfo();
                depthAttachmentInfo.SType = StructureType.RenderingAttachmentInfo;
                depthAttachmentInfo.ImageView = Presenter.DepthBuffer;
                depthAttachmentInfo.ImageLayout = ImageLayout.DepthStencilAttachmentOptimal;
                depthAttachmentInfo.ResolveMode = ResolveModeFlagBits.None;
                depthAttachmentInfo.LoadOp = AttachmentLoadOp.Clear;
                depthAttachmentInfo.StoreOp = AttachmentStoreOp.DontCare;
                depthAttachmentInfo.ClearValue = clearDepthValue;

                var renderingInfo = new RenderingInfo();
                renderingInfo.SType = StructureType.RenderingInfo;
                renderingInfo.RenderArea = new Rect2D();
                renderingInfo.RenderArea.Extent = new Extent2D(){ Width = Presenter.Width, Height = Presenter.Height};
                renderingInfo.RenderArea.Offset = new Offset2D();
                renderingInfo.PColorAttachments = new[] { colorAttachmentInfo };
                renderingInfo.ColorAttachmentCount = 1U;
                renderingInfo.PDepthAttachment = depthAttachmentInfo;
                renderingInfo.PStencilAttachment = depthAttachmentInfo;
                renderingInfo.LayerCount = 1;
                
                ImageSubresourceRange range = new ImageSubresourceRange
                {
                    AspectMask = ImageAspectFlagBits.ColorBit,
                    BaseMipLevel = 0,
                    LevelCount = (~0U),
                    BaseArrayLayer = 0,
                    LayerCount = (~0U)
                };

                ImageSubresourceRange depthRange = new ImageSubresourceRange
                {
                    AspectMask = ImageAspectFlagBits.DepthBit | ImageAspectFlagBits.StencilBit,
                    BaseMipLevel = 0,
                    LevelCount = (~0U),
                    BaseArrayLayer = 0,
                    LayerCount = (~0U)
                };

                InsertImageMemoryBarrier(commandBuffer,
                    Presenter.GetImage(imageIndex),
                    0,
                    AccessFlagBits.ColorAttachmentWriteBit,
                    ImageLayout.Undefined,
                    ImageLayout.ColorAttachmentOptimal,
                    PipelineStageFlagBits.TopOfPipeBit,
                    PipelineStageFlagBits.ColorAttachmentOutputBit,
                    range
                );

                InsertImageMemoryBarrier(commandBuffer,
                    Presenter.DepthBuffer,
                    0,
                    AccessFlagBits.DepthStencilAttachmentWriteBit,
                    ImageLayout.Undefined,
                    ImageLayout.DepthStencilAttachmentOptimal,
                    PipelineStageFlagBits.EarlyFragmentTestsBit | PipelineStageFlagBits.LateFragmentTestsBit,
                    PipelineStageFlagBits.EarlyFragmentTestsBit | PipelineStageFlagBits.LateFragmentTestsBit,
                    depthRange
                );
                
                commandBuffer.BeginRendering(renderingInfo);
            }
            else
            {
                var renderPassInfo = new RenderPassBeginInfo();
                renderPassInfo.RenderPass = renderPass;
                renderPassInfo.Framebuffer = Presenter.GetFramebuffer(ImageIndex);
                renderPassInfo.RenderArea = new Rect2D();
                renderPassInfo.RenderArea.Offset = new Offset2D();
                renderPassInfo.RenderArea.Extent = new Extent2D()
                    {Width = Presenter.Width, Height = Presenter.Height};

                ClearValue clearColorValueResolve = new ClearValue();
                clearColorValueResolve.Color = new ClearColorValue();
                clearColorValueResolve.Color.Float32 = ClearColor.ToFloatArray();

                renderPassInfo.PClearValues = new[] {clearColorValue, clearDepthValue, clearColorValueResolve};
                renderPassInfo.ClearValueCount = (uint) renderPassInfo.PClearValues.Length;

                commandBuffer.BeginRenderPass(renderPassInfo, SubpassContents.Inline);
            }
            
            ShouldChangeGraphicsPipeline = true;
        }

        private void ExecuteCommands()
        {
            if (DeviceType != GraphicsDeviceType.Secondary)
                throw new ArgumentException("Cannot call ExecuteCommands on PrimaryDevice");
            
            PrimaryDevice.CurrentCommandBuffer.ExecuteCommands(1, CurrentCommandBuffer); 
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
            commandBuffer.BindVertexBuffers(0, 1, vertexBuffer, offset);
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

            commandBuffer.BindVertexBuffers(0, 1, vertexBuffer, offset);

            commandBuffer.BindIndexBuffer(indexBuffer, 0, IndexType.Uint32);

            commandBuffer.DrawIndexed(indexBuffer.ElementCount, 1, 0, 0, 0);
        }

        public void UpdateDescriptorSets(params WriteDescriptorSet[] writeDescriptorSets)
        {
            if (writeDescriptorSets == null || writeDescriptorSets.Length == 0) return;
            
            // TODO: decide does wait for fences really need here
            var renderFence = InFlightFences[CurrentFrame];
            var result = LogicalDevice.WaitForFences(1, renderFence, true, ulong.MaxValue);
           
            LogicalDevice.UpdateDescriptorSets((uint)writeDescriptorSets.Length, writeDescriptorSets, 0, out var copySets);
        }

        public CommandBuffer BeginSingleTimeCommands()
        {
            mutex?.WaitOne();
            return LogicalDevice.BeginSingleTimeCommand(CommandPool);
        }

        public void EndSingleTimeCommands(CommandBuffer commandBuffer)
        {
            LogicalDevice.EndSingleTimeCommands(resourceQueue, CommandPool, commandBuffer);
            mutex?.ReleaseMutex();
        }

        public unsafe void* MapMemory(DeviceMemory memory, ulong offset, ulong size, uint flags)
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

        public bool ResizePresenter(uint width = 1, uint height = 1)
        {
            bool ResizeFunc() => Presenter.Resize(width, height);
            return ResizePresenter(ResizeFunc);
        }

        public bool ResizePresenter(PresentationParameters parameters)
        {
            bool ResizeFunc() => Presenter.Resize(parameters);
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

        public void Present(PresentationParameters parameters)
        {
            if (!CanPresent)
            {
                Console.WriteLine("Cannot call Present() because BeginDraw() was not called");
                return;
            }
            
            LastPresenterState = Presenter.Present();
            // if (presentResult is Result.SuboptimalKhr or Result.ErrorOutOfDateKhr)
            // {
            //     ResizePresenter(parameters.Width, parameters.Height, parameters.BuffersCount, parameters.ImageFormat, parameters.DepthFormat);
            // }
            
            UpdateCurrentFrameNumber();
        }
        
        public void Present()
        {
            if (!CanPresent)
            {
                Console.WriteLine("Cannot call Present() because BeginDraw() was not called");
                return;
            }
            
            LastPresenterState = Presenter.Present();
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

            if (IsResourceLoaderDevice)
            {
                Log.Logger.Debug("Disposing Resource loading device");
                LogicalDevice?.FreeCommandBuffers(CommandPool, (uint)commandBuffers.Length, commandBuffers);
                LogicalDevice?.DestroyCommandPool(CommandPool);
            }
            else
            {
                Log.Logger.Debug("Disposing render device");
                Presenter?.Dispose();
                Presenter = null;
                LogicalDevice?.DestroyRenderPass(renderPass);
                BasicEffect?.Dispose();
                DefaultEffectPool?.Dispose();
            
                for (int i = 0; i < commandBuffers.Length; i++)
                {
                    LogicalDevice?.DestroySemaphore(RenderFinishedSemaphores[i]);
                    LogicalDevice?.DestroySemaphore(ImageAvailableSemaphores[i]);
                    LogicalDevice?.DestroyFence(InFlightFences[i]);
                }
            
                LogicalDevice?.FreeCommandBuffers(CommandPool, (uint)commandBuffers.Length, commandBuffers);
                LogicalDevice?.DestroyCommandPool(CommandPool);
            
                pipelineManager?.Dispose();
                SamplerStates?.Dispose();
            }
            
            foreach (var disposableObject in _graphicsResources)
            {
                if (disposableObject.IsDisposed) continue;
                
                disposableObject?.Dispose();
            }
            
            _graphicsResources?.Clear();
        }

        internal static GraphicsDevice Create(MainGraphicsDevice device)
        {
            return new(device);
        }

        internal static GraphicsDevice Create(MainGraphicsDevice device, PresentationParameters parameters)
        {
            return new(device, parameters);
        }
        
        public GraphicsDevice CreateSecondary(PresentationParameters parameters)
        {
            if (!IsPrimaryDevice)
            {
                throw new ArgumentException($"Only primary graphics device could create secondary devices");
            }
            return new(this, parameters);
        }
    }
}
