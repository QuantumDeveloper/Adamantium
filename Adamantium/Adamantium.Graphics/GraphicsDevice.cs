using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Core;
using Adamantium.Core.Collections;
using Adamantium.EffectsCompiler;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Graphics.Core.Extensions;
using Adamantium.Graphics.Core.Presentation;
using Adamantium.Graphics.Effects;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using AdamantiumVulkan.Core;
using AdamantiumVulkan.Core.Interop;
using QuantumBinding.Utils;
using Serilog;
using EffectTechnique = Adamantium.Graphics.Core.EffectsFramework.EffectTechnique;
using Semaphore = AdamantiumVulkan.Core.Semaphore;
using Exception = System.Exception;
using Image = AdamantiumVulkan.Core.Image;

namespace Adamantium.Graphics;

public class GraphicsDevice : DisposableObject, IGraphicsDevice
{
    private object _locker = new object();
    public Guid DeviceId { get; private set; }

    private CommandBuffer[] commandBuffers;
    private Queue resourceQueue;
    private Queue computeQueue;
        
    private readonly SubmitInfo[] submitInfos = new SubmitInfo[1];
    private uint frame;
        
    private Type vertexType;
    private PrimitiveTopology primitiveTopology;
    private IEffectPass currentEffectPass;

    private Queue<IDisposable>[] _deferedDisposeQueue;
        
    // -- Drawing States
        
    private TrackingCollection<Viewport> viewports;
    private TrackingCollection<Rect2D> scissors;
    // Cached snapshots so SetDrawingState doesn't allocate a fresh array on every draw.
    private Viewport[] viewportsArray = Array.Empty<Viewport>();
    private Rect2D[] scissorsArray = Array.Empty<Rect2D>();

    // Dynamic-state cache: last value emitted per state, so a draw only re-emits what changed.
    // Invalidated (_stateInitialized=false) at BeginDraw - a fresh command buffer has undefined dynamic state.
    private bool _stateInitialized;
    private Type _cVertexType;
    private bool _cRasterizerDiscard;
    private PrimitiveTopology _cTopology;
    private bool _cPrimitiveRestart;
    private MSAALevel _cMsaa;
    private bool _cAlphaToCoverage;
    private PolygonMode _cPolygonMode;
    private CullModeFlagBits _cCullMode;
    private FrontFace _cFrontFace;
    private bool _cDepthWrite;
    private bool _cDepthTest;
    private CompareOp _cDepthCompare;
    private bool _cDepthBounds;
    private bool _cDepthBias;
    private bool _cDepthClamp;
    private bool _cStencilTest;
    private bool _cLogicOp;
        
    // --- End of drawing states

    private IRenderTarget[] renderTargets;
    private IDepthStencilBuffer depthBuffer;

    private readonly PipelineStageFlagBits[] waitStages = [PipelineStageFlagBits.ColorAttachmentOutputBit];
        
    public Device LogicalDevice => MainDevice?.LogicalDevice;
    public GraphicsAdapter Adapter => VulkanInstance?.MainGraphicsAdapter;
    public GraphicsPresenter Presenter { get; set; }

    public Queue GraphicsQueue { get; private set; }
    public IRenderTarget CurrentRenderTarget { get; private set; }
    public IDepthStencilBuffer CurrentDepthStencilBuffer { get; private set; }

    internal VulkanInstance VulkanInstance => MainDevice?.VulkanInstance;
        
    private Semaphore[] waitSemaphoresArray = new Semaphore[1];
    private Semaphore[] signalSemaphoresArray = new Semaphore[1];
    private CommandBuffer[] commandBuffersArray = new CommandBuffer[1];

    // Per-frame extra GPU sync registered by shared-surface producers/consumers, merged into the next Submit on
    // top of the swapchain semaphores and cleared afterwards. Timeline values are 0 for binary semaphores.
    private readonly System.Collections.Generic.List<Semaphore> _extraWaitSemaphores = new();
    private readonly System.Collections.Generic.List<PipelineStageFlagBits> _extraWaitStages = new();
    private readonly System.Collections.Generic.List<ulong> _extraWaitValues = new();
    private readonly System.Collections.Generic.List<Semaphore> _extraSignalSemaphores = new();
    private readonly System.Collections.Generic.List<ulong> _extraSignalValues = new();

    /// <summary>Register a semaphore the next <see cref="Submit"/> must wait on (timelineValue=0 for binary).</summary>
    public void AddWaitSemaphore(Semaphore semaphore, PipelineStageFlagBits stage, ulong timelineValue = 0)
    {
        _extraWaitSemaphores.Add(semaphore);
        _extraWaitStages.Add(stage);
        _extraWaitValues.Add(timelineValue);
    }

    /// <summary>Register a semaphore the next <see cref="Submit"/> must signal (timelineValue=0 for binary).</summary>
    public void AddSignalSemaphore(Semaphore semaphore, ulong timelineValue = 0)
    {
        _extraSignalSemaphores.Add(semaphore);
        _extraSignalValues.Add(timelineValue);
    }
        
    private SyncObject _submissionSync;
    private static string SyncGuid = Guid.NewGuid().ToString();

    private readonly HashSet<GraphicsResource> _graphicsResources = new HashSet<GraphicsResource>();

    private GraphicsDevice(MainGraphicsDevice mainDevice, GraphicsDeviceType deviceType)
    {
        if (deviceType == GraphicsDeviceType.ResourceLoader)
        {
            CreateResourceLoadingDevice(mainDevice);
        }
        else
        {
            CreateRenderDevice(mainDevice);
        }
    }

    private void CreateResourceLoadingDevice(MainGraphicsDevice mainDevice)
    {
        MainDevice = mainDevice;
        DeviceType = GraphicsDeviceType.ResourceLoader;
        InitializeSyncObject();
        DeviceId = Guid.NewGuid();
        MaxFramesInFlight = 1;
        InitializeDeferQueue();
        InitializeResourceLoadingDevice();
        Log.Logger.Debug($"Resource loader device created. Id: {DeviceId}");
    }

    private void CreateRenderDevice(MainGraphicsDevice mainDevice)
    {
        MainDevice = mainDevice;
        DeviceType = GraphicsDeviceType.Rendering;
        InitializeSyncObject();
        DeviceId = Guid.NewGuid();

        EnableDynamicRendering = mainDevice.EnableDynamicRendering;

        EffectPools = new List<EffectPool>();
        DefaultEffectPool = EffectPool.New(this);
        MaxFramesInFlight = mainDevice.BuffersCount;
        InitializeDeferQueue();
            
        InitializeRenderDevice();
        InitializePipeline();
        // One global descriptor heap per logical device, owned by the main device and shared by every render-device
        // wrapper (they share one VkDevice). Just reference it — no per-device allocation.
        DescriptorHeapManager = mainDevice.DescriptorHeapManager;

        // The per-frame constant-buffer arena stays per render device: devices render concurrently (parallel games)
        // and cycle frames independently, so this can't be shared.
        _dynamicBufferPools = new EffectPass.DynamicBufferPool[MaxFramesInFlight];
        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            _dynamicBufferPools[i] = new EffectPass.DynamicBufferPool(this);
        }

