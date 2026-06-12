using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using Adamantium.Core;
using Adamantium.EffectsCompiler;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.EffectsFramework;
using AdamantiumVulkan.Core;
using AdamantiumVulkan.Core.Interop;
using Serilog;
using EffectTechnique = Adamantium.Graphics.Core.EffectsFramework.EffectTechnique;

namespace Adamantium.Graphics.Effects;

/// <summary>
/// Contains rendering state for drawing with an effect; updates constant buffers and directly sets all needed resources for each shader stage/>
/// </summary>
public sealed class EffectPass : DisposableObject, IEffectPass
{
    private const uint MaxItemsPerBuffer = 4096;

    /// <summary>
    /// Resource-binding mechanism switch (engine-wide, set at startup).
    /// <c>false</c> = VK_EXT_descriptor_buffer (the working path on current hardware);
    /// <c>true</c>  = VK_EXT_descriptor_heap (implementation is correct, but on Turing the screen is black due to an NVIDIA driver bug).
    /// </summary>
    public static bool UseDescriptorHeap = false;

    /// <summary>
    ///   Gets the attributes associated with this pass.
    /// </summary>
    /// <value> The attributes. </value>
    public readonly PropertyKeyCollection PropertiesKey;

    /// <summary>
    /// The parent effect of this pass.
    /// </summary>
    public readonly Effect Effect;

    private readonly EffectData.Pass pass;
    private readonly IGraphicsDevice graphicsDevice;

    private readonly List<StageBlock> pipelineStages;

    internal EffectTechnique Technique;

    private List<PipelineShaderStageCreateInfo> shaderStages;

    private List<DescriptorSetLayoutBinding> layoutBindings;

    private DescriptorSetLayout descriptorSetLayout;

    private DescriptorSetLayout[] descriptorSetLayouts;

    // Reused across draws so BindDescriptors doesn't allocate a fresh array (+ per-element objects) each draw.
    private DescriptorBufferBindingInfoEXT[] bindingInfos;

    private readonly VkDeviceSize[] offsets = new VkDeviceSize[1];
    private readonly uint[] _bufferIndices = new uint[1];

    private readonly List<StageBlock> stages = new List<StageBlock>();

    private DescriptorHeapManager _descriptorHeapManager;

    private uint appliesCounter = 0;
    private bool geometryStagePresent = false;
    public ReadOnlyCollection<PipelineShaderStageCreateInfo> ShaderStages => shaderStages.AsReadOnly();

    /// <summary>
    ///   Initializes a new instance of the <see cref="EffectPass" /> class.
    /// </summary>
    /// <param name="logger">The logger used to log errors.</param>
    /// <param name="effect"> The effect. </param>
    /// <param name="technique">The technique. </param>
    /// <param name="pass"> The pass. </param>
    /// <param name="name"> The name. </param>
    internal EffectPass(Logger logger, Effect effect, EffectTechnique technique, EffectData.Pass pass, string name)
        : base(name)
    {
        Technique = technique;
        this.pass = pass;
        Effect = effect;
        graphicsDevice = effect.GraphicsDevice;
        pipelineStages = new List<StageBlock>();
        _descriptorHeapManager = ((EffectResourceLinker)Effect.ResourceLinker).DescriptorHeapManager;

        shaderStages = new List<PipelineShaderStageCreateInfo>();
        layoutBindings = new List<DescriptorSetLayoutBinding>();
        PropertiesKey = PrepareProperties(logger, pass.Properties);
        graphicsDevice.MainDevice.FrameFinished += GraphicsDeviceOnFrameFinished;
    }

    private void GraphicsDeviceOnFrameFinished()
    {
        appliesCounter = 0;
        _descriptorHeapManager.CurrentBufferPool.Reset();
    }

    private void ClearLayoutBindings()
    {
        foreach (var binding in layoutBindings)
        {
            //binding?.Dispose();
        }

        layoutBindings.Clear();
    }

    public PipelineLayout PipelineLayout { get; private set; }

    /// <summary>
    ///   Applies this pass to the device pipeline.
    /// </summary>
    /// <remarks>
    ///   This method is responsible to:
    ///   <ul>
    ///     <li>Setup the shader on each stage.</li>
    ///     <li>Upload constant buffers with dirty flag</li>
    ///     <li>Set all input constant buffers, shader resource view, unordered access views and sampler states to the stage.</li>
    ///   </ul>
    /// </remarks>
    public void Apply()
    {
       ApplyInternal();
    }

    private void ApplyInternal()
    {
        if (UseDescriptorHeap)
            ApplyHeap();
        else
            ApplyBuffer();
    }

