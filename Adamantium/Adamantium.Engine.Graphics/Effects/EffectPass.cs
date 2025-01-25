using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using Adamantium.Core;
using Adamantium.Engine.Effects;
using AdamantiumVulkan.Core;
using AdamantiumVulkan.Core.Interop;
using Serilog;

namespace Adamantium.Engine.Graphics.Effects;

/// <summary>
/// Contains rendering state for drawing with an effect; updates constant buffers and directly sets all needed resources for each shader stage/>
/// </summary>
public sealed class EffectPass : DisposableObject
{
    private const uint MaxItemsPerBuffer = 4096;
    
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
    private readonly GraphicsDevice graphicsDevice;
        
    private readonly List<StageBlock> pipelineStages;

    internal EffectTechnique Technique;

    private List<PipelineShaderStageCreateInfo> shaderStages;

    private List<DescriptorSetLayoutBinding> layoutBindings;

    private DescriptorSetLayout descriptorSetLayout;

    private DescriptorSetLayout[] descriptorSetLayouts;
    
    private List<DescriptorBufferBindingInfoEXT> bindingInfos;

    private List<DescriptorEntrySet> descriptorEntrySets;
    
    private readonly VkDeviceSize[] offsets = new VkDeviceSize[1];
    
    private readonly List<StageBlock> stages = new List<StageBlock>();

    private uint appliesCounter = 0;
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

        shaderStages = new List<PipelineShaderStageCreateInfo>();
        layoutBindings = new List<DescriptorSetLayoutBinding>();
        PropertiesKey = PrepareProperties(logger, pass.Properties);
        IsSubPass = pass.IsSubPass;
        graphicsDevice.MainDevice.FrameFinished += GraphicsDeviceOnFrameFinished;
            
        descriptorEntrySets = new List<DescriptorEntrySet>();
        bindingInfos = new List<DescriptorBufferBindingInfoEXT>();

