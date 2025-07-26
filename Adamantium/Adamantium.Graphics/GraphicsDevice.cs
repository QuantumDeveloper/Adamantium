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
using Adamantium.Graphics.Core.Vertices;
using Adamantium.Graphics.Effects;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using AdamantiumVulkan.Core;
using AdamantiumVulkan.Core.Interop;
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
        
    // -- Drawing States
        
    private TrackingCollection<Viewport> viewports;
    private TrackingCollection<Rect2D> scissors;
        
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
        
    private SyncObject _submissionSync;
    private static string SyncGuid = Guid.NewGuid().ToString();

    private List<GraphicsResource> _graphicsResources = new List<GraphicsResource>();

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

    public CommandPool CommandPool { get; private set; }
    internal Semaphore[] ImageAvailableSemaphores { get; private set; }
    internal Semaphore[] RenderFinishedSemaphores { get; private set; }
    internal Fence[] InFlightFences { get; private set; }

    public uint CurrentFrame => frame;

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

    public IEffectResourceLinker CreateEffectResourceLinker()
    {
        return new EffectResourceLinker();
    }

    public IEffectPass CreateEffectPass(Logger logger, Effect effect, EffectTechnique technique, EffectData.Pass pass, string name)
    {
        return new EffectPass(logger, effect, technique, pass, name);
    }

    public void BindShader(CommandBuffer cmd, ShaderStageFlagBits stage, ShaderEXT shader)
    {
        LogicalDevice.BindShader(cmd, stage, shader);
    }

    public RenderPass CreateRenderPass(RenderPassCreateInfo createInfo)
    {
        return LogicalDevice.CreateRenderPass(createInfo);
    }

    public uint AlignSize(uint size, uint alignment)
    {
        return (size + alignment - 1) & ~(alignment - 1);
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

    public uint GetDescriptorSetLayoutOffset(DescriptorSetLayout layout, uint bindingSlot)
    {
        return LogicalDevice.GetDescriptorSetLayoutOffset(layout, bindingSlot);
    }

    public ShaderEXT CreateShader(ShaderCreateInfoEXT shaderCreateInfo)
    {
        return LogicalDevice.CreateShader(shaderCreateInfo);
    }

    public void DestroyShader(ShaderEXT shaderObject)
    {
        LogicalDevice.DestroyShaderEXT(shaderObject);
    }

    private void CreateCommandPool()
    {
        var graphicsFamily = MainDevice.QueueFamilyContainer.GetFamilyInfo(QueueFlagBits.GraphicsBit);

        var poolInfo = new CommandPoolCreateInfo
        {
            QueueFamilyIndex = graphicsFamily.FamilyIndex,
            Flags = CommandPoolCreateFlagBits.ResetCommandBufferBit
        };
        CommandPool = LogicalDevice.CreateCommandPool(poolInfo);
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
        allocInfo.CommandPool = CommandPool;
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
        RenderFinishedSemaphores = LogicalDevice.CreateSemaphores(semaphoreInfo, MaxFramesInFlight);
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
        
        ImageMemoryBarrier barrier = new ImageMemoryBarrier();
        barrier.SrcQueueFamilyIndex = (~0U);
        barrier.DstQueueFamilyIndex = (~0U);
        barrier.SrcAccessMask = sourceAccessMask;
        barrier.DstAccessMask = destinationAccessMask;
        barrier.OldLayout = oldLayout;
        barrier.NewLayout = newLayout;
        barrier.Image = texture.GetImage();
        barrier.SubresourceRange = range;

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

        texture.ImageLayout = newLayout;
    }

    public void TransitionImagesForRendering(CommandBuffer commandBuffer, params ITexture[] inputTargets)
    {
        var barriers = new List<ImageMemoryBarrier2>();
        foreach (var renderTarget in inputTargets)
        {
            var barrier = new ImageMemoryBarrier2();
            barrier.SType = StructureType.ImageMemoryBarrier2;
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
        dependencyInfo.SType = StructureType.DependencyInfo;
        dependencyInfo.PImageMemoryBarriers = barriers.ToArray();
        dependencyInfo.ImageMemoryBarrierCount = (uint)barriers.Count;
        
        commandBuffer.PipelineBarrier2(dependencyInfo);
    }
    
    public void TransitionDepthBufferForRendering(CommandBuffer commandBuffer, IDepthStencilBuffer depthBuffer)
    {
        if (depthBuffer == null) return;
        
        var barriers = new List<ImageMemoryBarrier2>();

        var barrier = new ImageMemoryBarrier2();
        barrier.SType = StructureType.ImageMemoryBarrier2;
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
        dependencyInfo.SType = StructureType.DependencyInfo;
        dependencyInfo.PImageMemoryBarriers = barriers.ToArray();
        dependencyInfo.ImageMemoryBarrierCount = (uint)barriers.Count;
        
        commandBuffer.PipelineBarrier2(dependencyInfo);
    }

    public void TransitionImagesAfterRendering(CommandBuffer commandBuffer, params ITexture[] inputTargets)
    {
        var barriers = new List<ImageMemoryBarrier2>();
        foreach (var renderTarget in inputTargets)
        {
            var barrier = new ImageMemoryBarrier2();
            barrier.SType = StructureType.ImageMemoryBarrier2;
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
        dependencyInfo.SType = StructureType.DependencyInfo;
        dependencyInfo.PImageMemoryBarriers = barriers.ToArray();
        dependencyInfo.ImageMemoryBarrierCount = (uint)barriers.Count;
        
        commandBuffer.PipelineBarrier2(dependencyInfo);
    }
    
    public void TransitionDepthBufferAfterRendering(CommandBuffer commandBuffer, IDepthStencilBuffer depthBuffer)
    {
        if (depthBuffer == null) return;
        
        var barriers = new List<ImageMemoryBarrier2>();

        var barrier = new ImageMemoryBarrier2();
        barrier.SType = StructureType.ImageMemoryBarrier2;
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
        dependencyInfo.SType = StructureType.DependencyInfo;
        dependencyInfo.PImageMemoryBarriers = barriers.ToArray();
        dependencyInfo.ImageMemoryBarrierCount = (uint)barriers.Count;
        
        commandBuffer.PipelineBarrier2(dependencyInfo);
    }

    public bool BeginDraw(float depth = 1.0f, uint stencil = 0)
    {
        CanPresent = false;
        var renderFence = InFlightFences[CurrentFrame];
        var result = LogicalDevice.WaitForFences(1, renderFence, true, ulong.MaxValue);

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
                colorAttachmentInfo.SType = StructureType.RenderingAttachmentInfo;
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
            depthAttachmentInfo.SType = StructureType.RenderingAttachmentInfo;
            depthAttachmentInfo.ImageView = depthBuffer?.GetImageView();
            depthAttachmentInfo.ImageLayout = ImageLayout.DepthStencilAttachmentOptimal;
            depthAttachmentInfo.ResolveMode = ResolveModeFlagBits.None;
            depthAttachmentInfo.LoadOp = loadOperation;
            depthAttachmentInfo.StoreOp = AttachmentStoreOp.Store;
            depthAttachmentInfo.ClearValue = clearDepthValue;

            var renderingInfo = new RenderingInfo();
            renderingInfo.SType = StructureType.RenderingInfo;
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
            SType = StructureType.MemoryBarrier2,
            SrcStageMask = PipelineStageFlagBits2.ColorAttachmentOutputBit,
            SrcAccessMask = AccessFlagBits2.ColorAttachmentWriteBit,
            DstStageMask = PipelineStageFlagBits2.ColorAttachmentOutputBit,
            DstAccessMask = AccessFlagBits2.ColorAttachmentReadBit
        };

        var dependencyInfo = new DependencyInfo
        {
            SType = StructureType.DependencyInfo,
            MemoryBarrierCount = 1,
            PMemoryBarriers = [memoryBarrier]
        };

        CurrentCommandBuffer.PipelineBarrier2(dependencyInfo);
    }

    public void Submit()
    {
        if (!CommandBufferStarted) return;

        _submissionSync?.Wait();
            
        //Log.Logger.Debug($"Enter Submit for device {DeviceId}");

        var commandBuffer = CurrentCommandBuffer;
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

        
        if (Presenter is SwapChainGraphicsPresenter)
        {
            waitSemaphoresArray[0] = ImageAvailableSemaphores[CurrentFrame];
            submitInfo.WaitSemaphoreCount = (uint)waitSemaphoresArray.Length;
            submitInfo.PWaitSemaphores = waitSemaphoresArray;
                
            signalSemaphoresArray[0] = RenderFinishedSemaphores[CurrentFrame];
        
            submitInfo.SignalSemaphoreCount = (uint)signalSemaphoresArray.Length;
            submitInfo.PSignalSemaphores = signalSemaphoresArray;
        }

        submitInfo.PWaitDstStageMask = waitStages;
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
        LogicalDevice.WaitForFences(1, renderFence, true, ulong.MaxValue);
            
        if (result != Result.Success)
        {
            Log.Logger.Error($"failed to submit draw command buffer! Result was {result}");
        }

        //GraphicsQueue.QueueWaitIdle();
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
        LogicalDevice.SetObjectDebugNameEXT(objectHandle, objectType, name);
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

        ulong[] offset = new ulong[vertexBuffers.Length];
        var commandBuffer = commandBuffers[CurrentFrame];
        var buffers = vertexBuffers.Select(x=>x.GetBuffer()).ToArray();
        commandBuffer.BindVertexBuffers(0, (uint)buffers.Length, buffers, offset);
    }

    public void SetIndexBuffer(IBuffer indexBuffer)
    {
        var commandBuffer = commandBuffers[CurrentFrame];
        commandBuffer.BindIndexBuffer(indexBuffer.GetBuffer(), 0, IndexType.Uint32);
    }

    private void SetDrawingState(CommandBuffer commandBuffer)
    {
        LogicalDevice.SetViewportWithCountEXT(commandBuffer, viewports.ToArray());
        LogicalDevice.SetScissorsWithCountEXT(commandBuffer, scissors.ToArray());
        LogicalDevice.SetRasterizerDiscardEnableEXT(commandBuffer, RasterizerDiscardEnabled);
            
        var bindingDescription = VertexType.GetBindingDescription2();
        var attributes = VertexType.GetVertexAttributeDescription2();
        LogicalDevice.SetVertexInputEXT(commandBuffer,1, bindingDescription, (uint)attributes.Length, attributes);
        LogicalDevice.SetPrimitiveTopologyEXT(commandBuffer, PrimitiveTopology);
        LogicalDevice.SetPrimitiveRestartEnableEXT(commandBuffer, PrimitiveRestartEnable);
        LogicalDevice.SetRasterizationSamplesEXT(commandBuffer, (SampleCountFlagBits)MSAALevel);
        LogicalDevice.SetSampleMaskEXT(commandBuffer, (SampleCountFlagBits)MSAALevel, SampleMask);
        LogicalDevice.SetAlphaToCoverageEnableEXT(commandBuffer, AlphaToCoverageEnable);
        LogicalDevice.SetPolygonModeEXT(commandBuffer, PolygonMode);
        if (PolygonMode == PolygonMode.Line)
        {
            commandBuffer.SetLineWidth(LineWidth);  
        }
            
        LogicalDevice.SetCullModeEXT(commandBuffer, CullMode);
        LogicalDevice.SetFrontFaceEXT(commandBuffer, FrontFace);
        LogicalDevice.SetDepthWriteEnableEXT(commandBuffer, DepthWriteEnable);
        LogicalDevice.SetDepthTestEnableEXT(commandBuffer, DepthTestEnabled);
        LogicalDevice.SetDepthCompareOpEXT(commandBuffer, DepthCompareFunction);
        LogicalDevice.SetDepthBoundsTestEnableEXT(commandBuffer, DepthBoundsTestEnabled);
        LogicalDevice.SetDepthBiasEnableEXT(commandBuffer, DepthBiasEnabled);
        LogicalDevice.SetStencilTestEnableEXT(commandBuffer, StencilTestEnabled);
        LogicalDevice.SetLogicOpEnableEXT(commandBuffer, LogicOperationsEnabled);
            
        LogicalDevice.SetColorBlendEquationEXT(commandBuffer, 0, 1, ColorBlendEquation);
        LogicalDevice.SetColorBlendEnableEXT(commandBuffer, ColorBlendEnabled);
        LogicalDevice.SetColorWriteMaskEXT(commandBuffer, ColorComponentFlags);
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
        return LogicalDevice.BeginSingleTimeCommand(CommandPool);
    }

    public void EndSingleTimeCommand(CommandBuffer commandBuffer)
    {
        _submissionSync?.Wait();
        LogicalDevice.EndSingleTimeCommands(resourceQueue, CommandPool, commandBuffer);
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
        LogicalDevice.BindDescriptorBuffers(commandBuffer, bindings);
    }

    public void SetDescriptorBufferOffsets(CommandBuffer commandBuffer, PipelineBindPoint pipelineBindPoint, PipelineLayout layout,
        uint dataSet, uint setCount, uint[] bufferIndices, ulong[] offsets)
    {
        LogicalDevice.SetDescriptorBufferOffsets(commandBuffer, pipelineBindPoint, layout, dataSet, setCount,
            bufferIndices, offsets);
    }

    public uint GetDescriptorSetLayoutSize(DescriptorSetLayout layout)
    {
        return LogicalDevice.GetDescriptorSetLayoutSize(layout);
    }

    public ulong UniformBufferDescriptorSize => MainDevice.GraphicsAdapter
        .DeviceBufferProperties.UniformBufferDescriptorSize;
    public ulong SamplerDescriptorSize => MainDevice.GraphicsAdapter.DeviceBufferProperties.SamplerDescriptorSize;
    public ulong SampledImageDescriptorSize => MainDevice.GraphicsAdapter.DeviceBufferProperties
        .SampledImageDescriptorSize;

    public uint DescriptorBufferOffsetAlignment => (uint)MainDevice.GraphicsAdapter
        .DeviceBufferProperties.DescriptorBufferOffsetAlignment;
    public unsafe void GetDescriptor(DescriptorGetInfoEXT descriptorGetInfoExt, uint descriptorSize, void* descriptorPtr)
    {
        LogicalDevice.GetDescriptor(descriptorGetInfoExt, descriptorSize, descriptorPtr);
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

    public unsafe void* MapMemory(DeviceMemory memory, ulong offset, ulong size, uint flags)
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
            LogicalDevice?.FreeCommandBuffers(CommandPool, (uint)commandBuffers.Length, commandBuffers);
            LogicalDevice?.DestroyCommandPool(CommandPool);
        }
        else
        {
            Log.Logger.Debug("Disposing render device");
            DefaultEffectPool?.Dispose();
            
            for (int i = 0; i < commandBuffers.Length; i++)
            {
                LogicalDevice?.DestroySemaphore(RenderFinishedSemaphores[i]);
                LogicalDevice?.DestroySemaphore(ImageAvailableSemaphores[i]);
                LogicalDevice?.DestroyFence(InFlightFences[i]);
            }
            
            LogicalDevice?.FreeCommandBuffers(CommandPool, (uint)commandBuffers.Length, commandBuffers);
            LogicalDevice?.DestroyCommandPool(CommandPool);
            
            SamplerStates?.Dispose();
        }
            
        foreach (var disposableObject in _graphicsResources)
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