    // === VK_EXT_descriptor_heap: CB via push-address; textures/samplers — indices into heaps ===
    private unsafe void ApplyHeap()
    {
        Effect.CurrentTechnique = Technique;
        graphicsDevice.CurrentEffectPass = this;
        stages.Clear();

        var resourceLinker = (EffectResourceLinker)Effect.ResourceLinker;

        // Allocate a stack buffer for the full push-data size of this pass
        byte* pushDataBytes = stackalloc byte[(int)totalPushDataSize];

        // Shared per-frame pool: sub-allocates CB data (a per-draw chunk), without a separate buffer per Apply.
        var dynamicPool = _descriptorHeapManager.CurrentBufferPool;

        for (int stageIndex = 0; stageIndex < pipelineStages.Count; stageIndex++)
        {
            var stageBlock = pipelineStages[stageIndex];
            if (stageBlock == null || stageBlock.Index < 0) continue;

            // 1. CONSTANT BUFFERS (PushAddress):
            // sub-allocate a chunk in the pool, copy the CB data, and put its GPU address into push data.
            for (int i = 0; i < stageBlock.ConstantBufferLinks.Count; ++i)
            {
                var link = stageBlock.ConstantBufferLinks[i];

                ulong alignment = graphicsDevice.Adapter.AdapterProperties.Limits.MinUniformBufferOffsetAlignment;
                var (pageBuffer, bufferOffset) = dynamicPool.Allocate(link.ConstantBuffer.BackingBuffer.Size, alignment);

                pageBuffer.CopyFrom(link.ConstantBuffer.BackingBuffer, bufferOffset);

                uint pushOffset = parameterPushOffsets[link.Parameter];
                *(ulong*)(pushDataBytes + pushOffset) = pageBuffer.GetDeviceAddress() + bufferOffset;
            }

            // 2. TEXTURES (ShaderResourceView)
            var localLinks = stageBlock.ShaderResourceViewSlotLinks;
            for (int i = 0; i < localLinks.Count; ++i)
            {
                var links = localLinks[i];
                var resources = resourceLinker.GetShaderResources(links.ResourceParamDescription);
                var effectParam = Effect.Parameters[links.ResourceParamDescription.Name];
                uint basePushOffset = parameterPushOffsets[effectParam];

                for (int resIdx = 0; resIdx < resources.Length; resIdx++)
                {
                    // Take the ready GlobalHeapOffset from ResourceInfo, written there by the linker
                    uint heapOffset = resources[resIdx]?.GlobalHeapOffset ?? uint.MaxValue;
                    *(uint*)(pushDataBytes + basePushOffset + (uint)(resIdx * 4)) = heapOffset;
                }
            }

            // 3. SAMPLERS (SamplerState)
            localLinks = stageBlock.SamplerStateSlotLinks;
            for (int i = 0; i < localLinks.Count; ++i)
            {
                var links = localLinks[i];
                var resources = resourceLinker.GetSamplers(links.ResourceParamDescription);
                var effectParam = Effect.Parameters[links.ResourceParamDescription.Name];
                uint basePushOffset = parameterPushOffsets[effectParam];

                for (int resIdx = 0; resIdx < resources.Length; resIdx++)
                {
                    uint heapOffset = resources[resIdx]?.GlobalHeapOffset ?? uint.MaxValue;
                    *(uint*)(pushDataBytes + basePushOffset + (uint)(resIdx * 4)) = heapOffset;
                }
            }

            // 4. UAV (UnorderedAccessView)
            localLinks = stageBlock.UnorderedAccessViewSlotLinks;
            for (int i = 0; i < localLinks.Count; ++i)
            {
                var links = localLinks[i];
                var resources = resourceLinker.GetUAVs(links.ResourceParamDescription);
                var effectParam = Effect.Parameters[links.ResourceParamDescription.Name];
                uint basePushOffset = parameterPushOffsets[effectParam];

                for (int resIdx = 0; resIdx < resources.Length; resIdx++)
                {
                    uint heapOffset = resources[resIdx]?.GlobalHeapOffset ?? uint.MaxValue;
                    *(uint*)(pushDataBytes + basePushOffset + (uint)(resIdx * 4)) = heapOffset;
                }
            }

            stages.Add(stageBlock);

        }

        // 5. BIND SHADERS
        foreach (var stage in stages)
        {
            graphicsDevice.BindShader(graphicsDevice.CurrentCommandBuffer, stage.Stage, stage.ShaderObject);
        }

        if (!geometryStagePresent)
        {
            graphicsDevice.BindShader(graphicsDevice.CurrentCommandBuffer, ShaderStageFlagBits.GeometryBit, null);
        }

        // 6. SEND PUSH DATA — strictly AFTER binding shaders (in the reference, push data goes
        // after bind pipeline). Otherwise binding a shader resets push data before the draw.
        if (totalPushDataSize > 0)
        {
            var hostAddressRange = new HostAddressRangeConstEXT
            {
                Address = (nuint)pushDataBytes,
                Size = totalPushDataSize
            };

            var pushDataInfo = new PushDataInfoEXT
            {
                Offset = 0,
                Data = hostAddressRange
            };

            graphicsDevice.CurrentCommandBuffer.PushDataEXT(pushDataInfo);
        }

        appliesCounter++;
    }

    // === VK_EXT_descriptor_buffer: CB/texture/sampler descriptors are written into descriptor buffers ===
    private void ApplyBuffer()
    {
        Effect.CurrentTechnique = Technique;
        graphicsDevice.CurrentEffectPass = this;
        stages.Clear();

        var resourceLinker = (EffectResourceLinker)Effect.ResourceLinker;
        var dynamicPool = _descriptorHeapManager.CurrentBufferPool;
        ulong uboAlignment = graphicsDevice.Adapter.AdapterProperties.Limits.MinUniformBufferOffsetAlignment;

        for (int stageIndex = 0; stageIndex < pipelineStages.Count; stageIndex++)
        {
            var stageBlock = pipelineStages[stageIndex];
            if (stageBlock == null || stageBlock.Index < 0) continue;

            // Constant buffers: sub-allocate data from the shared pool and write a uniform descriptor.
            foreach (var link in stageBlock.ConstantBufferLinks)
            {
                var (page, offset) = dynamicPool.Allocate(link.ConstantBuffer.BackingBuffer.Size, uboAlignment);
                page.CopyFrom(link.ConstantBuffer.BackingBuffer, offset);

                CreateUniformBufferDescriptor(
                    page, offset, link.ConstantBuffer.BackingBuffer.Size,
                    link.ConstantBuffer.Description.Slot,
                    link.ConstantBuffer.Description.DescriptorSet);
            }

            // Samplers
            foreach (var links in stageBlock.SamplerStateSlotLinks)
            {
                var resources = resourceLinker.GetSamplers(links.ResourceParamDescription);
                CreateSamplerDescriptor(resources, links.SlotIndex, links.DescriptorSet);
            }

            // Textures (ShaderResourceView)
            foreach (var links in stageBlock.ShaderResourceViewSlotLinks)
            {
                var resources = resourceLinker.GetShaderResources(links.ResourceParamDescription);
                CreateImageViewDescriptor(resources, links.SlotIndex, links.DescriptorSet);
            }

            // UAV
            foreach (var links in stageBlock.UnorderedAccessViewSlotLinks)
            {
                var resources = resourceLinker.GetUAVs(links.ResourceParamDescription);
                CreateUAVWriteDescriptor(resources, links.SlotIndex, links.DescriptorSet);
            }

            stages.Add(stageBlock);
        }

        BindDescriptors();

        foreach (var stage in stages)
            graphicsDevice.BindShader(graphicsDevice.CurrentCommandBuffer, stage.Stage, stage.ShaderObject);

        if (!geometryStagePresent)
            graphicsDevice.BindShader(graphicsDevice.CurrentCommandBuffer, ShaderStageFlagBits.GeometryBit, null);

        appliesCounter++;
    }

    

    private void BindDescriptors()
    {
        if (bindingInfos == null || bindingInfos.Length != DescriptorDataSets.Length)
        {
            bindingInfos = new DescriptorBufferBindingInfoEXT[DescriptorDataSets.Length];
            for (int i = 0; i < bindingInfos.Length; i++)
                bindingInfos[i] = new DescriptorBufferBindingInfoEXT();
        }

        for (int i = 0; i < DescriptorDataSets.Length; i++)
        {
            var info = bindingInfos[i];
            info.Address = DescriptorDataSets[i].Buffer.GetDeviceAddress();
            info.Usage = (BufferUsageFlagBits)DescriptorDataSets[i].UsageFlags;
        }

        graphicsDevice.CurrentCommandBuffer.BindDescriptorBuffersEXT((uint)bindingInfos.Length, bindingInfos);

        for (int i = 0; i < DescriptorDataSets.Length; ++i)
        {
            offsets[0] = appliesCounter * DescriptorDataSets[0].Size;
            graphicsDevice.CurrentCommandBuffer.SetDescriptorBufferOffsetsEXT(
                PipelineBindPoint.Graphics,
                PipelineLayout,
                (uint)i,
                1,
                _bufferIndices,
                offsets);
        }
    }

