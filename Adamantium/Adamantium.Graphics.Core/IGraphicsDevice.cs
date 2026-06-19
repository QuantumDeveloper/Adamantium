using System;
using Adamantium.Core;
using Adamantium.EffectsCompiler;
using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Graphics.Core.Presentation;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.Vulkan.Core;
using Adamantium.Vulkan.Core.Interop;
using Buffer = Adamantium.Vulkan.Core.Buffer;
using EffectTechnique = Adamantium.Graphics.Core.EffectsFramework.EffectTechnique;
using Image = Adamantium.Vulkan.Core.Image;

namespace Adamantium.Graphics.Core;

public interface IDrawableDevice
{
    bool BeginDraw(float depth = 1.0f, uint stencil = 0, Action<CommandBuffer> beforeRenderPass = null);

    void BeginRendering(CommandBuffer commandBuffer, bool continueRendering = false, float depth = 1.0f, uint stencil = 0);
    
    void EndDraw();

    void Submit();

    void FrameEnded();

    /// <summary>Blocks until the device has completed all pending work (e.g. before reading a target back).</summary>
    Result DeviceWaitIdle();

    void Draw(ulong vertexCount, uint instanceCount, uint firstVertex = 0, uint firstInstance = 0);
    
    void DrawIndexed(IBuffer vertexBuffer, IBuffer indexBuffer, uint instanceCount = 1);
}

public interface IDestroyableDevice
{
    void Destroy(DescriptorSetLayout layout);
    void Destroy(PipelineLayout layout);
    void Destroy(Sampler sampler);
    void Destroy(Buffer buffer);
    void Destroy(DeviceMemory deviceMemory);
    void Destroy(Image image);
    void Destroy(ImageView imageView);
    void Destroy(SwapchainKHR swapchain);
    void Destroy(Semaphore semaphore);
}

public interface IDynamicStateDevice
{
    bool DepthTestEnabled { get; set; }
    
    bool PrimitiveRestartEnable { get; set; }
    
    bool ColorBlendEnabled { get; set; }
    
    ColorComponentFlagBits ColorComponentFlags { get; set; }
    
    MSAALevel MSAALevel { get; set; }
        
    bool AlphaToCoverageEnable { get; set; }
    
    PolygonMode PolygonMode { get; set; }
    
    CullModeFlagBits CullMode { get; set; }
    
    bool IsWireFrame { get; set; }
        
    VkSampleMask[] SampleMask { get; set; }

    Single LineWidth { get; set; }

    FrontFace FrontFace { get; set; }
    
    ColorBlendEquationEXT ColorBlendEquation { get; set; }
    
    bool DepthWriteEnable { get; set; }

    CompareOp DepthCompareFunction { get; set; }

    bool DepthBoundsTestEnabled { get; set; }
        
    bool DepthBiasEnabled { get; set; }
        
    bool StencilTestEnabled { get; set; }
        
    bool LogicOperationsEnabled { get; set; }
        
    LogicOp LogicOperation { get; set; }
    
    public void SetViewports(params Viewport[] viewports);
    
    public void SetScissors(params Rect2D[] scissors);

    void SetRenderTargets(params IRenderTarget[] renderTargets);

    void SetDepthBuffer(IDepthStencilBuffer depthBuffer);
    
    void SetVertexBuffer(IBuffer vertexBuffer);

    void SetIndexBuffer(IBuffer indexBuffer);

    void SetVertexBuffers(params IBuffer[] vertexBuffers);
    
    public SamplerStateCollection SamplerStates { get; }
    
    Queue GraphicsQueue { get; }
    
    IRenderTarget CurrentRenderTarget { get; }
    
    IDepthStencilBuffer CurrentDepthStencilBuffer { get; }
    
    UInt32 CurrentFrame { get; }
}

public unsafe interface IGraphicsDevice : IDrawableDevice, IDynamicStateDevice, IDestroyableDevice, IDisposable
{
    Guid DeviceId { get; }
    
    uint MaxFramesInFlight { get; }
    
    void AddResource(GraphicsResource resource);

    /// <summary>Unregisters a resource (called from its Dispose) so it stops being rooted by the device.</summary>
    void RemoveResource(GraphicsResource resource);
    
    void AddToDeferDisposeQueue(IDisposable obj);
    
    IEffectResourceLinker CreateEffectResourceLinker();
    
    IEffectPass CreateEffectPass(Logger logger, Effect effect, EffectTechnique technique, EffectData.Pass pass, string name);

    void BindShader(CommandBuffer cmd, ShaderStageFlagBits stage, ShaderEXT shader);

    DescriptorSetLayout CreateDescriptorSetLayout(DescriptorSetLayoutCreateInfo layoutCreateInfo);
    
