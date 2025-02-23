using System;
using AdamantiumVulkan.Core;

namespace Adamantium.Graphics.Core
{
    public class SamplerState : GraphicsResource
    {
        private readonly Sampler sampler;

        private SamplerState(IGraphicsDevice device, string name, Sampler sampler) : base(device)
        {
            Name = name;
            this.sampler = sampler;
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
            return new SamplerState(device, name, sampler);
        }

        protected override void Dispose(bool disposeManagedResources)
        {
            base.Dispose(disposeManagedResources);
            GraphicsDevice.Destroy(sampler);
        }
    }
}