using Adamantium.Engine.Core.Effects;
using Adamantium.Engine.Graphics;
using AdamantiumVulkan.Core;
using System.Collections.Generic;
using VulkanBuffer = AdamantiumVulkan.Core.Buffer;

namespace Adamantium.Engine.Effects
{
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

        public Dictionary<EffectData.Parameter, Sampler[]> SamplerStates;
        public Dictionary<EffectData.Parameter, ImageView[]> ShaderResourceViews;
        public Dictionary<EffectData.Parameter, VulkanBuffer[]> UnorderedAccessViews;
        public Dictionary<EffectData.Parameter, object> BoundResources;

        private static Sampler[] EmptySamplers = new Sampler[0];
        private static ImageView[] EmptyResourceViews = new ImageView[0];
        private static VulkanBuffer[] EmptyUAVs = new VulkanBuffer[0];

        /// <summary>
        /// Initializes this instance.
        /// </summary>
        public void Initialize()
        {
            ConstantBuffers = new Dictionary<EffectData.Parameter, EffectConstantBuffer>();

            SamplerStates = new Dictionary<EffectData.Parameter, Sampler[]>();
            ShaderResourceViews = new Dictionary<EffectData.Parameter, ImageView[]>();
            UnorderedAccessViews = new Dictionary<EffectData.Parameter, VulkanBuffer[]>();
            BoundResources = new Dictionary<EffectData.Parameter, object>();
        }

        public T GetResource<T>(EffectData.Parameter resourceName) where T : class
        {
            object res;
            BoundResources.TryGetValue(resourceName, out res);
            return (T)res;
        }

        public T[] GetResources<T>(EffectData.Parameter resourceName) where T : class
        {
            object res;
            BoundResources.TryGetValue((EffectData.ResourceParameter)resourceName, out res);
            return (T[])res;
        }

        public ImageView[] GetShaderResources(EffectData.Parameter resourceName)
        {
            ImageView[] views;
            if (ShaderResourceViews.TryGetValue(resourceName, out views))
            {
                return views;
            }
            return EmptyResourceViews;
        }

        public Sampler[] GetSamplers(EffectData.Parameter resourceName)
        {
            Sampler[] samplers;
            if (SamplerStates.TryGetValue(resourceName, out samplers))
            {
                return samplers;
            }
            return EmptySamplers;
        }

        public VulkanBuffer[] GetUAVs(EffectData.Parameter resourceName)
        {
            VulkanBuffer[] uavs;
            if (UnorderedAccessViews.TryGetValue(resourceName, out uavs))
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

                        Sampler[] states = null;
                        if (!SamplerStates.TryGetValue(parameter, out states))
                        {
                            states = new Sampler[parameter.Count];
                            SamplerStates.Add(parameter, states);
                            BoundResources.Add(parameter, states);
                        }

                        Sampler state = null;
                        if (value is Sampler)
                        {
                            state = value as Sampler;
                        }
                        states[index] = state;

                    }
                    break;
                case EffectResourceType.ShaderResourceView:
                    {
                        ImageView[] views = null;
                        if (!ShaderResourceViews.TryGetValue(parameter, out views))
                        {
                            views = new ImageView[parameter.Count];
                            ShaderResourceViews.Add(parameter, views);
                            BoundResources.Add(parameter, views);
                        }

                        ImageView view = null;
                        if (value is ImageView)
                        {
                            view = value as ImageView;
                        }
                        else if (value is Texture)
                        {
                            view = value as Texture;
                        }
                        views[index] = view;
                    }
                    break;
                case EffectResourceType.UnorderedAccessView:
                    {
                        VulkanBuffer[] uavs = null;

                        if (!UnorderedAccessViews.TryGetValue(parameter, out uavs))
                        {
                            uavs = new VulkanBuffer[parameter.Count];
                            UnorderedAccessViews.Add(parameter, uavs);
                            BoundResources.Add(parameter, uavs);
                        }

                        VulkanBuffer view = null;
                        if (value is VulkanBuffer)
                        {
                            view = value as VulkanBuffer;
                        }
                        else if (value is Graphics.Buffer)
                        {
                            view = value as VulkanBuffer;
                        }

                        uavs[index] = view;
                    }
                    break;
            }
        }
    }
}
