using Adamantium.Core;
using Adamantium.Vulkan.Core;

namespace Adamantium.Graphics.Core;

public class SamplerState : NamedObject
{
    public SamplerCreateInfo Info { get; init; }

    private SamplerState(string name, SamplerCreateInfo info) : base(name)
    {
        Info = info;
    }

    public static SamplerState New(string name, SamplerCreateInfo info)
    {
        return new SamplerState(name, info);
    }
}