using System;
using Adamantium.Vulkan.Core;

namespace Adamantium.Graphics.Core
{
    public class SamplerState : GraphicsResource
    {
        private readonly Sampler sampler;
        public SamplerCreateInfo Info { get; init; }

        private SamplerState(IGraphicsDevice device, string name, Sampler sampler, SamplerCreateInfo info) : base(device)
        {
            Name = name;
            this.sampler = sampler;
            Info = info;
        }

        public static implicit operator Sampler(SamplerState state)
        {
            return state.sampler;
        }

        public static SamplerState New(IGraphicsDevice device, string name, SamplerCreateInfo info)
        {
            if (device.LogicalDevice.CreateSampler(info, null, out var sampler) != Result.Success)
            {
                throw new Exception("failed to create texture sampler!");
            }
            return new SamplerState(device, name, sampler, info);
        }

        protected override void Dispose(bool disposeManagedResources)
        {
            base.Dispose(disposeManagedResources);
            GraphicsDevice.Destroy(sampler);
        }
    }
}