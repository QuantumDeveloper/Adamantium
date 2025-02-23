using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Adamantium.Core;
using Adamantium.Core.Collections;
using Adamantium.EffectsCompiler;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Graphics.Core.Presentation;
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
    internal Queue GraphicsQueue { get; private set; }
    private Queue resourceQueue;
    private Queue computeQueue;
        
    private readonly SubmitInfo[] submitInfos = new SubmitInfo[1];
    private uint imageIndex;
        
    private Type vertexType;
    private PrimitiveTopology primitiveTopology;
    private IEffectPass currentEffectPass;
        
    // -- Drawing States
        
    private TrackingCollection<Viewport> viewports;
    private TrackingCollection<Rect2D> scissors;
    private TrackingCollection<DynamicState> dynamicStates;
        
    // --- End of drawing states

    private IRenderTarget renderTarget;
    private IDepthStencilBuffer depthBuffer;

    private readonly PipelineStageFlagBits[] waitStages = { PipelineStageFlagBits.ColorAttachmentOutputBit };
        
    public Device LogicalDevice => MainDevice?.LogicalDevice;
    public GraphicsAdapter Adapter => VulkanInstance?.MainGraphicsAdapter;

    internal VulkanInstance VulkanInstance => MainDevice?.VulkanInstance;
        
    private Semaphore[] waitSemaphoresArray = new Semaphore[1];
    private Semaphore[] signalSemaphoresArray = new Semaphore[1];
    private CommandBuffer[] commandBuffersArray = new CommandBuffer[1];
        
    private SyncObject _submissionSync;
    private static string SyncGuid = Guid.NewGuid().ToString();

    private List<GraphicsResource> _graphicsResources = new List<GraphicsResource>();

    private GraphicsDevice(MainGraphicsDevice mainDevice)
    {
        CreateResourceLoadingDevice(mainDevice);
    }

    private GraphicsDevice(MainGraphicsDevice mainDevice, PresentationParameters presentationParameters)
    {
        CreateRenderDevice(mainDevice, presentationParameters);
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

    private void CreateRenderDevice(MainGraphicsDevice mainDevice, PresentationParameters presentationParameters)
    {
        MainDevice = mainDevice;
        DeviceType = GraphicsDeviceType.Primary;
        InitializeSyncObject();
        DeviceId = Guid.NewGuid();

        EnableDynamicRendering = mainDevice.EnableDynamicRendering;

        EffectPools = new List<EffectPool>();
        DefaultEffectPool = EffectPool.New(this);
            
        MaxFramesInFlight = presentationParameters.BuffersCount;
        InitializeRenderDevice(presentationParameters);
        InitializePipeline();

        Log.Logger.Debug($"Primary render device created. Id: {DeviceId}");

        SampleMask = [0xF];
    }

    private void InitializePipeline()
    {
        dynamicStates = new TrackingCollection<DynamicState>();
        viewports = new TrackingCollection<Viewport>();
        scissors = new TrackingCollection<Rect2D>();

        SamplerStates = new SamplerStateCollection(this);
        Sampler = SamplerStates.Default;
            
        ClearColor = Colors.CornflowerBlue;
    }

    public GraphicsDeviceType DeviceType { get; private set; }

    public bool IsPrimaryDevice => DeviceType == GraphicsDeviceType.Primary;
        
    public bool IsResourceLoaderDevice => DeviceType == GraphicsDeviceType.ResourceLoader;
        
    public bool EnableDynamicRendering { get; private set; }

    public CommandPool CommandPool { get; private set; }
    internal Semaphore[] ImageAvailableSemaphores { get; private set; }
    internal Semaphore[] RenderFinishedSemaphores { get; private set; }
    internal Fence[] InFlightFences { get; private set; }
        
    public PresenterState LastPresenterState { get; private set; }

    public uint CurrentFrame { get; private set; }

    public uint ImageIndex => imageIndex;

    public uint MaxFramesInFlight { get; private set; }

    public List<EffectPool> EffectPools { get; private set; }

    public EffectPool DefaultEffectPool { get; private set; }

    public GraphicsPresenter Presenter { get; private set; }

    public MainGraphicsDevice MainDevice { get; private set; }

    public PresentInfoKHR FillPresentInfo(SwapchainKHR[] swapchains)
    {
        var presentInfo = new PresentInfoKHR();
        presentInfo.WaitSemaphoreCount = 1;
        presentInfo.PWaitSemaphores = [GetRenderFinishedSemaphoreForCurrentFrame()];
        presentInfo.SwapchainCount = (uint)swapchains.Length;
        presentInfo.PSwapchains = swapchains;
        presentInfo.PImageIndices = [ImageIndex];
        
        return presentInfo;
    }

    public IDepthStencilBuffer CreateDepthBuffer(
        uint width, 
        uint height, 
        DepthFormat format, 
        MSAALevel msaa,
        ImageAspectFlagBits imageAspect = ImageAspectFlagBits.DepthBit)
    {
        return DepthStencilBuffer.New(this, width, height, format, msaa, imageAspect);
    }

    public IRenderTarget CreateRenderTarget(
        uint width, 
        uint height, 
        MSAALevel msaa, SurfaceFormat format,
        ImageUsageFlagBits usage = ImageUsageFlagBits.TransferSrcBit,
        ImageLayout desiredLayout = ImageLayout.ColorAttachmentOptimal)
    {
        return RenderTarget.New(this, width, height, msaa, format, usage, desiredLayout);
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
        
    public MSAALevel RasterizationSamples { get; set; }
        
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

    public bool IsDynamic => dynamicStates.Count > 0;
        
    public bool CommandBufferStarted { get; private set; }

    private void InitializeSyncObject()
    {
        _submissionSync = new SyncObject(SyncGuid, MainDevice.QueueFamilyContainer.IsGraphicsQueueEqualsTransferQueue());
    }

    public void ApplyViewports(params Viewport[] viewports)
    {
        this.viewports.Clear();
        this.viewports.AddRange(viewports);
            
        if (viewports == null) return;

        if (!IsDynamic || !dynamicStates.Contains(DynamicState.Viewport))
        {
        }
    }

    public void ApplyScissors(params Rect2D[] scissors)
    {
        this.scissors.Clear();
        this.scissors.AddRange(scissors);
            
        if (!IsDynamic || !dynamicStates.Contains(DynamicState.Viewport))
        {
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

    private void CreateGraphicsPresenter(PresentationParameters parameters)
    {
        switch (parameters.PresenterType)
        {
            case PresenterType.Swapchain:
                Presenter = new SwapChainGraphicsPresenter(this, parameters, "");
                break;
            case PresenterType.RenderTarget:
                Presenter = new RenderTargetGraphicsPresenter(this, parameters, "");
                break;
            default:
                throw new NotSupportedException($"Presenter type: {parameters.PresenterType} is not supported");
        }
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
        
    public void InsertImageMemoryBarrier(CommandBuffer commandBuffer,
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
        var renderFence = InFlightFences[CurrentFrame];
        var result = LogicalDevice.WaitForFences(1, renderFence, true, ulong.MaxValue);
                
        if (result != Result.Success && result != Result.Timeout)
        {
            Log.Logger.Information($"Wait for fences result: {result}");
            return false;
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
            imageIndex = CurrentFrame;
        }

        var commandBuffer = commandBuffers[ImageIndex];

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

        BeginRendering(commandBuffer, depth, stencil);

        return true;
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
            colorAttachmentInfo.LoadOp = AttachmentLoadOp.Clear;
            colorAttachmentInfo.StoreOp = AttachmentStoreOp.Store;
            colorAttachmentInfo.ClearValue = clearColorValue;
            if (Presenter.Description.MSAALevel != MSAALevel.None)
            {
                colorAttachmentInfo.ImageView = Presenter.RenderTarget.GetImageView();
                colorAttachmentInfo.ResolveImageView = Presenter.GetImageView(ImageIndex);
                colorAttachmentInfo.ResolveMode = ResolveModeFlagBits.AverageBit;
                colorAttachmentInfo.ResolveImageLayout = ImageLayout.ColorAttachmentOptimal;
            }
            else
            {
                colorAttachmentInfo.ImageView = Presenter.GetImageView(imageIndex);
            }

            var depthAttachmentInfo = new RenderingAttachmentInfo();
            depthAttachmentInfo.SType = StructureType.RenderingAttachmentInfo;
            depthAttachmentInfo.ImageView = Presenter.DepthBuffer.GetImageView();
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
            renderingInfo.PColorAttachments = [colorAttachmentInfo];
            renderingInfo.ColorAttachmentCount = 1U;
            renderingInfo.PDepthAttachment = depthAttachmentInfo;
            renderingInfo.PStencilAttachment = depthAttachmentInfo;
            renderingInfo.LayerCount = 1;
                
            var range = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlagBits.ColorBit,
                BaseMipLevel = 0,
                LevelCount = (~0U),
                BaseArrayLayer = 0,
                LayerCount = (~0U)
            };

            var depthRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlagBits.DepthBit | ImageAspectFlagBits.StencilBit,
                BaseMipLevel = 0,
                LevelCount = (~0U),
                BaseArrayLayer = 0,
                LayerCount = (~0U)
            };

            InsertImageMemoryBarrier(commandBuffer,
                Presenter.GetImage(ImageIndex),
                0,
                AccessFlagBits.ColorAttachmentWriteBit,
                ImageLayout.Undefined,
                ImageLayout.ColorAttachmentOptimal,
                PipelineStageFlagBits.TopOfPipeBit,
                PipelineStageFlagBits.ColorAttachmentOutputBit,
                range
            );

            InsertImageMemoryBarrier(commandBuffer,
                Presenter.DepthBuffer.GetImage(),
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
    }

    public void EndDraw()
    {
        var commandBuffer = commandBuffers[ImageIndex];
            
        if (EnableDynamicRendering)
        {
            commandBuffer.EndRendering();

            if (Presenter is not SwapChainGraphicsPresenter) return;
                
            var range = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlagBits.ColorBit,
                BaseMipLevel = 0,
                LevelCount = (~0U),
                BaseArrayLayer = 0,
                LayerCount = (~0U)
            };

            InsertImageMemoryBarrier(commandBuffer,
                Presenter.GetImage(ImageIndex),
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
    }

    public SubmitInfo PrepareSubmit()
    {
        if (!CommandBufferStarted) return null;
            
        var commandBuffer = CurrentCommandBuffer;
        var result = commandBuffer.EndCommandBuffer();
        if (result != Result.Success)
        {
            return null;
            //throw new Exception("failed to record command buffer!");
        }
        CommandBufferStarted = false;
        CanPresent = true;
            
        //Log.Logger.Debug($"Current frame index in PrepareSubmit: {CurrentFrame}");
            
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

        return submitInfo;
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

    public void SetRenderTarget(IRenderTarget renderTarget)
    {
        this.renderTarget = renderTarget;
    }

    public void SetDepthBuffer(IDepthStencilBuffer depthBuffer)
    {
        this.depthBuffer = depthBuffer;
    }

    public void SetVertexBuffer(IBuffer vertexBuffer)
    {
        ulong offset = 0;
        var commandBuffer = commandBuffers[ImageIndex];
        commandBuffer.BindVertexBuffers(0U, 1U, vertexBuffer.GetBuffer(), offset);
    }

    public void SetVertexBuffers(params IBuffer[] vertexBuffers)
    {
        if (vertexBuffers == null || vertexBuffers.Length == 0) return;

        ulong[] offset = new ulong[vertexBuffers.Length];
        var commandBuffer = commandBuffers[ImageIndex];
        var buffers = vertexBuffers.Select(x=>x.GetBuffer()).ToArray();
        commandBuffer.BindVertexBuffers(0, (uint)buffers.Length, buffers, offset);
    }

    public void SetIndexBuffer(IBuffer indexBuffer)
    {
        var commandBuffer = commandBuffers[ImageIndex];
        commandBuffer.BindIndexBuffer(indexBuffer.GetBuffer(), 0, IndexType.Uint32);
    }

    private void SetDrawingState(CommandBuffer commandBuffer)
    {
        LogicalDevice.SetViewportWithCountEXT(commandBuffer, viewports.ToArray());
        LogicalDevice.SetScissorsWithCountEXT(commandBuffer, scissors.ToArray());
        LogicalDevice.SetRasterizerDiscardEnableEXT(commandBuffer, RasterizerDiscardEnabled);
            
        var bindingDescription = VertexUtils.GetBindingDescription2(VertexType);
        var attributes = VertexUtils.GetVertexAttributeDescription2(VertexType);
        LogicalDevice.SetVertexInputEXT(commandBuffer,1, bindingDescription, (uint)attributes.Length, attributes);
        LogicalDevice.SetPrimitiveTopologyEXT(commandBuffer, PrimitiveTopology);
        LogicalDevice.SetPrimitiveRestartEnableEXT(commandBuffer, PrimitiveRestartEnable);
        LogicalDevice.SetRasterizationSamplesEXT(commandBuffer, (SampleCountFlagBits)Presenter.Description.MSAALevel);
        LogicalDevice.SetSampleMaskEXT(commandBuffer, (SampleCountFlagBits)Presenter.Description.MSAALevel, SampleMask);
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
            
        var commandBuffer = commandBuffers[ImageIndex];
        SetDrawingState(commandBuffer);

        commandBuffer.Draw((uint)vertexCount, instanceCount, firstVertex, firstInstance);
    }

    public void DrawIndexed(IBuffer vertexBuffer, IBuffer indexBuffer, uint instanceCount = 1)
    {
        ulong offset = 0;
        var commandBuffer = commandBuffers[ImageIndex];
            
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

    public bool ResizePresenter(uint width = 1, uint height = 1)
    {
        if (Presenter == null) return false;
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
        var resizeResult = resizeFunc();
        if (!resizeResult)
        {
            return false;
        }
        OnSurfaceSizeChanged();
        return true;
    }

    public void Present()
    {
        if (!CanPresent)
        {
            UpdateCurrentFrameNumber();
            //Console.WriteLine("Cannot call Present() because BeginDraw() was not called");
            return;
        }

        LastPresenterState = Presenter.Present();
            
        UpdateCurrentFrameNumber();
    }

    public async Task TakeScreenshotAsync(String fileName, ImageFileType fileType)
    {
        if (Presenter == null) return;
        
        await Presenter?.TakeScreenshotAsync(fileName, fileType);
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
            Presenter?.Dispose();
            Presenter = null;
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

    public static IGraphicsDevice Create(MainGraphicsDevice mainDevice)
    {
        return new GraphicsDevice(mainDevice);
    }

    internal static GraphicsDevice Create(MainGraphicsDevice device, PresentationParameters parameters)
    {
        return new(device, parameters);
    }
}