    /// <summary>
    /// Un-Applies this pass to the device pipeline by unbinding all resources/views previously bound by this pass. This is not mandatory to call this method, unless you want to explicitly unbind
    /// resource views that were bound by this pass.
    /// </summary>
    /// <param name="fullUnApply">if set to <c>true</c> this will unbind all resources; otherwise <c>false</c> will unbind only ShaderResourceView and UnorderedAccessView. Default is false.</param>
    public void UnApply(bool fullUnApply = false)
    {
    }

    /// <summary>
    /// Initializes this pass.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <exception cref="System.InvalidOperationException"></exception>
    public void Initialize(Logger logger)
    {
        foreach (var link in pass.Pipeline)
        {
            if (link.IsImport)
            {
                throw new InvalidOperationException(
                    $"Unable to resolve imported shader [{link.ImportName}] for stage [{link.ShaderType}]");
            }

            var stageBlock = new StageBlock(graphicsDevice, link.ShaderType);
            pipelineStages.Add(stageBlock);

            stageBlock.Index = link.Index;
            stageBlock.EntryPoint = link.EntryPoint;

            InitStageBlock(stageBlock, logger);

            if (stageBlock.Stage == ShaderStageFlagBits.GeometryBit)
            {
                geometryStagePresent = true;
            }
        }
    }

    public void PrepareData()
    {
        ComputeSlotLinks();
        if (UseDescriptorHeap)
            ComputePushDataLayout();   // creates shaders with heap mappings
        else
            PrepareDescriptorSets();   // creates set layouts + descriptor buffers + shaders
    }

    private readonly Dictionary<EffectParameter, uint> parameterPushOffsets = new();
    private uint totalPushDataSize = 0;

    private void ComputePushDataLayout()
    {
        parameterPushOffsets.Clear();
        uint currentOffset = 0;

        // Constant buffers (PushAddress): reserve 8 bytes for each unique CB to hold its
        // 64-bit GPU address. ApplyInternal sub-allocates CB data from the shared pool and writes the address here.
        for (int stageIndex = 0; stageIndex < pipelineStages.Count; stageIndex++)
        {
            var stageBlock = pipelineStages[stageIndex];
            if (stageBlock == null || stageBlock.Index < 0) continue;

            foreach (var link in stageBlock.ConstantBufferLinks)
            {
                if (!parameterPushOffsets.ContainsKey(link.Parameter))
                {
                    currentOffset = (currentOffset + 7) & ~7U;
                    parameterPushOffsets[link.Parameter] = currentOffset;
                    currentOffset += 8;
                }
            }
        }

        for (int stageIndex = 0; stageIndex < pipelineStages.Count; stageIndex++)
        {
            var stageBlock = pipelineStages[stageIndex];
            if (stageBlock == null || stageBlock.Index < 0) continue;

            if (stageBlock.Parameters != null)
            {
                foreach (var param in stageBlock.Parameters)
                {
                    if (param.Parameter.ResourceType == EffectResourceType.ConstantBuffer) 
                        continue;
                    
                    if (!parameterPushOffsets.ContainsKey(param.Parameter))
                    {
                        currentOffset = (currentOffset + 3) & ~3U;
                        parameterPushOffsets[param.Parameter] = currentOffset;
                        currentOffset += param.Parameter.ElementCount * 4; 
                    }
                }
            }
        }

        totalPushDataSize = currentOffset;
        
        CreateShaderObjects();
    }

    private void PrepareDescriptorSets()
    {
        var descriptorSets = new List<uint>();
        for (var index = 0; index < pipelineStages.Count; index++)
        {
            var stage = pipelineStages[index];

            if (stage?.Parameters != null)
            {
                descriptorSets.AddRange(stage.Parameters.Select(x => x.DescriptorSet));
            }
        }

        descriptorSets = descriptorSets.Distinct().Order().ToList();

        DescriptorDataSets = new DescriptorData[descriptorSets.Count];
        for (int i = 0; i < descriptorSets.Count; i++)
        {
            DescriptorDataSets[i] = new DescriptorData(descriptorSets[i]);
        }

        CreateDescriptorSetLayout(0, layoutBindings);
        CreatePipelineLayout();
        CreateShaderObjects();
    }

    private void CreateShaderObjects()
    {
        for (int i = 0; i < pipelineStages.Count; ++i)
        {
            var stage = pipelineStages[i];
            stage.Layouts = descriptorSetLayouts;
            if (i > 0)
            {
                pipelineStages[i - 1].NextStage = stage.Stage;
            }
        }

        pipelineStages.ForEach(stage => stage.CreateShader(parameterPushOffsets));
    }

    private DescriptorData[] DescriptorDataSets;

