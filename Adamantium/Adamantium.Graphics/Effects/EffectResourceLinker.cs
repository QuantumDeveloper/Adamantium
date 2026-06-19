using System.Collections.Generic;
using Adamantium.Core;
using Adamantium.EffectsCompiler;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Vulkan.Core;
using VulkanBuffer = Adamantium.Vulkan.Core.Buffer;

namespace Adamantium.Graphics.Effects;

internal class ResourceInfo<T> : PropertyChangedBase where T : class
{
    private T resource;

    public ResourceInfo()
    {
    }
        
    public ResourceInfo(T resource)
    {
        Resource = resource;
    }

    public T Resource
    {
        get => resource;
        set
        {
            if (resource != value)
            {
                IsDirty = true;
            }
            
            resource = value;
        }
    }

    public int SlotIndex { get; set; }
        
    public int DescriptorSet { get; set; } 
    
    public bool IsDirty { get; internal set; }
    
    public uint GlobalHeapOffset { get; internal set; } = uint.MaxValue;
}

internal class EffectResourceLinker : IEffectResourceLinker
{
    /// <summary>
    /// Real object resources, as they were set on the parameter.
    /// </summary>
    public Dictionary<EffectData.Parameter, EffectConstantBuffer> ConstantBuffers;

    /// <summary>
    /// Total number of resources.
    /// </summary>
    public int Count { get; set; }

    // public Dictionary<EffectData.Parameter, Sampler[]> SamplerStates;
    public Dictionary<EffectData.Parameter, ResourceInfo<SamplerState>[]> SamplerStates;
    public Dictionary<EffectData.Parameter, ResourceInfo<Texture>[]> ShaderResourceViews;
    public Dictionary<EffectData.Parameter, ResourceInfo<Buffer>[]> UnorderedAccessViews;
    public Dictionary<EffectData.Parameter, object> BoundResources;

    private static ResourceInfo<SamplerState>[] EmptySamplers = [];
    private static ResourceInfo<Texture>[] EmptyResourceViews = [];
    private static ResourceInfo<Buffer>[] EmptyUAVs = [];

    internal IDescriptorHeapManager DescriptorHeapManager { get; }

    public EffectResourceLinker(IDescriptorHeapManager descriptorHeapManager)
    {
        DescriptorHeapManager = descriptorHeapManager;
    }

    /// <summary>
    /// Initializes this instance.
    /// </summary>
    public void Initialize()
    {
        ConstantBuffers = new Dictionary<EffectData.Parameter, EffectConstantBuffer>();

        SamplerStates = new Dictionary<EffectData.Parameter, ResourceInfo<SamplerState>[]>();
        ShaderResourceViews = new Dictionary<EffectData.Parameter, ResourceInfo<Texture>[]>();
        UnorderedAccessViews = new Dictionary<EffectData.Parameter, ResourceInfo<Buffer>[]>();
        BoundResources = new Dictionary<EffectData.Parameter, object>();
    }

    public T GetResource<T>(EffectData.Parameter resourceName) where T : class
    {
        BoundResources.TryGetValue(resourceName, out var res);
        return (T)res;
    }

    public T[] GetResources<T>(EffectData.Parameter resourceName) where T : class
    {
        BoundResources.TryGetValue((EffectData.ResourceParameter)resourceName, out var res);
        return (T[])res;
    }

    public ResourceInfo<Texture>[] GetShaderResources(EffectData.Parameter resourceName)
    {
        return ShaderResourceViews.GetValueOrDefault(resourceName, EmptyResourceViews);
    }

    public ResourceInfo<SamplerState>[] GetSamplers(EffectData.Parameter resourceName)
    {
        return SamplerStates.GetValueOrDefault(resourceName, EmptySamplers);
    }

    public ResourceInfo<Buffer>[] GetUAVs(EffectData.Parameter resourceName)
    {
        return UnorderedAccessViews.GetValueOrDefault(resourceName, EmptyUAVs);
    }

    public void SetResource(EffectData.ResourceParameter resourceName, EffectResourceType type, VulkanBuffer view)
    {
        ResolveResource(resourceName, type, view, 0);
    }

    public void SetResource<T>(EffectData.ResourceParameter paramDescription, EffectResourceType type, T value)
    {
        ResolveResource(paramDescription, type, value, 0);
    }

    public void SetResource<T>(EffectData.ResourceParameter resourceName, EffectResourceType type,
        params T[] valueArray) where T : class
    {
        for (int i = 0; i < valueArray.Length; ++i)
        {
            ResolveResource(resourceName, type, valueArray[i], i);
        }
    }

    public Dictionary<EffectData.Parameter, object> GetBoundResources()
    {
        return BoundResources;
    }

    public void AddBoundResource(EffectData.Parameter resourceName, object value)
    {
        BoundResources[resourceName] = value;
    }

