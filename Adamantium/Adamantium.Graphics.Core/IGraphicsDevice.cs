using System;
using System.Threading.Tasks;
using Adamantium.Core;
using Adamantium.EffectsCompiler;
using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Graphics.Core.Presentation;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using AdamantiumVulkan.Core;
using Buffer = AdamantiumVulkan.Core.Buffer;
using EffectTechnique = Adamantium.Graphics.Core.EffectsFramework.EffectTechnique;
using Image = AdamantiumVulkan.Core.Image;

namespace Adamantium.Graphics.Core;

public interface IDrawableDevice
{
    bool BeginDraw(float depth = 1.0f, uint stencil = 0);
    void EndDraw();

    void Submit();

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
}

public interface IDynamicStateDevice
{
    bool DepthTestEnabled { get; set; }
    
    bool PrimitiveRestartEnable { get; set; }
    
    bool ColorBlendEnabled { get; set; }
    
    PolygonMode PolygonMode { get; set; }
    
    CullModeFlagBits CullMode { get; set; }
    
    ColorBlendEquationEXT ColorBlendEquation { get; set; }
    
    public void SetViewports(params Viewport[] viewports);
    
    public void SetScissors(params Rect2D[] scissors);

    void SetRenderTarget(IRenderTarget renderTarget);

    void SetDepthBuffer(IDepthStencilBuffer depthBuffer);
    
    void SetVertexBuffer(IBuffer vertexBuffer);

    void SetIndexBuffer(IBuffer indexBuffer);

    void SetVertexBuffers(params IBuffer[] vertexBuffers);
    
    public SamplerStateCollection SamplerStates { get; }
}

public unsafe interface IGraphicsDevice : IDrawableDevice, IDynamicStateDevice, IDestroyableDevice, IDisposable
{
    Guid DeviceId { get; }
    
    void AddResource(GraphicsResource resource);
    
    IEffectResourceLinker CreateEffectResourceLinker();
    
    IEffectPass CreateEffectPass(Logger logger, Effect effect, EffectTechnique technique, EffectData.Pass pass, string name);

    void BindShader(CommandBuffer cmd, ShaderStageFlagBits stage, ShaderEXT shader);

    DescriptorSetLayout CreateDescriptorSetLayout(DescriptorSetLayoutCreateInfo layoutCreateInfo);
    
    CommandBuffer CurrentCommandBuffer { get; }
    
    EffectPool DefaultEffectPool { get; }

    unsafe void* MapMemory(DeviceMemory memory, ulong offset, ulong size, uint flags);

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
    
    void SetDescriptorBufferOffsets(CommandBuffer commandBuffer, PipelineBindPoint pipelineBindPoint, PipelineLayout layout, uint dataSet, uint setCount, uint[] bufferIndices, ulong[] offsets);
    
    uint GetDescriptorSetLayoutSize(DescriptorSetLayout layout);
    
    ulong UniformBufferDescriptorSize { get; }
    
    ulong SamplerDescriptorSize { get; }
    
    ulong SampledImageDescriptorSize { get; }

    uint AlignSize(uint size, uint alignment);
    
    uint DescriptorBufferOffsetAlignment { get; }

    void GetDescriptor(DescriptorGetInfoEXT descriptorGetInfoExt, uint descriptorSize, void* descriptorPtr);
    
    Color ClearColor { get; set; }
    
    Device LogicalDevice { get; }
    
    GraphicsAdapter Adapter { get; }
    
    MainGraphicsDevice MainDevice { get; }
    
    GraphicsPresenter Presenter { get;  }

    PresentInfoKHR FillPresentInfo(SwapchainKHR[] swapchains);

    void Present();

    bool ResizePresenter(uint width, uint height);

    bool ResizePresenter(PresentationParameters parameters);
    
    PrimitiveTopology PrimitiveTopology { get; set; }
    
    Type VertexType { get; set; }

    Task TakeScreenshotAsync(string path, ImageFileType fileType);

    IDepthStencilBuffer CreateDepthBuffer(uint width, 
        uint height, 
        DepthFormat format, 
        MSAALevel msaa,
        ImageAspectFlagBits imageAspect = ImageAspectFlagBits.DepthBit);
    
    IRenderTarget CreateRenderTarget(UInt32 width, 
        UInt32 height, 
        MSAALevel msaa, 
        SurfaceFormat format, 
        ImageUsageFlagBits usage = ImageUsageFlagBits.TransferSrcBit,
        ImageLayout desiredLayout = ImageLayout.ColorAttachmentOptimal);
    
    SurfaceKHR GetOrCreateSurface(PresentationParameters parameters);

    void InsertImageMemoryBarrier(CommandBuffer commandBuffer,
        Image image,
        AccessFlagBits sourceAccessMask,
        AccessFlagBits destinationAccessMask,
        ImageLayout oldLayout,
        ImageLayout newLayout,
        PipelineStageFlagBits sourceStageMask,
        PipelineStageFlagBits destinationStageMask,
        ImageSubresourceRange subresourceRange);
}