    /// <summary>
    /// Initializes the stage block.
    /// </summary>
    /// <param name="stageBlock">The stage block.</param>
    /// <param name="logger">The logger.</param>
    private void InitStageBlock(StageBlock stageBlock, Logger logger)
    {
        // If null shader, then skip init
        var shaderIndex = stageBlock.Index;
        if (shaderIndex < 0)
        {
            return;
        }

        // stageBlock.ShaderModule = Effect.Pool.GetOrCompileShader(
        //     stageBlock.Type,
        //     shaderIndex,
        //     out var errorProfile);

        stageBlock.ByteCode = Effect.Pool.GetShaderBytecode(stageBlock.Type, shaderIndex);

        // if (stageBlock.ShaderModule == null)
        // {
        //     logger.Error(
        //         "Unsupported shader profile [{0} / {1}] on current GraphicsDevice in (effect [{2}] Technique [{3}] Pass: [{4}])",
        //         stageBlock.Type,
        //         errorProfile,
        //         Effect.Name,
        //         Technique.Name,
        //         Name);
        //     return;
        // }

        var shaderStageInfo = new PipelineShaderStageCreateInfo();
        shaderStageInfo.Stage = EffectShaderTypeToShaderStage(stageBlock.Type);
        shaderStageInfo.Module = stageBlock.ShaderModule;
        shaderStageInfo.PName = stageBlock.EntryPoint;

        shaderStages.Add(shaderStageInfo);

        var shaderRaw = Effect.Pool.RegisteredShaders[shaderIndex];

        for (int i = 0; i < shaderRaw.ConstantBuffers.Count; i++)
        {
            var constantBufferRaw = shaderRaw.ConstantBuffers[i];

            // Constant buffers with a null size are skipped
            if (constantBufferRaw.Size == 0)
                continue;

            var constantBuffer = Effect.GetOrCreateConstantBuffer(constantBufferRaw);
            // IF constant buffer is null, it means that there is a conflict
            if (constantBuffer == null)
            {
                logger.Error(
                    "Constant buffer [{0}] cannot have multiple size or different content declaration inside the same effect pool",
                    constantBufferRaw.Name);
                continue;
            }

            CreateAndAddLayoutBinding(constantBuffer.Description.DescriptorSet, constantBuffer.Description.Slot, 1U,
                DescriptorType.UniformBuffer, EffectShaderTypeToShaderStage(stageBlock.Type));

            // Test if this constant buffer is not already part of the effect
            if (Effect.ConstantBuffers[constantBufferRaw.Name] == null)
            {
                // Add the declared constant buffer to the effect shader.
                Effect.ConstantBuffers.Add(constantBuffer);

                // Declare all parameter from constant buffer at the effect level.
                foreach (var parameter in constantBuffer.Parameters)
                {
                    var previousParameter = Effect.Parameters[parameter.Name];
                    if (previousParameter == null)
                    {
                        // Add an effect parameter linked to the appropriate constant buffer at the effect level.
                        Effect.Parameters.Add(
                            new EffectParameter(
                                (EffectData.ValueTypeParameter)parameter.ParameterDescription,
                                constantBuffer));
                    }
                    else if (parameter.ParameterDescription != previousParameter.ParameterDescription ||
                             parameter.Buffer != previousParameter.Buffer)
                    {
                        // If registered parameters is different
                        logger.Error(
                            "Parameter [{0}] defined in Constant buffer [{0}] is already defined by another constant buffer with the definition [{2}]",
                            parameter,
                            constantBuffer.Name,
                            previousParameter);
                    }
                }
            }
        }

        stageBlock.ConstantBufferLinks.Clear();

        // Declare all resource parameters at the effect level.
        foreach (var parameterRaw in shaderRaw.ResourceParameters)
        {
            EffectParameter parameter;
            var previousParameter = Effect.Parameters[parameterRaw.Name];

            // Skip empty constant buffers.
            if (parameterRaw.Type == EffectParameterType.ConstantBuffer &&
                Effect.ConstantBuffers[parameterRaw.Name] == null)
            {
                continue;
            }

            if (previousParameter == null)
            {
                var paramType = EffectResourceTypeHelper.ConvertFromParameterType(parameterRaw.Type);
                parameter = new EffectParameter(
                    parameterRaw,
                    paramType,
                    Effect.ResourceLinker.Count,
                    Effect.ResourceLinker);

                Effect.Parameters.Add(parameter);

                Effect.ResourceLinker.Count += parameterRaw.Count;
            }
            else
            {
                if (CompareResourceParameter(
                        parameterRaw,
                        (EffectData.ResourceParameter)previousParameter.ParameterDescription))
                {
                    // If registered parameters is different
                    logger.Error(
                        "Resource Parameter [{0}] is already defined with a different definition [{1}]",
                        parameterRaw,
                        previousParameter.ParameterDescription);
                }

                parameter = previousParameter;
            }

            CreateAndAddLayoutBinding(parameterRaw.DescriptorSet, parameterRaw.Slot, parameterRaw.Count,
                ConvertFromEffectParameterType(parameterRaw.Type),
                EffectShaderTypeToShaderStage(stageBlock.Type));

            // For constant buffers, we need to store explicit link
            if (parameter.ResourceType == EffectResourceType.ConstantBuffer)
            {
                stageBlock.ConstantBufferLinks.Add(new ConstantBufferLink(Effect.ConstantBuffers[parameter.Name], parameter));
            }

            stageBlock.Parameters ??= new List<ParameterBinding>(shaderRaw.ResourceParameters.Count);
            stageBlock.Parameters.Add(new ParameterBinding(parameter, parameterRaw.Slot, parameterRaw.DescriptorSet));
        }
    }

    private DescriptorSetLayoutBinding CreateAndAddLayoutBinding(uint descriptorSet, uint slot, uint descriptorCount,
        DescriptorType descriptorType, ShaderStageFlagBits stageFlags)
    {
        var binding = layoutBindings.FirstOrDefault(x => x.Binding == slot);

        if (binding != null)
        {
            binding.StageFlags |= stageFlags;
        }
        else
        {
            var resourceBinding = new DescriptorSetLayoutBinding();
            resourceBinding.Binding = slot;
            resourceBinding.DescriptorCount = descriptorCount;
            resourceBinding.DescriptorType = descriptorType;
            resourceBinding.StageFlags = stageFlags;

            layoutBindings.Add(resourceBinding);
            binding = resourceBinding;
        }

        return binding;
    }

    private void CreateDescriptorSetLayout(uint descriptorSet, List<DescriptorSetLayoutBinding> bindings)
    {
        InitDescriptorBuffer(descriptorSet, bindings);

        var layoutInfo = new DescriptorSetLayoutCreateInfo();
        layoutInfo.BindingCount = (uint)bindings.Count;
        layoutInfo.PBindings = bindings.ToArray();
        layoutInfo.Flags = DescriptorSetLayoutCreateFlagBits.DescriptorBufferBitExt;

        descriptorSetLayout = graphicsDevice.CreateDescriptorSetLayout(layoutInfo);
        descriptorSetLayouts = DescriptorDataSets.Select(x => x.Layout).ToArray();
    }

