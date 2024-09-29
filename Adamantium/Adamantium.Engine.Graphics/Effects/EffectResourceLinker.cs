using System.Collections.Generic;
using Adamantium.Core;
using Adamantium.Engine.Effects;
using AdamantiumVulkan.Core;
using VulkanBuffer = AdamantiumVulkan.Core.Buffer;

namespace Adamantium.Engine.Graphics.Effects
{
    internal class ResourceInfo<T> : PropertyChangedBase where T : class
    {
        private T resource;

        public ResourceInfo()
        {
            
        }

        public T Resource
        {
            get => resource;
            set
            {
                if (SetProperty(ref resource, value))
                {
                    IsDirty = true;
                }
            }
        }

        public int SlotIndex { get; set; }
        
        public int DescriptorSet { get; set; } 
        
        public bool IsDirty { get; set; }
    }
    
    internal class EffectResourceLinker
    {
        /// <summary>
        /// Real object resources, as they were set on the parameter.
        /// </summary>
        public Dictionary<EffectData.Parameter, EffectConstantBuffer> ConstantBuffers;

        /// <summary>
        /// Total number of resources.
        /// </summary>
        public int Count;

        // public Dictionary<EffectData.Parameter, Sampler[]> SamplerStates;
        public Dictionary<EffectData.Parameter, ResourceInfo<Sampler>[]> SamplerStates;
        public Dictionary<EffectData.Parameter, ResourceInfo<Texture>[]> ShaderResourceViews;
        public Dictionary<EffectData.Parameter, BufferView[]> UnorderedAccessViews;
        public Dictionary<EffectData.Parameter, object> BoundResources;

        private static ResourceInfo<Sampler>[] EmptySamplers = [];
        private static ResourceInfo<Texture>[] EmptyResourceViews = [];
        private static BufferView[] EmptyUAVs = [];

        /// <summary>
        /// Initializes this instance.
        /// </summary>
        public void Initialize()
        {
            ConstantBuffers = new Dictionary<EffectData.Parameter, EffectConstantBuffer>();

            SamplerStates = new Dictionary<EffectData.Parameter, ResourceInfo<Sampler>[]>();
            ShaderResourceViews = new Dictionary<EffectData.Parameter, ResourceInfo<Texture>[]>();
            UnorderedAccessViews = new Dictionary<EffectData.Parameter, BufferView[]>();
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
            if (ShaderResourceViews.TryGetValue(resourceName, out var views))
            {
                return views;
            }
            return EmptyResourceViews;
        }

        public ResourceInfo<Sampler>[] GetSamplers(EffectData.Parameter resourceName)
        {
            if (SamplerStates.TryGetValue(resourceName, out var samplers))
            {
                return samplers;
            }
            return EmptySamplers;
        }

        public BufferView[] GetUAVs(EffectData.Parameter resourceName)
        {
            if (UnorderedAccessViews.TryGetValue(resourceName, out var uavs))
            {
                return uavs;
            }
            return EmptyUAVs;
        }

        public void SetResource(EffectData.ResourceParameter resourceName, EffectResourceType type, VulkanBuffer view)
        {
            ResolveResource(resourceName, type, view, 0);
        }

        public void SetResource<T>(EffectData.ResourceParameter paramDescription, EffectResourceType type, T value)
        {
            ResolveResource(paramDescription, type, value, 0);
        }

        public void SetResource<T>(EffectData.ResourceParameter resourceName, EffectResourceType type, params T[] valueArray) where T : class
        {
            for (int i = 0; i < valueArray.Length; ++i)
            {
                ResolveResource(resourceName, type, valueArray[i], i);
            }
        }

        public void SetResource(EffectData.ResourceParameter resourceName, EffectResourceType type, VulkanBuffer[] valueArray, int[] uavInitialCount)
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

        private void ProcessReferenceResources(EffectData.ResourceParameter parameter, EffectResourceType type, object value, int index)
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
                            states = new ResourceInfo<Sampler>[parameter.Count];
                            SamplerStates.Add(parameter, states);
                            BoundResources.Add(parameter, states);
                        }

                        Sampler state = null;
                        if (value is Sampler sampler)
                        {
                            state = sampler;
                        }
                        else if (value is SamplerState samplerState)
                        {
                            state = samplerState;
                        }

                        if (states[index] == null)
                        {
                            states[index] = new ResourceInfo<Sampler>();
                        }
                        
                        states[index].Resource = state;
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
                        }
                    }
                    break;
                case EffectResourceType.UnorderedAccessView:
                    {
                        if (!UnorderedAccessViews.TryGetValue(parameter, out var uavs))
                        {
                            uavs = new BufferView[parameter.Count];
                            UnorderedAccessViews.Add(parameter, uavs);
                            BoundResources.Add(parameter, uavs);
                        }

                        BufferView view = null;
                        if (value is BufferView)
                        {
                            view = value as BufferView;
                            uavs[index] = view;
                        }
                    }
                    break;
            }
        }
    }
}
