using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using Adamantium.Core;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Effects;
using AdamantiumVulkan.Core;

namespace Adamantium.Engine.Graphics.Effects
{
    /// <summary>
    /// Contains rendering state for drawing with an effect;  updates constant buffers and directly sets all needed resources for each <see cref="CommonShaderStage"/>
    /// </summary>
    public sealed class EffectPass : NamedObject
    {
        private const int StageCount = 6;

        /// <summary>
        ///   Gets the attributes associated with this pass.
        /// </summary>
        /// <value> The attributes. </value>
        public readonly PropertyCollection Properties;

        /// <summary>
        /// The parent effect of this pass.
        /// </summary>
        public readonly Effect Effect;

        private readonly EffectData.Pass pass;
        private readonly GraphicsDevice graphicsDevice;

        private PipelineBlock pipeline;

        internal EffectTechnique Technique;

        private List<PipelineShaderStageCreateInfo> shaderStages;

        private List<DescriptorSetLayoutBinding> layoutBindings;

        private DescriptorSetLayout descriptorSetLayout;

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
            pipeline = new PipelineBlock()
            {
                Stages = new StageBlock[StageCount],
            };

            shaderStages = new List<PipelineShaderStageCreateInfo>();
            layoutBindings = new List<DescriptorSetLayoutBinding>();
            Properties = PrepareProperties(logger, pass.Properties);
            IsSubPass = pass.IsSubPass;

            // Don't create SubPasses collection for subpass.
            if (!IsSubPass)
                SubPasses = new EffectPassCollection();
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
            //graphicsDevice.CurrentPass = this;

            //TODO: here we need to setup graphics/compute pipeline for current EffectPass before rendering begins
            //and then update all descriptors for each stage

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

                /*
                var shaderStage = stageBlock.ShaderStage;
                // If Shader is a null shader, then skip further processing
                if (stageBlock.Index < 0)
                {
                    continue;
                }

                // ----------------------------------------------
                // Setup the shader for this stage.
                // ----------------------------------------------               
                shaderStage.SetShader(stageBlock.Shader, null, 0);

                var mergerStage = pipeline.OutputMergerStage;

                // Upload all constant buffers to the GPU if they have been modified.
                // ----------------------------------------------
                // Setup Constant buffers
                // ----------------------------------------------
                for (int i = 0; i < stageBlock.ConstantBufferLinks.Length; ++i)
                {
                    var link = stageBlock.ConstantBufferLinks[i];
                    link.ConstantBuffer.Update();
                    shaderStage.SetConstantBuffer(link.Parameter.SlotIndex, link.ConstantBuffer.NativeBuffer);
                }

                // ----------------------------------------------
                // Setup ShaderResourceView
                // ----------------------------------------------
                var localLinks = stageBlock.ShaderResourceViewSlotLinks;
                for (int i = 0; i < localLinks.Count; ++i)
                {
                    var links = localLinks[i];
                    var resources = Effect.ResourceLinker.GetShaderResources(localLinks[i].ResourceParamDescription);
                    shaderStage.SetShaderResources(links.SlotIndex, resources);
                }

                // ----------------------------------------------
                // Setup SamplerStates
                // ----------------------------------------------
                localLinks = stageBlock.SamplerStateSlotLinks;
                for (int i = 0; i < localLinks.Count; ++i)
                {
                    var links = localLinks[i];
                    var resources = Effect.ResourceLinker.GetSamplers(links.ResourceParamDescription);
                    shaderStage.SetSamplers(links.SlotIndex, resources);
                }

                // ----------------------------------------------
                // Setup UnorderedAccessView
                // ----------------------------------------------
                localLinks = stageBlock.UnorderedAccessViewSlotLinks;
                switch (stageBlock.Type)
                {
                    case EffectShaderType.Compute:
                        var computeStage = ((ComputeShaderStage)shaderStage);
                        for (int i = 0; i < localLinks.Count; ++i)
                        {
                            var link = localLinks[i];
                            var resources = Effect.ResourceLinker.GetUAVs(link.ResourceParamDescription);
                            computeStage.SetUnorderedAccessViews(link.SlotIndex, resources);
                        }
                        break;
                    default:
                        for (int i = 0; i < localLinks.Count; ++i)
                        {
                            var link = localLinks[i];
                            var resources = Effect.ResourceLinker.GetUAVs(link.ResourceParamDescription);
                            mergerStage.SetUnorderedAccessViews(link.SlotIndex, resources);
                        }
                        break;
                }
                */
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
            for (int i = 0; i < StageCount; i++)
            {
                var shaderType = (EffectShaderType)i;
                var link = pass.Pipeline[shaderType];
                if (link == null)
                    continue;

                if (link.IsImport)
                {
                    throw new InvalidOperationException(
                       $"Unable to resolve imported shader [{link.ImportName}] for stage [{shaderType}]");
                }

                var stageBlock = new StageBlock(shaderType);
                pipeline.Stages[i] = stageBlock;

                stageBlock.Index = link.Index;
                stageBlock.EntryPoint = link.EntryPoint;

                InitStageBlock(stageBlock, logger);
            }

            descriptorSetLayout = CreateDescriptorSetLayout(layoutBindings);
            PipelineLayout = CreatePipelineLayout(descriptorSetLayout);

        }

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

            string errorProfile;
            stageBlock.Shader = Effect.Pool.GetOrCompileShader(
               stageBlock.Type,
               shaderIndex,
               out errorProfile);