    private void InitDescriptorBuffer(uint descriptorSetIndex, List<DescriptorSetLayoutBinding> bindings)
    {
        if (bindings == null || bindings.Count == 0) return;

        var layoutInfo = new DescriptorSetLayoutCreateInfo();
        layoutInfo.BindingCount = (uint)bindings.Count;
        layoutInfo.PBindings = bindings.ToArray();
        layoutInfo.Flags = DescriptorSetLayoutCreateFlagBits.DescriptorBufferBitExt;

        var descriptorTypes = bindings.Select(x => x.DescriptorType).ToList();
        var descriptorData = DescriptorDataSets[descriptorSetIndex];
        descriptorData.DescriptorType = bindings[0].DescriptorType;
        descriptorData.Layout = graphicsDevice.CreateDescriptorSetLayout(layoutInfo);
        var size = graphicsDevice.GetDescriptorSetLayoutSize(descriptorData.Layout);
        descriptorData.UniformBufferDescriptorSize = (uint)graphicsDevice.UniformBufferDescriptorSize;
        descriptorData.SamplerDescriptorSize = (uint)graphicsDevice.SamplerDescriptorSize;
        descriptorData.ImageDescriptorSize = (uint)graphicsDevice.SampledImageDescriptorSize;

        descriptorData.Size = (uint)Utilities.AlignSize(size, graphicsDevice.DescriptorBufferOffsetAlignment);

        var bufferFlags = BufferUsageFlags.ResourceDescriptorBufferExt | BufferUsageFlags.ShaderDeviceAddress;
        if (descriptorTypes.Contains(DescriptorType.Sampler) || descriptorTypes.Contains(DescriptorType.SampledImage))
        {
            bufferFlags |= BufferUsageFlags.SamplerDescriptorBufferExt;
        }

        descriptorData.UsageFlags = bufferFlags;

        descriptorData.Buffer = ToDispose(
            Buffer.New((GraphicsDevice)graphicsDevice,
            descriptorData.Size * MaxItemsPerBuffer,
            bufferFlags,
            MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.DeviceLocal));
    }

    private void CreatePipelineLayout()
    {
        var pipelineLayoutInfo = new PipelineLayoutCreateInfo();
        pipelineLayoutInfo.SetLayoutCount = (uint)descriptorSetLayouts.Length;
        pipelineLayoutInfo.PSetLayouts = descriptorSetLayouts;

        PipelineLayout = graphicsDevice.CreatePipelineLayout(pipelineLayoutInfo);
    }

    private unsafe void CreateUniformBufferDescriptor(Buffer buffer, ulong bufferOffset, ulong range, uint bindingIndex, uint descriptorSet)
    {
        var addrInfo = new DescriptorAddressInfoEXT();
        addrInfo.Address = buffer.GetDeviceAddress() + bufferOffset;
        addrInfo.Range = range;
        addrInfo.Format = Format.UNDEFINED;

        var bufferDescriptorInfo = new DescriptorGetInfoEXT();
        bufferDescriptorInfo.Type = DescriptorType.UniformBuffer;
        bufferDescriptorInfo.Data = new DescriptorDataEXT();
        bufferDescriptorInfo.Data.PUniformBuffer = addrInfo;

        var descriptorData = DescriptorDataSets[descriptorSet];
        var dataPtr = descriptorData.Buffer.MapMemory();
        var offset = graphicsDevice.GetDescriptorSetLayoutOffset(descriptorData.Layout,
            bindingIndex);

        graphicsDevice.GetDescriptor(bufferDescriptorInfo, descriptorData.UniformBufferDescriptorSize,
            (nuint)((IntPtr)dataPtr + (appliesCounter * descriptorData.Size) + offset));

        descriptorData.Buffer.UnmapMemory();
    }

    private void CreateImageViewDescriptor(ResourceInfo<Texture>[] images, uint bindingIndex, uint descriptorSet)
    {
        var descriptorData = DescriptorDataSets[descriptorSet];
        var dataPtr = descriptorData.Buffer.MapMemory();
        for (uint i = 0; i < images.Length; i++)
        {
            var imageInfo = new DescriptorImageInfo
            {
                ImageView = images[i].Resource,
                ImageLayout = images[i].Resource.ImageLayout
            };

            var bufferDescriptorInfo = new DescriptorGetInfoEXT();
            bufferDescriptorInfo.Type = DescriptorType.SampledImage;
            bufferDescriptorInfo.Data = new DescriptorDataEXT();
            bufferDescriptorInfo.Data.PSampledImage = imageInfo;

            var offset =
                graphicsDevice.GetDescriptorSetLayoutOffset(descriptorData.Layout,
                    bindingIndex + i);

            graphicsDevice.GetDescriptor(bufferDescriptorInfo, descriptorData.ImageDescriptorSize,
                (nuint)((IntPtr)dataPtr + (appliesCounter * descriptorData.Size) + offset));
        }

        descriptorData.Buffer.UnmapMemory();
    }

    private unsafe void CreateSamplerDescriptor(ResourceInfo<SamplerState>[] samplers, uint bindingIndex, uint descriptorSet)
    {
        var descriptorData = DescriptorDataSets[descriptorSet];
        var dataPtr = descriptorData.Buffer.MapMemory();
        for (uint i = 0; i < samplers.Length; i++)
        {
            var sampleInfo = new DescriptorImageInfo();
            sampleInfo.Sampler = samplers[i].Resource;

            var bufferDescriptorInfo = new DescriptorGetInfoEXT();
            bufferDescriptorInfo.Type = DescriptorType.Sampler;
            bufferDescriptorInfo.Data = new DescriptorDataEXT();
            bufferDescriptorInfo.Data.PSampledImage = sampleInfo;

            var offset =
                graphicsDevice.GetDescriptorSetLayoutOffset(descriptorData.Layout,
                    bindingIndex + i);
            graphicsDevice.GetDescriptor(bufferDescriptorInfo, descriptorData.SamplerDescriptorSize,
                (nuint)((IntPtr)dataPtr + (appliesCounter * descriptorData.Size) + offset));
        }

        descriptorData.Buffer.UnmapMemory();
    }

    private unsafe void CreateUAVWriteDescriptor(ResourceInfo<Buffer>[] buffers, uint bindingIndex,
        uint descriptorSet)
    {
        var descriptorData = DescriptorDataSets[descriptorSet];
        uint storageBufferDescriptorSize = (uint)graphicsDevice.Adapter.DeviceBufferProperties.StorageBufferDescriptorSize;
        var dataPtr = descriptorData.Buffer.MapMemory();
        for (uint i = 0; i < buffers.Length; i++)
        {
            // Address/size — for EACH buffer separately (previously buffers[0] was mistakenly taken outside the loop).
            var addrInfo = new DescriptorAddressInfoEXT
            {
                Address = buffers[i].Resource.GetDeviceAddress(),
                Range = buffers[i].Resource.TotalSize,
                Format = Format.UNDEFINED
            };

            // UAV buffer = storage buffer (as in the heap branch); PStorageBuffer field, storage-descriptor size.
            var bufferDescriptorInfo = new DescriptorGetInfoEXT();
            bufferDescriptorInfo.Type = DescriptorType.StorageBuffer;
            bufferDescriptorInfo.Data = new DescriptorDataEXT();
            bufferDescriptorInfo.Data.PStorageBuffer = addrInfo;

            var offset =
                graphicsDevice.GetDescriptorSetLayoutOffset(descriptorData.Layout,
                    bindingIndex + i);

            graphicsDevice.GetDescriptor(bufferDescriptorInfo, storageBufferDescriptorSize,
                (nuint)((IntPtr)dataPtr + (appliesCounter * descriptorData.Size) + offset));
        }

        descriptorData.Buffer.UnmapMemory();
    }