    public void SetResource(EffectData.ResourceParameter resourceName, EffectResourceType type,
        VulkanBuffer[] valueArray, int[] uavInitialCount)
    {
        for (int i = 0; i < valueArray.Length; ++i)
        {
            ResolveResource(resourceName, type, valueArray[i], i);
        }
    }

    private void ResolveResource(EffectData.Parameter resourceName, EffectResourceType type, object value, int index)
    {
        switch (type)
        {
            case EffectResourceType.ConstantBuffer:
                ProcessConstantBuffer(resourceName, value);
                break;
            case EffectResourceType.SamplerState:
            case EffectResourceType.ShaderResourceView:
            case EffectResourceType.UnorderedAccessView:
                ProcessReferenceResources((EffectData.ResourceParameter)resourceName, type, value, index);
                break;
        }
    }

    private void ProcessConstantBuffer(EffectData.Parameter resourceName, object value)
    {
        var constantBuffer = value as EffectConstantBuffer;
        if (ConstantBuffers.ContainsKey(resourceName))
        {
            ConstantBuffers[resourceName] = constantBuffer;
            BoundResources[resourceName] = constantBuffer;
        }
        else
        {
            ConstantBuffers.Add(resourceName, constantBuffer);
            BoundResources.Add(resourceName, constantBuffer);
        }
    }

    private void ProcessReferenceResources(EffectData.ResourceParameter parameter, EffectResourceType type,
        object value, int index)
    {
        if (index >= parameter.Count)
        {
            return;
        }

        switch (type)
        {
            case EffectResourceType.SamplerState:
            {
                if (!SamplerStates.TryGetValue(parameter, out var states))
                {
                    states = new ResourceInfo<SamplerState>[parameter.Count];
                    SamplerStates.Add(parameter, states);
                    BoundResources.Add(parameter, states);
                }

                SamplerState state = null;
                if (value is SamplerState samplerState)
                {
                    state = samplerState;
                }

                if (states[index] == null)
                {
                    states[index] = new ResourceInfo<SamplerState>();
                }

                states[index].Resource = state;

                // Descriptor-heap path only: in descriptor_buffer mode the GPU samples via per-pass descriptor
                // buffers (EffectPass.Create*Descriptor reads .Resource directly), so writing into the global heap
                // here is wasted work and the heap isn't even allocated. Skip it.
                // Same bindless fix as textures: each sampler gets its OWN stable heap slot instead of sharing one
                // per-parameter slot (which made the last-bound sampler apply to every draw).
                if (state != null && EffectPass.UseDescriptorHeap)
                {
                    states[index].GlobalHeapOffset = DescriptorHeapManager.GetOrAllocateSamplerOffset(state);
                }
            }
                break;
            case EffectResourceType.ShaderResourceView:
            {
                if (!ShaderResourceViews.TryGetValue(parameter, out var views))
                {
                    views = new ResourceInfo<Texture>[parameter.Count];
                    ShaderResourceViews.Add(parameter, views);
                    BoundResources.Add(parameter, views);
                }

                if (views[index] == null)
                {
                    views[index] = new ResourceInfo<Texture>();
                }

                if (value is Texture texture)
                {
                    views[index].Resource = texture;

                    // Descriptor-heap path: bind this texture's OWN stable heap slot (bindless). Previously the offset
                    // was allocated per parameter, so every texture bound to ShaderTexture shared ONE slot and the
                    // last-written one showed up on every draw. Now the slot belongs to the texture itself.
                    if (EffectPass.UseDescriptorHeap)
                    {
                        views[index].GlobalHeapOffset =
                            DescriptorHeapManager.GetOrAllocateTextureOffset(texture, DescriptorType.SampledImage);
                    }
                }
            }
                break;
            case EffectResourceType.UnorderedAccessView:
            {
                if (!UnorderedAccessViews.TryGetValue(parameter, out var uavs))
                {
                    uavs = new ResourceInfo<Buffer>[parameter.Count];
                    UnorderedAccessViews.Add(parameter, uavs);
                    BoundResources.Add(parameter, uavs);
                }

                if (value is Buffer buffer)
                {
                    if (uavs[index] == null)
                    {
                        uavs[index] = new ResourceInfo<Buffer>();
                    }
                    
                    uavs[index].Resource = buffer;

                    // Descriptor-heap path: bind this buffer's OWN stable heap slot (bindless) — same per-resource fix
                    // as textures/samplers. The old per-parameter slot would make the last-bound UAV apply to every
                    // draw once compute/UAV is actually used.
                    if (EffectPass.UseDescriptorHeap)
                    {
                        uavs[index].GlobalHeapOffset =
                            DescriptorHeapManager.GetOrAllocateBufferOffset(buffer, DescriptorType.StorageBuffer);
                    }
                }
            }
                break;
        }
    }
}