            if (stageBlock.Shader == null)
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
            shaderStageInfo.Module = stageBlock.Shader;
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

                CreateAndAddLayoutBinding(constantBuffer.Description.Slot, DescriptorType.UniformBuffer, (uint)EffectShaderTypeToShaderStage(stageBlock.Type));

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
                                 parameter.buffer != previousParameter.buffer)
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

                    CreateAndAddLayoutBinding(parameterRaw.Slot, ConvertFromEffectParameterType(parameterRaw.Type), (uint)EffectShaderTypeToShaderStage(stageBlock.Type));
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

                // For constant buffers, we need to store explicit link
                if (parameter.ResourceType == EffectResourceType.ConstantBuffer)
                {
                    constantBufferLinks.Add(new ConstantBufferLink(Effect.ConstantBuffers[parameter.Name], parameter));
                }

                if (stageBlock.Parameters == null)
                {
                    stageBlock.Parameters = new List<ParameterBinding>(shaderRaw.ResourceParameters.Count);
                }

                stageBlock.Parameters.Add(new ParameterBinding(parameter, parameterRaw.Slot));
            }

            stageBlock.ConstantBufferLinks = constantBufferLinks.ToArray();

        }

        private void CreateAndAddLayoutBinding(uint slot, DescriptorType descriptorType, uint stageFlags)
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
                resourceBinding.DescriptorCount = 1;
                resourceBinding.DescriptorType = descriptorType;
                resourceBinding.StageFlags = stageFlags;

                layoutBindings.Add(resourceBinding);
            }
        }

        private DescriptorSetLayout CreateDescriptorSetLayout(List<DescriptorSetLayoutBinding> bindings)
        {
            var layoutInfo = new DescriptorSetLayoutCreateInfo();
            layoutInfo.BindingCount = (uint)bindings.Count;
            layoutInfo.PBindings = bindings.ToArray();

            return graphicsDevice.CreateDescriptorSetLayout(layoutInfo);
        }

        private PipelineLayout CreatePipelineLayout(DescriptorSetLayout setLayout)
        {
            var pipelineLayoutInfo = new PipelineLayoutCreateInfo();
            pipelineLayoutInfo.SetLayoutCount = 1;
            pipelineLayoutInfo.PSetLayouts = new DescriptorSetLayout[] { setLayout };

            return graphicsDevice.CreatePipelineLayout(pipelineLayoutInfo);
        }

        private ShaderStageFlagBits EffectShaderTypeToShaderStage(EffectShaderType type)
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
            foreach (var stageBlockVar in pipeline.Stages)
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
                        link = new SlotLink(parameter.Parameter.SlotIndex, parameter.Parameter.ParameterDescription);
                        stageBlock.ShaderResourceViewSlotLinks.Add(link);
                        break;
                    case EffectResourceType.SamplerState:
                        link = new SlotLink(parameter.Parameter.SlotIndex, parameter.Parameter.ParameterDescription);
                        stageBlock.SamplerStateSlotLinks.Add(link);
                        break;
                    case EffectResourceType.UnorderedAccessView:
                        link = new SlotLink(parameter.Parameter.SlotIndex, parameter.Parameter.ParameterDescription);
                        stageBlock.UnorderedAccessViewSlotLinks.Add(link);
                        break;
                }
            }
        }

        private bool CompareResourceParameter(EffectData.ResourceParameter left, EffectData.ResourceParameter right)
        {
            return left.Class != right.Class || left.Type != right.Type || left.Count != right.Count;
        }

        private PropertyCollection PrepareProperties(Logger logger, CommonData.PropertyCollection properties)
        {
            var passProperties = new PropertyCollection();

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

        #region Nested type: PipelineBlock

        private struct PipelineBlock
        {
            public StageBlock[] Stages;
        }

        private struct ParameterBinding
        {
            public ParameterBinding(EffectParameter parameter, int slot)
            {
                Parameter = parameter;
                Slot = slot;
            }

            public readonly EffectParameter Parameter;

            public readonly int Slot;
        }

        #endregion

        #region Nested type: SlotLink

        [StructLayout(LayoutKind.Sequential)]
        private struct SlotLink
        {
            public SlotLink(int slotIndex, EffectData.Parameter paramDescription)
            {
                ResourceParamDescription = paramDescription;
                SlotIndex = slotIndex;
            }

            public readonly EffectData.Parameter ResourceParamDescription;

            public readonly int SlotIndex;

        }

        #endregion

        #region Nested type: StageBlock

        private class StageBlock
        {
            public List<ParameterBinding> Parameters;
            public readonly List<SlotLink> SamplerStateSlotLinks;
            public readonly List<SlotLink> ShaderResourceViewSlotLinks;
            public readonly List<SlotLink> UnorderedAccessViewSlotLinks;

            public ConstantBufferLink[] ConstantBufferLinks;
            public int Index;

            public ShaderModule Shader;

            public string EntryPoint;

            public readonly EffectShaderType Type;

            public int StreamOutputRasterizedStream;

            public StageBlock(EffectShaderType type)
            {
                Type = type;
                SamplerStateSlotLinks = new List<SlotLink>();
                ShaderResourceViewSlotLinks = new List<SlotLink>();
                UnorderedAccessViewSlotLinks = new List<SlotLink>();
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

            public int ResourceIndex;
        }

        #endregion
    }
}
