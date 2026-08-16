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
using Adamantium.Vulkan.Core;
using Adamantium.Vulkan.Core.Interop;
using QuantumBinding.Utils;
using Serilog;
using EffectTechnique = Adamantium.Graphics.Core.EffectsFramework.EffectTechnique;
using Semaphore = Adamantium.Vulkan.Core.Semaphore;
using Exception = System.Exception;
using Image = Adamantium.Vulkan.Core.Image;

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
    private const StencilFaceFlagBits BothStencilFaces = StencilFaceFlagBits.FrontAndBack;
    private bool _cStencilTest;
    private bool _cLogicOp;
        
    // --- End of drawing states

    private IRenderTarget[] renderTargets;
    private IDepthStencilBuffer depthBuffer;

    private readonly PipelineStageFlagBits[] waitStages = [PipelineStageFlagBits.ColorAttachmentOutputBit];
        
    public Device LogicalDevice => MainDevice?.LogicalDevice;
    public GraphicsAdapter Adapter => VulkanInstance?.MainGraphicsAdapter;

    // The device-memory sub-allocator is SHARED (one per logical device), owned by the MAIN device. A render-device
    // wrapper never creates its own - it references the shared one, so no window grabs its own big host-visible BAR block
    // (that per-window block only device.Dispose freed, so a few open windows exhausted the ~214 MB BAR heap). Mirrors
    // the shared DescriptorHeapManager.
    public DeviceMemoryAllocator MemoryAllocator => (DeviceMemoryAllocator)MainDevice.MemoryAllocator;
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

    public ulong FrameTicket { get; private set; }

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

    public ITexture CreateTextureArray(TextureDescription description, IReadOnlyList<byte[]> layers)
    {
        return Texture.CreateArrayFrom(this, description, layers);
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

    /// <summary>Stencil comparison, reference, and what a passing fragment writes back - for marking COVERAGE: one pass
    /// stamps where it drew, a later pass tests against it.</summary>
    public CompareOp StencilCompareOp { get; set; } = CompareOp.Always;

    public StencilOp StencilPassOp { get; set; } = StencilOp.Keep;

    public uint StencilReference { get; set; }

    public uint StencilWriteMask { get; set; } = 0xFF;

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
        // Always enabled. The old condition (only sync when graphics == transfer queue) wrongly assumed separate queues
        // never need host synchronization - but our devices (primary render + resource loader) submit and record from
        // different threads, and command pools / a shared VkQueue require external sync regardless of queue families.
        // Leaving it off on separate-queue hardware produced vkQueueSubmit / vkAllocate/Free/EndCommandBuffer
        // THREADING ERRORs during concurrent content-load uploads. The mutex is process-shared (SyncGuid) + reentrant.
        _submissionSync = new SyncObject(SyncGuid, true);
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

    /// <summary>Hand a GPU resource over for disposal. Held by the LOGICAL device (see
    /// <see cref="MainGraphicsDevice.RetireResource"/>) until every drawing wrapper has retired the frames it had in
    /// flight - the wrappers share one VkDevice and share resources, so this device's own fences prove too little.</summary>
    public void AddToDeferDisposeQueue(IDisposable obj)
    {
        MainDevice.RetireResource(obj);
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

    public ShaderEXT CreateShader(ShaderCreateInfoEXT shaderCreateInfo, string name = null)
    {
        // Shader-object binary cache (dodges the Turing vkCreateShadersEXT NVVM flake). On a cache hit, create from the
        // driver-compiled BINARY - no NVVM, no flake. On a miss (or an incompatible binary after a driver/device change),
        // compile from SPIR-V once and persist the binary for next launch. The binary path goes through
        // CreateShaderFromBinary, which honours the mandatory 16-byte pCode alignment for VK_SHADER_CODE_TYPE_BINARY_EXT.
        // `name` is what the cache file is called - effect.technique.pass.stage - so the folder says which shaders exist
        // and which ones a launch actually compiled.
        if (ShaderBinaryCache.TryLoad(this, shaderCreateInfo, name, out var binary))
        {
            var result = LogicalDevice.CreateShaderFromBinary(ShaderBinaryCache.AsBinary(shaderCreateInfo, binary), out var cached);
            if (result == Result.Success) return cached;
            // else: incompatible binary (driver/device change) -> fall through and recompile from SPIR-V (re-caches below).
        }

        LogicalDevice.CreateShadersEXT(1, shaderCreateInfo, null, out var shaderObject);
        ShaderBinaryCache.Save(this, shaderCreateInfo, name, shaderObject[0]);
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
            // The swapchain image is filled by a BLIT (EndDraw copies the resolved render target into it), not by
            // colour-attachment writes, so the source side has to name the transfer stage as well. Naming only
            // ColorAttachmentOutput left the layout transition unsynchronized with the blit that had just written the
            // image - a WRITE_AFTER_WRITE hazard the layer reports by name, and one that lets the presented image
            // carry a partially copied frame.
            SrcStageMask = PipelineStageFlagBits2.ColorAttachmentOutputBit | PipelineStageFlagBits2.AllTransferBit,
            SrcAccessMask = AccessFlagBits2.ColorAttachmentWriteBit | AccessFlagBits2.TransferWriteBit,
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
    
    /// <summary>Why the last <see cref="BeginDraw"/> returned false (a Vulkan device/surface error). Surfaced so a
    /// caller that only sees the bool (e.g. the designer's headless render) can report the real reason, not "render failed".</summary>
    public string LastFrameError { get; private set; }

    // Last fence-wait error already logged. A persistent device error makes BeginDraw fail EVERY frame; logging it per
    // frame floods the Serilog console sink and the render thread then blocks forever on that sink's lock (a slow/stalled
    // console write, held across all loggers) - which turned a recoverable device hiccup into a hard freeze. Log only on a
    // NEW error transition instead.
    private Result _lastFenceWaitError = Result.Success;

    // The factual error code (e.g. ErrorDeviceLost) is only the SYMPTOM; the validation layers carry the real cause.
    // Append whatever they reported so the report isn't a guess. Empty (with a hint) when validation is off.
    private static string DescribeRecentValidation()
    {
        var messages = Adamantium.Graphics.Core.VulkanInstance.RecentValidationMessages;
        if (messages.Count == 0)
            return " (no validation messages captured — run with graphics debug enabled to get the real cause)";
        return "\nValidation layer messages leading up to it:\n  - " + string.Join("\n  - ", messages);
    }

    // After a device-lost, VK_EXT_device_fault returns the driver's REAL fault description (and how many faulting
    // address regions / vendor records there are) - the diagnostic that works without validation layers. Best-effort:
    // only when the extension was enabled, and never throws back into the caller.
    private string DescribeDeviceFault()
    {
        try
        {
            if (MainDevice is not { DeviceFaultSupported: true }) return string.Empty;
            if (LogicalDevice.GetDeviceFaultInfoEXT(out var fault) != Result.Success || fault == null) return string.Empty;

            var sb = new System.Text.StringBuilder($"\nGPU device fault (VK_EXT_device_fault): \"{fault.Description}\"");
            var addresses = fault.PAddressInfos.Span;
            for (var i = 0; i < addresses.Length; i++)
            {
                var a = addresses[i];
                sb.Append($"\n  - {a.AddressType}: address 0x{(ulong)a.ReportedAddress:X} (precision 0x{(ulong)a.AddressPrecision:X})");
            }
            var vendors = fault.PVendorInfos.Span;
            for (var i = 0; i < vendors.Length; i++)
            {
                var v = vendors[i];
                sb.Append($"\n  - vendor \"{v.Description}\": code=0x{v.VendorFaultCode:X} data=0x{v.VendorFaultData:X}");
            }
            return sb.ToString();
        }
        catch { return string.Empty; }
    }

    public bool BeginDraw(float depth = 1.0f, uint stencil = 0, Action<CommandBuffer> beforeRenderPass = null)
    {
        CanPresent = false;
        LastFrameError = null;
        var renderFence = InFlightFences[CurrentFrame];
        var result = LogicalDevice.WaitForFences(1, renderFence, true, ulong.MaxValue);

        FrameTicket++;
        MainDevice.OnFrameStarted();

        if (result != Result.Success && result != Result.Timeout)
        {
            // Only on a NEW error - the per-frame flood on a persistent error froze the render thread on the console sink
            // lock (see _lastFenceWaitError). The expensive fault/validation describe is likewise done once per transition.
            if (result != _lastFenceWaitError)
            {
                _lastFenceWaitError = result;
                LastFrameError = $"WaitForFences returned {result}{DescribeDeviceFault()}{DescribeRecentValidation()}";
                Log.Logger.Information($"Wait for fences result: {result}. DETAIL: {LastFrameError}");
            }
            return false;
        }
        _lastFenceWaitError = Result.Success;   // recovered - re-arm the log for the next new error

        // Swapchain image is ACQUIRED LATE - at the very end of BeginDraw, after the command buffer + beforeRenderPass
        // record (see below). The frame renders to an OFFSCREEN target; the swapchain image is only needed for EndDraw's
        // blit + present. Acquiring HERE (before the fallible record) leaked the image + its ImageAvailable semaphore
        // whenever anything after it aborted the frame before Submit - the acquire/present imbalance that exhausted the
        // swapchain until AcquireNextImage blocked forever (VUID-vkAcquireNextImageKHR-semaphore-01286 / -surface-07783).

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

        // LATE acquire (see the note near the top of BeginDraw): every fallible step above - CB begin, image transitions,
        // the beforeRenderPass record, BeginRendering - has run WITHOUT touching the swapchain image, so a failure there
        // aborts the frame with no dangling acquired image. Only now, committed to Submit + Present, do we take one.
        if (Presenter is SwapChainGraphicsPresenter)
        {
            if (!Presenter.AcquireNextImage(null, ImageAvailableSemaphores[CurrentFrame]))
            {
                LastFrameError = $"swapchain AcquireNextImage failed{DescribeRecentValidation()}";
                return false;
            }
        }

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
            // The first thing this command buffer does to the acquired image is a LAYOUT TRANSITION into TransferDst,
            // followed by the blit - both transfer-stage work. Waiting only at ColorAttachmentOutput left those two free
            // to run before the image was actually available, so the frame was written into an image the presentation
            // engine had not released yet: a WRITE_AFTER_READ against vkAcquireNextImageKHR, and on screen a frame that
            // flickers as a whole regardless of what it contains.
            waitStageList.Add(PipelineStageFlagBits.ColorAttachmentOutputBit | PipelineStageFlagBits.TransferBit);
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

    // The most recently set viewport/scissor, so code that interrupts the pass to render to another target (the font
    // renderer's offscreen text target) can save and restore the exact clip that was active rather than resetting it
    // to the full attachment (which left the resumed pass drawing unclipped).
    public Rect2D[] CurrentScissors => scissorsArray;
    public Viewport[] CurrentViewports => viewportsArray;

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
            if (VertexType == null)
            {
                // No vertex input: a draw whose vertices come only from SV_VertexID and whose per-instance data is read
                // from a storage buffer by SV_InstanceID (the instanced SDF batch). Bind an EMPTY vertex-input state so
                // the pipeline expects no vertex buffer (else GetBindingDescription2(null) throws + aborts the frame).
                commandBuffer.SetVertexInputEXT(0, default(VertexInputBindingDescription2EXT), 0, System.Array.Empty<VertexInputAttributeDescription2EXT>());
            }
            else
            {
                var bindingDescription = VertexType.GetBindingDescription2();
                var attributes = VertexType.GetVertexAttributeDescription2();
                commandBuffer.SetVertexInputEXT(1, bindingDescription, (uint)attributes.Length, attributes);
            }
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
        if (StencilTestEnabled)
        {
            commandBuffer.SetStencilOp(BothStencilFaces, StencilOp.Keep, StencilPassOp, StencilOp.Keep, StencilCompareOp);
            commandBuffer.SetStencilCompareMask(BothStencilFaces, 0xFF);
            commandBuffer.SetStencilWriteMask(BothStencilFaces, StencilWriteMask);
            commandBuffer.SetStencilReference(BothStencilFaces, StencilReference);
        }
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

        // A pass applied with an unbound resource carries an out-of-heap index; drawing it puts ANOTHER effect's live
        // descriptor on screen - an evening went into chasing the glyph atlas smeared across a frame that way. Skip the
        // draw; the pass has already said which parameter nobody bound.
        if (!CurrentEffectPass.ResourcesBound)
        {
            return;
        }

        var commandBuffer = commandBuffers[CurrentFrame];
        SetDrawingState(commandBuffer);

        commandBuffer.Draw((uint)vertexCount, instanceCount, firstVertex, firstInstance);
    }

    // indexCount lets the caller draw fewer indices than the buffer holds - needed for an over-allocated/reused index
    // buffer (its capacity exceeds the live geometry). 0 means "the whole buffer" (its ElementCount), as before.
    public void DrawIndexed(IBuffer vertexBuffer, IBuffer indexBuffer, uint instanceCount = 1, uint indexCount = 0)
    {
        // Same refusal as Draw: an unbound resource means an out-of-heap index, and drawing with one shows another
        // effect's texture.
        if (CurrentEffectPass is { ResourcesBound: false })
        {
            return;
        }

        ulong offset = 0;
        var commandBuffer = commandBuffers[CurrentFrame];

        SetDrawingState(commandBuffer);

        commandBuffer.BindVertexBuffers(0, 1, vertexBuffer.GetBuffer(), offset);

        commandBuffer.BindIndexBuffer(indexBuffer.GetBuffer(), 0, IndexType.Uint32);

        commandBuffer.DrawIndexed(indexCount > 0 ? indexCount : (uint)indexBuffer.ElementCount, instanceCount, 0, 0, 0);
    }

    // Non-indexed instanced draw: binds the vertex buffer and draws vertexCount vertices as instanceCount instances. For
    // a mesh that is an explicit triangle list with no index buffer (how UI shape fills tessellate) - the non-indexed
    // twin of DrawIndexed; instancing (instanceCount) works identically, indices are not required.
    public void Draw(IBuffer vertexBuffer, uint vertexCount, uint instanceCount = 1)
    {
        ulong offset = 0;
        var commandBuffer = commandBuffers[CurrentFrame];

        // BIND the vertex buffer BEFORE SetDrawingState (the proven SetVertexBuffer()+Draw() order). SetDrawingState emits
        // the DYNAMIC vertex-input (SetVertexInputEXT); binding the buffer AFTER that emit read back as zero on this driver
        // (a valid, populated buffer that the shader still fetched as 0 -> collapsed geometry, no fragments, no VL error).
        commandBuffer.BindVertexBuffers(0, 1, vertexBuffer.GetBuffer(), offset);

        SetDrawingState(commandBuffer);

        commandBuffer.Draw(vertexCount, instanceCount, 0, 0);
    }

    // --- GPU-driven geometry (compute amplification -> indirect draw) ---------------------------------------------
    // Building blocks for the stroke/line compute pipeline: a compute pass writes vertices + a draw-args buffer into
    // SSBOs, a barrier makes those writes visible, then an indirect draw consumes them. The amount of geometry is
    // decided on the GPU, so the CPU never re-tessellates per frame.

    /// <summary>Dispatches the currently-bound compute shader. The compute pass (its shader + resource bindings) must
    /// be applied first, just as a draw needs an effect pass. Record this OUTSIDE a dynamic-rendering pass.</summary>
    public void Dispatch(uint groupCountX, uint groupCountY = 1, uint groupCountZ = 1)
    {
        commandBuffers[CurrentFrame].Dispatch(groupCountX, groupCountY, groupCountZ);
    }

    /// <summary>Indirect draw: vertexCount/instanceCount/firstVertex/firstInstance come from <paramref name="indirectBuffer"/>
    /// (an array of VkDrawIndirectCommand, 16 bytes each), so a compute pass can size the draw on the GPU.
    /// </summary>
    public void DrawIndirect(IBuffer vertexBuffer, IBuffer indirectBuffer, uint drawCount = 1, uint stride = 16)
    {
        var commandBuffer = commandBuffers[CurrentFrame];
        SetDrawingState(commandBuffer);
        commandBuffer.BindVertexBuffers(0, 1, vertexBuffer.GetBuffer(), 0UL);
        commandBuffer.DrawIndirect(indirectBuffer.GetBuffer(), 0, drawCount, stride);
    }

    /// <summary>Buffer memory barrier on the current frame's command buffer (synchronization2). Used to make a compute
    /// SSBO write visible to a later read - e.g. compute write -> vertex-attribute / indirect-command read.
    /// </summary>
    public void BufferBarrier(IBuffer buffer, PipelineStageFlagBits2 srcStage, AccessFlagBits2 srcAccess,
        PipelineStageFlagBits2 dstStage, AccessFlagBits2 dstAccess)
    {
        // BufferMemoryBarrier2 exposes the raw interop flag types (unlike ImageMemoryBarrier2, which takes the friendly
        // enums); they implicitly-convert from ulong, so cast the enums through ulong.
        var barrier = new BufferMemoryBarrier2
        {
            SrcStageMask = (ulong)srcStage,
            SrcAccessMask = (ulong)srcAccess,
            DstStageMask = (ulong)dstStage,
            DstAccessMask = (ulong)dstAccess,
            SrcQueueFamilyIndex = ~0U,
            DstQueueFamilyIndex = ~0U,
            Buffer = buffer.GetBuffer(),
            Offset = 0,
            Size = ~0UL,   // VK_WHOLE_SIZE
        };
        var dependencyInfo = new DependencyInfo
        {
            PBufferMemoryBarriers = new[] { barrier },
            BufferMemoryBarrierCount = 1,
        };
        commandBuffers[CurrentFrame].PipelineBarrier2(dependencyInfo);
    }

    public CommandBuffer BeginSingleTimeCommand()
    {
        // Held across the whole single-time scope (until EndSingleTimeCommand): serializes command-pool access AND the
        // queue submit in EndSingleTimeCommand against the main render submit and other uploads (see _submissionSync).
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

    public void Destroy(Adamantium.Vulkan.Core.Buffer buffer)
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

        // The device-memory allocator is SHARED and owned by the main device (disposed with it) - a render/resource
        // device must not dispose it here.
        _submissionSync?.Dispose();
        _graphicsResources?.Clear();
    }

    internal static IGraphicsDevice Create(MainGraphicsDevice device, GraphicsDeviceType deviceType)
    {
        return new GraphicsDevice(device, deviceType);
    }
}
