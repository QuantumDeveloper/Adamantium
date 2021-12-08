using System;
using Adamantium.Core;
using AdamantiumVulkan.Core;

namespace Adamantium.Engine.Graphics
{
    public class SamplerState : NamedObject
    {
        private Sampler sampler;

        private SamplerState(string name, Sampler sampler)
        {
            Name = name;
            this.sampler = sampler;
        }

        public static implicit operator Sampler(SamplerState state)
        {
            return state.sampler;
        }

        public static SamplerState New(GraphicsDevice device, string name, SamplerCreateInfo info)
        {
            var sampler = device.CreateSampler(info);
            return new SamplerState(name, sampler);
        }
    }
}