        Log.Logger.Debug($"Primary render device created. Id: {DeviceId}");

        SampleMask = [0xF];
    }

    private void InitializeDeferQueue()
    {
        _deferedDisposeQueue = new Queue<IDisposable>[MaxFramesInFlight];
        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            _deferedDisposeQueue[i] = new Queue<IDisposable>();
        }
    }

    private void InitializePipeline()
    {
        viewports = new TrackingCollection<Viewport>();
        scissors = new TrackingCollection<Rect2D>();

        SamplerStates = new SamplerStateCollection(this);
        Sampler = SamplerStates.Default;
            
        ClearColor = Colors.CornflowerBlue;
    }

    public GraphicsDeviceType DeviceType { get; private set; }

    public bool IsPrimaryDevice => DeviceType == GraphicsDeviceType.Rendering;
        
    public bool IsResourceLoaderDevice => DeviceType == GraphicsDeviceType.ResourceLoader;
        
    public bool EnableDynamicRendering { get; private set; }

    public CommandPool GraphicsCommandPool { get; private set; }
    public CommandPool TransferCommandPool { get; private set; }
    internal Semaphore[] ImageAvailableSemaphores { get; private set; }
    internal Semaphore[] RenderFinishedSemaphores { get; private set; }
    internal Fence[] InFlightFences { get; private set; }

    public uint CurrentFrame => frame;
    
    public IDescriptorHeapManager DescriptorHeapManager { get; private set; }

    // Per render device: the transient per-frame constant-buffer arena (see CreateRenderDevice for why it's not shared).
    private EffectPass.DynamicBufferPool[] _dynamicBufferPools;
    public EffectPass.DynamicBufferPool CurrentBufferPool => _dynamicBufferPools[CurrentFrame];

    public uint MaxFramesInFlight { get; private set; }

    public List<EffectPool> EffectPools { get; private set; }

    public EffectPool DefaultEffectPool { get; private set; }

    public MainGraphicsDevice MainDevice { get; private set; }
    
    public Fence GetCurrentFence()
    {
        return InFlightFences[CurrentFrame];
    }

    public Semaphore GetRenderFinishedSemaphore()
    {
        return RenderFinishedSemaphores[CurrentFrame];
    }

    public IDepthStencilBuffer CreateDepthBuffer(
        uint width, 
        uint height, 
        DepthFormat format, 
        MSAALevel msaa,
        ImageAspectFlagBits imageAspect = ImageAspectFlagBits.DepthBit,
        string name = "")
    {
        return DepthStencilBuffer.New(this, width, height, format, msaa, imageAspect, name);
    }

    public IRenderTarget CreateRenderTarget(
        uint width, 
        uint height, 
        MSAALevel msaa, SurfaceFormat format,
        ImageUsageFlagBits usage = ImageUsageFlagBits.TransferSrcBit,
        ImageLayout desiredLayout = ImageLayout.ColorAttachmentOptimal,
        string name = "")
    {
        return RenderTarget.New(this, width, height, msaa, format, usage, desiredLayout, name);
    }

    public ITexture CreateTexture(TextureDescription description, byte[] pixelData)
    {
        return Texture.CreateFrom(this, description, pixelData);
    }

    public ITexture ImportSharedSurface(SharedSurfaceDescriptor descriptor)
    {
        return SharedSurface.Import(this, descriptor);
    }

    public ITexture CreateTextureFromImage(Image image, 
        uint width, 
        uint height, 
        MSAALevel msaa, 
        SurfaceFormat format,
        ImageUsageFlagBits usage = ImageUsageFlagBits.TransferSrcBit,
        ImageLayout desiredLayout = ImageLayout.ColorAttachmentOptimal,
        string name = "")
    {
        var description = new TextureDescription
        {
            Width = width,
            Height = height,
            Depth = 1,
            Dimension = TextureDimension.Texture2D,
            ArrayLayers = 1,
            Usage = usage,
            Format = format,
            DesiredImageLayout = desiredLayout,
            ImageTiling = ImageTiling.Optimal,
            ImageType = ImageType._2d,
            MipLevels = 1,
            SharingMode = SharingMode.Exclusive,
            ImageAspect = ImageAspectFlagBits.ColorBit,
            Samples = msaa
        };
        return Texture.CreateFrom(this, image, description, usage, name);
    }

    public SurfaceKHR GetOrCreateSurface(PresentationParameters parameters)
    {
        return VulkanInstance.GetOrCreateSurface(parameters);
    }

    public bool CanPresent { get; private set; }
        
    public SamplerStateCollection SamplerStates { get; internal set; }
        
    public Color ClearColor { get; set; }
        
    public SamplerState Sampler { get; set; }

    public Type VertexType
    {
        get => vertexType;
        set
        {
            if (SetProperty(ref vertexType, value))
            {
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
            }
        }
    }
        
    public bool RasterizerDiscardEnabled { get; set; }

    public ColorBlendEquationEXT ColorBlendEquation { get; set; } = new ColorBlendEquationEXT();
        
    public bool PrimitiveRestartEnable { get; set; }

    public MSAALevel MSAALevel { get; set; } = MSAALevel.None;
        
    public bool AlphaToCoverageEnable { get; set; }
        
    public PolygonMode PolygonMode { get; set; }

    public CullModeFlagBits CullMode { get; set; } = CullModeFlagBits.None;
        
    public bool IsWireFrame { get; set; }
        
    public VkSampleMask[] SampleMask { get; set; }

    public Single LineWidth { get; set; } = 1.0f;

    public FrontFace FrontFace { get; set; } = FrontFace.Clockwise;

    public bool DepthTestEnabled { get; set; } = true;

    public bool DepthWriteEnable { get; set; } = true;

    public CompareOp DepthCompareFunction { get; set; } = CompareOp.LessOrEqual;

    public bool DepthBoundsTestEnabled { get; set; } = false;
        
    public bool DepthBiasEnabled { get; set; } = false;
    
    public bool DepthClampEnable { get; set; } = false;
        
    public bool StencilTestEnabled { get; set; } = false;
        
    public bool LogicOperationsEnabled { get; set; } = false;
        
    public LogicOp LogicOperation { get; set; }
        
    public bool ColorBlendEnabled { get; set; } = true;

    public ColorComponentFlagBits ColorComponentFlags { get; set; } = ColorComponentFlagBits.RBit |
                                                                      ColorComponentFlagBits.GBit |
                                                                      ColorComponentFlagBits.BBit |
                                                                      ColorComponentFlagBits.ABit;

    public IEffectPass CurrentEffectPass
    {
        get => currentEffectPass;
        set
        {
            if (SetProperty(ref currentEffectPass, value))
            {
            }
        }
    }

    public Viewport[] Viewports => viewports.ToArray();

    public Rect2D[] Scissors => scissors.ToArray();

    public bool CommandBufferStarted { get; private set; }

    private void InitializeSyncObject()
    {
        _submissionSync = new SyncObject(SyncGuid, MainDevice.QueueFamilyContainer.IsGraphicsQueueEqualsTransferQueue());
    }

    public CommandBuffer CurrentCommandBuffer => commandBuffers[CurrentFrame]; 

    private void InitializeRenderDevice()
    {
        CreateCommandPool();
        CreateCommandBuffers();
        CreateSyncObjects();
    }

    private void InitializeResourceLoadingDevice()
    {
        CreateCommandPool();
        CreateCommandBuffers();
    }

    public void AddResource(GraphicsResource resource)
    {
        _graphicsResources.Add(resource);
    }

    public void RemoveResource(GraphicsResource resource)
    {
        _graphicsResources.Remove(resource);
    }

    /// <summary>Number of live (registered) graphics resources. Used by leak regression tests.</summary>
    public int RegisteredResourceCount => _graphicsResources.Count;

    public void AddToDeferDisposeQueue(IDisposable obj)
    {
        _deferedDisposeQueue[CurrentFrame].Enqueue(obj);
    }

    public IEffectResourceLinker CreateEffectResourceLinker()
    {
        return new EffectResourceLinker(DescriptorHeapManager);
    }

    public IEffectPass CreateEffectPass(Logger logger, Effect effect, EffectTechnique technique, EffectData.Pass pass, string name)
    {
        return new EffectPass(logger, effect, technique, pass, name);
    }

    public void BindShader(CommandBuffer cmd, ShaderStageFlagBits stage, ShaderEXT shader)
    {
        cmd.BindShadersEXT(1, stage, shader);
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
        var result = LogicalDevice.CreateDescriptorSetLayout(layoutCreateInfo, null, out var descriptorSetLayout);
        ResultHelper.CheckResult(result, nameof(CreateDescriptorSetLayout));
        return descriptorSetLayout;
    }

    public PipelineLayout CreatePipelineLayout(PipelineLayoutCreateInfo createInfo)
    {
        return LogicalDevice.CreatePipelineLayout(createInfo);
    }

    public uint GetDescriptorSetLayoutOffset(DescriptorSetLayout layout, uint bindingSlot)
    {
        return LogicalDevice.GetDescriptorSetLayoutOffset(layout, bindingSlot);
    }

    public ShaderEXT CreateShader(ShaderCreateInfoEXT shaderCreateInfo)
    {
        //return LogicalDevice.CreateShader(shaderCreateInfo);
        LogicalDevice.CreateShadersEXT(1, shaderCreateInfo, null, out var shaderObject);
        return shaderObject[0];
    }

    public void DestroyShader(ShaderEXT shaderObject)
    {
        LogicalDevice.DestroyShaderEXT(shaderObject);
    }

    private void CreateCommandPool()
    {
        var graphicsFamily = MainDevice.QueueFamilyContainer.GetFamilyInfo(QueueFlagBits.GraphicsBit);
        var resourceFamily = MainDevice.QueueFamilyContainer.GetFamilyInfo(QueueFlagBits.TransferBit);

        var poolInfo = new CommandPoolCreateInfo
        {
            QueueFamilyIndex = graphicsFamily.FamilyIndex,
            Flags = CommandPoolCreateFlagBits.ResetCommandBufferBit
        };
        GraphicsCommandPool = LogicalDevice.CreateCommandPool(poolInfo);
        
        poolInfo = new CommandPoolCreateInfo
        {
            QueueFamilyIndex = resourceFamily.FamilyIndex,
            Flags = CommandPoolCreateFlagBits.TransientBit
        };
        
        TransferCommandPool = LogicalDevice.CreateCommandPool(poolInfo);
        GraphicsQueue = MainDevice.GetAvailableGraphicsQueue();
        unsafe
        {
            Log.Logger.Information($"Graphics Queue address of Logical device {new IntPtr(LogicalDevice.NativePointer)}: 0x{new IntPtr(GraphicsQueue.NativePointer).ToString("X2")}");
        }
            
        resourceQueue = MainDevice.GetAvailableTransferQueue();
    }

    private void CreateCommandBuffers()
    {
        var buffersCount = MaxFramesInFlight;
            
        commandBuffers = new CommandBuffer[buffersCount];

        var allocInfo = new CommandBufferAllocateInfo();
        allocInfo.CommandPool = GraphicsCommandPool;
        //allocInfo.Level = IsPrimaryDevice ? CommandBufferLevel.Primary : CommandBufferLevel.Secondary;
        allocInfo.Level = CommandBufferLevel.Primary;
        allocInfo.CommandBufferCount = buffersCount;

        commandBuffers = LogicalDevice.AllocateCommandBuffers(allocInfo);
    }

    private void CreateSyncObjects()
    {
        var semaphoreInfo = new SemaphoreCreateInfo();
            
        var fenceInfo = new FenceCreateInfo();
        fenceInfo.Flags = FenceCreateFlagBits.SignaledBit;

        ImageAvailableSemaphores = LogicalDevice.CreateSemaphores(semaphoreInfo, MaxFramesInFlight);
        //RenderFinishedSemaphores = LogicalDevice.CreateSemaphores(semaphoreInfo, MaxFramesInFlight);
        InFlightFences ??= LogicalDevice.CreateFences(fenceInfo, MaxFramesInFlight);
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
        
    public void InsertImageMemoryBarrier(
        CommandBuffer commandBuffer,
        ITexture texture,
        AccessFlagBits sourceAccessMask,
        AccessFlagBits destinationAccessMask,
        ImageLayout oldLayout,
        ImageLayout newLayout,
        PipelineStageFlagBits sourceStageMask,
        PipelineStageFlagBits destinationStageMask)
    {
        if (texture == null) return;
        
        var range = new ImageSubresourceRange
        {
            AspectMask = texture.ImageAspect,
            BaseMipLevel = 0,
            LevelCount = (~0U),
            BaseArrayLayer = 0,
            LayerCount = (~0U)
        };
        
        var barrier = new ImageMemoryBarrier();
        barrier.SrcQueueFamilyIndex = (~0U);
        barrier.DstQueueFamilyIndex = (~0U);
        barrier.SrcAccessMask = sourceAccessMask;
        barrier.DstAccessMask = destinationAccessMask;
        barrier.OldLayout = oldLayout;
        barrier.NewLayout = newLayout;
        barrier.Image = texture.GetImage();
        barrier.SubresourceRange = range;

        commandBuffer.PipelineBarrier(
            sourceStageMask,
            destinationStageMask,
            0,
            0,
            null,
            0,
            null,
            1,
            barrier);

        texture.ImageLayout = newLayout;
    }

    public void TransitionImagesForRendering(CommandBuffer commandBuffer, params ITexture[] inputTargets)
    {
        var barriers = new List<ImageMemoryBarrier2>();
        foreach (var renderTarget in inputTargets)
        {
            var barrier = new ImageMemoryBarrier2();
            barrier.SrcStageMask = PipelineStageFlagBits2.TopOfPipeBit;
            barrier.SrcAccessMask = AccessFlagBits2.None;
            barrier.DstStageMask = PipelineStageFlagBits2.ColorAttachmentOutputBit;
            barrier.DstAccessMask = AccessFlagBits2.ColorAttachmentWriteBit;
            barrier.OldLayout = renderTarget.ImageLayout;
            barrier.NewLayout = ImageLayout.ColorAttachmentOptimal;
            barrier.SrcQueueFamilyIndex = Constants.VK_QUEUE_FAMILY_IGNORED;
            barrier.DstQueueFamilyIndex = Constants.VK_QUEUE_FAMILY_IGNORED;
            barrier.Image = renderTarget.GetImage();
            barrier.SubresourceRange = new ImageSubresourceRange()
            {
                AspectMask = renderTarget.ImageAspect,
                BaseMipLevel = 0,
                LevelCount = (~0U),
                BaseArrayLayer = 0,
                LayerCount = (~0U)
            };
            barriers.Add(barrier);

            renderTarget.ImageLayout = ImageLayout.ColorAttachmentOptimal;
        }

        var dependencyInfo = new DependencyInfo();
        dependencyInfo.PImageMemoryBarriers = barriers.ToArray();
        dependencyInfo.ImageMemoryBarrierCount = (uint)barriers.Count;
        
        commandBuffer.PipelineBarrier2(dependencyInfo);
    }
    
    public void TransitionDepthBufferForRendering(CommandBuffer commandBuffer, IDepthStencilBuffer depthBuffer)
    {
        if (depthBuffer == null) return;
        
        var barriers = new List<ImageMemoryBarrier2>();

        var barrier = new ImageMemoryBarrier2();
        barrier.SrcStageMask = PipelineStageFlagBits2.TopOfPipeBit;
        barrier.SrcAccessMask = AccessFlagBits2.None;
        barrier.DstStageMask = PipelineStageFlagBits2.EarlyFragmentTestsBit | PipelineStageFlagBits2.LateFragmentTestsBit;
        barrier.DstAccessMask = AccessFlagBits2.DepthStencilAttachmentWriteBit;
        barrier.OldLayout = ImageLayout.Undefined;
        barrier.NewLayout = ImageLayout.DepthStencilAttachmentOptimal;
        barrier.SrcQueueFamilyIndex = Constants.VK_QUEUE_FAMILY_IGNORED;
        barrier.DstQueueFamilyIndex = Constants.VK_QUEUE_FAMILY_IGNORED;
        barrier.Image = depthBuffer.GetImage();
        barrier.SubresourceRange = new ImageSubresourceRange()
        {
            AspectMask =  this.depthBuffer.ImageAspect,
            BaseMipLevel = 0,
            LevelCount = (~0U),
            BaseArrayLayer = 0,
            LayerCount = (~0U)
        };
        barriers.Add(barrier);

        depthBuffer.ImageLayout = ImageLayout.DepthStencilAttachmentOptimal;

        var dependencyInfo = new DependencyInfo();
        dependencyInfo.PImageMemoryBarriers = barriers.ToArray();
        dependencyInfo.ImageMemoryBarrierCount = (uint)barriers.Count;
        
        commandBuffer.PipelineBarrier2(dependencyInfo);
    }

    /// <summary>
    /// Records a ColorAttachmentOptimal -> PresentSrcKhr barrier for the swapchain image into the given (main)
    /// command buffer, so no separate single-time submit is needed at present time.
    /// </summary>
    private void TransitionPresenterImageForPresent(CommandBuffer commandBuffer, ITexture image)
    {
        if (image == null || image.ImageLayout == ImageLayout.PresentSrcKhr) return;

        var barrier = new ImageMemoryBarrier2
        {
            SrcStageMask = PipelineStageFlagBits2.ColorAttachmentOutputBit,
            SrcAccessMask = AccessFlagBits2.ColorAttachmentWriteBit,
            DstStageMask = PipelineStageFlagBits2.BottomOfPipeBit,
            DstAccessMask = AccessFlagBits2.None,
            OldLayout = image.ImageLayout,
            NewLayout = ImageLayout.PresentSrcKhr,
            SrcQueueFamilyIndex = Constants.VK_QUEUE_FAMILY_IGNORED,
            DstQueueFamilyIndex = Constants.VK_QUEUE_FAMILY_IGNORED,
            Image = image.GetImage(),
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = image.ImageAspect,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };

        var dependencyInfo = new DependencyInfo
        {
            PImageMemoryBarriers = new[] { barrier },
            ImageMemoryBarrierCount = 1
        };

        commandBuffer.PipelineBarrier2(dependencyInfo);
        image.ImageLayout = ImageLayout.PresentSrcKhr;
    }

    public void TransitionImagesAfterRendering(CommandBuffer commandBuffer, params ITexture[] inputTargets)
    {
        var barriers = new List<ImageMemoryBarrier2>();
        foreach (var renderTarget in inputTargets)
        {
            var barrier = new ImageMemoryBarrier2();
            barrier.SrcStageMask = PipelineStageFlagBits2.ColorAttachmentOutputBit;
            barrier.SrcAccessMask = AccessFlagBits2.ColorAttachmentWriteBit;
            barrier.DstStageMask = PipelineStageFlagBits2.FragmentShaderBit;
            barrier.DstAccessMask = AccessFlagBits2.ShaderReadBit;
            barrier.OldLayout = ImageLayout.ColorAttachmentOptimal;
            barrier.NewLayout = ImageLayout.ShaderReadOnlyOptimal;
            barrier.SrcQueueFamilyIndex = Constants.VK_QUEUE_FAMILY_IGNORED;
            barrier.DstQueueFamilyIndex = Constants.VK_QUEUE_FAMILY_IGNORED;
            barrier.Image = renderTarget.GetImage();
            barrier.SubresourceRange = new ImageSubresourceRange()
            {
                AspectMask = renderTarget.ImageAspect,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1
            };
            barriers.Add(barrier);

            renderTarget.ImageLayout = ImageLayout.ShaderReadOnlyOptimal;
        }
        
        var dependencyInfo = new DependencyInfo();
        dependencyInfo.PImageMemoryBarriers = barriers.ToArray();
        dependencyInfo.ImageMemoryBarrierCount = (uint)barriers.Count;
        
        commandBuffer.PipelineBarrier2(dependencyInfo);
    }
    
    public void TransitionDepthBufferAfterRendering(CommandBuffer commandBuffer, IDepthStencilBuffer depthBuffer)
    {
        if (depthBuffer == null) return;
        
        var barriers = new List<ImageMemoryBarrier2>();

        var barrier = new ImageMemoryBarrier2();
        barrier.SrcStageMask = PipelineStageFlagBits2.EarlyFragmentTestsBit | PipelineStageFlagBits2.LateFragmentTestsBit;
        barrier.SrcAccessMask = AccessFlagBits2.DepthStencilAttachmentWriteBit;
        barrier.DstStageMask = PipelineStageFlagBits2.FragmentShaderBit | PipelineStageFlagBits2.ComputeShaderBit;
        barrier.DstAccessMask = AccessFlagBits2.None;
        barrier.OldLayout = ImageLayout.Undefined;
        barrier.NewLayout = ImageLayout.DepthStencilReadOnlyOptimal;
        barrier.SrcQueueFamilyIndex = Constants.VK_QUEUE_FAMILY_IGNORED;
        barrier.DstQueueFamilyIndex = Constants.VK_QUEUE_FAMILY_IGNORED;
        barrier.Image = depthBuffer.GetImage();
        barrier.SubresourceRange = new ImageSubresourceRange()
        {
            AspectMask = this.depthBuffer.ImageAspect,
            BaseMipLevel = 0,
            LevelCount = (~0U),
            BaseArrayLayer = 0,
            LayerCount = (~0U)
        };
        barriers.Add(barrier);

        var dependencyInfo = new DependencyInfo();
        dependencyInfo.PImageMemoryBarriers = barriers.ToArray();
        dependencyInfo.ImageMemoryBarrierCount = (uint)barriers.Count;
        
        commandBuffer.PipelineBarrier2(dependencyInfo);
    }
    
    private void DisposeDeferredObjects(uint currentFrame)
    {
        var queue = _deferedDisposeQueue[currentFrame];
        foreach (var obj in queue)
        {
            obj?.Dispose();
        }
        queue.Clear();
    }

    public bool BeginDraw(float depth = 1.0f, uint stencil = 0, Action<CommandBuffer> beforeRenderPass = null)
    {
        CanPresent = false;
        var renderFence = InFlightFences[CurrentFrame];
        var result = LogicalDevice.WaitForFences(1, renderFence, true, ulong.MaxValue);
        
        DisposeDeferredObjects(CurrentFrame);

        if (result != Result.Success && result != Result.Timeout)
        {
            Log.Logger.Information($"Wait for fences result: {result}");
            return false;
        }

        if (Presenter is SwapChainGraphicsPresenter swapchain)
        {
            if (!Presenter.AcquireNextImage(null, ImageAvailableSemaphores[CurrentFrame]))
            {
                return false;
            }
        }

        // if (Presenter is SwapChainGraphicsPresenter swapchain)
        // {
        // result = LogicalDevice.AcquireNextImageKHR(swapchain, ulong.MaxValue,
        //     ImageAvailableSemaphores[CurrentFrame], null, ref imageIndex);
        //
        //     if (result == Result.ErrorOutOfDateKhr)
        //     {
        //         return false;
        //     }
        //
        //     if (result != Result.Success && result != Result.SuboptimalKhr)
        //     {
        //         throw new ArgumentException("Failed to acquire swap chain image!");
        //     }
        // }

        var commandBuffer = commandBuffers[CurrentFrame];

        var beginInfo = new CommandBufferBeginInfo();
        beginInfo.Flags = CommandBufferUsageFlagBits.SimultaneousUseBit;
            
        result = commandBuffer.ResetCommandBuffer(0);
            
        if (result != Result.Success)
        {
            throw new Exception("failed to begin recording command buffer!");
        }

        CommandBufferStarted = true;

        // Fresh command buffer => dynamic state is undefined, so the state cache must re-emit everything next draw.
        _stateInitialized = false;

        //Log.Logger.Information($"Begin Command buffer on {DeviceType} device {DeviceId}");
        result = commandBuffer.BeginCommandBuffer(beginInfo);
        // unsafe
        // {
        //     Log.Logger.Debug($"BeginCommandBuffer was called for {new IntPtr(commandBuffer.NativePointer)}");
        // }
            
        if (result != Result.Success)
        {
            throw new Exception("failed to begin recording command buffer!");
        }
        
        TransitionImagesForRendering(commandBuffer, renderTargets);
        TransitionDepthBufferForRendering(commandBuffer, depthBuffer);

        // Out-of-render-pass work (e.g. shared-surface latch copies) must be recorded here, before BeginRendering,
        // so a later in-pass draw can sample the result the SAME frame (no latency).
        beforeRenderPass?.Invoke(commandBuffer);

        BeginRendering(commandBuffer, false, depth, stencil);

        return true;
    }
        
    public void BeginRendering(CommandBuffer commandBuffer, bool continueRendering = false, float depth = 1.0f, uint stencil = 0)
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
        
        var loadOperation = continueRendering ? AttachmentLoadOp.Load : AttachmentLoadOp.Clear;
        
        if (EnableDynamicRendering)
        {
            var colorAttachments = new RenderingAttachmentInfo[renderTargets.Length];
            for (var i = 0; i < renderTargets.Length; i++)
            {
                var renderTarget = renderTargets[i];
                var colorAttachmentInfo = new RenderingAttachmentInfo();
                colorAttachmentInfo.ImageLayout = ImageLayout.ColorAttachmentOptimal;
                colorAttachmentInfo.LoadOp = loadOperation;
                colorAttachmentInfo.StoreOp = AttachmentStoreOp.Store;
                colorAttachmentInfo.ClearValue = clearColorValue;
                if (renderTarget.MSAALevel != MSAALevel.None)
                {
                    colorAttachmentInfo.ImageView = renderTarget.GetImageView();
                    colorAttachmentInfo.ResolveImageView = renderTarget.ResolveTexture.GetImageView();
                    colorAttachmentInfo.ResolveMode = ResolveModeFlagBits.AverageBit;
                    colorAttachmentInfo.ResolveImageLayout = ImageLayout.ColorAttachmentOptimal;
                }
                else
                {
                    colorAttachmentInfo.ImageView = renderTarget.GetImageView();
                }
                colorAttachments[i] = colorAttachmentInfo;
            }

            var width = renderTargets[0].Width;
            var height = renderTargets[0].Height;

            var depthAttachmentInfo = new RenderingAttachmentInfo();
            depthAttachmentInfo.ImageView = depthBuffer?.GetImageView();
            depthAttachmentInfo.ImageLayout = ImageLayout.DepthStencilAttachmentOptimal;
            depthAttachmentInfo.ResolveMode = ResolveModeFlagBits.None;
            depthAttachmentInfo.LoadOp = loadOperation;
            depthAttachmentInfo.StoreOp = AttachmentStoreOp.Store;
            depthAttachmentInfo.ClearValue = clearDepthValue;

            var renderingInfo = new RenderingInfo();
            renderingInfo.RenderArea = new Rect2D();
            renderingInfo.RenderArea.Extent = new Extent2D(){ Width = width, Height = height};
            renderingInfo.RenderArea.Offset = new Offset2D();
            renderingInfo.PColorAttachments = colorAttachments;
            renderingInfo.ColorAttachmentCount = (uint)colorAttachments.Length;
            if (depthBuffer != null)
            {
                renderingInfo.PDepthAttachment = depthAttachmentInfo;
                renderingInfo.PStencilAttachment = depthAttachmentInfo;
            }

            renderingInfo.LayerCount = 1;
            
            // InsertImageMemoryBarrier(commandBuffer,
            //     renderTargets[0],
            //     0,
            //     AccessFlagBits.ColorAttachmentWriteBit,
            //     ImageLayout.Undefined,
            //     ImageLayout.ColorAttachmentOptimal,
            //     PipelineStageFlagBits.TopOfPipeBit,
            //     PipelineStageFlagBits.ColorAttachmentOutputBit
            // );
            //
            // InsertImageMemoryBarrier(commandBuffer,
            //     depthBuffer,
            //     0,
            //     AccessFlagBits.DepthStencilAttachmentWriteBit,
            //     ImageLayout.Undefined,
            //     ImageLayout.DepthStencilAttachmentOptimal,
            //     PipelineStageFlagBits.EarlyFragmentTestsBit | PipelineStageFlagBits.LateFragmentTestsBit,
            //     PipelineStageFlagBits.EarlyFragmentTestsBit | PipelineStageFlagBits.LateFragmentTestsBit
            // );
            
            commandBuffer.BeginRendering(renderingInfo);
            if (EffectPass.UseDescriptorHeap)
                DescriptorHeapManager.BindDescriptorHeaps(this);
        }
    }

    public void EndDraw()
    {
        var commandBuffer = commandBuffers[CurrentFrame];
            
        if (EnableDynamicRendering)
        {
            commandBuffer.EndRendering();

            //InsertMemoryBarrier();
            // TransitionImagesAfterRendering(commandBuffer, renderTargets);
            // TransitionDepthBufferAfterRendering(commandBuffer, depthBuffer);

            // InsertImageMemoryBarrier(commandBuffer,
            //     renderTargets[0],
            //     AccessFlagBits.ColorAttachmentWriteBit,
            //     0,
            //     ImageLayout.ColorAttachmentOptimal,
            //     ImageLayout.PresentSrcKhr,
            //     PipelineStageFlagBits.ColorAttachmentOutputBit,
            //     PipelineStageFlagBits.BottomOfPipeBit);
        }
        else
        {
            // if (DeviceType == GraphicsDeviceType.Primary)
            // {
            //     commandBuffer.EndRenderPass();
            // }
        }
    }

    public void InsertMemoryBarrier()
    {
        var memoryBarrier = new MemoryBarrier2
        {
            SrcStageMask = PipelineStageFlagBits2.ColorAttachmentOutputBit,
            SrcAccessMask = AccessFlagBits2.ColorAttachmentWriteBit,
            DstStageMask = PipelineStageFlagBits2.ColorAttachmentOutputBit,
            DstAccessMask = AccessFlagBits2.ColorAttachmentReadBit
        };

        var dependencyInfo = new DependencyInfo
        {
            MemoryBarrierCount = 1,
            PMemoryBarriers = new[] {memoryBarrier}
        };

        CurrentCommandBuffer.PipelineBarrier2(dependencyInfo);
    }

    public void Submit()
    {
        if (!CommandBufferStarted) return;

        _submissionSync?.Wait();
            
        //Log.Logger.Debug($"Enter Submit for device {DeviceId}");

        var commandBuffer = CurrentCommandBuffer;

        // Transition the swapchain image to PresentSrc inside the MAIN command buffer instead of a separate
        // per-frame single-time submit in Present() (which Present()'s guard then skips).
        if (Presenter is SwapChainGraphicsPresenter presenterForLayout)
        {
            TransitionPresenterImageForPresent(commandBuffer, presenterForLayout.GetCurrentImage());
        }

        var result = commandBuffer.EndCommandBuffer();
        // unsafe
        // {
        //     Log.Logger.Debug($"EndCommandBuffer was called for {new IntPtr(commandBuffer.NativePointer)}");
        // }

        if (result != Result.Success)
        {
            throw new Exception("failed to record command buffer!");
        }

        CommandBufferStarted = false;
            
        commandBuffersArray[0] = CurrentCommandBuffer;
        var submitInfo = new SubmitInfo();

        // Swapchain present-sync (binary) + per-frame extras from shared-surface compositing (timeline). The
        // timeline value array must align 1:1 with the semaphore arrays (binary entries carry value 0, ignored).
        var waitSems = new System.Collections.Generic.List<Semaphore>();
        var waitStageList = new System.Collections.Generic.List<PipelineStageFlagBits>();
        var waitValues = new System.Collections.Generic.List<ulong>();
        var signalSems = new System.Collections.Generic.List<Semaphore>();
        var signalValues = new System.Collections.Generic.List<ulong>();

        if (Presenter is SwapChainGraphicsPresenter swapChainGraphicsPresenter)
        {
            waitSems.Add(ImageAvailableSemaphores[CurrentFrame]);
            waitStageList.Add(PipelineStageFlagBits.ColorAttachmentOutputBit);
            waitValues.Add(0);
            signalSems.Add(swapChainGraphicsPresenter.CurrentRenderFinishedSemaphore);
            signalValues.Add(0);
        }

        var hasTimeline = false;
        for (int i = 0; i < _extraWaitSemaphores.Count; i++)
        {
            waitSems.Add(_extraWaitSemaphores[i]);
            waitStageList.Add(_extraWaitStages[i]);
            waitValues.Add(_extraWaitValues[i]);
            if (_extraWaitValues[i] != 0) hasTimeline = true;
        }
        for (int i = 0; i < _extraSignalSemaphores.Count; i++)
        {
            signalSems.Add(_extraSignalSemaphores[i]);
            signalValues.Add(_extraSignalValues[i]);
            if (_extraSignalValues[i] != 0) hasTimeline = true;
        }
        _extraWaitSemaphores.Clear(); _extraWaitStages.Clear(); _extraWaitValues.Clear();
        _extraSignalSemaphores.Clear(); _extraSignalValues.Clear();

        if (waitSems.Count > 0)
        {
            submitInfo.WaitSemaphoreCount = (uint)waitSems.Count;
            submitInfo.PWaitSemaphores = waitSems.ToArray();
            submitInfo.PWaitDstStageMask = waitStageList.ToArray();
        }
        if (signalSems.Count > 0)
        {
            submitInfo.SignalSemaphoreCount = (uint)signalSems.Count;
            submitInfo.PSignalSemaphores = signalSems.ToArray();
        }
        if (hasTimeline)
        {
            submitInfo.PNext = new TimelineSemaphoreSubmitInfo
            {
                WaitSemaphoreValueCount = (uint)waitValues.Count,
                PWaitSemaphoreValues = waitValues.ToArray(),
                SignalSemaphoreValueCount = (uint)signalValues.Count,
                PSignalSemaphoreValues = signalValues.ToArray()
            };
        }

        submitInfo.CommandBufferCount = (uint)commandBuffersArray.Length;
        submitInfo.PCommandBuffers = commandBuffersArray;

        submitInfos[0] = submitInfo;

        var renderFence = InFlightFences[CurrentFrame];
            
        result = LogicalDevice.ResetFences(1, renderFence);

        if (result != Result.Success)
        {
            Log.Logger.Error($"failed to reset fences. Result: {result}");
            //throw new Exception($"failed to reset fences. Result: {result}");
        }

        result = GraphicsQueue.QueueSubmit(1, submitInfos, renderFence);

        if (result != Result.Success)
        {
            Log.Logger.Error($"failed to submit draw command buffer! Result was {result}");
        }

        CanPresent = true;

        _submissionSync?.Release();
    }

    public void FrameEnded()
    {
        UpdateCurrentFrameNumber();
    }

    private void UpdateCurrentFrameNumber()
    {
        frame = (CurrentFrame + 1) % MaxFramesInFlight;
    }

    public void SetObjectDebugName(ulong objectHandle, ObjectType objectType, string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }
            
        var nameInfo = new DebugUtilsObjectNameInfoEXT
        {
            ObjectType = objectType,
            ObjectHandle = objectHandle,
            PObjectName = name
        };
        using var ctx = new NativeContext(nameInfo.GetSize(), stackalloc byte[(int)MarshalingUtils.StackAllocThreshold]);
        var infoPtr = nameInfo.MarshalToNative(ctx);
        var result = LogicalDevice.SetDebugUtilsObjectNameEXT(infoPtr);
        ResultHelper.CheckResult(result, nameof(SetObjectDebugName));
    }

    public void SetViewports(params Viewport[] viewports)
    {
        if (viewports == null || viewports.Length == 0) return;

        this.viewports.Clear();
        this.viewports.AddRange(viewports);
        viewportsArray = viewports;

        CurrentCommandBuffer.SetViewport(0, (uint)viewports.Length, viewports);
    }
        
    public void SetScissors(params Rect2D[] scissors)
    {
        if (scissors == null || scissors.Length == 0) return;
            
        this.scissors.Clear();
        this.scissors.AddRange(scissors);
        scissorsArray = scissors;

        CurrentCommandBuffer.SetScissor(0, (uint)scissors.Length, scissors);
    }

    public void SetRenderTargets(params IRenderTarget[] renderTargets)
    {
        this.renderTargets = renderTargets;
        if (renderTargets.Length > 0)
        {
            CurrentRenderTarget = renderTargets[0];
        }
        else
        {
            CurrentRenderTarget = null;
        }
    }

    public void SetDepthBuffer(IDepthStencilBuffer depthBuffer)
    {
        this.depthBuffer = depthBuffer;
        if (depthBuffer != null)
        {
            CurrentDepthStencilBuffer = depthBuffer;
        }
    }

    public void SetVertexBuffer(IBuffer vertexBuffer)
    {
        ulong offset = 0;
        var commandBuffer = commandBuffers[CurrentFrame];
        commandBuffer.BindVertexBuffers(0U, 1U, vertexBuffer.GetBuffer(), offset);
    }

    public void SetVertexBuffers(params IBuffer[] vertexBuffers)
    {
        if (vertexBuffers == null || vertexBuffers.Length == 0) return;

        var offsets = new VkDeviceSize[vertexBuffers.Length];
        var commandBuffer = commandBuffers[CurrentFrame];
        var buffers = vertexBuffers.Select(x=>x.GetBuffer()).ToArray();
        commandBuffer.BindVertexBuffers(0, (uint)buffers.Length, buffers, offsets);
    }

    public void SetIndexBuffer(IBuffer indexBuffer)
    {
        var commandBuffer = commandBuffers[CurrentFrame];
        commandBuffer.BindIndexBuffer(indexBuffer.GetBuffer(), 0, IndexType.Uint32);
    }

    private void SetDrawingState(CommandBuffer commandBuffer)
    {
        // Viewport/scissor are re-emitted every draw (cheap; scissor will also vary per clip region later).
        commandBuffer.SetViewportWithCount((uint)viewportsArray.Length, viewportsArray);
        commandBuffer.SetScissorWithCount((uint)scissorsArray.Length, scissorsArray);

        // Emit each remaining dynamic state only when it changed since the previous draw. The cache is
        // invalidated at BeginDraw, so the first draw of a command buffer still emits everything.
        if (!_stateInitialized || _cVertexType != VertexType)
        {
            var bindingDescription = VertexType.GetBindingDescription2();
            var attributes = VertexType.GetVertexAttributeDescription2();
            commandBuffer.SetVertexInputEXT(1, bindingDescription, (uint)attributes.Length, attributes);
            _cVertexType = VertexType;
        }
        if (!_stateInitialized || _cRasterizerDiscard != RasterizerDiscardEnabled)
        { commandBuffer.SetRasterizerDiscardEnable(RasterizerDiscardEnabled); _cRasterizerDiscard = RasterizerDiscardEnabled; }
        if (!_stateInitialized || _cTopology != PrimitiveTopology)
        { commandBuffer.SetPrimitiveTopology(PrimitiveTopology); _cTopology = PrimitiveTopology; }
        if (!_stateInitialized || _cPrimitiveRestart != PrimitiveRestartEnable)
        { commandBuffer.SetPrimitiveRestartEnable(PrimitiveRestartEnable); _cPrimitiveRestart = PrimitiveRestartEnable; }
        if (!_stateInitialized || _cMsaa != MSAALevel)
        {
            commandBuffer.SetRasterizationSamplesEXT((SampleCountFlagBits)MSAALevel);
            commandBuffer.SetSampleMaskEXT((SampleCountFlagBits)MSAALevel, SampleMask);
            _cMsaa = MSAALevel;
        }
        if (!_stateInitialized || _cAlphaToCoverage != AlphaToCoverageEnable)
        { commandBuffer.SetAlphaToCoverageEnableEXT(AlphaToCoverageEnable); _cAlphaToCoverage = AlphaToCoverageEnable; }
        if (!_stateInitialized || _cPolygonMode != PolygonMode)
        { commandBuffer.SetPolygonModeEXT(PolygonMode); _cPolygonMode = PolygonMode; }

        if (PolygonMode == PolygonMode.Line)
        {
            commandBuffer.SetLineWidth(LineWidth);
        }

        if (!_stateInitialized || _cCullMode != CullMode)
        { commandBuffer.SetCullMode(CullMode); _cCullMode = CullMode; }
        if (!_stateInitialized || _cFrontFace != FrontFace)
        { commandBuffer.SetFrontFace(FrontFace); _cFrontFace = FrontFace; }
        if (!_stateInitialized || _cDepthWrite != DepthWriteEnable)
        { commandBuffer.SetDepthWriteEnable(DepthWriteEnable); _cDepthWrite = DepthWriteEnable; }
        if (!_stateInitialized || _cDepthTest != DepthTestEnabled)
        { commandBuffer.SetDepthTestEnable(DepthTestEnabled); _cDepthTest = DepthTestEnabled; }
        if (!_stateInitialized || _cDepthCompare != DepthCompareFunction)
        { commandBuffer.SetDepthCompareOp(DepthCompareFunction); _cDepthCompare = DepthCompareFunction; }
        if (!_stateInitialized || _cDepthBounds != DepthBoundsTestEnabled)
        { commandBuffer.SetDepthBoundsTestEnable(DepthBoundsTestEnabled); _cDepthBounds = DepthBoundsTestEnabled; }
        if (!_stateInitialized || _cDepthBias != DepthBiasEnabled)
        { commandBuffer.SetDepthBiasEnable(DepthBiasEnabled); _cDepthBias = DepthBiasEnabled; }
        if (!_stateInitialized || _cDepthClamp != DepthClampEnable)
        { commandBuffer.SetDepthClampEnableEXT(DepthClampEnable); _cDepthClamp = DepthClampEnable; }
        if (!_stateInitialized || _cStencilTest != StencilTestEnabled)
        { commandBuffer.SetStencilTestEnable(StencilTestEnabled); _cStencilTest = StencilTestEnabled; }
        if (!_stateInitialized || _cLogicOp != LogicOperationsEnabled)
        { commandBuffer.SetLogicOpEnableEXT(LogicOperationsEnabled); _cLogicOp = LogicOperationsEnabled; }

        // Colour-blend state stays always-emitted: it's the one that actually varies between UI draws (e.g.
        // text uses premultiplied) and these structs aren't cheaply comparable.
        commandBuffer.SetColorBlendEquationEXT(0, 1, ColorBlendEquation);
        commandBuffer.SetColorBlendEnableEXT(0, 1, ColorBlendEnabled);
        commandBuffer.SetColorWriteMaskEXT(0, 1, ColorComponentFlags);

        _stateInitialized = true;
    }

    public void Draw(ulong vertexCount, uint instanceCount, uint firstVertex = 0, uint firstInstance = 0)
    {
        if (CurrentEffectPass == null)
        {
            throw new ArgumentNullException("Effect pass should be applied before executing draw");
        }
            
        var commandBuffer = commandBuffers[CurrentFrame];
        SetDrawingState(commandBuffer);

        commandBuffer.Draw((uint)vertexCount, instanceCount, firstVertex, firstInstance);
    }

    public void DrawIndexed(IBuffer vertexBuffer, IBuffer indexBuffer, uint instanceCount = 1)
    {
        ulong offset = 0;
        var commandBuffer = commandBuffers[CurrentFrame];
            
        SetDrawingState(commandBuffer);

        commandBuffer.BindVertexBuffers(0, 1, vertexBuffer.GetBuffer(), offset);

        commandBuffer.BindIndexBuffer(indexBuffer.GetBuffer(), 0, IndexType.Uint32);

        commandBuffer.DrawIndexed((uint)indexBuffer.ElementCount, instanceCount, 0, 0, 0);
    }

    public CommandBuffer BeginSingleTimeCommand()
    {
        _submissionSync?.Wait();
        return LogicalDevice.BeginSingleTimeCommand(TransferCommandPool);
    }

    public void EndSingleTimeCommand(CommandBuffer commandBuffer)
    {
        LogicalDevice.EndSingleTimeCommands(resourceQueue, TransferCommandPool, commandBuffer);
        _submissionSync?.Release();
    }

    public void AddEffectPool(EffectPool pool)
    {
        EffectPools.Add(pool);
    }

    public void RemoveEffectPool(EffectPool pool)
    {
        EffectPools.Remove(pool);
    }

    public void BindDescriptorBuffers(CommandBuffer commandBuffer, params DescriptorBufferBindingInfoEXT[] bindings)
    {
        commandBuffer.BindDescriptorBuffersEXT((uint)bindings.Length, bindings);
    }

    // public void SetDescriptorBufferOffsets(CommandBuffer commandBuffer, PipelineBindPoint pipelineBindPoint, PipelineLayout layout,
    //     uint dataSet, uint setCount, uint[] bufferIndices, ulong[] offsets)
    // {
    //     LogicalDevice.SetDescriptorBufferOffsets(commandBuffer, pipelineBindPoint, layout, dataSet, setCount,
    //         bufferIndices, offsets);
    // }

    public uint GetDescriptorSetLayoutSize(DescriptorSetLayout layout)
    {
        LogicalDevice.GetDescriptorSetLayoutSizeEXT(layout, out var size);
        return (uint)size;
    }

    public ulong UniformBufferDescriptorSize => MainDevice.GraphicsAdapter
        .DeviceBufferProperties.UniformBufferDescriptorSize;
    public ulong SamplerDescriptorSize => MainDevice.GraphicsAdapter.DeviceBufferProperties.SamplerDescriptorSize;
    public ulong SampledImageDescriptorSize => MainDevice.GraphicsAdapter.DeviceBufferProperties
        .SampledImageDescriptorSize;

    public uint DescriptorBufferOffsetAlignment => (uint)MainDevice.GraphicsAdapter
        .DeviceBufferProperties.DescriptorBufferOffsetAlignment;
    public void GetDescriptor(DescriptorGetInfoEXT descriptorGetInfoExt, uint descriptorSize, nuint descriptorPtr)
    {
        LogicalDevice.GetDescriptorEXT(descriptorGetInfoExt, descriptorSize, descriptorPtr);
    }

    public void Destroy(DescriptorSetLayout layout)
    {
        layout?.Destroy(LogicalDevice);
    }

    public void Destroy(PipelineLayout layout)
    {
        layout?.Destroy(LogicalDevice);
    }

    public void Destroy(Sampler sampler)
    {
        LogicalDevice.DestroySampler(sampler);
    }

    public void Destroy(AdamantiumVulkan.Core.Buffer buffer)
    {
        buffer?.Destroy(LogicalDevice);
    }

    public void Destroy(DeviceMemory deviceMemory)
    {
        deviceMemory?.FreeMemory(LogicalDevice);
    }

    public void Destroy(Image image)
    {
        image?.Destroy(LogicalDevice);
    }

    public void Destroy(ImageView imageView)
    {
        imageView?.Destroy(LogicalDevice);
    }

    public void Destroy(SwapchainKHR swapchain)
    {
        swapchain.Destroy(LogicalDevice);
    }
    
    public void Destroy(Semaphore semaphore)
    {
        LogicalDevice?.DestroySemaphore(semaphore);
    }

    public nuint MapMemory(DeviceMemory memory, ulong offset, ulong size, MemoryMapFlagBits flags)
    {
        return LogicalDevice.MapMemory(memory, offset, size, flags);
    }

    public void UnmapMemory(DeviceMemory memory)
    {
        LogicalDevice.UnmapMemory(memory);
    }

    public SamplerState CreateSampler(SamplerCreateInfo samplerInfo, string name)
    {
        return SamplerState.New(this, name, samplerInfo);
    }

    internal Semaphore GetImageAvailableSemaphoreForCurrentFrame()
    {
        return ImageAvailableSemaphores[CurrentFrame];
    }

    internal Semaphore GetRenderFinishedSemaphoreForCurrentFrame()
    {
        //Log.Logger.Debug($"Current frame index in GetRenderFinishedSemaphoreForCurrentFrame: {CurrentFrame}");
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
            LogicalDevice?.FreeCommandBuffers(GraphicsCommandPool, (uint)commandBuffers.Length, commandBuffers);
            LogicalDevice?.DestroyCommandPool(GraphicsCommandPool);
            LogicalDevice?.DestroyCommandPool(TransferCommandPool);
        }
        else
        {
            Log.Logger.Debug("Disposing render device");
            DefaultEffectPool?.Dispose();
            
            for (int i = 0; i < commandBuffers.Length; i++)
            {
                //LogicalDevice?.DestroySemaphore(RenderFinishedSemaphores[i]);
                LogicalDevice?.DestroySemaphore(ImageAvailableSemaphores[i]);
                LogicalDevice?.DestroyFence(InFlightFences[i]);
            }
            
            LogicalDevice?.FreeCommandBuffers(GraphicsCommandPool, (uint)commandBuffers.Length, commandBuffers);
            LogicalDevice?.DestroyCommandPool(GraphicsCommandPool);
            
            SamplerStates?.Dispose();
        }
            
        // Snapshot: each Dispose() now calls RemoveResource, which mutates the set.
        foreach (var disposableObject in _graphicsResources.ToArray())
        {
            if (disposableObject.IsDisposed) continue;

            disposableObject?.Dispose();
        }

        _submissionSync?.Dispose();
        _graphicsResources?.Clear();
    }

    internal static IGraphicsDevice Create(MainGraphicsDevice device, GraphicsDeviceType deviceType)
    {
        return new GraphicsDevice(device, deviceType);
    }
}