    CommandBuffer CurrentCommandBuffer { get; }

    /// <summary>Register a semaphore the next <see cref="Submit"/> must wait on (timelineValue=0 for binary).</summary>
    void AddWaitSemaphore(Semaphore semaphore, PipelineStageFlagBits stage, ulong timelineValue = 0);

    /// <summary>Register a semaphore the next <see cref="Submit"/> must signal (timelineValue=0 for binary).</summary>
    void AddSignalSemaphore(Semaphore semaphore, ulong timelineValue = 0);

    EffectPool DefaultEffectPool { get; }

    nuint MapMemory(DeviceMemory memory, ulong offset, ulong size, MemoryMapFlagBits flags);

    public void UnmapMemory(DeviceMemory memory);
    
    public IEffectPass CurrentEffectPass { get; set; }

    PipelineLayout CreatePipelineLayout(PipelineLayoutCreateInfo createInfo);

    uint GetDescriptorSetLayoutOffset(DescriptorSetLayout layout, uint bindingSlot);

    ShaderEXT CreateShader(ShaderCreateInfoEXT shaderCreateInfo);

    void DestroyShader(ShaderEXT shaderObject);

    CommandBuffer BeginSingleTimeCommand();

    void EndSingleTimeCommand(CommandBuffer commandBuffer);

    void AddEffectPool(EffectPool pool);
    
    void RemoveEffectPool(EffectPool pool);
    
    void BindDescriptorBuffers(CommandBuffer commandBuffer, params DescriptorBufferBindingInfoEXT[] bindings);
    
    // void SetDescriptorBufferOffsets(CommandBuffer commandBuffer, PipelineBindPoint pipelineBindPoint, PipelineLayout layout, uint dataSet, uint setCount, uint[] bufferIndices, ulong[] offsets);
    
    uint GetDescriptorSetLayoutSize(DescriptorSetLayout layout);
    
    ulong UniformBufferDescriptorSize { get; }
    
    ulong SamplerDescriptorSize { get; }
    
    ulong SampledImageDescriptorSize { get; }
    
    uint DescriptorBufferOffsetAlignment { get; }

    void GetDescriptor(DescriptorGetInfoEXT descriptorGetInfoExt, uint descriptorSize, nuint descriptorPtr);
    
    Color ClearColor { get; set; }
    
    Device LogicalDevice { get; }
    
    GraphicsAdapter Adapter { get; }
    
    GraphicsPresenter Presenter { get; set; }
    
    MainGraphicsDevice MainDevice { get; }

    Fence GetCurrentFence();

    Semaphore GetRenderFinishedSemaphore();

    PrimitiveTopology PrimitiveTopology { get; set; }
    
    Type VertexType { get; set; }

    IDepthStencilBuffer CreateDepthBuffer(uint width, 
        uint height, 
        DepthFormat format, 
        MSAALevel msaa,
        ImageAspectFlagBits imageAspect = ImageAspectFlagBits.DepthBit,
        string name = "");
    
    IRenderTarget CreateRenderTarget(UInt32 width, 
        UInt32 height, 
        MSAALevel msaa, 
        SurfaceFormat format, 
        ImageUsageFlagBits usage = ImageUsageFlagBits.TransferSrcBit,
        ImageLayout desiredLayout = ImageLayout.ColorAttachmentOptimal,
        string name = "");

    ITexture CreateTextureFromImage(Image image, 
        UInt32 width, 
        UInt32 height, 
        MSAALevel msaa, 
        SurfaceFormat format, 
        ImageUsageFlagBits usage = ImageUsageFlagBits.TransferSrcBit,
        ImageLayout desiredLayout = ImageLayout.ColorAttachmentOptimal,
        string name = "");

    ITexture CreateTexture(TextureDescription description, byte[] pixelData);

    /// <summary>Imports an externally produced shared surface zero-copy and returns it as a sampleable texture.
    /// The producer hands off the <paramref name="descriptor"/> after exporting its memory/semaphores.</summary>
    ITexture ImportSharedSurface(SharedSurfaceDescriptor descriptor);

    SurfaceKHR GetOrCreateSurface(PresentationParameters parameters);

    void InsertImageMemoryBarrier(CommandBuffer commandBuffer,
        ITexture texture,
        AccessFlagBits sourceAccessMask,
        AccessFlagBits destinationAccessMask,
        ImageLayout oldLayout,
        ImageLayout newLayout,
        PipelineStageFlagBits sourceStageMask,
        PipelineStageFlagBits destinationStageMask);

    void SetObjectDebugName(ulong objectHandle, ObjectType objectType, string name);
}