        // Don't create SubPasses collection for subpass.
        if (!IsSubPass)
            SubPasses = new EffectPassCollection();
    }

    private void GraphicsDeviceOnFrameFinished()
    {
        appliesCounter = 0;
    }

    private void ClearDescriptorsCache()
    {
        foreach (var entrySet in descriptorEntrySets)
        {
            entrySet?.Dispose();
        }
            
        descriptorEntrySets.Clear();
    }

    private void ClearLayoutBindings()
    {
        foreach (var binding in layoutBindings)
        {
            binding?.Dispose();
        }
            
        layoutBindings.Clear();
    }

    /// <summary>
    /// Gets the sub-pass attached to a global pass.
    /// </summary>
    /// <remarks>
    /// As a subpass cannot have subpass, if this pass is already a subpass, this field is null.
    /// </remarks>
    public readonly EffectPassCollection SubPasses;

    /// <summary>
    /// Gets a boolean indicating if this pass is a subpass.
    /// </summary>
    public readonly bool IsSubPass;

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
        // Give a chance to the effect callback to prepare this pass before it is actually applied (the OnApply can completely
        // change the pass and use for example a subpass).
        var realPass = Effect.OnApply(this);
        realPass.ApplyInternal();
    }
    
    /// <summary>
    /// Internal apply.
    /// </summary>
    private void ApplyInternal()
    {
        // By default, we set the Current technique 
        Effect.CurrentTechnique = Technique;

        // Sets the current pass on the graphics device
        graphicsDevice.CurrentEffectPass = this;
            
        DescriptorEntrySet descriptorEntry;
        
        stages.Clear();

        if (descriptorEntrySets.Count > appliesCounter)
        {
            descriptorEntry = descriptorEntrySets[(int)appliesCounter];
        }
        else
        {
            descriptorEntry = new DescriptorEntrySet();
            descriptorEntrySets.Add(descriptorEntry);
        }

        // ----------------------------------------------
        // Iterate on each stage to setup all inputs
        // ----------------------------------------------
        //for (int stageIndex = 0; stageIndex < shadersObjects.Count; stageIndex++)
        for (int stageIndex = 0; stageIndex < pipelineStages.Count; stageIndex++)
        {
            //var stageBlock = shadersObjects[stageIndex];
            var stageBlock = pipelineStages[stageIndex];
            if (stageBlock == null)
            {
                continue;
            }

            // If Shader is a null shader, then skip further processing
            if (stageBlock.Index < 0)
            {
                continue;
            }

            // Upload all constant buffers to the GPU if they have been modified.
            // ----------------------------------------------
            // Setup Constant buffers
            // ----------------------------------------------
            for (int i = 0; i < stageBlock.ConstantBufferLinks.Length; ++i)
            {
                var link = stageBlock.ConstantBufferLinks[i];
                if (link.ConstantBuffer.IsDirty)
                {
                    if (!descriptorEntry.TryGetConstantBuffer(link.Parameter.DescriptorSet, link.ResourceIndex, out var entry))
                    {
                        var nativeBuffer = ToDispose(Buffer.Uniform.New(graphicsDevice,
                            link.ConstantBuffer.BackingBuffer.Size, BufferUsageFlags.ShaderDeviceAddress));
                        entry = new BufferEntry(nativeBuffer, link.Parameter.DescriptorSet, link.ResourceIndex);
                        descriptorEntry.ConstantBufferEntries.Add(entry);
                    }

                    link.ConstantBuffer.CopyTo(entry.UniformBuffer);

                    CreateUniformBufferDescriptor(
                        entry.UniformBuffer,
                        link.ConstantBuffer.Description.Slot, 
                        link.ConstantBuffer.Description.DescriptorSet);
                }
            }
            
            // ----------------------------------------------
            // Setup SamplerStates
            // ----------------------------------------------
            var localLinks = stageBlock.SamplerStateSlotLinks;
            for (int i = 0; i < localLinks.Count; ++i)
            {
                var links = localLinks[i];
                var resources = Effect.ResourceLinker.GetSamplers(links.ResourceParamDescription);
                CreateSamplerDescriptor(resources, links.SlotIndex, links.DescriptorSet);
            } 

            // ----------------------------------------------
            // Setup ShaderResourceView
            // ----------------------------------------------
            localLinks = stageBlock.ShaderResourceViewSlotLinks;
            for (int i = 0; i < localLinks.Count; ++i)
            {
                var links = localLinks[i];
                var resources = Effect.ResourceLinker.GetShaderResources(localLinks[i].ResourceParamDescription);
                CreateImageViewDescriptor(resources, links.SlotIndex, links.DescriptorSet);
            }
                
            // ----------------------------------------------
            // Setup UnorderedAccessView
            // ----------------------------------------------
            localLinks = stageBlock.UnorderedAccessViewSlotLinks;
            for (int i = 0; i < localLinks.Count; ++i)
            {
                var links = localLinks[i];
                var resources = Effect.ResourceLinker.GetUAVs(links.ResourceParamDescription);
                CreateUAVWriteDescriptor(resources, links.SlotIndex, (int) graphicsDevice.ImageIndex);
            }
            
            stages.Add(stageBlock);
        }
        
        BindDescriptors();

        foreach (var stage in stages)
        {
            graphicsDevice.LogicalDevice.BindShader(graphicsDevice.CurrentCommandBuffer, stage.Stage, stage.ShaderObject);
        }
        
        if (!geometryStagePresent)
        {
            graphicsDevice.LogicalDevice.BindShader(graphicsDevice.CurrentCommandBuffer, ShaderStageFlagBits.GeometryBit, null);
        }
        
        appliesCounter++;
    } 

    private void BindDescriptors()
    {
        bindingInfos.Clear();
        foreach (var descriptorData in DescriptorDataSets)
        {
            var bindingInfoExt = new DescriptorBufferBindingInfoEXT();
            bindingInfoExt.SType = StructureType.DescriptorBufferBindingInfoExt;
            bindingInfoExt.Address = descriptorData.Buffer.GetDeviceAddress();
            bindingInfoExt.Usage = (BufferUsageFlagBits)descriptorData.UsageFlags;
            bindingInfos.Add(bindingInfoExt);
        }

        graphicsDevice.LogicalDevice.BindDescriptorBuffers(graphicsDevice.CurrentCommandBuffer, bindingInfos.ToArray());
        bindingInfos.ForEach((item) => item.Dispose());

        for (int i = 0; i < DescriptorDataSets.Length; ++i)
        {
            offsets[0] = appliesCounter * DescriptorDataSets[0].Size;
            graphicsDevice.LogicalDevice.SetDescriptorBufferOffsets(
                graphicsDevice.CurrentCommandBuffer,
                PipelineBindPoint.Graphics,
                PipelineLayout,
                (uint)i,
                1,
                [0],
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
        /*
        // If nothing to clear, return immediately
        if (graphicsDevice.CurrentPass == null)
        {
            return;
        }

        // Sets the current pass on the graphics device
        graphicsDevice.CurrentPass = null;

        // ----------------------------------------------
        // Iterate on each stage to setup all inputs
        // ----------------------------------------------
        for (int stageIndex = 0; stageIndex < pipeline.Stages.Length; stageIndex++)
        {
            var stageBlock = pipeline.Stages[stageIndex];
            if (stageBlock == null)
            {
                continue;
            }

            var shaderStage = stageBlock.ShaderStage;

            // ----------------------------------------------
            // Setup the shader for this stage.
            // ----------------------------------------------
            if (fullUnApply)
            {
                shaderStage.SetShader(null, null, 0);
            }

            // If Shader is a null shader, then skip further processing
            if (stageBlock.Index < 0)
            {
                continue;
            }

            if (shaderStage is GeometryShaderStage)
            {
                graphicsDevice.ResetStreamOutputTargets();
            }

            var mergerStage = pipeline.OutputMergerStage;

            // ----------------------------------------------
            // Reset ShaderResourceView
            // ----------------------------------------------
            var localLinks = stageBlock.ShaderResourceViewSlotLinks;
            if (localLinks.Count > 0)
            {
                for (int i = 0; i < localLinks.Count; ++i)
                {
                    shaderStage.SetShaderResource(localLinks[i].SlotIndex, null);
                }
            }

            // ----------------------------------------------
            // Reset UnorderedAccessView
            // ----------------------------------------------
            localLinks = stageBlock.UnorderedAccessViewSlotLinks;
            if (localLinks.Count > 0)
            {
                if (stageBlock.Type == EffectShaderType.Compute)
                {
                    var stage = (ComputeShaderStage)shaderStage;
                    for (int i = 0; i < localLinks.Count; ++i)
                    {
                        stage.SetUnorderedAccessView(localLinks[i].SlotIndex, null);
                    }
                }
                else
                {
                    // Otherwise, for OutputMergerStage.
                    for (int i = 0; i < localLinks.Count; ++i)
                    {
                        mergerStage.SetUnorderedAccessView(localLinks[i].SlotIndex, null);
                    }
                }
            }

            if (fullUnApply)
            {
                // ----------------------------------------------
                // Reset Constant Buffers
                // ----------------------------------------------
                for (int i = 0; i < stageBlock.ConstantBufferLinks.Length; ++i)
                {
                    var link = stageBlock.ConstantBufferLinks[i];
                    shaderStage.SetConstantBuffer(link.Parameter.SlotIndex, null);
                }
            }
        }
        */
    }

    /// <summary>
    /// Initializes this pass.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <exception cref="System.InvalidOperationException"></exception>
    internal void Initialize(Logger logger)
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

    private bool geometryStagePresent = false;

    internal void PrepareDescriptorSets()
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
        
        pipelineStages.ForEach(stage => stage.CreateShader());
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

        stageBlock.ShaderModule = Effect.Pool.GetOrCompileShader(
            stageBlock.Type,
            shaderIndex,
            out var errorProfile);
        
        stageBlock.ByteCode = Effect.Pool.GetShaderBytecode(stageBlock.Type, shaderIndex);

        if (stageBlock.ShaderModule == null)
        {
            logger.Error(
                "Unsupported shader profile [{0} / {1}] on current GraphicsDevice in (effect [{2}] Technique [{3}] Pass: [{4}])",
                stageBlock.Type,
                errorProfile,
                Effect.Name,
                Technique.Name,
                Name);
            return;
        }

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

            var constantBuffer = Effect.GetOrCreateConstantBuffer(Effect.GraphicsDevice, constantBufferRaw);
            // IF constant buffer is null, it means that there is a conflict
            if (constantBuffer == null)
            {
                logger.Error(
                    "Constant buffer [{0}] cannot have multiple size or different content declaration inside the same effect pool",
                    constantBufferRaw.Name);
                continue;
            }

            CreateAndAddLayoutBinding(constantBuffer.Description.DescriptorSet, constantBuffer.Description.Slot,  1U, DescriptorType.UniformBuffer, EffectShaderTypeToShaderStage(stageBlock.Type));

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

        var constantBufferLinks = new List<ConstantBufferLink>();

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

            CreateAndAddLayoutBinding(parameterRaw.DescriptorSet, parameterRaw.Slot, parameterRaw.Count, ConvertFromEffectParameterType(parameterRaw.Type),
                EffectShaderTypeToShaderStage(stageBlock.Type));

            // For constant buffers, we need to store explicit link
            if (parameter.ResourceType == EffectResourceType.ConstantBuffer)
            {
                constantBufferLinks.Add(new ConstantBufferLink(Effect.ConstantBuffers[parameter.Name], parameter));
            }

            stageBlock.Parameters ??= new List<ParameterBinding>(shaderRaw.ResourceParameters.Count);
            stageBlock.Parameters.Add(new ParameterBinding(parameter, parameterRaw.Slot, parameterRaw.DescriptorSet));
        }

        stageBlock.ConstantBufferLinks = constantBufferLinks.ToArray();
    }

    private DescriptorSetLayoutBinding CreateAndAddLayoutBinding(uint descriptorSet, uint slot, uint descriptorCount, DescriptorType descriptorType, ShaderStageFlagBits stageFlags)
    {
        var binding = layoutBindings.FirstOrDefault(x=>x.Binding == slot);

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
        InitDescriptorBuffer(descriptorSet, bindings.ToArray());
        
        var layoutInfo = new DescriptorSetLayoutCreateInfo();
        layoutInfo.BindingCount = (uint)bindings.Count;
        layoutInfo.PBindings = bindings.ToArray();
        layoutInfo.Flags = DescriptorSetLayoutCreateFlagBits.DescriptorBufferBitExt;
            
        descriptorSetLayout = graphicsDevice.CreateDescriptorSetLayout(layoutInfo);
        descriptorSetLayouts = DescriptorDataSets.Select(x => x.Layout).ToArray();
    }

    private void InitDescriptorBuffer(uint descriptorSetIndex, params DescriptorSetLayoutBinding[] bindings)
    {
        if (bindings == null || bindings.Length == 0) return;
            
        var layoutInfo = new DescriptorSetLayoutCreateInfo();
        layoutInfo.BindingCount = (uint)bindings.Length;
        layoutInfo.PBindings = bindings.ToArray();
        layoutInfo.Flags = DescriptorSetLayoutCreateFlagBits.DescriptorBufferBitExt;

        var descriptorTypes = bindings.Select(x => x.DescriptorType).ToList();
        var descriptorData = DescriptorDataSets[descriptorSetIndex];
        descriptorData.DescriptorType = bindings[0].DescriptorType;
        descriptorData.Layout = graphicsDevice.CreateDescriptorSetLayout(layoutInfo);
        var size = graphicsDevice.LogicalDevice.GetDescriptorSetLayoutSize(descriptorData.Layout);
        descriptorData.UniformBufferDescriptorSize = (uint)graphicsDevice.MainDevice.GraphicsAdapter
            .DeviceBufferProperties.UniformBufferDescriptorSize;
        descriptorData.SamplerDescriptorSize = (uint)graphicsDevice.MainDevice.GraphicsAdapter.DeviceBufferProperties.SamplerDescriptorSize;
        descriptorData.ImageDescriptorSize = (uint)graphicsDevice.MainDevice.GraphicsAdapter.DeviceBufferProperties.SampledImageDescriptorSize;
        
        descriptorData.Size = graphicsDevice.AlignSize(size, (uint)graphicsDevice.MainDevice.GraphicsAdapter.DeviceBufferProperties.DescriptorBufferOffsetAlignment);
        
        var bufferFlags = BufferUsageFlags.ResourceDescriptorBufferExt | BufferUsageFlags.ShaderDeviceAddress;
        if (descriptorTypes.Contains(DescriptorType.Sampler) || descriptorTypes.Contains(DescriptorType.SampledImage))
        {
            bufferFlags |= BufferUsageFlags.SamplerDescriptorBufferExt;
        }

        descriptorData.UsageFlags = bufferFlags;

        descriptorData.Buffer = ToDispose(Buffer.New(graphicsDevice,
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
        
    private unsafe void CreateUniformBufferDescriptor(Buffer buffer, uint bindingIndex, uint descriptorSet)
    {
        var addrInfo = new DescriptorAddressInfoEXT();
        addrInfo.SType = StructureType.DescriptorAddressInfoExt;
        addrInfo.Address = buffer.GetDeviceAddress();
        addrInfo.Range   = buffer.TotalSize;
        addrInfo.Format  = Format.UNDEFINED;

        var bufferDescriptorInfo = new DescriptorGetInfoEXT();
        bufferDescriptorInfo.SType = StructureType.DescriptorGetInfoExt;
        bufferDescriptorInfo.Type  = DescriptorType.UniformBuffer;
        bufferDescriptorInfo.Data = new DescriptorDataEXT();
        bufferDescriptorInfo.Data.PUniformBuffer = addrInfo;

        var descriptorData = DescriptorDataSets[descriptorSet];;
        var dataPtr = descriptorData.Buffer.MapMemory();
        var offset = graphicsDevice.LogicalDevice.GetDescriptorSetLayoutOffset(descriptorData.Layout,
            bindingIndex);
        
        graphicsDevice.LogicalDevice.GetDescriptor(bufferDescriptorInfo, descriptorData.UniformBufferDescriptorSize,
            (void*)((IntPtr)dataPtr + (appliesCounter * descriptorData.Size) + offset));
        
        descriptorData.Buffer.UnmapMemory();
    }
        
    private unsafe void CreateImageViewDescriptor(ResourceInfo<Texture>[] images, uint bindingIndex, uint descriptorSet)
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
            bufferDescriptorInfo.SType = StructureType.DescriptorGetInfoExt;
            bufferDescriptorInfo.Type  = DescriptorType.SampledImage;
            bufferDescriptorInfo.Data = new DescriptorDataEXT();
            bufferDescriptorInfo.Data.PSampledImage = imageInfo;
                
            var offset =
                graphicsDevice.LogicalDevice.GetDescriptorSetLayoutOffset(descriptorData.Layout,
                    bindingIndex + i);
            
            graphicsDevice.LogicalDevice.GetDescriptor(bufferDescriptorInfo, descriptorData.ImageDescriptorSize, 
                (void*)((IntPtr)dataPtr + (appliesCounter * descriptorData.Size) + offset));
        }
        descriptorData.Buffer.UnmapMemory();
    }
        
    private unsafe void CreateSamplerDescriptor(ResourceInfo<Sampler>[] samplers, uint bindingIndex, uint descriptorSet)
    {
        var descriptorData = DescriptorDataSets[descriptorSet];
        var dataPtr = descriptorData.Buffer.MapMemory();
        for (uint i = 0; i < samplers.Length; i++)
        {
            var sampleInfo = new DescriptorImageInfo();
            sampleInfo.Sampler = samplers[i].Resource;

            var bufferDescriptorInfo = new DescriptorGetInfoEXT();
            bufferDescriptorInfo.SType = StructureType.DescriptorGetInfoExt;
            bufferDescriptorInfo.Type  = DescriptorType.Sampler;
            bufferDescriptorInfo.Data = new DescriptorDataEXT();
            bufferDescriptorInfo.Data.PSampledImage = sampleInfo;
                
            var offset =
                graphicsDevice.LogicalDevice.GetDescriptorSetLayoutOffset(descriptorData.Layout,
                    bindingIndex+i);
            graphicsDevice.LogicalDevice.GetDescriptor(bufferDescriptorInfo, descriptorData.SamplerDescriptorSize, 
                (void*)((IntPtr)dataPtr + (appliesCounter * descriptorData.Size) + offset));
        }
        descriptorData.Buffer.UnmapMemory();
    }
        
    private unsafe void CreateUAVWriteDescriptor(ResourceInfo<Buffer>[] texelBuffers, uint bindingIndex, int descriptorSet)
    {
        var addrInfo = new DescriptorAddressInfoEXT();
        addrInfo.SType = StructureType.DescriptorAddressInfoExt;
        addrInfo.Address = texelBuffers[0].Resource.GetDeviceAddress();
        addrInfo.Range   = texelBuffers[0].Resource.TotalSize;
        addrInfo.Format  = Format.UNDEFINED;

        var descriptorData = DescriptorDataSets[descriptorSet];
        var dataPtr = descriptorData.Buffer.MapMemory();
        for (uint i = 0; i < texelBuffers.Length; i++)
        {
            var bufferDescriptorInfo = new DescriptorGetInfoEXT();
            bufferDescriptorInfo.SType = StructureType.DescriptorGetInfoExt;
            bufferDescriptorInfo.Type  = DescriptorType.UniformTexelBuffer;
            bufferDescriptorInfo.Data = new DescriptorDataEXT();
            bufferDescriptorInfo.Data.PStorageTexelBuffer = addrInfo;
                
            var offset =
                graphicsDevice.LogicalDevice.GetDescriptorSetLayoutOffset(descriptorData.Layout,
                    bindingIndex + i);
            
            graphicsDevice.LogicalDevice.GetDescriptor(bufferDescriptorInfo, descriptorData.ImageDescriptorSize, 
                (void*)((IntPtr)dataPtr + (appliesCounter * descriptorData.Size) + offset));
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
                throw new ArgumentOutOfRangeException($"Effect type {type} currently has no equivalent for ShaderStageFlagBits");
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
                throw new ArgumentOutOfRangeException($"Cannot convert parameter {type} to corresponding DescriptorType");
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
                    link = new SlotLink((uint)parameter.Parameter.SlotIndex, (uint)parameter.Parameter.DescriptorSet, parameter.Parameter.ParameterDescription);
                    stageBlock.ShaderResourceViewSlotLinks.Add(link);
                    break;
                case EffectResourceType.SamplerState:
                    link = new SlotLink((uint)parameter.Parameter.SlotIndex, (uint)parameter.Parameter.DescriptorSet, parameter.Parameter.ParameterDescription);
                    stageBlock.SamplerStateSlotLinks.Add(link);
                    break;
                case EffectResourceType.UnorderedAccessView:
                    link = new SlotLink((uint)parameter.Parameter.SlotIndex, (uint)parameter.Parameter.DescriptorSet, parameter.Parameter.ParameterDescription);
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
        ClearLayoutBindings();
        descriptorSetLayout?.Destroy(graphicsDevice);
        ClearDescriptorsCache();
        graphicsDevice.LogicalDevice.DestroyPipelineLayout(PipelineLayout);
        PipelineLayout = null;
        pipelineStages.Clear();
        base.Dispose(disposeManagedResources);
    }

    #endregion

    #region Nested type: SlotLink

    [StructLayout(LayoutKind.Sequential)]
    private struct SlotLink
    {
        public SlotLink(uint slotIndex, uint descriptorSet, EffectData.Parameter paramDescription)
        {
            ResourceParamDescription = paramDescription;
            SlotIndex = slotIndex;
            DescriptorSet = descriptorSet;
        }

        public readonly EffectData.Parameter ResourceParamDescription;

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

        public ConstantBufferLink[] ConstantBufferLinks;
        public int Index;

        public ShaderModule ShaderModule;
        public ShaderEXT ShaderObject;
        
        public byte[] ByteCode;
        public DescriptorSetLayout[] Layouts;
        public string EntryPoint;
        public readonly EffectShaderType Type;
        public readonly GraphicsDevice GraphicsDevice;

        public StageBlock(GraphicsDevice device,
            EffectShaderType type)
        {
            GraphicsDevice = device;
            Type = type;
            Stage = EffectShaderTypeToShaderStage(type);
            SamplerStateSlotLinks = new List<SlotLink>();
            ShaderResourceViewSlotLinks = new List<SlotLink>();
            UnorderedAccessViewSlotLinks = new List<SlotLink>();
        }
        
        public void CreateShader()
        {
            var shaderCreateInfo = new ShaderCreateInfoEXT();
            shaderCreateInfo.Stage = Stage;
            shaderCreateInfo.NextStage = NextStage;
            shaderCreateInfo.CodeType = ShaderCodeTypeEXT.SpirvExt;
            shaderCreateInfo.CodeSize = (uint)ByteCode.Length;
            shaderCreateInfo.PCode = ByteCode;
            shaderCreateInfo.PName = EntryPoint;
            shaderCreateInfo.PSetLayouts = Layouts;
            shaderCreateInfo.SetLayoutCount = (uint)Layouts.Length;

            ShaderObject = GraphicsDevice.LogicalDevice.CreateShader(shaderCreateInfo);
        }
            
        protected override void Dispose(bool disposeManagedResources)
        {
            if (disposeManagedResources)
            {
                GraphicsDevice.LogicalDevice.DestroyShaderEXT(ShaderObject);
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
        
    #region Nested Type: DescriptorEntrySet
        
    private class BufferEntry
    {
        public BufferEntry(Buffer uniformBuffer, uint descriptorSet, uint resourceIndex)
        {
            UniformBuffer = uniformBuffer;
            DescriptorSet = descriptorSet;
            ResourceIndex = resourceIndex;
        }
        
        public readonly Buffer UniformBuffer;

        public readonly uint DescriptorSet;
        
        public readonly uint ResourceIndex;
    }

    private class ResourceEntry<T> where T : class
    {
        private T resource;

        public T Resource
        {
            get => resource;
            set
            {
                if (resource == value) return;
                resource = value;
                IsDirty = true;
            }
        }

        public uint SlotIndex { get; set; }
        
        public uint DescriptorIndex { get; set; }
        
        public bool IsDirty { get; set; }
    }
        
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

    private class DescriptorEntrySet : DisposableObject
    {
        public DescriptorEntrySet()
        {
            ConstantBufferEntries = new List<BufferEntry>();
        }

        public readonly List<BufferEntry> ConstantBufferEntries;
        
        public bool TryGetConstantBuffer(uint descriptorSet, uint resourceId, out BufferEntry entry)
        {
            entry = null;
            for (int i = 0; i < ConstantBufferEntries.Count; i++)
            {
                if (ConstantBufferEntries[i].ResourceIndex == resourceId && ConstantBufferEntries[i].DescriptorSet == descriptorSet)
                {
                    entry = ConstantBufferEntries[i];
                    return true;
                }
            }
    
            return false;
        }
    
        protected override void Dispose(bool disposeManagedResources)
        {
            if (disposeManagedResources)
            {
                foreach (var entry in ConstantBufferEntries)
                {
                    entry.UniformBuffer?.Dispose();
                }
            }
            base.Dispose(disposeManagedResources);
        }
    }

    #endregion
}