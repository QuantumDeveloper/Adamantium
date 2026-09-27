using QuantumBinding.Generator;

namespace Adamantium.GameInput.Generator;

public static class Program
{
    public static void Main()
    {
        QuantumBindingGenerator generator = new GameInputBindingGenerator();
        generator.Run();
    }
}