    public static ShaderStageFlagBits EffectShaderTypeToShaderStage(EffectShaderType type)
    {
        switch (type)
        {
            case EffectShaderType.Vertex:
                return ShaderStageFlagBits.VertexBit;
            case EffectShaderType.Hull:
                return ShaderStageFlagBits.TessellationControlBit;
            case EffectShaderType.Domain:
                return ShaderStageFlagBits.TessellationEvaluationBit;
            case EffectShaderType.Geometry:
                return ShaderStageFlagBits.GeometryBit;
            case EffectShaderType.Fragment:
                return ShaderStageFlagBits.FragmentBit;
            case EffectShaderType.Compute:
                return ShaderStageFlagBits.ComputeBit;
            default:
                throw new ArgumentOutOfRangeException(
                    $"Effect type {type} currently has no equivalent for ShaderStageFlagBits");
        }
    }

    private DescriptorType ConvertFromEffectParameterType(EffectParameterType type)
    {
        switch (type)
        {
            case EffectParameterType.ConstantBuffer:
                return DescriptorType.UniformBuffer;
            case EffectParameterType.Sampler:
            case EffectParameterType.Sampler1D:
            case EffectParameterType.Sampler2D:
            case EffectParameterType.Sampler3D:
            case EffectParameterType.SamplerCube:
                return DescriptorType.Sampler;
            case EffectParameterType.Texture:
            case EffectParameterType.Texture1D:
            case EffectParameterType.Texture1DArray:
            case EffectParameterType.Texture2D:
            case EffectParameterType.Texture2DArray:
            case EffectParameterType.Texture2DMultisampled:
            case EffectParameterType.Texture2DMultisampledArray:
            case EffectParameterType.Texture3D:
            case EffectParameterType.TextureCube:
            case EffectParameterType.TextureCubeArray:
                return DescriptorType.SampledImage;
            case EffectParameterType.RWTexture1D:
            case EffectParameterType.RWTexture1DArray:
            case EffectParameterType.RWTexture2D:
            case EffectParameterType.RWTexture2DArray:
            case EffectParameterType.RWTexture3D:
                return DescriptorType.StorageTexelBuffer;
            case EffectParameterType.StorageBuffer:
                return DescriptorType.StorageBuffer;
            case EffectParameterType.StorageImage:
                return DescriptorType.StorageImage;
            default:
                throw new ArgumentOutOfRangeException(
                    $"Cannot convert parameter {type} to corresponding DescriptorType");
        }
    }

    internal void ComputeSlotLinks()
    {
        foreach (var stageBlockVar in pipelineStages)
        {
            var stageBlock = stageBlockVar;

            if (stageBlock?.Parameters == null)
                continue;

            PrepareSlotLinks(ref stageBlock);
        }
    }

    /// <summary>
    /// Optimizes the slot links.
    /// </summary>
    /// <param name="stageBlock">The stage block.</param>
    private void PrepareSlotLinks(ref StageBlock stageBlock)
    {
        foreach (var parameter in stageBlock.Parameters)
        {
            SlotLink link;
            var resourceType = parameter.Parameter.ResourceType;
            switch (resourceType)
            {
                case EffectResourceType.ShaderResourceView:
                    link = new SlotLink((uint)parameter.Parameter.SlotIndex, (uint)parameter.Parameter.DescriptorSet,
                        parameter.Parameter.ParameterDescription, parameter.Parameter);
                    stageBlock.ShaderResourceViewSlotLinks.Add(link);
                    break;
                case EffectResourceType.SamplerState:
                    link = new SlotLink((uint)parameter.Parameter.SlotIndex, (uint)parameter.Parameter.DescriptorSet,
                        parameter.Parameter.ParameterDescription, parameter.Parameter);
                    stageBlock.SamplerStateSlotLinks.Add(link);
                    break;
                case EffectResourceType.UnorderedAccessView:
                    link = new SlotLink((uint)parameter.Parameter.SlotIndex, (uint)parameter.Parameter.DescriptorSet,
                        parameter.Parameter.ParameterDescription, parameter.Parameter);
                    stageBlock.UnorderedAccessViewSlotLinks.Add(link);
                    break;
            }
        }
    }

    private bool CompareResourceParameter(EffectData.ResourceParameter left, EffectData.ResourceParameter right)
    {
        return left.Class != right.Class || left.Type != right.Type || left.Count != right.Count;
    }

    private PropertyKeyCollection PrepareProperties(Logger logger, EffectPropertyCollection properties)
    {
        var passProperties = new PropertyKeyCollection();

        foreach (var property in properties)
        {
            switch (property.Key)
            {
                //case EffectData.PropertyKeys.Blending:
                //    BlendState = graphicsDevice.BlendStates[(string)property.Value];
                //    if (BlendState == null)
                //        logger.Error("Unable to find registered BlendState [{0}]", (string)property.Value);
                //    break;
                //case EffectData.PropertyKeys.BlendingColor:
                //    BlendStateColor = (Color4)(Vector4F)property.Value;
                //    break;
                //case EffectData.PropertyKeys.BlendingSampleMask:
                //    BlendStateSampleMask = (uint)property.Value;
                //    break;

                //case EffectData.PropertyKeys.DepthStencil:
                //    DepthStencilState = graphicsDevice.DepthStencilStates[(string)property.Value];
                //    if (DepthStencilState == null)
                //        logger.Error("Unable to find registered DepthStencilState [{0}]", (string)property.Value);
                //    break;
                //case EffectData.PropertyKeys.DepthStencilReference:
                //    DepthStencilReference = (int)property.Value;
                //    break;

                //case EffectData.PropertyKeys.Rasterizer:
                //    RasterizerState = graphicsDevice.RasterizerStates[(string)property.Value];
                //    if (RasterizerState == null)
                //        logger.Error("Unable to find registered RasterizerState [{0}]", (string)property.Value);
                //    break;
                default:
                    passProperties[new PropertyKey(property.Key)] = property.Value;
                    break;
            }
        }

        return passProperties;
    }

    public override int GetHashCode()
    {
        int hashCode = 0;

        foreach (var stage in pipelineStages)
        {
            if (stage == null) continue;

            hashCode = stage.Type.GetHashCode();
            hashCode = (hashCode * 397) ^ stage.EntryPoint.GetHashCode();
        }

        return hashCode;
    }

    #region Nested type: PipelineBlock

