namespace Adamantium.Game.Sandbox;

// Graphics debug (Vulkan validation layers) is controlled by the entry point (Program.cs: EnableGraphicsDebug).
// Previously this ctor forced it true, which silently won over Program.cs's false and left the validation layer
// loaded - costing ~3/4 of the frame time. Validation is a dev tool; flip it on in Program.cs when chasing a GPU bug.
public class AdamantiumGameApplication : GameApplication
{
}