    private struct ParameterBinding
    {
        public ParameterBinding(EffectParameter parameter, int slot, uint descriptorSet)
        {
            Parameter = parameter;
            Slot = slot;
            DescriptorSet = descriptorSet;
        }

        public readonly EffectParameter Parameter;

        public readonly int Slot;

        public readonly uint DescriptorSet;
    }

    protected override void Dispose(bool disposeManagedResources)
    {
        Log.Logger.Debug("Disposing EffectPass resources");
        graphicsDevice.MainDevice.FrameFinished -= GraphicsDeviceOnFrameFinished;
        ClearLayoutBindings();
        graphicsDevice.Destroy(descriptorSetLayout);
        graphicsDevice.Destroy(PipelineLayout);
        PipelineLayout = null;
        pipelineStages.Clear();
        base.Dispose(disposeManagedResources);
    }

    #endregion

    #region Nested type: SlotLink

    [StructLayout(LayoutKind.Sequential)]
    private struct SlotLink
    {
        public SlotLink(uint slotIndex, uint descriptorSet, EffectData.Parameter paramDescription, EffectParameter parameter)
        {
            ResourceParamDescription = paramDescription;
            SlotIndex = slotIndex;
            DescriptorSet = descriptorSet;
            Parameter = parameter;
        }

        public readonly EffectData.Parameter ResourceParamDescription;
        
        public readonly EffectParameter Parameter;

        public readonly uint SlotIndex;

        public readonly uint DescriptorSet;
    }

    #endregion

    #region Nested type: StageBlock

    private class StageBlock : DisposableObject
    {
        public List<ParameterBinding> Parameters;
        public readonly List<SlotLink> SamplerStateSlotLinks;
        public readonly List<SlotLink> ShaderResourceViewSlotLinks;
        public readonly List<SlotLink> UnorderedAccessViewSlotLinks;

        public readonly ShaderStageFlagBits Stage;
        public ShaderStageFlagBits NextStage;

        public List<ConstantBufferLink> ConstantBufferLinks;
        public int Index;

        public ShaderModule ShaderModule;
        public ShaderEXT ShaderObject;

        public byte[] ByteCode;
        public DescriptorSetLayout[] Layouts;
        public string EntryPoint;
        public readonly EffectShaderType Type;
        public readonly IGraphicsDevice GraphicsDevice;

        public StageBlock(IGraphicsDevice device,
            EffectShaderType type)
        {
            GraphicsDevice = device;
            Type = type;
            Stage = EffectShaderTypeToShaderStage(type);
            SamplerStateSlotLinks = new List<SlotLink>();
            ShaderResourceViewSlotLinks = new List<SlotLink>();
            UnorderedAccessViewSlotLinks = new List<SlotLink>();
            ConstantBufferLinks = new List<ConstantBufferLink>();
        }

        public void CreateShader(Dictionary<EffectParameter, uint> pushOffsets)
        {
            var shaderCreateInfo = new ShaderCreateInfoEXT();
            shaderCreateInfo.Stage = Stage;
            shaderCreateInfo.NextStage = NextStage;
            shaderCreateInfo.CodeType = ShaderCodeTypeEXT.SpirvExt;
            shaderCreateInfo.CodeSize = (uint)ByteCode.Length;
            shaderCreateInfo.PCode = ByteCode;
            shaderCreateInfo.PName = EntryPoint;
            
            if (!UseDescriptorHeap)
            {
                // VK_EXT_descriptor_buffer: classic descriptor set layouts, without the heap flag or mappings.
                shaderCreateInfo.PSetLayouts = Layouts;
                shaderCreateInfo.SetLayoutCount = Layouts != null ? (uint)Layouts.Length : 0;
                ShaderObject = GraphicsDevice.CreateShader(shaderCreateInfo);
                return;
            }

            // VK_EXT_descriptor_heap: no set layouts; classic (set,binding) are remapped to heap/push-address.
            shaderCreateInfo.Flags = ShaderCreateFlagBitsEXT.DescriptorHeapBitExt;
            shaderCreateInfo.PSetLayouts = null;
            shaderCreateInfo.SetLayoutCount = 0;

            int mappingCount = ConstantBufferLinks.Count +
                               ShaderResourceViewSlotLinks.Count +
                               SamplerStateSlotLinks.Count +
                               UnorderedAccessViewSlotLinks.Count;

            if (mappingCount > 0)
            {
                var mappings = new  DescriptorSetAndBindingMappingEXT[mappingCount];
                int mapIdx = 0;
                uint descriptorSize = (uint)GraphicsDevice.Adapter.DeviceHeapProperties.BufferDescriptorSize;

                // 1. Map the classic cbuffer register(b0) to PushAddress: the shader reads the CB
                // directly via the 64-bit address from push data (written by ApplyInternal at this same offset).
                foreach (var link in ConstantBufferLinks)
                {
                    mappings[mapIdx] = new DescriptorSetAndBindingMappingEXT
                    {
                        DescriptorSet = link.Parameter.DescriptorSet,
                        FirstBinding = (uint)link.Parameter.SlotIndex,
                        BindingCount = 1,
                        ResourceMask = SpirvResourceTypeFlagBitsEXT.UniformBufferBitExt,
                        Source = DescriptorMappingSourceEXT.PushAddressExt
                    };
                    mappings[mapIdx].SourceData = new DescriptorMappingSourceDataEXT();
                    mappings[mapIdx].SourceData.PushAddressOffset = pushOffsets[link.Parameter];
                    mapIdx++;
                }

                // 2. Map the classic texture register(t0) to an index in the resource heap
                foreach (var link in ShaderResourceViewSlotLinks)
                {
                    var effectParam = link.Parameter;
                    uint offsetInPushData = pushOffsets[effectParam];
                    // var resourceParam = effectParam.

                    mappings[mapIdx] = new DescriptorSetAndBindingMappingEXT
                    {
                        DescriptorSet = link.DescriptorSet,
                        FirstBinding = link.SlotIndex,
                        BindingCount = 1,
                        ResourceMask = SpirvResourceTypeFlagBitsEXT.SampledImageBitExt|SpirvResourceTypeFlagBitsEXT.ReadOnlyImageBitExt,
                        Source = DescriptorMappingSourceEXT.HeapWithPushIndexExt 
                    };
                    mappings[mapIdx].SourceData = new DescriptorMappingSourceDataEXT();
                    mappings[mapIdx].SourceData.PushIndex = new DescriptorMappingSourcePushIndexEXT
                    {
                        PushOffset = offsetInPushData,
                        HeapOffset = 0,
                        HeapIndexStride = 1,
                        HeapArrayStride = (uint)GraphicsDevice.Adapter.DeviceHeapProperties.ImageDescriptorSize
                    };
                    mapIdx++;
                }

                // 3. Map the classic sampler register(s0) to an index in the sampler heap
                foreach (var link in SamplerStateSlotLinks)
                {
                    var effectParam = link.Parameter;
                    uint offsetInPushData = pushOffsets[effectParam];

                    mappings[mapIdx] = new DescriptorSetAndBindingMappingEXT
                    {
                        DescriptorSet = link.DescriptorSet,
                        FirstBinding = link.SlotIndex,
                        BindingCount = 1,
                        ResourceMask = SpirvResourceTypeFlagBitsEXT.SamplerBitExt,
                        Source = DescriptorMappingSourceEXT.HeapWithPushIndexExt
                    };
                    mappings[mapIdx].SourceData = new DescriptorMappingSourceDataEXT();
                    mappings[mapIdx].SourceData.PushIndex = new DescriptorMappingSourcePushIndexEXT
                    {
                        PushOffset = offsetInPushData,
                        HeapOffset = 0,
                        HeapIndexStride = 1,
                        HeapArrayStride = (uint)GraphicsDevice.Adapter.DeviceHeapProperties.SamplerDescriptorSize
                    };
                    mapIdx++;
                }

                // 4. Map the classic UAV register(u0) to an index in the resource heap
                foreach (var link in UnorderedAccessViewSlotLinks)
                {
                    var effectParam = link.Parameter;
                    uint offsetInPushData = pushOffsets[effectParam];
                    
                    mappings[mapIdx] = new DescriptorSetAndBindingMappingEXT
                    {
                        DescriptorSet = link.DescriptorSet,
                        FirstBinding = link.SlotIndex,
                        BindingCount = 1,
                        ResourceMask = SpirvResourceTypeFlagBitsEXT.ReadWriteImageBitExt |
                                       SpirvResourceTypeFlagBitsEXT.ReadWriteStorageBufferBitExt,
                        Source = DescriptorMappingSourceEXT.HeapWithPushIndexExt
                    };
    
                    mappings[mapIdx].SourceData = new DescriptorMappingSourceDataEXT();
                    mappings[mapIdx].SourceData.PushIndex = new DescriptorMappingSourcePushIndexEXT
                    {
                        PushOffset = offsetInPushData,
                        HeapOffset = 0,
                        HeapIndexStride = 1,
                        HeapArrayStride = (uint)GraphicsDevice.Adapter.DeviceHeapProperties.BufferDescriptorSize
                    };
                    mapIdx++;
                }
                
                Array.Sort(mappings, (a, b) => {
                    int setCmp = a.DescriptorSet.CompareTo(b.DescriptorSet);
                    if (setCmp != 0) return setCmp;
                    return a.FirstBinding.CompareTo(b.FirstBinding);
                });

                // Assemble the final mapping structure for pNext
                var mappingInfo = new ShaderDescriptorSetAndBindingMappingInfoEXT
                {
                    MappingCount = (uint)mappingCount,
                    PMappings = mappings
                };

                // Pass it into native shader creation via a pointer
                shaderCreateInfo.PNext = mappingInfo;
            }

            ShaderObject = GraphicsDevice.CreateShader(shaderCreateInfo);
        }

        protected override void Dispose(bool disposeManagedResources)
        {
            if (disposeManagedResources)
            {
                GraphicsDevice.DestroyShader(ShaderObject);
                ShaderModule = null;
            }

            base.Dispose(disposeManagedResources);
        }
    }

    private struct ConstantBufferLink
    {
        public ConstantBufferLink(EffectConstantBuffer constantBuffer, EffectParameter parameter)
        {
            ConstantBuffer = constantBuffer;
            Parameter = parameter;
            ResourceIndex = 0;
        }

        public readonly EffectConstantBuffer ConstantBuffer;

        public readonly EffectParameter Parameter;

        public readonly uint ResourceIndex;
    }

    #endregion

    #region Nested Types

    public class DescriptorData
    {
        public DescriptorData(uint set)
        {
            DescriptorSet = set;
        }

        public DescriptorSetLayout Layout;
        public Buffer Buffer;
        public uint Size;
        public DescriptorType DescriptorType;
        public readonly uint DescriptorSet;
        public BufferUsageFlags UsageFlags;

        public uint SamplerDescriptorSize;
        public uint ImageDescriptorSize;
        public uint UniformBufferDescriptorSize;
    }

    public class DynamicBufferPool : IDisposable
    {
        private readonly IGraphicsDevice graphicsDevice;
        private readonly ulong pageSize;
        private readonly List<Buffer> pages = new();
    
        private int currentPageIndex = 0;
        private ulong currentOffset = 0;
        private DescriptorHeapManager _descriptorHeapManager;

        public DynamicBufferPool(DescriptorHeapManager descriptorHeapManager, IGraphicsDevice device, ulong pageSize = 8 * 1024 * 1024) // 8 MB by default
        {
            _descriptorHeapManager = descriptorHeapManager;
            graphicsDevice = device;
            this.pageSize = pageSize;
        
            // Create the first page immediately
            CreateNewPage();
        }

        public void Reset()
        {
            currentPageIndex = 0;
            currentOffset = 0;
        }
        
        public (Buffer Page, ulong Offset) Allocate(ulong size, ulong alignment)
        {
            // Align the current offset to hardware requirements (e.g., to 16 or 256 bytes)
            ulong alignedOffset = (currentOffset + alignment - 1) & ~(alignment - 1);

            // If the data does not fit within the current page, create/take the next one
            if (alignedOffset + size > pageSize)
            {
                currentPageIndex++;
                if (currentPageIndex >= pages.Count)
                {
                    CreateNewPage();
                }
            
                // On a new page the allocation is guaranteed to start at zero
                alignedOffset = 0;
            }

            Buffer page = pages[currentPageIndex];

            // Advance the allocator pointer for the next Allocate call
            currentOffset = alignedOffset + size;

            // Return the page itself and the exact byte offset within it
            return (page, alignedOffset);
        }

        private void CreateNewPage()
        {
            // CRITICALLY IMPORTANT: the ShaderDeviceAddress flag is required for Vulkan to allow obtaining the buffer's GPU address
            var flags = BufferUsageFlags.UniformBuffer | BufferUsageFlags.ShaderDeviceAddress;
            var memFlags = MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.DeviceLocal | MemoryPropertyFlags.HostCoherent;
        
            var buffer = Buffer.New(graphicsDevice, pageSize, flags, memFlags);
            pages.Add(buffer);
        }

        public void Dispose()
        {
            foreach (var page in pages) page.Dispose();
            pages.Clear();
        }
    }

    #endregion
}